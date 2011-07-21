//
// TrackInfoPopup.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
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
using Gtk;

using Banshee.Base;
using Banshee.Gui.Widgets;
using Banshee.Widgets;
using Banshee.ServiceStack;
using Banshee.Gui;
using Hyena;

namespace Banshee.NotificationArea
{
    public class TrackInfoPopup : Gtk.Window
    {
        private TrackInfoDisplay header;
        private ConnectedSeekSlider seek_slider;

        public TrackInfoPopup () : base (Gtk.WindowType.Popup)
        {
            BorderWidth = 8;
            AppPaintable = true;
            Resizable = false;
            TypeHint = Gdk.WindowTypeHint.Notification;

            VBox box = new VBox ();
            box.Spacing = 4;

            header = new ClassicTrackInfoDisplay ();
            header.SetSizeRequest (320, 64);

            seek_slider = new ConnectedSeekSlider (SeekSliderLayout.Horizontal);
            seek_slider.StreamPositionLabel.FormatString = "<small>{0}</small>";
            seek_slider.LeftPadding = 0;
            seek_slider.RightPadding = 0;

            box.PackStart (header, true, true, 0);
            box.PackStart (seek_slider, false, false, 0);

            Add (box);
            box.ShowAll ();
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                seek_slider.Disconnect ();
                header.Dispose ();
            }
            base.Dispose (disposing);
        }

        protected override bool OnDrawn (Cairo.Context cr)
        {
            StyleContext.Save ();
            StyleContext.AddClass ("tooltip");
            StyleContext.RenderBackground (cr, 0, 0, Allocation.Width, Allocation.Height);
            StyleContext.RenderFrame (cr, 0, 0, Allocation.Width, Allocation.Height);
            StyleContext.Restore ();
            return base.OnDrawn (cr);
        }
    }
}
