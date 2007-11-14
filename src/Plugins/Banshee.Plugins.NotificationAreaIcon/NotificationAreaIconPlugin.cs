/***************************************************************************
 *  NotificationAreaIconPlugin.cs
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
 *  Copyright (C) 2006 Sebastian Dröge
 * 
 *  Written by Sebastian Dröge <slomo@circular-chaos.org>
 *             Aaron Bockover <aaron@abock.org>
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */

using System;
using Gtk;
using GLib;
using Mono.Unix;

using Notifications;

using Banshee.Base;
using Banshee.MediaEngine;
using Banshee.Widgets;
using Banshee.Configuration;
using Banshee.Sources;
using Banshee.SmartPlaylist;

public static class PluginModuleEntry
{
    public static Type [] GetTypes()
    {
        return new Type [] {
            typeof(Banshee.Plugins.NotificationAreaIcon.NotificationAreaIconPlugin)
        };
    }
}

namespace Banshee.Plugins.NotificationAreaIcon 
{
    public class NotificationAreaIconPlugin : Banshee.Plugins.Plugin 
    {
        protected override string ConfigurationName { get { return "notification_area"; } }
        public override string DisplayName { get { return Catalog.GetString("Notification Area Icon"); } }

        public override string Description {
            get { return Catalog.GetString("Shows the Notification Area Icon"); }
        }

        public override string[] Authors {
            get {
                return new string[] {
                    "Sebastian Dr\u00f6ge",
                    "Aaron Bockover",
                    "Ruben Vermeersch",
                    "Gabriel Burt"
                };
            }
        }

        private EventBox event_box;
        private NotificationArea notif_area;
        private Menu menu;
        private bool menu_is_reversed = false;
		private RatingMenuItem rating_menu_item;
        private ActionGroup actions;
        private uint ui_manager_id;

        private TrackInfoPopup popup;
        private bool can_show_popup = false;
        private bool cursor_over_trayicon = false;
        private bool show_notifications = false;
        private TrackInfo current_track = null;
        private string notify_last_title = null;
        private string notify_last_artist = null;

        private static readonly uint SkipDelta = 10;
        private static readonly int VolumeDelta = 10;
        
        protected override void PluginInitialize()
        {
        }
        
        protected override void InterfaceInitialize() 
        {
            Init();

            actions = new ActionGroup("NotificationArea");

            actions.Add(new ActionEntry [] {
                new ActionEntry("CloseAction", Stock.Close,
                    Catalog.GetString("_Close"), "<Control>W",
                    Catalog.GetString("Close"), CloseWindow)
            });

            actions.Add(new ToggleActionEntry [] {
                new ToggleActionEntry("ToggleNotificationsAction", null,
                    Catalog.GetString("_Show Notifications"), null,
                    Catalog.GetString("Show notifications when song changes"), ToggleNotifications, ShowNotifications)
            });

            Globals.ActionManager.UI.InsertActionGroup(actions, 0);
            ui_manager_id = Globals.ActionManager.UI.AddUiFromResource("NotificationAreaIconMenu.xml");      
            menu = (Menu) Globals.ActionManager.UI.GetWidget("/NotificationAreaIconMenu");

            for(int i = 0; i < menu.Children.Length; i++) {
                if(menu.Children[i].Name == "Next") {
                    rating_menu_item = new RatingMenuItem();
                    rating_menu_item.Activated += OnItemRatingActivated;
                    ToggleRatingMenuSensitive();
                    menu.Insert(rating_menu_item, i + 2);
                    break;
                }
            }

            menu.Show();
            
            popup = new TrackInfoPopup();
            PlayerEngineCore.EventChanged += OnPlayerEngineEventChanged;

            // When we're already playing fill the TrackInfoPopup with the current track
            if (PlayerEngineCore.CurrentState == PlayerEngineState.Playing) {
                FillPopup();
            }

            // Forcefully load this value
            show_notifications = ShowNotifications;
            InterfaceElements.MainWindow.KeyPressEvent += OnKeyPressEvent;
        }

        protected override void PluginDispose() 
        {
            if (notif_area != null) {
                notif_area.Destroy();
                event_box = null;
                notif_area = null;
            }
            
            InterfaceElements.PrimaryWindowClose = null;
            
            PlayerEngineCore.EventChanged -= OnPlayerEngineEventChanged;
            
            Globals.ActionManager.UI.RemoveUi(ui_manager_id);
            Globals.ActionManager.UI.RemoveActionGroup(actions);
        }

        public override Gtk.Widget GetConfigurationWidget()
        {            
            return new NotificationAreaIconConfigPage(this);
        }
        
