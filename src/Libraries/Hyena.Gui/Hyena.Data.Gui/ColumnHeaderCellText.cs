//
// ColumnHeaderCellText.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2007 Novell, Inc.
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

namespace Hyena.Data.Gui
{
    public class ColumnHeaderCellText : ColumnCellText, IHeaderCell
    {
        public delegate Column DataHandler ();
        
        private DataHandler data_handler;
        private bool has_sort;
        
        public ColumnHeaderCellText (DataHandler data_handler) : base(null, true)
        {
            this.data_handler = data_handler;
        }
    
        public override void Render (CellContext context, StateType state, double cellWidth, double cellHeight)
        {
            if(data_handler == null) {
                return;
            }
            
            if (!has_sort) {
                base.Render (context, state, cellWidth - 10, cellHeight);
                return;
            }
            
            Gdk.Rectangle alloc = new Gdk.Rectangle ();
            alloc.Width = (int)(cellHeight / 3.5);
            alloc.Height = (int)((double)alloc.Width / 1.6);
            alloc.X = (int)cellWidth - alloc.Width - 10;
            alloc.Y = ((int)cellHeight - alloc.Height) / 2;
            
            context.Theme.DrawColumnHighlight (context.Context, cellWidth, cellHeight);
            base.Render (context, state, cellWidth - 2 * alloc.Width - 10, cellHeight);
            context.Theme.DrawArrow (context.Context, alloc, ((ISortableColumn)data_handler ()).SortType);
        }
        
        protected override string Text {
            get { return data_handler ().Title; }
        }
        
        public bool HasSort {
            get { return has_sort; }
            set { has_sort = value; }
        }
        
        public int MinWidth {
            get { return TextWidth + 25; }
        }
    }
}
