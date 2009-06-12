//
// LargeTrackInfoDisplay.cs
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
using System.Collections.Generic;

using Gtk;
using Cairo;

using Hyena;
using Hyena.Gui;
using Banshee.Collection;
using Banshee.Collection.Gui;

namespace Banshee.Gui.Widgets
{
    public class LargeTrackInfoDisplay : TrackInfoDisplay
    {
        private Gdk.Rectangle text_alloc;
        private Dictionary<ImageSurface, Surface> surfaces = new Dictionary<ImageSurface, Surface> ();
        private Pango.Layout first_line_layout;
        private Pango.Layout second_line_layout;
        private Pango.Layout third_line_layout;
    
        public LargeTrackInfoDisplay ()
        {
        }
        
        protected LargeTrackInfoDisplay (IntPtr native) : base (native)
        {
        }
        
        protected override int MissingIconSizeRequest {
            get { return 128; }
        }
        
        protected virtual int MaxArtworkSize {
            get { return 300; }
        }
        
        protected virtual int Spacing {
            get { return 30; }
        }
        
        protected override int ArtworkSizeRequest {
            get { return Math.Min (Math.Min (Allocation.Height, Allocation.Width), MaxArtworkSize); }
        }

        protected virtual Gdk.Rectangle RenderAllocation {
            get {
                int width = ArtworkSizeRequest * 2 + Spacing;
                int height = (int)Math.Ceiling (ArtworkSizeRequest * 1.2);
                int x = Allocation.X + (Allocation.Width - width) / 2;
                int y = Allocation.Y + (Allocation.Height - height) / 2;
                return new Gdk.Rectangle (x, y, width, height);
            }
        }
        
        protected override void OnSizeAllocated (Gdk.Rectangle allocation)
        {
            base.OnSizeAllocated (allocation);
            QueueDraw ();
        }

        protected override void OnThemeChanged ()
        {
            if (first_line_layout != null) {
                first_line_layout.Dispose ();
                first_line_layout = null;
            }
            
            if (second_line_layout != null) {
                second_line_layout.Dispose ();
                second_line_layout = null;
            }
            
            if (third_line_layout != null) {
                third_line_layout.Dispose ();
                third_line_layout = null;
            }
        }
       
        protected override void RenderCoverArt (Cairo.Context cr, ImageSurface image)
        {
            if (image == null) {
                return;
            }
            
            Gdk.Rectangle alloc = RenderAllocation;
            int asr = ArtworkSizeRequest;
            int reflect = (int)(image.Height * 0.2);
            int surface_w = image.Width;
            int surface_h = image.Height + reflect;
            int x = alloc.X + alloc.Width - asr;
            int y = alloc.Y;
            
            Surface scene = null;
            if (!surfaces.TryGetValue (image, out scene)) {
                scene = CreateScene (cr, image, reflect);
                surfaces.Add (image, scene);
            }

            cr.Rectangle (x, y, asr, alloc.Height);
            cr.Color = BackgroundColor;
            cr.Fill ();
            
            x += (asr - surface_w) / 2;
            y += surface_h > asr ? 0 : (asr - surface_h) / 2;
            
            cr.SetSource (scene, x, y);
            cr.Paint ();
        }
        
        private Surface CreateScene (Cairo.Context window_cr, ImageSurface image, int reflect)
        {
            Surface surface = window_cr.Target.CreateSimilar (window_cr.Target.Content, 
                image.Width, image.Height + reflect);
            Cairo.Context cr = new Context (surface);
            
            cr.Save ();
            
            cr.SetSource (image);
            cr.Paint ();
            
            cr.Rectangle (0, image.Height, image.Width, reflect);
            cr.Clip ();
            
            Matrix matrix = new Matrix ();
            matrix.InitScale (1, -1);
            matrix.Translate (0, -(2 * image.Height) + 1);
            cr.Transform (matrix);
            
            cr.SetSource (image);
            cr.Paint ();
            
            cr.Restore ();
            
            Color bg_transparent = BackgroundColor;
            bg_transparent.A = 0.65;
            
            LinearGradient mask = new LinearGradient (0, image.Height, 0, image.Height + reflect);
            mask.AddColorStop (0, bg_transparent);
            mask.AddColorStop (1, BackgroundColor);
            
            cr.Rectangle (0, image.Height, image.Width, reflect);
            cr.Pattern = mask;
            cr.Fill ();
            
            ((IDisposable)cr).Dispose ();
            return surface;
        }
        