        private void Init() 
        {
            notif_area = new NotificationArea(Catalog.GetString("Banshee"));
            notif_area.DestroyEvent += OnDestroyEvent;

            event_box = new EventBox();
            event_box.ButtonPressEvent += OnNotificationAreaIconClick;
            event_box.EnterNotifyEvent += OnEnterNotifyEvent;
            event_box.LeaveNotifyEvent += OnLeaveNotifyEvent;
            event_box.ScrollEvent += OnMouseScroll;
            
            event_box.Add(new Gtk.Image(IconThemeUtils.LoadIcon(22, "music-player-banshee", "tray-icon")));

            notif_area.Add(event_box);
            notif_area.ShowAll();
            
            if(!QuitOnCloseSchema.Get()) {
                RegisterCloseHandler();
            }
        }
        
        private void RegisterCloseHandler()
        {
            if(InterfaceElements.PrimaryWindowClose == null) {
                InterfaceElements.PrimaryWindowClose = OnPrimaryWindowClose;
            }
        }
        
        private void UnregisterCloseHandler()
        {
            if(InterfaceElements.PrimaryWindowClose != null) {
                InterfaceElements.PrimaryWindowClose = null;
            }
        }
        
        private bool OnPrimaryWindowClose()
        {
            CloseWindow(null, null);
            return true;
        }

        private void OnDestroyEvent(object o, DestroyEventArgs args) 
        {
            Init();
        }

        private void CloseWindow(object o, EventArgs args)
        {
            try {
                if(NotifyOnCloseSchema.Get()) {
                    Gdk.Pixbuf image = Branding.ApplicationLogo.ScaleSimple(42, 42, Gdk.InterpType.Bilinear);
                    Notification nf = new Notification(
                        Catalog.GetString("Still Running"), 
                        Catalog.GetString("Banshee was closed to the notification area. " + 
                            "Use the <i>Quit</i> option to end your session."),
                        image, event_box);
                    nf.Urgency = Urgency.Low;
                    nf.Timeout = 4500;
                    nf.Show();
                    
                    NotifyOnCloseSchema.Set(false);
                }
            } catch {
            }

            ShowHideMainWindow();
        }

        private void ToggleNotifications(object o, EventArgs args)
        {
            ShowNotifications = (actions["ToggleNotificationsAction"] as ToggleAction).Active;
        }

        private void ShowHideMainWindow()
        {
            if (InterfaceElements.MainWindow.IsActive) {
                SaveWindowSizePosition();
                InterfaceElements.MainWindow.Visible = false;
            } else {
                RestoreWindowSizePosition();
                InterfaceElements.MainWindow.Present();
            }
        }

        private void ShowNotification()
        {
            // This has to happen before the next if, otherwise the last_* members aren't set correctly.
            if(current_track == null || (notify_last_title == current_track.DisplayTitle 
                && notify_last_artist == current_track.DisplayArtist)) {
                return;
            }
            
            notify_last_title = current_track.DisplayTitle;
            notify_last_artist = current_track.DisplayArtist;

            if(cursor_over_trayicon || !show_notifications || InterfaceElements.MainWindow.HasToplevelFocus) {
                return;
            }
            
            string message = String.Format("{0}\n<i>{1}</i>", 
                GLib.Markup.EscapeText(current_track.DisplayTitle),
                GLib.Markup.EscapeText(current_track.DisplayArtist));
            
            Gdk.Pixbuf image = null;
            
            try {
                if(current_track.CoverArtFileName != null) {
                    image = new Gdk.Pixbuf(current_track.CoverArtFileName);
                } 
            } catch {
            }
            
            if(image == null) {
                image = Branding.DefaultCoverArt;
            }
            
            image = image.ScaleSimple(42, 42, Gdk.InterpType.Bilinear);
            
            try {
                Notification nf = new Notification(Catalog.GetString("Now Playing"), message, image, event_box);
                nf.Urgency = Urgency.Low;
                nf.Timeout = 4500;
                nf.AddHint("desktop-entry", "banshee.desktop");
                if(Globals.ActionManager["NextAction"].Sensitive) {
                    nf.AddAction("skip-song", Catalog.GetString("Play next song"), OnSongSkipped);
                }
                nf.Show();
            } catch(Exception e) {
                LogCore.Instance.PushError(Catalog.GetString("Cannot show notification"), e.Message, false);
            }
        }
        
        private void OnSongSkipped(object o, ActionArgs args)
        {
            if(args.Action == "skip-song") {
                Globals.ActionManager["NextAction"].Activate();
            }
        }

        [GLib.ConnectBefore]
        private void OnKeyPressEvent(object o, KeyPressEventArgs args)
        {
            bool handled = false;
            
            if (args.Event.Key == Gdk.Key.w && (args.Event.State & Gdk.ModifierType.ControlMask) != 0) {
                handled = true;
                ShowHideMainWindow();
            }
            
            args.RetVal = handled;
        }

