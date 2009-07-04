//
// X11NotificationAreaBox.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//   Sebastian Dröge <slomo@circular-chaos.org>
//   Alexander Hixon <hixon.alexander@mediati.org>
//
// Copyright (C) 2005-2008 Novell, Inc.
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
using Mono.Unix;
using Gtk;

using Banshee.MediaEngine;
using Banshee.ServiceStack;
using Banshee.Gui;

namespace Banshee.NotificationArea
{
    public class X11NotificationAreaBox : X11NotificationArea, INotificationAreaBox
    {
        private enum PanelOrientation { 
            Horizontal, 
            Vertical 
        }
    
        private EventBox event_box;
        private Image icon;
        
        private TrackInfoPopup popup;
        private bool can_show_popup = false;
        private bool cursor_over_trayicon = false;
        private bool hide_delay_started = false;
        private int panel_size;
        
        public event EventHandler Disconnected;
        public event EventHandler Activated;
        public event PopupMenuHandler PopupMenuEvent;
        
        public Widget Widget {
            get { return event_box; }
        }
        
        public X11NotificationAreaBox () : base (Catalog.GetString ("Banshee"))
        {
            event_box = new EventBox ();
            Add (event_box);
            icon = new Image ();
            
            // Load a 16x16-sized icon to ensure we don't end up with a 1x1 pixel.
            panel_size = 16;
            event_box.Add (icon);
            
            event_box.ButtonPressEvent += OnButtonPressEvent;
            event_box.EnterNotifyEvent += OnEnterNotifyEvent;
            event_box.LeaveNotifyEvent += OnLeaveNotifyEvent;
            event_box.ScrollEvent += OnMouseScroll;
            
            event_box.ShowAll ();
        }
        
        public override void Dispose ()
        {
            HidePopup ();
        
            event_box.ButtonPressEvent -= OnButtonPressEvent;
            event_box.EnterNotifyEvent -= OnEnterNotifyEvent;
            event_box.LeaveNotifyEvent -= OnLeaveNotifyEvent;
            event_box.ScrollEvent -= OnMouseScroll;
        
            Destroy ();
        }
        
        public void PositionMenu (Menu menu, out int x, out int y, out bool push_in) 
        {
            PositionWidget (menu, out x, out y, 0);
            push_in = true;
        }
        
        private bool PositionWidget (Widget widget, out int x, out int y, int yPadding) 
        {
            int button_y, panel_width, panel_height;
            Gtk.Requisition requisition = widget.SizeRequest ();
            
            event_box.GdkWindow.GetOrigin (out x, out button_y);
            (event_box.Toplevel as Gtk.Window).GetSize (out panel_width, out panel_height);
            
            bool on_bottom = button_y + panel_height + requisition.Height >= event_box.Screen.Height;

            y = on_bottom
                ? button_y - requisition.Height - yPadding
                : button_y + panel_height + yPadding;
                
            return on_bottom;
        }
        
#region Panel Icon Sizing

        // This code has been shamelessly ripped off from 
        // Tomboy, the wonderful life organizer!
        
        private void ConfigureIconSize ()
        {   
            // For some reason, the first time we ask for the allocation,
            // it's a 1x1 pixel.  Prevent against this by returning a
            // reasonable default.  Setting the icon causes OnSizeAllocated
            // to be called again anyhow. (--Boyd)
            int icon_size = panel_size;
            if (icon_size < 16) {
                icon_size = 16;
            }
            
            // Control specifically which icon is used at the smaller sizes
            // so that no scaling occurs. See bug #403500 for more info (--Boyd)
            if (icon_size <= 21) {
                icon_size = 16;
            } else if (icon_size <= 31) {
                icon_size = 22;
            } else if (icon_size <= 47) {
                icon_size = 32;
            }
            
            icon.IconName = Banshee.ServiceStack.Application.IconName;
            icon.PixelSize = icon_size;
        }

        private PanelOrientation GetPanelOrientation ()
        {
            // Determine whether the tray is inside a horizontal or vertical
            // panel so the size of the icon can adjust correctly.
        
            if (event_box.ParentWindow == null) {
                return PanelOrientation.Horizontal;
            }

            Gdk.Window top_level_window = event_box.ParentWindow.Toplevel;

            Gdk.Rectangle rect = top_level_window.FrameExtents;
            if (rect.Width < rect.Height) {
                return PanelOrientation.Vertical;
            }
            
            return PanelOrientation.Horizontal;
        }

        private bool first_alloc = true;

        protected override void OnSizeAllocated (Gdk.Rectangle rect)
        {
            // Ignore the first allocation dimensions (typically 200x200) and use only 
            // the allocation's position and the default icon size for dimension (16x16).
            // A proper size request will be queued later through ConfigureIconSize ().
            // Fix for BGO #540885
            if (first_alloc) {
                base.OnSizeAllocated (new Gdk.Rectangle (rect.X, rect.Y, panel_size, panel_size));
                first_alloc = false;
                return;
            }
            
            base.OnSizeAllocated (rect);
            
            if (GetPanelOrientation () == PanelOrientation.Horizontal) {
                if (panel_size == Allocation.Height) {
                    return;
                }
                
                panel_size = Allocation.Height;
            } else {
                if (panel_size == Allocation.Width) {
                    return;
                }
                
                panel_size = Allocation.Width;
            }

            ConfigureIconSize ();
        }
        
#endregion
        
        private void HidePopup () 
        {
            if (popup == null) {
                return;
            }
            
            popup.Hide ();
            popup.Dispose ();
            popup = null;
        }
        
