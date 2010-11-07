//
// FullScreenWindow.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//   Larry Ewing <lewing@novell.com>
//   Gabriel Burt <gburt@novell.com>
//
// Copyright 2008-2010 Novell, Inc.
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

using Banshee.Gui;
using Banshee.MediaEngine;
using Banshee.ServiceStack;

namespace Banshee.NowPlaying
{
    public class FullscreenWindow : Window
    {
        private Gtk.Window parent;
        private FullscreenControls controls;
        private InterfaceActionService action_service;

        public FullscreenWindow (Window parent) : base (WindowType.Toplevel)
        {
            Title = parent.Title;
            AppPaintable = true;

            this.parent = parent;
            this.action_service = ServiceManager.Get<InterfaceActionService> ();

            AddAccelGroup (action_service.UIManager.AccelGroup);

            SetupWidget ();
        }

        protected override bool OnExposeEvent (Gdk.EventExpose evnt)
        {
            evnt.Window.DrawRectangle (Style.BlackGC, true, Allocation);
            return base.OnExposeEvent (evnt);
        }

        protected override bool OnKeyPressEvent (Gdk.EventKey evnt)
        {
            PlayerEngineService player = ServiceManager.PlayerEngine;

            bool control = (evnt.State & Gdk.ModifierType.ShiftMask) != 0;
            bool shift = (evnt.State & Gdk.ModifierType.ControlMask) != 0;
            bool mod = control || shift;

            uint fixed_seek = 15000; // 15 seconds
            uint fast_seek = player.Length > 0 ? (uint)(player.Length * 0.15) : fixed_seek; // 15% or fixed
            uint slow_seek = player.Length > 0 ? (uint)(player.Length * 0.05) : fixed_seek; // 5% or fixed

            switch (evnt.Key) {
                case Gdk.Key.F11:
                case Gdk.Key.Escape:
                    Hide ();
                    return true;
                case Gdk.Key.C:
                case Gdk.Key.c:
                case Gdk.Key.V:
                case Gdk.Key.v:
                case Gdk.Key.Return:
                case Gdk.Key.KP_Enter:
                case Gdk.Key.Tab:
                    if (controls == null || !controls.Visible) {
                        ShowControls ();
                    } else {
                        HideControls ();
                    }
                    return true;
                case Gdk.Key.Right:
                case Gdk.Key.rightarrow:
                    player.Position += mod ? fast_seek : slow_seek;
                    ShowControls ();
                    break;
                case Gdk.Key.Left:
                case Gdk.Key.leftarrow:
                    player.Position -= mod ? fast_seek : slow_seek;
                    ShowControls ();
                    break;
            }

            return base.OnKeyPressEvent (evnt);
        }

#region Widgetry and show/hide logic

        private void SetupWidget ()
        {
            Deletable = false;
            TransientFor = null;
            Decorated = false;
            CanFocus = true;

            ConfigureWindow ();
        }

        private void ConfigureWindow ()
        {
            Gdk.Screen screen = Screen;
            int monitor = screen.GetMonitorAtWindow (parent.GdkWindow);
            Gdk.Rectangle bounds = screen.GetMonitorGeometry (monitor);
            Move (bounds.X, bounds.Y);
            Resize (bounds.Width, bounds.Height);
        }

        protected override void OnRealized ()
        {
            Events |= Gdk.EventMask.PointerMotionMask;

            base.OnRealized ();

            GdkWindow.OverrideRedirect = true;

            Screen.SizeChanged += OnScreenSizeChanged;
        }

        protected override void OnUnrealized ()
        {
            base.OnUnrealized ();
            Screen.SizeChanged -= OnScreenSizeChanged;
        }

        protected override bool OnDeleteEvent (Gdk.Event evnt)
        {
            Hide ();
            return true;
        }

        protected override void OnShown ()
        {
            base.OnShown ();
            if (Child != null) {
                Child.Show ();
            }

            OnHideCursorTimeout ();
            ConfigureWindow ();
            HasFocus = true;
            parent.AddNotification ("is-active", ParentActiveNotification);
        }

        protected override void OnHidden ()
        {
            base.OnHidden ();
            DestroyControls ();
        }

        private void OnScreenSizeChanged (object o, EventArgs args)
        {
            ConfigureWindow ();
        }