        private void OnNotificationAreaIconClick(object o, ButtonPressEventArgs args) 
        {
            if(args.Event.Type != Gdk.EventType.ButtonPress) {
                return;
            }
        
            switch(args.Event.Button) {
                case 1:
                    if((args.Event.State & Gdk.ModifierType.ControlMask) != 0) {
                        Globals.ActionManager["PreviousAction"].Activate();
                    } else {
                        ShowHideMainWindow();
                    }
                    break;
                case 2:
                    Globals.ActionManager["PlayPauseAction"].Activate();
                    break;
                case 3:
                    if((args.Event.State & Gdk.ModifierType.ControlMask) != 0) {
                        Globals.ActionManager["NextAction"].Activate();
                    } else {
                        menu.Popup(null, null, new MenuPositionFunc(PositionMenu), 
                            args.Event.Button, args.Event.Time);
                    }
                    break;
            }
        }

        private void OnItemRatingActivated(object o, EventArgs args)
        {
            if(PlayerEngineCore.CurrentTrack != null) {
                PlayerEngineCore.CurrentTrack.Rating = (uint)rating_menu_item.Value;
                PlayerEngineCore.TrackInfoUpdated();
            }
        }
        
        private void ToggleRatingMenuSensitive() 
        {
            if(PlayerEngineCore.CurrentTrack != null && (SourceManager.ActiveSource is LibrarySource || 
                SourceManager.ActiveSource is PlaylistSource ||
                SourceManager.ActiveSource is SmartPlaylistSource)) {
                rating_menu_item.Reset((int)PlayerEngineCore.CurrentTrack.Rating);
                rating_menu_item.Show();
            } else {
                rating_menu_item.Hide();
            }
        }

        private void PositionMenu(Menu menu, out int x, out int y, out bool push_in) 
        {
            bool on_bottom = PositionWidget(menu, out x, out y, 0);
            push_in = true;
            
            if((on_bottom && !menu_is_reversed) || (!on_bottom && menu_is_reversed)) {
                menu_is_reversed = !menu_is_reversed;
                Widget [] frozen_children = new Widget[menu.Children.Length - 1];
                Array.Copy(menu.Children, 1, frozen_children, 0, frozen_children.Length);
                for(int i = 0; i < frozen_children.Length; i++) {
                    menu.ReorderChild(frozen_children[i], 0);
                }
            }
        }

        private bool PositionWidget(Widget widget, out int x, out int y, int yPadding) 
        {
            int button_y, panel_width, panel_height;
            Gtk.Requisition requisition = widget.SizeRequest();
            
            event_box.GdkWindow.GetOrigin(out x, out button_y);
            (event_box.Toplevel as Gtk.Window).GetSize(out panel_width, out panel_height);
            
            bool on_bottom = button_y + panel_height + requisition.Height >= event_box.Screen.Height;

            y = on_bottom
                ? button_y - requisition.Height - yPadding
                : button_y + panel_height + yPadding;
                
            return on_bottom;
        }

        private void OnMouseScroll(object o, ScrollEventArgs args) 
        {
            switch(args.Event.Direction) {
                case Gdk.ScrollDirection.Up:
                    if((args.Event.State & Gdk.ModifierType.ControlMask) != 0) {
                        PlayerEngineCore.Volume += (ushort)VolumeDelta;
                    } else if((args.Event.State & Gdk.ModifierType.ShiftMask) != 0) {
                        PlayerEngineCore.Position += SkipDelta;
                    } else {
                        Globals.ActionManager["NextAction"].Activate();
                    }
                    break;
                case Gdk.ScrollDirection.Down:
                    if((args.Event.State & Gdk.ModifierType.ControlMask) != 0) {
                        if (PlayerEngineCore.Volume < (ushort)VolumeDelta)
                            PlayerEngineCore.Volume = 0;
                        else
                            PlayerEngineCore.Volume -= (ushort)VolumeDelta;
                    } else if((args.Event.State & Gdk.ModifierType.ShiftMask) != 0) {
                        PlayerEngineCore.Position -= SkipDelta;
                    } else {
                        Globals.ActionManager["PreviousAction"].Activate();
                    }
                    break;
            }
        }

        private void HidePopup() 
        {
            popup.Hide();
        }
        
        private void ShowPopup() 
        {
            PositionPopup();
            popup.Show();
        }
        
        private void PositionPopup() 
        {
            int x, y;
            Gtk.Requisition event_box_req = event_box.SizeRequest();
            Gtk.Requisition popup_req = popup.SizeRequest();
            
            PositionWidget(popup, out x, out y, 5);
            
            x = x - (popup_req.Width / 2) + (event_box_req.Width / 2);     
            if(x + popup_req.Width >= event_box.Screen.Width) { 
                x = event_box.Screen.Width - popup_req.Width - 5;
            } else if(x < 5) {
                x = 5;
            }
            
            popup.Move(x, y);
        }
        
