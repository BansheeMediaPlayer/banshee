//
// ClassicTrackInfoDisplay.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2007-2008 Novell, Inc.
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
using Gdk;
using Gtk;
using Cairo;

using Hyena.Gui;
using Banshee.Collection;
using Banshee.Collection.Gui;

namespace Banshee.Gui.Widgets
{
    public class ClassicTrackInfoDisplay : TrackInfoDisplay
    {
        private Gdk.Window event_window;

        private ArtworkPopup popup;
        private uint popup_timeout_id;
        private bool in_popup;
        private bool in_thumbnail_region;
        private Pango.Layout first_line_layout;
        private Pango.Layout second_line_layout;

        public ClassicTrackInfoDisplay () : base ()
        {
            ArtworkSpacing = 10;
        }

        protected ClassicTrackInfoDisplay (IntPtr native) : base (native)
        {
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                HidePopup ();
            }
            base.Dispose (disposing);
        }

        protected override int ArtworkSizeRequest {
            get { return artwork_size ?? base.ArtworkSizeRequest; }
        }

        private int? artwork_size;
        public int ArtworkSize {
            get { return ArtworkSizeRequest; }
            set { artwork_size = value; }
        }

        public int ArtworkSpacing { get; set; }

#region Widget Window Management

        protected override void OnRealized ()
        {
            base.OnRealized ();

            WindowAttr attributes = new WindowAttr ();
            attributes.WindowType = Gdk.WindowType.Child;
            attributes.X = Allocation.X;
            attributes.Y = Allocation.Y;
            attributes.Width = Allocation.Width;
            attributes.Height = Allocation.Height;
            attributes.Wclass = WindowWindowClass.InputOnly;
            attributes.EventMask = (int)(
                EventMask.PointerMotionMask |
                EventMask.EnterNotifyMask |
                EventMask.LeaveNotifyMask |
                EventMask.ExposureMask);

            WindowAttributesType attributes_mask =
                WindowAttributesType.X | WindowAttributesType.Y | WindowAttributesType.Wmclass;

            event_window = new Gdk.Window (Window, attributes, attributes_mask);
            event_window.UserData = Handle;
        }

        protected override void OnUnrealized ()
        {
            IsRealized = false;
            event_window.UserData = IntPtr.Zero;
            event_window.Destroy ();
            event_window = null;

            base.OnUnrealized ();
        }

        protected override void OnMapped ()
        {
            event_window.Show ();
            base.OnMapped ();
        }

        protected override void OnUnmapped ()
        {
            event_window.Hide ();
            base.OnUnmapped ();
        }

        protected override void OnSizeAllocated (Gdk.Rectangle allocation)
        {
            base.OnSizeAllocated (allocation);

            if (IsRealized) {
                event_window.MoveResize (allocation);
            }
        }

        protected override void OnGetPreferredHeight (out int minimum_height, out int natural_height)
        {
            minimum_height = ComputeWidgetHeight ();
            natural_height = minimum_height;
        }

        private int ComputeWidgetHeight ()
        {
            int width, height;
            Pango.Layout layout = new Pango.Layout (PangoContext);
            layout.SetText ("W");
            layout.GetPixelSize (out width, out height);
            layout.Dispose ();
            return 2 * height;
        }

        protected override void OnThemeChanged ()
        {
            if (first_line_layout != null) {
                first_line_layout.FontDescription.Dispose ();
                first_line_layout.Dispose ();
                first_line_layout = null;
            }

            if (second_line_layout != null) {
                second_line_layout.FontDescription.Dispose ();
                second_line_layout.Dispose ();
                second_line_layout = null;
            }
        }

#endregion

#region Drawing

