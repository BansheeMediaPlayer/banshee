//
// NotificationAreaService.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//   Sebastian Dröge <slomo@circular-chaos.org>
//   Alexander Hixon <hixon.alexander@mediati.org>
//
// Copyright (C) 2005-2008 Novell, Inc.
// Copyright (C) 2006-2007 Sebastian Dröge
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using Gtk;
using Mono.Unix;

using Notifications;

using Banshee.Base;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.ServiceStack;
using Banshee.Configuration;
using Banshee.Gui;
using Banshee.Collection.Gui;
using Banshee.MediaEngine;

using Banshee.IO;

using Hyena;
using Hyena.Gui;
using Hyena.Widgets;

namespace Banshee.NotificationArea
{
    public class NotificationAreaService : IExtensionService
    {
        private INotificationAreaBox notif_area;
        private GtkElementsService elements_service;
        private InterfaceActionService interface_action_service;
        private ArtworkManager artwork_manager_service;
        private bool disposed;

        private Menu menu;
        private RatingMenuItem rating_menu_item;
        private BansheeActionGroup actions;
        private bool? actions_supported;

        private int ui_manager_id;

        private bool show_notifications;
        private string notify_last_title;
        private string notify_last_artist;
        private TrackInfo current_track;
        private Notification current_nf;

        private const int icon_size = 42;

        public NotificationAreaService ()
        {
        }

        void IExtensionService.Initialize ()
        {
            elements_service = ServiceManager.Get<GtkElementsService> ();
            interface_action_service = ServiceManager.Get<InterfaceActionService> ();

            if (!ServiceStartup ()) {
                ServiceManager.ServiceStarted += OnServiceStarted;
            }
        }

        private void OnServiceStarted (ServiceStartedArgs args)
        {
            if (args.Service is Banshee.Gui.InterfaceActionService) {
                interface_action_service = (InterfaceActionService)args.Service;
            } else if (args.Service is GtkElementsService) {
                elements_service = (GtkElementsService)args.Service;
            }

            ServiceStartup ();
        }

        private bool ServiceStartup ()
        {
            if (elements_service == null || interface_action_service == null) {
                return false;
            }

            Initialize ();

            ServiceManager.ServiceStarted -= OnServiceStarted;
            if (!BuildNotificationArea ()) {
                Hyena.Log.Warning ("No available notification area drivers could be found.", false);
                Dispose ();
                return false;
            }

            GLib.Timeout.Add (1000, delegate {
                if (notif_area != null) {
                    notif_area.Show ();
                }
                return false;
            });

            return true;
        }

        private void Initialize ()
        {
            interface_action_service.GlobalActions.Add (new ActionEntry [] {
                new ActionEntry ("CloseAction", Stock.Close,
                    Catalog.GetString ("_Close"), "<Control>W",
                    Catalog.GetString ("Close"), CloseWindow)
            });

            actions = new BansheeActionGroup (interface_action_service, "NotificationArea");
            actions.Add (new ToggleActionEntry [] {
                new ToggleActionEntry ("ToggleNotificationsAction", null,
                    Catalog.GetString ("_Show Notifications"), null,
                    Catalog.GetString ("Show notifications when item changes"), ToggleNotifications, ShowNotifications)
            });

            interface_action_service.AddActionGroup (actions);
            ui_manager_id = (int)interface_action_service.UIManager.AddUiFromResource ("NotificationAreaMenu.xml");

            ServiceManager.PlayerEngine.ConnectEvent (OnPlayerEvent,
               PlayerEvent.StartOfStream |
               PlayerEvent.EndOfStream |
               PlayerEvent.TrackInfoUpdated |
               PlayerEvent.StateChange);

            // Forcefully load this
            show_notifications = ShowNotifications;

            artwork_manager_service = ServiceManager.Get<ArtworkManager> ();
            artwork_manager_service.AddCachedSize (icon_size);
        }

        public void Dispose ()
        {
            if (disposed) {
                return;
            }

            if (current_nf != null) {
                try {
                    current_nf.Close ();
                } catch {}
            }

            if (notif_area != null) {
                notif_area.Dispose ();
                notif_area = null;
            }

            ServiceManager.PlayerEngine.DisconnectEvent (OnPlayerEvent);

            elements_service.PrimaryWindowClose = null;

            Gtk.Action close_action = interface_action_service.GlobalActions["CloseAction"];
            if (close_action != null) {
                interface_action_service.GlobalActions.Remove (close_action);
            }

            if (ui_manager_id >= 0) {
                interface_action_service.RemoveActionGroup ("NotificationArea");
                interface_action_service.UIManager.RemoveUi ((uint)ui_manager_id);
                ui_manager_id = -1;
            }

            actions = null;
            elements_service = null;
            interface_action_service = null;

            disposed = true;
        }

