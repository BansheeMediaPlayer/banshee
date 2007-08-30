//
// ListViewGraphics.cs
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
using Gdk;
using Cairo;

namespace Hyena.Data.Gui
{
    public enum GtkColorClass 
    {
        Light,
        Mid,
        Dark,
        Base,
        Background,
        Foreground
    }
    
    public class ListViewGraphics
    {
        private const int border_radius = 5;
        
        private Cairo.Color [] gtk_colors;
        
        private Cairo.Color selection_fill;
        private Cairo.Color selection_stroke;
        
        private Cairo.Color view_fill;
        private Cairo.Color view_fill_transparent;
        
        private Cairo.Color border_color;
        
        private Widget widget;
        
        public ListViewGraphics(Widget widget)
        {
            this.widget = widget;
            widget.StyleSet += delegate { RefreshColors(); };
        }
        
        public Cairo.Color GetWidgetColor(GtkColorClass @class, StateType state)
        {
            if(gtk_colors == null) {
                RefreshColors();
            }
            
            return gtk_colors[(int)@class * (int)GtkColorClass.Foreground + (int)state];
        }
        
        private bool refreshing = false;
        
        public void RefreshColors()
        {
            if(refreshing) {
                return;
            }
            
            refreshing = true;
            
            int mc = (int)GtkColorClass.Foreground;
            int ms = (int)StateType.Insensitive;
            
            if(gtk_colors == null) {
                gtk_colors = new Cairo.Color[(mc + 1) * (ms + 1)];
            }
                
            for(int c = (int)GtkColorClass.Light; c <= mc; c++) {
                for(int s = (int)StateType.Normal; s <= ms; s++) {
                    Gdk.Color color = Gdk.Color.Zero;
                    
                    if(widget != null && widget.IsRealized) {
                        switch((GtkColorClass)c) {
                            case GtkColorClass.Light:      color = widget.Style.LightColors[s]; break;
                            case GtkColorClass.Mid:        color = widget.Style.MidColors[s];   break;
                            case GtkColorClass.Dark:       color = widget.Style.DarkColors[s];  break;
                            case GtkColorClass.Base:       color = widget.Style.BaseColors[s];  break;
                            case GtkColorClass.Background: color = widget.Style.Backgrounds[s]; break;
                            case GtkColorClass.Foreground: color = widget.Style.Foregrounds[s]; break;
                        }
                    } else {
                        color = new Gdk.Color(0, 0, 0);
                    }
                    
                    gtk_colors[c * mc + s] = CairoExtensions.GdkColorToCairoColor(color);
                }
            }
            
            selection_fill = GetWidgetColor(GtkColorClass.Dark, StateType.Active);
            selection_stroke = GetWidgetColor(GtkColorClass.Background, StateType.Selected);
            
            view_fill = GetWidgetColor(GtkColorClass.Base, StateType.Normal);
            view_fill_transparent = view_fill;
            view_fill_transparent.A = 0;
            
            border_color = GetWidgetColor(GtkColorClass.Dark, StateType.Active);
            
            refreshing = false;
        }
        
        public void DrawHeaderSeparator(Cairo.Context cr, Gdk.Rectangle alloc, int x, int bottom_offset)
        {
            Cairo.Color gtk_background_color = GetWidgetColor(GtkColorClass.Background, StateType.Normal);
            Cairo.Color dark_color = CairoExtensions.ColorShade(gtk_background_color, 0.80);
            Cairo.Color light_color = CairoExtensions.ColorShade(gtk_background_color, 1.1);
            
            int y_1 = alloc.Y + 2;
            int y_2 = alloc.Y + alloc.Height - 4 - bottom_offset;
            
            cr.LineWidth = 1;
            cr.Antialias = Cairo.Antialias.None;
            
            cr.Color = dark_color;
            cr.MoveTo(x, y_1);
            cr.LineTo(x, y_2);
            cr.Stroke();
            
            cr.Color = light_color;
            cr.MoveTo(x + 1, y_1);
            cr.LineTo(x + 1, y_2);
            cr.Stroke();
            
            cr.Antialias = Cairo.Antialias.Default;
        }
        
        public void DrawHeaderBackground(Cairo.Context cr, Gdk.Rectangle alloc, int bottom_offset, bool fill)
        {
            Cairo.Color gtk_background_color = GetWidgetColor(GtkColorClass.Background, StateType.Normal);
            Cairo.Color gtk_base_color = GetWidgetColor(GtkColorClass.Base, StateType.Normal);
            Cairo.Color light_color = CairoExtensions.ColorShade(gtk_background_color, 1.1);
            Cairo.Color dark_color = CairoExtensions.ColorShade(gtk_background_color, 0.95);
            
            CairoCorners corners = CairoCorners.TopLeft | CairoCorners.TopRight;
            
            if(fill) {
                LinearGradient grad = new LinearGradient(alloc.X, alloc.Y, alloc.X, alloc.Y + alloc.Height);
                grad.AddColorStop(0, light_color);
                grad.AddColorStop(0.75, dark_color);
                grad.AddColorStop(0, light_color);
            
                cr.Pattern = grad;
                CairoExtensions.RoundedRectangle(cr, alloc.X, alloc.Y, alloc.Width, 
                alloc.Height - bottom_offset, border_radius, corners);
                cr.Fill();
            
                cr.Color = gtk_base_color;
                cr.Rectangle(alloc.X, alloc.Y + alloc.Height - bottom_offset, alloc.Width, bottom_offset);
                cr.Fill();
            } else {
                cr.Color = gtk_base_color;
                CairoExtensions.RoundedRectangle(cr, alloc.X, alloc.Y, alloc.Width, alloc.Height, border_radius, corners);
                cr.Fill();
            }
            
            cr.LineWidth = 1.0;
            cr.Translate(alloc.X + 0.5, alloc.Y + 0.5);
            cr.Color = border_color;
            CairoExtensions.RoundedRectangle(cr, alloc.X, alloc.Y, alloc.Width - 1, 
                alloc.Height + 4, border_radius, corners);
            cr.Stroke();
            
            if(fill) {
                cr.LineWidth = 1;
                cr.Antialias = Cairo.Antialias.None;
                cr.MoveTo(alloc.X + 1, alloc.Y + alloc.Height - 1 - bottom_offset);
                cr.LineTo(alloc.X + alloc.Width - 1, alloc.Y + alloc.Height - 1 - bottom_offset);
                cr.Stroke();
                cr.Antialias = Cairo.Antialias.Default;
            }
        }
        
