// 
// ColumnCellCreativeCommons.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
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

using Banshee.Gui;
using Banshee.Streaming;
using Banshee.MediaEngine;
using Banshee.ServiceStack;

namespace Banshee.Collection.Gui
{
    public class ColumnCellCreativeCommons : ColumnCell
    {
        private static Gdk.Pixbuf [] pixbufs;
        private static string [] attributes = new string [] {"by", "nc", "nd", "sa", "pd"};
        private const string CC_ID = "creativecommons.org/licenses/";
        private static int CC_ID_LEN = CC_ID.Length;

        public ColumnCellCreativeCommons (string property, bool expand) : base (property, expand)
        {
            LoadPixbufs ();
        }

        private void LoadPixbufs ()
        {
            if (pixbufs == null) {
                pixbufs = new Gdk.Pixbuf[attributes.Length];
            }
            
            for (int i = 0; i < attributes.Length; i++) {
                pixbufs[i] = IconThemeUtils.LoadIcon (16, String.Format ("creative-commons-{0}", attributes[i]));
            }
        }

        public override void Render (CellContext context, StateType state, double cellWidth, double cellHeight)
        {
            string license_uri = BoundObject as string;
            if (String.IsNullOrEmpty (license_uri)) {
                return;
            }

            int start_index = license_uri.IndexOf (CC_ID);
            if (start_index == -1) {
                return;
            }

            start_index += CC_ID_LEN;
            context.Context.Translate (0, 0.5);

            int draw_x = 0;
            for (int i = 0; i < attributes.Length; i++) {
                if (license_uri.IndexOf (attributes[i], start_index) != -1) {
                    Gdk.Pixbuf render_pixbuf = pixbufs[i];

                    Cairo.Rectangle pixbuf_area = new Cairo.Rectangle (draw_x,
                        (cellHeight - render_pixbuf.Height) / 2, render_pixbuf.Width, render_pixbuf.Height);
                    
                    if (!context.Opaque) {
                        context.Context.Save ();
                    }
                    
                    Gdk.CairoHelper.SetSourcePixbuf (context.Context, render_pixbuf, pixbuf_area.X, pixbuf_area.Y);
                    context.Context.Rectangle (pixbuf_area);
                    
                    if (!context.Opaque) {
                        context.Context.Clip ();
                        context.Context.PaintWithAlpha (0.5);
                        context.Context.Restore ();
                    } else {
                        context.Context.Fill ();
                    }

                    draw_x += render_pixbuf.Width;
                }
            }
        }
    }
}