        protected override void RenderTrackInfo (Context cr, TrackInfo track, bool renderTrack, bool renderArtistAlbum)
        {
            if (track == null) {
                return;
            }

            double offset = ArtworkSizeRequest + ArtworkSpacing, y = 0;
            double x = offset;
            double width = Allocation.Width - offset;
            int fl_width, fl_height, sl_width, sl_height;
            int pango_width = (int)(width * Pango.Scale.PangoScale);

            if (first_line_layout == null) {
                first_line_layout = CairoExtensions.CreateLayout (this, cr);
                first_line_layout.Ellipsize = Pango.EllipsizeMode.End;
            }

            if (second_line_layout == null) {
                second_line_layout = CairoExtensions.CreateLayout (this, cr);
                second_line_layout.Ellipsize = Pango.EllipsizeMode.End;
            }

            // Set up the text layouts
            first_line_layout.Width = pango_width;
            second_line_layout.Width = pango_width;

            // Compute the layout coordinates
            first_line_layout.SetMarkup (GetFirstLineText (track));
            first_line_layout.GetPixelSize (out fl_width, out fl_height);
            second_line_layout.SetMarkup (GetSecondLineText (track));
            second_line_layout.GetPixelSize (out sl_width, out sl_height);

            if (fl_height + sl_height > Allocation.Height) {
                SetSizeRequest (-1, fl_height + sl_height);
            }

            y = (Allocation.Height - (fl_height + sl_height)) / 2;

            // Render the layouts
            cr.Antialias = Cairo.Antialias.Default;

            if (renderTrack) {
                cr.MoveTo (x, y);
                Pango.CairoHelper.ShowLayout (cr, first_line_layout);
            }

            if (!renderArtistAlbum) {
                return;
            }

            cr.MoveTo (x, y + fl_height);
            Pango.CairoHelper.ShowLayout (cr, second_line_layout);
        }

#endregion

#region Interaction Events

        protected override bool OnEnterNotifyEvent (EventCrossing evnt)
        {
            in_thumbnail_region = evnt.X <= Allocation.Height;
            return ShowHideCoverArt ();
        }

        protected override bool OnLeaveNotifyEvent (EventCrossing evnt)
        {
            in_thumbnail_region = false;
            return ShowHideCoverArt ();
        }

        protected override bool OnMotionNotifyEvent (EventMotion evnt)
        {
            in_thumbnail_region = evnt.X <= Allocation.Height;
            return ShowHideCoverArt ();
        }

        private void OnPopupEnterNotifyEvent (object o, EnterNotifyEventArgs args)
        {
            in_popup = true;
        }

        private void OnPopupLeaveNotifyEvent (object o, LeaveNotifyEventArgs args)
        {
            in_popup = false;
            HidePopup ();
        }

        private bool ShowHideCoverArt ()
        {
            if (!in_thumbnail_region) {
                if (popup_timeout_id > 0) {
                    GLib.Source.Remove (popup_timeout_id);
                    popup_timeout_id = 0;
                }

                GLib.Timeout.Add (100, delegate {
                    if (!in_popup) {
                        HidePopup ();
                    }

                    return false;
                });
            } else {
                if (popup_timeout_id > 0) {
                    return false;
                }

                popup_timeout_id = GLib.Timeout.Add (500, delegate {
                    if (in_thumbnail_region) {
                        UpdatePopup ();
                    }

                    popup_timeout_id = 0;
                    return false;
                });
            }

            return true;
        }

#endregion

#region Popup Window

        protected override void OnArtworkChanged ()
        {
            UpdatePopup ();
        }

        private bool UpdatePopup ()
        {
            if (CurrentTrack == null || ArtworkManager == null || !in_thumbnail_region) {
                HidePopup ();
                return false;
            }

            Gdk.Pixbuf pixbuf = ArtworkManager.LookupPixbuf (CurrentTrack.ArtworkId);

            if (pixbuf == null) {
                HidePopup ();
                return false;
            }

            if (popup == null) {
                popup = new ArtworkPopup ();
                popup.EnterNotifyEvent += OnPopupEnterNotifyEvent;
                popup.LeaveNotifyEvent += OnPopupLeaveNotifyEvent;
            }

            popup.Label = String.Format ("{0} - {1}", CurrentTrack.DisplayArtistName,
                CurrentTrack.DisplayAlbumTitle);

            if (popup.Image != null) {
                ArtworkManager.DisposePixbuf (popup.Image);
            }
            popup.Image = pixbuf;

            if (in_thumbnail_region) {
                popup.Show ();
            }

            return true;
        }

        private void HidePopup ()
        {
            if (popup != null) {
                ArtworkManager.DisposePixbuf (popup.Image);
                popup.Destroy ();
                popup.EnterNotifyEvent -= OnPopupEnterNotifyEvent;
                popup.LeaveNotifyEvent -= OnPopupLeaveNotifyEvent;
                popup = null;
            }
        }

#endregion

    }
}
