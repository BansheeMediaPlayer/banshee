//
// ColumnCellPodcast.cs
//
// Authors:
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

using Mono.Unix;

using Hyena.Gui;
using Hyena.Gui.Theming;
using Hyena.Data.Gui;

using Migo.Syndication;

using Banshee.Gui;
using Banshee.ServiceStack;
using Banshee.Collection.Gui;

namespace Banshee.Podcasting.Gui
{
    public class ColumnCellPodcast : ColumnCell
    {
        private static int pixbuf_spacing = 4;
        private static int pixbuf_size = 48;
        
        // TODO replace this w/ new icon installation etc
        private static Gdk.Pixbuf default_cover_pixbuf = IconThemeUtils.LoadIcon (48, "podcast");
        
        private ArtworkManager artwork_manager;

        public ColumnCellPodcast () : base (null, true)
        {
            artwork_manager = ServiceManager.Get<ArtworkManager> ();
        }
    
        public override void Render (CellContext context, StateType state, double cellWidth, double cellHeight)
        {
            if (BoundObject == null) {
                return;
            }
            
            if (!(BoundObject is Feed)) {
                throw new InvalidCastException("ColumnCellPodcast can only bind to Feed objects");
            }
            
            Feed feed = (Feed)BoundObject;
            
            bool is_default = false;          
            Gdk.Pixbuf pixbuf = artwork_manager == null ? null : artwork_manager.LookupScale (PodcastService.ArtworkIdFor (feed), pixbuf_size);
            
            if (pixbuf == null) {
                pixbuf = default_cover_pixbuf;
                is_default = true;
            }
            
            // int pixbuf_render_size = is_default ? pixbuf.Height : (int)cellHeight - 8;
            int pixbuf_render_size = pixbuf_size;
            int x = pixbuf_spacing;
            int y = ((int)cellHeight - pixbuf_render_size) / 2;

            ArtworkRenderer.RenderThumbnail (context.Context, pixbuf, !is_default, x, y, 
                pixbuf_render_size, pixbuf_render_size, !is_default, context.Theme.Context.Radius);
                
            int fl_width = 0, fl_height = 0, sl_width = 0, sl_height = 0;
            Cairo.Color text_color = context.Theme.Colors.GetWidgetColor (GtkColorClass.Text, state);
            text_color.A = 0.75;
            
            Pango.Layout layout = context.Layout;
            layout.Width = (int)((cellWidth - cellHeight - x - 10) * Pango.Scale.PangoScale);
            layout.Ellipsize = Pango.EllipsizeMode.End;
            layout.FontDescription.Weight = Pango.Weight.Bold;
            
            // Compute the layout sizes for both lines for centering on the cell
            int old_size = layout.FontDescription.Size;
            
            layout.SetText (feed.Title ?? String.Empty);
            layout.GetPixelSize (out fl_width, out fl_height);
            
            if (feed.DbId > 0) {
                layout.FontDescription.Weight = Pango.Weight.Normal;
                layout.FontDescription.Size = (int)(old_size * Pango.Scale.Small);
                layout.FontDescription.Style = Pango.Style.Italic;
                
                if (feed.LastDownloadTime == DateTime.MinValue) {
                    layout.SetText (Catalog.GetString ("Never updated"));
                } else if (feed.LastDownloadTime.Date == DateTime.Now.Date) {
                    layout.SetText (String.Format (Catalog.GetString ("Updated at {0}"), feed.LastDownloadTime.ToShortTimeString ()));
                } else {
                    layout.SetText (String.Format (Catalog.GetString ("Updated {0}"), Hyena.Query.RelativeTimeSpanQueryValue.RelativeToNow (feed.LastDownloadTime).ToUserQuery ()));
                }    
                layout.GetPixelSize (out sl_width, out sl_height);
            }
            
            // Calculate the layout positioning
            x = ((int)cellHeight - x) + 10;
            y = (int)((cellHeight - (fl_height + sl_height)) / 2);
            
            // Render the second line first since we have that state already
            if (feed.DbId > 0) {
                context.Context.MoveTo (x, y + fl_height);
                context.Context.Color = text_color;
                PangoCairoHelper.ShowLayout (context.Context, layout);
            }
            
            // Render the first line, resetting the state
            layout.SetText (feed.Title ?? String.Empty);
            layout.FontDescription.Weight = Pango.Weight.Bold;
            layout.FontDescription.Size = old_size;
            layout.FontDescription.Style = Pango.Style.Normal;
            
            layout.SetText (feed.Title ?? String.Empty);
            
            context.Context.MoveTo (x, y);
            text_color.A = 1;
            context.Context.Color = text_color;
            PangoCairoHelper.ShowLayout (context.Context, layout);
        }
        
        public int ComputeRowHeight (Widget widget)
        {
            int height;
            int text_w, text_h;
            
            Pango.Layout layout = new Pango.Layout (widget.PangoContext);
            layout.FontDescription = widget.PangoContext.FontDescription.Copy ();
            
            layout.FontDescription.Weight = Pango.Weight.Bold;
            layout.SetText ("W");
            layout.GetPixelSize (out text_w, out text_h);
            height = text_h;
            
            layout.FontDescription.Weight = Pango.Weight.Normal;
            layout.FontDescription.Size = (int)(layout.FontDescription.Size * Pango.Scale.Small);
            layout.FontDescription.Style = Pango.Style.Italic;
            layout.SetText ("W");
            layout.GetPixelSize (out text_w, out text_h);
            height += text_h;
            
            layout.Dispose ();
            
            return (height < pixbuf_size ? pixbuf_size : height) + 6;
        }
    }
}
