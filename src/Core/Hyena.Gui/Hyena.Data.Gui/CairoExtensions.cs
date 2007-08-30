//
// CairoExtensions.cs
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
using Gdk;
using Cairo;

namespace Hyena.Data.Gui
{
    [Flags]
    public enum CairoCorners
    {
        None = 0,
        TopLeft = 1,
        TopRight = 2,
        BottomLeft = 4,
        BottomRight = 8,
        All = 15
    }
    
    public static class CairoExtensions
    {        
        public static Cairo.Color GdkColorToCairoColor(Gdk.Color color)
        {
            return GdkColorToCairoColor(color, 1.0);
        }
        
        public static Cairo.Color GdkColorToCairoColor(Gdk.Color color, double alpha)
        {
            return new Cairo.Color(
                (double)(color.Red >> 8) / 255.0,
                (double)(color.Green >> 8) / 255.0,
                (double)(color.Blue >> 8) / 255.0,
                alpha);
        }
        
        public static void HsbFromColor(Cairo.Color color, out double hue, 
            out double saturation, out double brightness)
        {
            double min, max, delta;
            double red = color.R;
            double green = color.G;
            double blue = color.B;
            
            hue = 0;
            saturation = 0;
            brightness = 0;
            
            if(red > green) {
                max = Math.Max(red, blue);
                min = Math.Min(green, blue);
            } else {
                max = Math.Max(green, blue);
                min = Math.Min(red, blue);
            }
            
            brightness = (max + min) / 2;
            
            if(Math.Abs(max - min) < 0.0001) {
                hue = 0;
                saturation = 0;
            } else {
                saturation = brightness <= 0.5
                    ? (max - min) / (max + min)
                    : (max - min) / (2 - max - min);
               
                delta = max - min;
                
                if(red == max) {
                    hue = (green - blue) / delta;
                } else if(green == max) {
                    hue = 2 + (blue - red) / delta;
                } else if(blue == max) {
                    hue = 4 + (red - green) / delta;
                }
                
                hue *= 60;
                if(hue < 0) {
                    hue += 360;
                }
            }
        }
        
        private static double Modula(double number, double divisor)
        {
            return ((int)number % divisor) + (number - (int)number);
        }
        
        public static Cairo.Color ColorFromHsb(double hue, double saturation, double brightness)
        {
            int i;
            double [] hue_shift = { 0, 0, 0 };
            double [] color_shift = { 0, 0, 0 };
            double m1, m2, m3;
            
            m2 = brightness <= 0.5
                ? brightness * (1 + saturation)
                : brightness + saturation - brightness * saturation;
            
            m1 = 2 * brightness - m2;
            
            hue_shift[0] = hue + 120;
            hue_shift[1] = hue;
            hue_shift[2] = hue - 120;
            
            color_shift[0] = color_shift[1] = color_shift[2] = brightness;
            
            i = saturation == 0 ? 3 : 0;
            
            for(; i < 3; i++) {
                m3 = hue_shift[i];
                
                if(m3 > 360) {
                    m3 = Modula(m3, 360);
                } else if(m3 < 0) {
                    m3 = 360 - Modula(Math.Abs(m3), 360);
                }
                
                if(m3 < 60) {
                    color_shift[i] = m1 + (m2 - m1) * m3 / 60;
                } else if(m3 < 180) {
                    color_shift[i] = m2;
                } else if(m3 < 240) {
                    color_shift[i] = m1 + (m2 - m1) * (240 - m3) / 60;
                } else {
                    color_shift[i] = m1;
                }       
            }
            
            return new Cairo.Color(color_shift[0], color_shift[1], color_shift[2]);
        }
        
        public static Cairo.Color ColorShade(Cairo.Color @base, double ratio)
        {
            double h, s, b;
            
            HsbFromColor(@base, out h, out s, out b);
            
            b = Math.Max(Math.Min(b * ratio, 1), 0);
            s = Math.Max(Math.Min(s * ratio, 1), 0);
            
            return ColorFromHsb(h, s, b);
        }
        
        public static void RoundedRectangle(Cairo.Context cr, double x, double y, double w, double h, double r)
        {
            RoundedRectangle(cr, x, y, w, h, r, CairoCorners.All);
        }
        
        public static void RoundedRectangle(Cairo.Context cr, double x, double y, double w, double h, 
            double r, CairoCorners corners)
        {
            if(r < 0.0001 || corners == CairoCorners.None) {
                cr.Rectangle(x, y, w, h);
                return;
            }
            
            if((corners & CairoCorners.TopLeft) != 0) {
                cr.MoveTo(x + r, y);
            } else {
                cr.MoveTo(x, y);
            }
            
            if((corners & CairoCorners.TopRight) != 0) {
                cr.Arc(x + w - r, y + r, r, Math.PI * 1.5, Math.PI * 2);
            } else {
                cr.LineTo(x + w, y);
            }
            
            if((corners & CairoCorners.BottomRight) != 0) {
                cr.Arc(x + w - r, y + h - r, r, 0, Math.PI * 0.5);
            } else {
                cr.LineTo(x + w, y + h);
            }
            
            if((corners & CairoCorners.BottomLeft) != 0) {
                cr.Arc(x + r, y + h - r, r, Math.PI * 0.5, Math.PI);
            } else {
                cr.LineTo(x, y + h);
            }
            
            if((corners & CairoCorners.TopLeft) != 0) {
                cr.Arc(x + r, y + r, r, Math.PI, Math.PI * 1.5);
            } else {
                cr.LineTo(x, y);
            }
        }
    }
}
