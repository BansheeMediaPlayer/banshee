//
// ColumnCellTrack.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
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
using Cairo;

using Hyena.Gui;
using Hyena.Data.Gui;
using Hyena.Gui.Theming;

namespace Banshee.Collection.Gui
{
    public class ColumnCellTrack : ColumnCell, ITextCell
    {
        private Pango.Weight font_weight = Pango.Weight.Normal;

        public ColumnCellTrack () : base (null, true)
        {
        }

        public override Gdk.Size Measure (Widget widget)
        {
            using (var layout = new Pango.Layout (widget.PangoContext)) {
                int lw, lh;
                layout.SetMarkup ("<b>W</b>\n<small><i>W</i></small>");
                layout.GetPixelSize (out lw, out lh);
                return new Gdk.Size (0, lh + 8);
            }
        }

        public override void Render (CellContext context, double cellWidth, double cellHeight)
        {
            if (BoundObject == null) {
                return;
            }

            if (!(BoundObject is TrackInfo)) {
                throw new InvalidCastException ("ColumnCellAlbum can only bind to AlbumInfo objects");
            }

            TrackInfo track = (TrackInfo)BoundObject;

            context.Layout.Width = (int)((cellWidth - 8) * Pango.Scale.PangoScale);
            context.Layout.Ellipsize = Pango.EllipsizeMode.End;
            context.Layout.FontDescription.Weight = font_weight;
            context.Layout.SetMarkup (String.Format ("<b>{0}</b>\n<small><i>{1}</i></small>",
                GLib.Markup.EscapeText (track.DisplayTrackTitle),
                GLib.Markup.EscapeText (track.DisplayArtistName)));

            int text_width;
            int text_height;
            context.Layout.GetPixelSize (out text_width, out text_height);

            context.Context.MoveTo (4, ((int)cellHeight - text_height) / 2);
            context.Widget.StyleContext.Save ();
            context.Widget.StyleContext.AddClass ("entry");
            Cairo.Color color = CairoExtensions.GdkRGBAToCairoColor (context.Widget.StyleContext.GetColor (StateFlags.Selected));
            context.Widget.StyleContext.Restore ();
            color.A = (!context.Opaque) ? 0.3 : 1.0;
            context.Context.SetSourceColor (color);

            Pango.CairoHelper.ShowLayout (context.Context, context.Layout);
        }

        public Pango.Weight FontWeight {
            get { return font_weight; }
            set { font_weight = value; }
        }
    }
}