        private void ShowPopup () 
        {
            if (popup != null) {
                return;
            }
            
            popup = new TrackInfoPopup ();
            popup.EnterNotifyEvent += delegate {
                hide_delay_started = false;
            };
            popup.LeaveNotifyEvent += delegate(object o, LeaveNotifyEventArgs args) {
                Gdk.Rectangle rect;
                if (!popup.Intersect (new Gdk.Rectangle ((int)args.Event.X, (int)args.Event.Y, 1, 1), out rect)) {
                    OnLeaveNotifyEvent (o, args);
                }
            };

            PositionPopup ();

            popup.Show ();
        }
        
        private void PositionPopup () 
        {
            int x, y;
            Gtk.Requisition event_box_req = event_box.SizeRequest ();
            Gtk.Requisition popup_req = popup.SizeRequest ();
            
            PositionWidget (popup, out x, out y, 5);
            
            int monitor = event_box.Screen.GetMonitorAtPoint (x, y);
            var monitor_rect = event_box.Screen.GetMonitorGeometry(monitor);

            x = x - (popup_req.Width / 2) + (event_box_req.Width / 2);

            if (x + popup_req.Width >= monitor_rect.Right - 5) {
                x = monitor_rect.Right - popup_req.Width - 5;
            } else if (x < monitor_rect.Left + 5) {
                x = monitor_rect.Left + 5;
            }
            
            popup.Move (x, y);
        }
        
        private void OnButtonPressEvent (object o, ButtonPressEventArgs args)
        {
            if (args.Event.Type != Gdk.EventType.ButtonPress) {
                return;
            }
        
            switch (args.Event.Button) {
                case 1:
                    if ((args.Event.State & Gdk.ModifierType.ControlMask) != 0) {
                        ServiceManager.PlaybackController.Next ();
                    } else {
                        OnActivated ();
                    }
                    break;
                case 2:
                    ServiceManager.PlayerEngine.TogglePlaying ();
                    break;
                case 3:
                    if ((args.Event.State & Gdk.ModifierType.ControlMask) != 0) {
                        ServiceManager.PlaybackController.Next ();
                    } else {
                        OnPopupMenuEvent ();
                    }
                    break;
            }
        }
        
        private void OnMouseScroll (object o, ScrollEventArgs args)
        {
            switch (args.Event.Direction) {
                case Gdk.ScrollDirection.Up:
                    if ((args.Event.State & Gdk.ModifierType.ControlMask) != 0) {
                        ServiceManager.PlayerEngine.Volume += (ushort)PlayerEngine.VolumeDelta;
                    } else if((args.Event.State & Gdk.ModifierType.ShiftMask) != 0) {
                        ServiceManager.PlayerEngine.Position += PlayerEngine.SkipDelta;
                    } else {
                        ServiceManager.PlaybackController.Next ();
                    }
                    break;
                    
                case Gdk.ScrollDirection.Down:
                    if ((args.Event.State & Gdk.ModifierType.ControlMask) != 0) {
                        if (ServiceManager.PlayerEngine.Volume < (ushort)PlayerEngine.VolumeDelta) {
                            ServiceManager.PlayerEngine.Volume = 0;
                        } else {
                            ServiceManager.PlayerEngine.Volume -= (ushort)PlayerEngine.VolumeDelta;
                        }
                    } else if((args.Event.State & Gdk.ModifierType.ShiftMask) != 0) {
                        ServiceManager.PlayerEngine.Position -= PlayerEngine.SkipDelta;
                    } else {
                        ServiceManager.PlaybackController.Previous ();
                    }
                    break;
            }
        }
        
        private void OnEnterNotifyEvent (object o, EnterNotifyEventArgs args) 
        {
            hide_delay_started = false;
            cursor_over_trayicon = true;
            if (can_show_popup) {
                // only show the popup when the cursor is still over the
                // tray icon after 500ms
                GLib.Timeout.Add (500, delegate {
                    if (cursor_over_trayicon && can_show_popup) {
                        ShowPopup ();
                    }
                    return false;
                });
            }
        }
        
        private void OnLeaveNotifyEvent (object o, LeaveNotifyEventArgs args) 
        {
            // Give the user half a second to move the mouse cursor to the popup.
            if (!hide_delay_started) {
                hide_delay_started = true;
                GLib.Timeout.Add (500, delegate {
                    if (hide_delay_started) {
                        hide_delay_started = false;
                        cursor_over_trayicon = false;
                        HidePopup ();
                    }
                    return false;
                });
            }
        }
        
        public void OnPlayerEvent (PlayerEventArgs args)
        {
            switch (args.Event) {
                case PlayerEvent.StartOfStream:
                    can_show_popup = true;
                    break;
                    
                case PlayerEvent.EndOfStream:
                    // only hide the popup when we don't play again after 250ms
                    GLib.Timeout.Add (250, delegate {
                        if (ServiceManager.PlayerEngine.CurrentState != PlayerState.Playing) {
                            can_show_popup = false;
                            HidePopup ();
                         }
                         return false;
                    });
                    break;
            }
        }
        
        protected virtual void OnActivated ()
        {
            EventHandler handler = Activated;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }
        
        protected virtual void OnPopupMenuEvent ()
        {
            PopupMenuHandler handler = PopupMenuEvent;
            if (handler != null) {
                handler (this, new PopupMenuArgs ());
            }
        }
        
        protected override bool OnDestroyEvent (Gdk.Event evnt)
        {
            bool result = base.OnDestroyEvent (evnt);
            
            EventHandler handler = Disconnected;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
            
            return result;
        }
    }
}