        private void ParentActiveNotification (object o, GLib.NotifyArgs args)
        {
            // If our parent window is ever somehow activated while we are
            // visible, this will ensure we merge back into the parent
            if (parent.IsActive) {
                parent.GdkWindow.SkipPagerHint = false;
                parent.GdkWindow.SkipTaskbarHint = false;
                Hide ();
                parent.RemoveNotification ("is-active", ParentActiveNotification);
            } else {
                parent.GdkWindow.SkipPagerHint = true;
                parent.GdkWindow.SkipTaskbarHint = true;
            }
        }

#endregion

#region Control Window

        private void ShowControls ()
        {
            if (controls == null) {
                controls = new FullscreenControls (this, action_service);
            }

            controls.Show ();
        }

        private void HideControls ()
        {
            if (controls != null) {
                controls.Hide ();
                QueueDraw ();
            }
        }

        private void DestroyControls ()
        {
            if (controls != null) {
                controls.Destroy ();
                controls = null;
            }
        }

        private bool ControlsActive {
            get {
                if (controls == null || !controls.Visible) {
                    return false;
                } else if (controls.Active) {
                    return true;
                }

                int cursor_x, cursor_y;
                int window_x, window_y;

                controls.GdkWindow.Screen.Display.GetPointer (out cursor_x, out cursor_y);
                controls.GetPosition (out window_x, out window_y);

                Gdk.Rectangle box = new Gdk.Rectangle (window_x, window_y,
                    controls.Allocation.Width, controls.Allocation.Height);

                return box.Contains (cursor_x, cursor_y);
            }
        }

#endregion

#region Mouse Cursor Logic

        private const int CursorUpdatePositionDelay = 500;   // How long (ms) before the cursor position is updated
        private const int CursorHideDelay = 5000;            // How long (ms) to remain stationary before it hides
        private const int CursorShowMovementThreshold = 150; // How far (px) to move before it shows again

        private uint hide_cursor_timeout_id;
        private uint cursor_update_position_timeout_id;
        private int hide_cursor_x;
        private int hide_cursor_y;
        private bool cursor_is_hidden = false;

        protected override bool OnMotionNotifyEvent (Gdk.EventMotion evnt)
        {
            if (cursor_is_hidden) {
                if (Math.Abs (hide_cursor_x - evnt.X) > CursorShowMovementThreshold ||
                    Math.Abs (hide_cursor_y - evnt.Y) > CursorShowMovementThreshold) {
                    ShowCursor ();
                    ShowControls ();
                } else {
                    if (cursor_update_position_timeout_id > 0) {
                        GLib.Source.Remove (cursor_update_position_timeout_id);
                    }

                    cursor_update_position_timeout_id = GLib.Timeout.Add (CursorUpdatePositionDelay,
                        OnCursorUpdatePositionTimeout);
                }
            } else if (!ControlsActive) {
                if (hide_cursor_timeout_id > 0) {
                    GLib.Source.Remove (hide_cursor_timeout_id);
                }

                hide_cursor_timeout_id = GLib.Timeout.Add (CursorHideDelay, OnHideCursorTimeout);
            }

            return base.OnMotionNotifyEvent (evnt);
        }

        private bool OnCursorUpdatePositionTimeout ()
        {
            UpdateHiddenCursorPosition ();
            cursor_update_position_timeout_id = 0;
            return false;
        }

        private bool OnHideCursorTimeout ()
        {
            if (!ControlsActive) {
                HideCursor ();
                HideControls ();
            }

            hide_cursor_timeout_id = 0;
            return false;
        }

        private void UpdateHiddenCursorPosition ()
        {
            GetPointer (out hide_cursor_x, out hide_cursor_y);
        }

        private void ShowCursor ()
        {
            cursor_is_hidden = false;
            GdkWindow.Cursor = null;
        }

        private void HideCursor ()
        {
            if (GdkWindow == null) {
                return;
            }

            Gdk.Pixmap pixmap = Gdk.Pixmap.CreateBitmapFromData (GdkWindow, "0x0", 1, 1);
            if (pixmap == null) {
                return;
            }

            UpdateHiddenCursorPosition ();
            cursor_is_hidden = true;

            Gdk.Color color = new Gdk.Color (0, 0, 0);
            Gdk.Cursor cursor = new Gdk.Cursor (pixmap, pixmap, color, color, 0, 0);

            GdkWindow.Cursor = cursor;

            pixmap.Dispose ();
            cursor.Dispose ();
        }

#endregion

    }
}
