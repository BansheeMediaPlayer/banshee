//
// ColumnCellCheckBox.cs
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

namespace Hyena.Data.Gui
{    
    public class ColumnCellCheckBox : ColumnCell, IInteractiveCell, ISizeRequestCell
    {   
        public ColumnCellCheckBox (string property, bool expand) : base (property, expand)
        {
        }
            
        public override void Render (CellContext context, StateType state, double cellWidth, double cellHeight)
        {
            int cell_width = context.Area.Width - 2 * Xpad;
            int cell_height = context.Area.Height - 2 * Ypad;
            int x = context.Area.X + xpad + ((cell_width - Size) / 2);
            int y = context.Area.Y + ypad + ((cell_height - Size) / 2);
            
            if (state == StateType.Normal && last_hover_bound == BoundObjectParent) {
                state = StateType.Prelight;
            }
            
            Style.PaintCheck (context.Widget.Style, context.Drawable, state, 
                Value ? ShadowType.In : ShadowType.Out, 
                context.Clip, context.Widget, "cellcheck", x, y, Size, Size);
        }
        
        private object last_pressed_bound;
        private object last_hover_bound;
        
        public bool ButtonEvent (int x, int y, bool pressed, Gdk.EventButton evnt)
        {
            if (pressed) {
                last_pressed_bound = BoundObjectParent;
                return false;
            }
            
            if (last_pressed_bound != null && last_pressed_bound.Equals (BoundObjectParent)) {
                Value = !Value;
                last_pressed_bound = null;
            }
            
            return true;
        }
        
        public bool MotionEvent (int x, int y, Gdk.EventMotion evnt)
        {
            if (last_hover_bound == BoundObjectParent) {
                return false;
            }
            
            last_hover_bound = BoundObjectParent;
            return true;
        }
        
        public bool PointerLeaveEvent ()
        {
            last_hover_bound = null;
            return true;
        }
        
        public void GetWidthRange (Pango.Layout layout, out int min, out int max)
        {
            min = max = 2 * Xpad + Size;
        }
        
        private bool restrict_size = true;
        public bool RestrictSize {
            get { return restrict_size; }
            set { restrict_size = value; }
        }
        
        private bool Value {
            get { return (bool)BoundObject; }
            set { BoundObject = value; }
        }
        
        private int size = 13;
        public int Size {
            get { return size; }
            set { size = value; }
        }
        
        private int xpad = 2;
        public int Xpad {
            get { return xpad; }
            set { xpad = value; }
        }
        
        public int ypad = 2;
        public int Ypad {
            get { return ypad; }
            set { ypad = value; }
        }
    }
}
