//
// ColumnArtistCellText.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//   Frank Ziegler <funtastix@googlemail.com>
//
// Copyright (C) 2007 Novell, Inc.
// Copyright (C) 2013 Frank Ziegler
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

using Gtk;

using Hyena.Data.Gui;

using Banshee.I18n;

namespace Banshee.Collection.Gui
{
    public class ColumnCellArtistText : ColumnCellText, IArtistListRenderer
    {
        private readonly ColumnController column_controller;
        private readonly string name = Catalog.GetString ("Default Text Artist List");

        public ColumnCellArtistText () : base ("DisplayName", true)
        {
            column_controller = new ColumnController ();
            var current_layout = new Column ("Artist", this, 1.0);
            column_controller.Add (current_layout);
        }

        public string Name {
            get { return name; }
        }

        public ColumnController ColumnController {
            get { return column_controller; }
        }

        public int ComputeRowHeight (Widget widget)
        {
            int w_width, row_height;
            using (var layout = new Pango.Layout (widget.PangoContext)) {
                layout.SetText ("W");
                layout.GetPixelSize (out w_width, out row_height);
                return row_height + 8;
            }
        }
    }
}