        private void OnEnterNotifyEvent(object o, EnterNotifyEventArgs args) 
        {
            cursor_over_trayicon = true;
            if(can_show_popup) {
                // only show the popup when the cursor is still over the
                // tray icon after 500ms
                GLib.Timeout.Add(500, delegate {
                    if ((cursor_over_trayicon) && (can_show_popup)) {
                        ShowPopup();
                    }
                    return false;
                });
            }
        }
        
        private void OnLeaveNotifyEvent(object o, LeaveNotifyEventArgs args) 
        {
            cursor_over_trayicon = false;
            HidePopup();
        }

        private void OnPlayerEngineEventChanged(object o, PlayerEngineEventArgs args) 
        {
            switch (args.Event) {
                case PlayerEngineEvent.Iterate:
                    if(PlayerEngineCore.CurrentTrack != null) {
                        popup.Duration = (uint)PlayerEngineCore.CurrentTrack.Duration.TotalSeconds;
                        popup.Position = PlayerEngineCore.Position;

                        if (current_track != PlayerEngineCore.CurrentTrack) {
                            current_track = PlayerEngineCore.CurrentTrack;
                            ShowNotification();
                        }
                    } else {
                        popup.Duration = 0;
                        popup.Position = 0;
                    }
                    break;
                case PlayerEngineEvent.StartOfStream:
                case PlayerEngineEvent.TrackInfoUpdated:
                    ToggleRatingMenuSensitive();
                    FillPopup();
                    ShowNotification();
                    break;
                case PlayerEngineEvent.EndOfStream:
                    // only hide the popup when we don't play again after 250ms
                    GLib.Timeout.Add(250, delegate {
                        if (PlayerEngineCore.CurrentState != PlayerEngineState.Playing) {
                            ToggleRatingMenuSensitive();
                            popup.Duration = 0;
                            popup.Position = 0;
                            can_show_popup = false;
                            popup.Hide();
                         }
                         return false;
                    });
                    break;
            }
        }

        private void FillPopup() 
        {
            can_show_popup = true;
            popup.Artist = PlayerEngineCore.CurrentTrack.DisplayArtist;
            popup.Album = PlayerEngineCore.CurrentTrack.Album;
            popup.TrackTitle = PlayerEngineCore.CurrentTrack.DisplayTitle;
            try {
                popup.CoverArtFileName = PlayerEngineCore.CurrentTrack.CoverArtFileName;
            } catch {
            }
            popup.QueueDraw();
            if (!popup.Visible) {
                PositionPopup();
            }
        }

        private int x, y, w, h;
        private bool maximized;
        private void SaveWindowSizePosition()
        {
            maximized = ((InterfaceElements.MainWindow.GdkWindow.State & Gdk.WindowState.Maximized) > 0);

            if (!maximized) {
                InterfaceElements.MainWindow.GetPosition(out x, out y);
                InterfaceElements.MainWindow.GetSize(out w, out h);
            }
        }

        private void RestoreWindowSizePosition() 
        {
            if (maximized) {
                InterfaceElements.MainWindow.Maximize();
            } else {
                InterfaceElements.MainWindow.Resize(w, h);
                InterfaceElements.MainWindow.Move(x, y);
            }
        }

        public bool ShowNotifications {
            get { 
                show_notifications = ShowNotificationsSchema.Get(); 
                return show_notifications;
            }
            set { 
                ShowNotificationsSchema.Set(value);
                show_notifications = value;
            }
        }
        
        public bool QuitOnClose {
            get {
                return QuitOnCloseSchema.Get();
            }
            
            set {
                QuitOnCloseSchema.Set(value);
                if(value) {
                    UnregisterCloseHandler();
                } else {
                    RegisterCloseHandler();
                }
            }
        }
        
        public static readonly SchemaEntry<bool> EnabledSchema = new SchemaEntry<bool>(
            "plugins.notification_area", "enabled",
            true,
            "Plugin enabled",
            "Notification area plugin enabled"
        );
                
        public static readonly SchemaEntry<bool> ShowNotificationsSchema = new SchemaEntry<bool>(
            "plugins.notification_area", "show_notifications",
            true,
            "Show notifications",
            "Show track information notifications when track starts playing"
        );
                
        public static readonly SchemaEntry<bool> NotifyOnCloseSchema = new SchemaEntry<bool>(
            "plugins.notification_area", "notify_on_close",
            true,
            "Show a notification when closing main window",
            "When the main window is closed, show a notification stating this has happened."
        );
                
        public static readonly SchemaEntry<bool> QuitOnCloseSchema = new SchemaEntry<bool>(
            "plugins.notification_area", "quit_on_close",
            false,
            "Quit on close",
            "Quit instead of hide to notification area on close"
        );
    }
}