        public void DrawColumnHighlight(Cairo.Context cr, Gdk.Rectangle alloc, int bottom_offset)
        {
            Cairo.Color gtk_selection_color = GetWidgetColor(GtkColorClass.Background, StateType.Selected);
            Cairo.Color light_color = CairoExtensions.ColorShade(gtk_selection_color, 1.6);
            Cairo.Color dark_color = CairoExtensions.ColorShade(gtk_selection_color, 1.3);
            
            LinearGradient grad = new LinearGradient(alloc.X, alloc.Y + 2, alloc.X, alloc.Y + alloc.Height - 3 - bottom_offset);
            grad.AddColorStop(0, light_color);
            grad.AddColorStop(1, dark_color);
            
            cr.Pattern = grad;
            cr.Rectangle(alloc.X, alloc.Y + 2, alloc.Width - 1, alloc.Height - 3 - bottom_offset);
            cr.Fill();
        }
        
        public void DrawFooter(Cairo.Context cr, Gdk.Rectangle alloc)
        {
            Cairo.Color gtk_base_color = GetWidgetColor(GtkColorClass.Base, StateType.Normal);
            CairoCorners corners = CairoCorners.BottomLeft | CairoCorners.BottomRight;
            
            cr.Color = gtk_base_color;
            CairoExtensions.RoundedRectangle(cr, alloc.X , alloc.Y, alloc.Width, 
                alloc.Height, border_radius, corners);
            cr.Fill();
            
            cr.LineWidth = 1.0;
            cr.Translate(alloc.Y + 0.5, alloc.Y + 0.5);
            
            cr.Color = border_color;
            CairoExtensions.RoundedRectangle(cr, alloc.X, alloc.Y - 4, alloc.Width - 1, 
                alloc.Height + 3, border_radius, corners);
            cr.Stroke();
        }
        
        public void DrawLeftBorder(Cairo.Context cr, Gdk.Rectangle alloc)
        {
            DrawLeftOrRightBorder(cr, alloc.X + 1, alloc);
        }
        
        public void DrawRightBorder(Cairo.Context cr, Gdk.Rectangle alloc)
        {
            DrawLeftOrRightBorder(cr, alloc.X + alloc.Width, alloc);   
        }
        
        private void DrawLeftOrRightBorder(Cairo.Context cr, int x, Gdk.Rectangle alloc)
        {
            cr.LineWidth = 1.0;
            cr.Antialias = Cairo.Antialias.None;
            
            cr.Color = border_color;
            cr.MoveTo(x, alloc.Y);
            cr.LineTo(x, alloc.Y + alloc.Height);
            cr.Stroke();
            
            cr.Antialias = Cairo.Antialias.Default;
        }
        
        public void DrawRowSelection(Cairo.Context cr, int x, int y, int width, int height)
        {
            Cairo.Color selection_color = GetWidgetColor(GtkColorClass.Background, StateType.Selected);
            Cairo.Color selection_stroke = CairoExtensions.ColorShade(selection_color, 0.75);
            Cairo.Color selection_fill_light = CairoExtensions.ColorShade(selection_color, 1.1);
            Cairo.Color selection_fill_dark = CairoExtensions.ColorShade(selection_color, 0.85);
            
            LinearGradient grad = new LinearGradient(x, y, x, y + height);
            grad.AddColorStop(0, selection_fill_light);
            grad.AddColorStop(1, selection_fill_dark);
            
            cr.Pattern = grad;
            CairoExtensions.RoundedRectangle(cr, x, y, width, height, border_radius);
            cr.Fill();
            
            cr.LineWidth = 0.5;
            cr.Color = selection_stroke;
            CairoExtensions.RoundedRectangle(cr, x + 1, y + 1, width - 2, height - 2, border_radius);
            cr.Stroke();
        }
        
        public Cairo.Color ViewFill {
            get { return view_fill; }
        }
        
        public Cairo.Color ViewFillTransparent {
            get { return view_fill_transparent; }
        }
        
        public Cairo.Color SelectionFill {
            get { return selection_fill; }
        }
        
        public Cairo.Color SelectionStroke {
            get { return selection_stroke; }
        }
    }
}
