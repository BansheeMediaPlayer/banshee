//
// ColumnCellStatusIndicator.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2007-2008 Novell, Inc.
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
    public class ColumnCellStatusIndicator : ColumnCell
    {
        protected enum Icon : int {
            Playing,
            Paused,
            Error,
            Protected
        }
        
        private int pixbuf_size = 16;
        protected virtual int PixbufSize {
            get { return pixbuf_size; }
            set { pixbuf_size = value; }
        }
        
        private int pixbuf_spacing = 4;
        protected virtual int PixbufSpacing {
            get { return pixbuf_spacing; }
            set { pixbuf_spacing = value; }
        }
        
        private Gdk.Pixbuf [] pixbufs;
        protected Gdk.Pixbuf [] Pixbufs {
            get { return pixbufs; }
        }
        
        public ColumnCellStatusIndicator (string property) : this (property, true)
        {
        }
        
        public ColumnCellStatusIndicator (string property, bool expand) : base (property, expand)
        {
            LoadPixbufs ();
        }
        
        protected virtual int PixbufCount {
            get { return 4; }
        }
        
        protected virtual int GetIconIndex (TrackInfo track)
        {
            return -1;
        }
        
        protected virtual void LoadPixbufs ()
        {
            if (pixbufs != null && pixbufs.Length > 0) {
                for (int i = 0; i < pixbufs.Length; i++) {
                    if (pixbufs[i] != null) {
                        pixbufs[i].Dispose ();
                        pixbufs[i] = null;
                    }
                }
            }
            
            if (pixbufs == null) {
                pixbufs = new Gdk.Pixbuf[PixbufCount];
            }
            
            pixbufs[(int)Icon.Playing] = IconThemeUtils.LoadIcon (PixbufSize, "media-playback-start");
            pixbufs[(int)Icon.Paused] = IconThemeUtils.LoadIcon (PixbufSize, "media-playback-pause");
            pixbufs[(int)Icon.Error] = IconThemeUtils.LoadIcon (PixbufSize, "emblem-unreadable", "dialog-error");
            pixbufs[(int)Icon.Protected] = IconThemeUtils.LoadIcon (PixbufSize, "emblem-readonly", "dialog-error");
        }
        
        public override void NotifyThemeChange ()
        {
            LoadPixbufs ();
        }

        public override void Render (CellContext context, StateType state, double cellWidth, double cellHeight)
        {
            TrackInfo track = BoundTrack;
            if (track == null) {
                return;
            }
            
            int icon_index = -1;

            if (track.PlaybackError != StreamPlaybackError.None) {
                icon_index = (int)(track.PlaybackError == StreamPlaybackError.Drm
                    ? Icon.Protected
                    : Icon.Error);
            } else if (track.IsPlaying) {
                icon_index = (int)(ServiceManager.PlayerEngine.CurrentState == PlayerState.Paused
                    ? Icon.Paused
                    : Icon.Playing);
            } else {
                icon_index = GetIconIndex (track);
            }
            
            if (icon_index < 0 || pixbufs == null || pixbufs[icon_index] == null) {
                return;
            }
            
            context.Context.Translate (0, 0.5);
            
            Gdk.Pixbuf render_pixbuf = pixbufs[icon_index];
            
            Cairo.Rectangle pixbuf_area = new Cairo.Rectangle ((cellWidth - render_pixbuf.Width) / 2, 
                (cellHeight - render_pixbuf.Height) / 2, render_pixbuf.Width, render_pixbuf.Height);
            
            if (!context.Sensitive) {
                context.Context.Save ();
            }
            
            Gdk.CairoHelper.SetSourcePixbuf (context.Context, render_pixbuf, pixbuf_area.X, pixbuf_area.Y);
            context.Context.Rectangle (pixbuf_area);
            
            if (!context.Sensitive) {
                context.Context.Clip ();
                context.Context.PaintWithAlpha (0.5);
                context.Context.Restore ();
            } else {
                context.Context.Fill ();
            }
        }
        
        protected TrackInfo BoundTrack {
            get { return BoundObject as TrackInfo; }
        }
    }
}