        private bool ActionsSupported {
            get {
                if (!actions_supported.HasValue) {
                    actions_supported = Notifications.Global.Capabilities != null &&
                        Array.IndexOf (Notifications.Global.Capabilities, "actions") > -1;
                }

                return actions_supported.Value;
            }
        }

        private bool BuildNotificationArea ()
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix) {
                try {
                    notif_area = new X11NotificationAreaBox ();
                } catch {
                }
            }

            if (notif_area == null) {
                notif_area = new GtkNotificationAreaBox (elements_service.PrimaryWindow);
            }

            if (notif_area == null) {
                return false;
            }

            notif_area.Disconnected += OnNotificationAreaDisconnected;
            notif_area.Activated += OnNotificationAreaActivated;
            notif_area.PopupMenuEvent += OnNotificationAreaPopupMenuEvent;

            if (!QuitOnCloseSchema.Get ()) {
                RegisterCloseHandler ();
            }

            return true;
        }

        private void DisposeNotificationArea ()
        {
            if (notif_area != null) {
                notif_area.Disconnected -= OnNotificationAreaDisconnected;
                notif_area.Activated -= OnNotificationAreaActivated;
                notif_area.PopupMenuEvent -= OnNotificationAreaPopupMenuEvent;
            }
        }

        private void BuildContextMenu ()
        {
            if (menu != null) {
                return;
            }

            menu = (Menu)interface_action_service.UIManager.GetWidget("/NotificationAreaIconMenu");
            menu.Show ();

            for (int i = 0; i < menu.Children.Length; i++) {
                if (menu.Children[i].Name == "Previous") {
                    int j = i;
                    PlaybackRepeatActions repeat_group = interface_action_service.FindActionGroup ("PlaybackRepeat")
                         as PlaybackRepeatActions;
                    if (repeat_group != null) {
                        menu.Insert (repeat_group.CreateSubmenu (), i++ + 2);
                    }

                    PlaybackShuffleActions shuffle_group = interface_action_service.FindActionGroup ("PlaybackShuffle")
                         as PlaybackShuffleActions;
                    if (shuffle_group != null) {
                        menu.Insert (shuffle_group.CreateSubmenu (), i++ + 2);
                    }

                    if (j != i) {
                        menu.Insert (new SeparatorMenuItem (), i++ + 2);
                    }

                    rating_menu_item = new RatingMenuItem ();
                    rating_menu_item.Activated += OnRatingChanged;
                    ToggleRatingMenuSensitive ();
                    menu.Insert (rating_menu_item, i + 2);
                    break;
                }
            }
        }

        private void RegisterCloseHandler ()
        {
            if (elements_service.PrimaryWindowClose == null) {
                elements_service.PrimaryWindowClose = OnPrimaryWindowClose;
            }
        }

        private void UnregisterCloseHandler ()
        {
            if (elements_service.PrimaryWindowClose != null) {
                elements_service.PrimaryWindowClose = null;
            }
        }

        private bool OnPrimaryWindowClose ()
        {
            CloseWindow (null, null);
            return true;
        }

        private void OnNotificationAreaDisconnected (object o, EventArgs args)
        {
            // Ensure we don't keep the destroyed reference around
            DisposeNotificationArea ();
            BuildNotificationArea ();
        }

        private void OnNotificationAreaActivated (object o, EventArgs args)
        {
            elements_service.PrimaryWindow.ToggleVisibility ();
        }

        private void OnNotificationAreaPopupMenuEvent (object o, PopupMenuArgs args)
        {
            BuildContextMenu ();

            if (rating_menu_item.Visible) {
                TrackInfo track = ServiceManager.PlayerEngine.CurrentTrack;
                if (track != null) {
                    if (track is DatabaseTrackInfo) {
                        (track as DatabaseTrackInfo).Refresh ();
                    }
                    rating_menu_item.Reset (track.Rating);
                }
            }

            // Work around/fix bgo#641608 on Windows with this if/else/endif
            if (PlatformDetection.IsWindows) {
                menu.Popup (null, null, null, 3, Gtk.Global.CurrentEventTime);
            } else {
                menu.Popup (null, null, notif_area.PositionMenu, 3, Gtk.Global.CurrentEventTime);
            }
        }

        private void CloseWindow (object o, EventArgs args)
        {
            try {
                if (NotifyOnCloseSchema.Get ()) {
                    Gdk.Pixbuf image = IconThemeUtils.LoadIcon (48, Banshee.ServiceStack.Application.IconName);
                    if (image != null) {
                        image = image.ScaleSimple (icon_size, icon_size, Gdk.InterpType.Bilinear);
                    }

                    Notification nf = new Notification (
                        Catalog.GetString ("Still Running"),
                        Catalog.GetString ("Banshee was closed to the notification area. " +
                            "Use the <i>Quit</i> option to end your session."),
                        image, notif_area.Widget);
                    nf.Urgency = Urgency.Low;
                    nf.Timeout = 4500;
                    nf.Show ();

                    NotifyOnCloseSchema.Set (false);
                }
            } catch {
            }

            elements_service.PrimaryWindow.SetVisible (false);
        }

        private void ToggleNotifications (object o, EventArgs args)
        {
            ShowNotifications = ((ToggleAction)actions["ToggleNotificationsAction"]).Active;
        }

        private void OnPlayerEvent (PlayerEventArgs args)
        {
            switch (args.Event) {
                case PlayerEvent.StartOfStream:
                case PlayerEvent.TrackInfoUpdated:
                    current_track = ServiceManager.PlayerEngine.CurrentTrack;
                    ToggleRatingMenuSensitive ();
                    ShowTrackNotification ();
                    break;
                case PlayerEvent.EndOfStream:
                    current_track = null;
                    ToggleRatingMenuSensitive ();
                    break;
            }

            notif_area.OnPlayerEvent (args);
        }

        private void OnRatingChanged (object o, EventArgs args)
        {
            TrackInfo track = ServiceManager.PlayerEngine.CurrentTrack;
            if (track != null) {
                DatabaseTrackInfo db_track = track as DatabaseTrackInfo;
                if (db_track != null) {
                    db_track.SavedRating = rating_menu_item.Value;
                } else {
                    track.Rating = rating_menu_item.Value;
                    track.Save ();
                }

                ServiceManager.PlayerEngine.TrackInfoUpdated ();
            }
        }

        private void ToggleRatingMenuSensitive ()
        {
            if (rating_menu_item == null) {
                return;
            }

            if (current_track is DatabaseTrackInfo) {
                rating_menu_item.Show ();
            } else {
                rating_menu_item.Hide ();
            }
        }

        private void ShowTrackNotification ()
        {
            // This has to happen before the next if, otherwise the last_* members aren't set correctly.
            if (current_track == null || (notify_last_title == current_track.DisplayTrackTitle
                && notify_last_artist == current_track.DisplayArtistName)) {
                return;
            }

            notify_last_title = current_track.DisplayTrackTitle;
            notify_last_artist = current_track.DisplayArtistName;

            if (!show_notifications) {
                return;
            }

            foreach (var window in elements_service.ContentWindows) {
                if (window.HasToplevelFocus) {
                    return;
                }
            }

            bool is_notification_daemon = false;
            try {
                var name = Notifications.Global.ServerInformation.Name;
                is_notification_daemon = name == "notification-daemon" || name == "Notification Daemon";
            } catch {
                // This will be reached if no notification daemon is running
                return;
            }

            string message = GetByFrom (
                current_track.ArtistName, current_track.DisplayArtistName,
                current_track.AlbumTitle, current_track.DisplayAlbumTitle);

            string image = null;

            image = is_notification_daemon
                ? CoverArtSpec.GetPathForSize (current_track.ArtworkId, icon_size)
                : CoverArtSpec.GetPath (current_track.ArtworkId);

            if (!File.Exists (new SafeUri(image))) {
                if (artwork_manager_service != null) {
                    // artwork does not exist, try looking up the pixbuf to trigger scaling or conversion
                    Gdk.Pixbuf tmp_pixbuf = is_notification_daemon
                        ? artwork_manager_service.LookupScalePixbuf (current_track.ArtworkId, icon_size)
                        : artwork_manager_service.LookupPixbuf (current_track.ArtworkId);

                    if (tmp_pixbuf == null) {
                        image = "audio-x-generic";
                    } else {
                        tmp_pixbuf.Dispose ();
                    }
                }
            }

            try {
                if (current_nf == null) {
                    current_nf = new Notification (current_track.DisplayTrackTitle,
                        message, image, notif_area.Widget);
                } else {
                    current_nf.Summary = current_track.DisplayTrackTitle;
                    current_nf.Body = message;
                    current_nf.IconName = image;
                    if (notif_area != null && notif_area.Widget != null) {
                        current_nf.AttachToWidget (notif_area.Widget);
                    }
                }
                current_nf.Urgency = Urgency.Low;
                current_nf.Timeout = 4500;
                if (!current_track.IsLive && ActionsSupported && interface_action_service.PlaybackActions["NextAction"].Sensitive) {
                    current_nf.AddAction ("skip-song", Catalog.GetString("Skip this item"), OnSongSkipped);
                }
                current_nf.Show ();
            } catch (Exception e) {
                Hyena.Log.Exception (Catalog.GetString ("Cannot show notification"), e);
            }
        }

        private string MarkupFormat (string fmt, params string [] args)
        {
            string [] new_args = new string [args.Length + 2];
            new_args[0] = "<i>";
            new_args[1] = "</i>";

            for (int i = 0; i < args.Length; i++) {
                new_args[i + 2] = GLib.Markup.EscapeText (args[i]);
            }

            return String.Format (fmt, new_args);
        }

        private string GetByFrom (string artist, string display_artist, string album, string display_album)
        {
            bool has_artist = !String.IsNullOrEmpty (artist);
            bool has_album = !String.IsNullOrEmpty (album);

            string markup = null;
            if (has_artist && has_album) {
                // Translators: {0} and {1} are for markup so ignore them, {2} and {3}
                // are Artist Name and Album Title, respectively;
                // e.g. 'by Parkway Drive from Killing with a Smile'
                markup = MarkupFormat (Catalog.GetString ("{0}by{1} {2}\n{0}from{1} {3}"), display_artist, display_album);
            } else if (has_album) {
                // Translators: {0} and {1} are for markup so ignore them, {2} is for Album Title;
                // e.g. 'from Killing with a Smile'
                markup = MarkupFormat (Catalog.GetString ("{0}from{1} {2}"), display_album);
            } else {
                // Translators: {0} and {1} are for markup so ignore them, {2} is for Artist Name;
                // e.g. 'by Parkway Drive'
                markup = MarkupFormat (Catalog.GetString ("{0}by{1} {2}"), display_artist);
            }
            return markup;
        }

        private void OnSongSkipped (object o, ActionArgs args)
        {
            if (args.Action == "skip-song") {
                ServiceManager.PlaybackController.Next ();
            }
        }

        public bool ShowNotifications {
            get {
                show_notifications = ShowNotificationsSchema.Get ();
                return show_notifications;
            }

            set {
                ShowNotificationsSchema.Set (value);
                show_notifications = value;
            }
        }

        public bool QuitOnClose {
            get {
                return QuitOnCloseSchema.Get ();
            }

            set {
                QuitOnCloseSchema.Set (value);
                if (value) {
                    UnregisterCloseHandler ();
                } else {
                    RegisterCloseHandler ();
                }
            }
        }

        string IService.ServiceName {
            get { return "NotificationAreaService"; }
        }

        public static readonly SchemaEntry<bool> EnabledSchema = new SchemaEntry<bool> (
            "plugins.notification_area", "enabled",
            true,
            "Plugin enabled",
            "Notification area plugin enabled"
        );

        public static readonly SchemaEntry<bool> ShowNotificationsSchema = new SchemaEntry<bool> (
            "plugins.notification_area", "show_notifications",
            true,
            "Show notifications",
            "Show information notifications when item starts playing"
        );

        public static readonly SchemaEntry<bool> NotifyOnCloseSchema = new SchemaEntry<bool> (
            "plugins.notification_area", "notify_on_close",
            true,
            "Show a notification when closing main window",
            "When the main window is closed, show a notification stating this has happened."
        );

        public static readonly SchemaEntry<bool> QuitOnCloseSchema = new SchemaEntry<bool> (
            "plugins.notification_area", "quit_on_close",
            false,
            "Quit on close",
            "Quit instead of hide to notification area on close"
        );
    }
}