        protected override void RenderTrackInfo (Context cr, TrackInfo track, bool renderTrack, bool renderArtistAlbum)
        {
            if (track == null) {
                return;
            }

            Gdk.Rectangle alloc = RenderAllocation;
            int width = ArtworkSizeRequest;
            int fl_width, fl_height, sl_width, sl_height, tl_width, tl_height;
            int pango_width = (int)(width * Pango.Scale.PangoScale);

            string first_line = GetFirstLineText (track);
            
            // FIXME: This is incredibly bad, but we don't want to break
            // translations right now. It needs to be replaced for 1.4!!
            string line = GetSecondLineText (track);
            string second_line = line, third_line = String.Empty;
            int split_pos = line.LastIndexOf ("<span");
            if (split_pos >= 0
                // Check that there are at least 3 spans in the string, else this
                // will break for tracks with missing artist or album info.
                && StringUtil.SubstringCount (line, "<span") >= 3) {
                
                second_line = line.Substring (0, Math.Max (0, split_pos - 1)) + "</span>";
                third_line = String.Format ("<span color=\"{0}\">{1}",
                    CairoExtensions.ColorGetHex (TextColor, false),
                    line.Substring (split_pos, line.Length - split_pos));
            }

            if (first_line_layout == null) {
                first_line_layout = CairoExtensions.CreateLayout (this, cr);
                first_line_layout.Ellipsize = Pango.EllipsizeMode.End;
                first_line_layout.Alignment = Pango.Alignment.Right;
                
                int base_size = first_line_layout.FontDescription.Size;
                first_line_layout.FontDescription.Size = (int)(base_size * Pango.Scale.XLarge);
            }
            
            if (second_line_layout == null) {
                second_line_layout = CairoExtensions.CreateLayout (this, cr);
                second_line_layout.Ellipsize = Pango.EllipsizeMode.End;
                second_line_layout.Alignment = Pango.Alignment.Right;
            }
            
            if (third_line_layout == null) {
                third_line_layout = CairoExtensions.CreateLayout (this, cr);
                third_line_layout.Ellipsize = Pango.EllipsizeMode.End;
                third_line_layout.Alignment = Pango.Alignment.Right;
            }

            // Set up the text layouts
            first_line_layout.Width = pango_width;
            second_line_layout.Width = pango_width;
            third_line_layout.Width = pango_width;
            
            // Compute the layout coordinates
            first_line_layout.SetMarkup (first_line);
            first_line_layout.GetPixelSize (out fl_width, out fl_height);
            second_line_layout.SetMarkup (second_line);
            second_line_layout.GetPixelSize (out sl_width, out sl_height);
            third_line_layout.SetMarkup (third_line);
            third_line_layout.GetPixelSize (out tl_width, out tl_height);

            text_alloc.X = alloc.X;
            text_alloc.Width = width;
            text_alloc.Height = fl_height + sl_height + tl_height;
            text_alloc.Y = alloc.Y + (ArtworkSizeRequest - text_alloc.Height) / 2;

            // Render the layouts
            cr.Antialias = Cairo.Antialias.Default;
            
            if (renderTrack) {
                cr.MoveTo (text_alloc.X, text_alloc.Y);
                cr.Color = TextColor;
                PangoCairoHelper.ShowLayout (cr, first_line_layout);
            }

            if (!renderArtistAlbum) {
                return;
            }
            
            cr.MoveTo (text_alloc.X, text_alloc.Y + fl_height);
            PangoCairoHelper.ShowLayout (cr, second_line_layout);
            
            cr.MoveTo (text_alloc.X, text_alloc.Y + fl_height + sl_height);
            PangoCairoHelper.ShowLayout (cr, third_line_layout);
        }
        
        protected override void Invalidate ()
        {
            if (CurrentImage == null || CurrentTrack == null || IncomingImage == null || IncomingTrack == null) {
                QueueDraw ();
            } else {
                Gdk.Rectangle alloc = RenderAllocation;
                QueueDrawArea (text_alloc.X, text_alloc.Y, text_alloc.Width, text_alloc.Height);
                QueueDrawArea (alloc.X + text_alloc.Width + Spacing, alloc.Y, 
                    alloc.Width - text_alloc.Width - Spacing, alloc.Height);
            }
        }
        
        protected override void InvalidateCache ()
        {
            foreach (Surface surface in surfaces.Values) {
                ((IDisposable)surface).Dispose ();
            }
            
            surfaces.Clear ();
        }
    }
}
