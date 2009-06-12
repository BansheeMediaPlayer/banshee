//
// ArtworkRenderer.cs
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
using Cairo;

using Hyena.Gui;

namespace Banshee.Collection.Gui
{
    public static class ArtworkRenderer
    {
        private static Color cover_border_light_color = new Color (1.0, 1.0, 1.0, 0.5);
        private static Color cover_border_dark_color = new Color (0.0, 0.0, 0.0, 0.65);
        
        public static void RenderThumbnail (Cairo.Context cr, ImageSurface image, bool dispose,
            double x, double y, double width, double height, bool drawBorder, double radius)
        {
            RenderThumbnail (cr, image, dispose, x, y, width, height, 
                drawBorder, radius, false, cover_border_light_color);
        }
        
        public static void RenderThumbnail (Cairo.Context cr, ImageSurface image, bool dispose,
            double x, double y, double width, double height, bool drawBorder, double radius, 
            bool fill, Color fillColor)
        {
            RenderThumbnail (cr, image, dispose, x, y, width, height, drawBorder, radius, 
                fill, fillColor, CairoCorners.All);
        }
        
        public static void RenderThumbnail (Cairo.Context cr, ImageSurface image, bool dispose,
            double x, double y, double width, double height, bool drawBorder, double radius, 
            bool fill, Color fillColor, CairoCorners corners)
        {
            if (image == null || image.Handle == IntPtr.Zero) {
                return;
            }
            
            double p_x = x;
            double p_y = y;
            p_x += image.Width < width ? (width - image.Width) / 2 : 0;
            p_y += image.Height < height ? (height - image.Height) / 2 : 0;
            
            cr.Antialias = Cairo.Antialias.Default;
            
            if (fill) {
                cr.Rectangle (x, y, width, height);
                cr.Color = fillColor;
                cr.Fill();
            }
            
            CairoExtensions.RoundedRectangle (cr, p_x, p_y, image.Width, image.Height, radius, corners);
            cr.SetSource (image, p_x, p_y);
            cr.Fill ();
            
            if (!drawBorder) {
                if (dispose) {
                    ((IDisposable)image).Dispose ();
                }
                
                return;
            }
            
            cr.LineWidth = 1.0;
            if (radius < 1) {
                cr.Antialias = Antialias.None;
                
                CairoExtensions.RoundedRectangle (cr, x + 1.5, y + 1.5, width - 3, height - 3, radius, corners);
                cr.Color = cover_border_light_color;
                cr.Stroke ();
            }
            
            CairoExtensions.RoundedRectangle (cr, x + 0.5, y + 0.5, width - 1, height - 1, radius, corners);
            cr.Color = cover_border_dark_color;
            cr.Stroke ();
            
            if (dispose) {
                ((IDisposable)image).Dispose ();
            }
        }
    }
}
