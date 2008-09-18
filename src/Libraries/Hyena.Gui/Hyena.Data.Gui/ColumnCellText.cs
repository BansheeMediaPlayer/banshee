//
// ColumnCellText.cs
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

using Hyena.Gui;
using Hyena.Gui.Theming;

namespace Hyena.Data.Gui
{
    public class ColumnCellText : ColumnCell, ISizeRequestCell, ITextCell
    {
        internal const int Spacing = 4;

        public delegate string DataHandler ();
    
        private Pango.Weight font_weight = Pango.Weight.Normal;
        private Pango.EllipsizeMode ellipsize_mode = Pango.EllipsizeMode.End;
        private Pango.Alignment alignment = Pango.Alignment.Left;
        private double opacity = 1.0;
        private int text_width;
        private int text_height;
        private string text_format = null;
        
        public ColumnCellText (string property, bool expand) : base (property, expand)
        {
        }

        protected void SetMinMaxStrings (object min_max)
        {
            SetMinMaxStrings (min_max, min_max);
        }
        
        protected void SetMinMaxStrings (object min, object max)
        {
            // Set the min/max strings from the min/max objects
            min_string = GetText (min);
            max_string = GetText (max);
            RestrictSize = true;
        }
    
        public override void Render (CellContext context, StateType state, double cellWidth, double cellHeight)
        {
            string text = GetText (BoundObject);
            if (String.IsNullOrEmpty (text)) {
                return;
            }

            // TODO why doesn't Spacing (eg 4 atm) work here instead of 8?  Rendering
            // seems to be off when changed to Spacing/4
            context.Layout.Width = (int)((cellWidth - 8) * Pango.Scale.PangoScale);
            context.Layout.FontDescription.Weight = font_weight;
            context.Layout.Ellipsize = EllipsizeMode;
            context.Layout.Alignment = alignment;

            if (text_format == null) {
                context.Layout.SetText (text);
            } else {
                context.Layout.SetText (String.Format (text_format, text));
            }
            
            context.Layout.GetPixelSize (out text_width, out text_height);
            
            context.Context.MoveTo (Spacing, ((int)cellHeight - text_height) / 2);
            Cairo.Color color = context.Theme.Colors.GetWidgetColor (
                context.TextAsForeground ? GtkColorClass.Foreground : GtkColorClass.Text, state);
            color.A = (!context.Sensitive) ? 0.3 : opacity;
            context.Context.Color = color;

            PangoCairoHelper.ShowLayout (context.Context, context.Layout);
        }
        
        protected virtual string GetText (object obj)
        {
            return obj == null ? String.Empty : obj.ToString ();
        }

        private string min_string;
        private string max_string;
        
        protected int TextWidth {
            get { return text_width; }
        }
        
        protected int TextHeight {
            get { return text_height; }
        }

        public string TextFormat {
            get { return text_format; }
            set { text_format = value; }
        }
        
        protected Pango.Alignment Alignment {
            get { return alignment; }
            set { alignment = value; }
        }
        
        public virtual Pango.Weight FontWeight {
            get { return font_weight; }
            set { font_weight = value; }
        }
        
        public virtual Pango.EllipsizeMode EllipsizeMode {
            get { return ellipsize_mode; }
            set { ellipsize_mode = value; }
        }
        
        public virtual double Opacity {
            get { return opacity; }
            set { opacity = value; }
        }
        
        internal static int ComputeRowHeight (Widget widget)
        {
            int w_width, row_height;
            Pango.Layout layout = new Pango.Layout (widget.PangoContext);
            layout.SetText ("W");
            layout.GetPixelSize (out w_width, out row_height);
            layout.Dispose ();
            return row_height + 8;
        }


        #region ISizeRequestCell implementation 
        
        public void GetWidthRange (Pango.Layout layout, out int min, out int max)
        {
            int height;
            min = max = -1;
            
            if (!String.IsNullOrEmpty (min_string)) {
                layout.SetText (min_string);
                layout.GetPixelSize (out min, out height);
                min += 2*Spacing;
                //Console.WriteLine ("for {0} got min {1} for {2}", this, min, min_string);
            }

            if (!String.IsNullOrEmpty (max_string)) {
                layout.SetText (max_string);
                layout.GetPixelSize (out max, out height);
                max += 2*Spacing;
                //Console.WriteLine ("for {0} got max {1} for {2}", this, max, max_string);
            }
        }
        
        private bool restrict_size = false;
        public bool RestrictSize {
            get { return restrict_size; }
            set { restrict_size = value; }
        }
        
        #endregion
    }
}
