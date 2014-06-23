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
using Mono.Unix;

using Hyena.Gui;
using Hyena.Data.Gui;
using Hyena.Data.Gui.Accessibility;
using Banshee.Gui;

using Banshee.Streaming;
using Banshee.MediaEngine;
using Banshee.ServiceStack;

namespace Banshee.Collection.Gui
{
    class ColumnCellStatusIndicatorAccessible : ColumnCellAccessible, Atk.IImageImplementor
    {
        private string image_description;

        public ColumnCellStatusIndicatorAccessible (object bound_object, ColumnCellStatusIndicator cell, ICellAccessibleParent parent) : base (bound_object, cell as ColumnCell, parent)
        {
            image_description = cell.GetTextAlternative (bound_object);
        }

        public override void Redrawn ()
        {
            string new_image_description = cell.GetTextAlternative (bound_object);

            if (image_description != new_image_description)
                GLib.Signal.Emit (this, "visible-data-changed");

            image_description = new_image_description;
        }

        public string ImageLocale { get { return null; } }

        public bool SetImageDescription (string description)
        {
            return false;
        }

        public void GetImageSize (out int width, out int height)
        {
            if (!String.IsNullOrEmpty (cell.GetTextAlternative (bound_object)))
                width = height = 16;
            else
                width = height = Int32.MinValue;
        }

        public string ImageDescription {
            get {
                return image_description;
            }
        }

        public void GetImagePosition (out int x, out int y, Atk.CoordType coordType)
        {
            if (!String.IsNullOrEmpty (cell.GetTextAlternative (bound_object))) {
                GetPosition (out x, out y, coordType);
                x += 4;
                y += 4;
            } else {
                x = y = Int32.MinValue;
            }
        }
    }

    public class ColumnCellStatusIndicator : ColumnCell, ISizeRequestCell, ITooltipCell
    {
        const int padding = 2;

        protected enum Icon : int {
            Playing,
            Paused,
            Error,
            Protected,
            External
        }

        private string [] status_names;
        protected string [] StatusNames {
            get { return status_names; }
        }

        private int pixbuf_size;
        protected int PixbufSize {
            get { return pixbuf_size; }
            set {
                pixbuf_size = value;
                Width = Height = value;
            }
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
            RestrictSize = true;
            PixbufSize = 16;
            LoadPixbufs ();
        }

        public bool RestrictSize { get; set; }

        public void GetWidthRange (Pango.Layout layout, out int min_width, out int max_width)
        {
            min_width = max_width = pixbuf_size + 2 * padding;
        }

        public override Atk.Object GetAccessible (ICellAccessibleParent parent)
        {
            return new ColumnCellStatusIndicatorAccessible (BoundObject, this, parent);
        }

        public override string GetTextAlternative (object obj)
        {
            var track = obj as TrackInfo;
            if (track == null)
                return "";

            int icon_index = GetIconIndex (track);

            if ((icon_index < 0) || (icon_index >= status_names.Length)) {
                return "";
            } else if (icon_index == (int)Icon.Error) {
                return track.GetPlaybackErrorMessage () ?? "";
            } else {
                return status_names[icon_index];
            }
        }

        protected virtual int PixbufCount {
            get { return 5; }
        }

        protected virtual int GetIconIndex (TrackInfo track)
        {
            int icon_index = -1;

            if (track.PlaybackError != StreamPlaybackError.None) {
                icon_index = (int)(track.PlaybackError == StreamPlaybackError.Drm
                    ? Icon.Protected
                    : Icon.Error);
            } else if (track.IsPlaying) {
                icon_index = (int)(ServiceManager.PlayerEngine.CurrentState == PlayerState.Paused
                    ? Icon.Paused
                    : Icon.Playing);
            } else if ((track.MediaAttributes & TrackMediaAttributes.ExternalResource) != 0) {
                icon_index = (int)Icon.External;
            } else {
                icon_index = -1;
            }

            return icon_index;
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

            pixbufs[(int)Icon.Playing]   = IconThemeUtils.LoadIcon (PixbufSize, "media-playback-start");
            pixbufs[(int)Icon.Paused]    = IconThemeUtils.LoadIcon (PixbufSize, "media-playback-pause");
            pixbufs[(int)Icon.Error]     = IconThemeUtils.LoadIcon (PixbufSize, "emblem-unreadable", "dialog-error");
            pixbufs[(int)Icon.Protected] = IconThemeUtils.LoadIcon (PixbufSize, "emblem-readonly", "dialog-error");
            pixbufs[(int)Icon.External]  = IconThemeUtils.LoadIcon (PixbufSize, "x-office-document");

            if (status_names == null) {
                status_names = new string[PixbufCount];
                for (int i=0; i<PixbufCount; i++)
                    status_names[i] = "";
            }

            status_names[(int)Icon.Playing]   = Catalog.GetString ("Playing");
            status_names[(int)Icon.Paused]    = Catalog.GetString ("Paused");
            status_names[(int)Icon.Error]     = Catalog.GetString ("Error");
            status_names[(int)Icon.Protected] = Catalog.GetString ("Protected");
            status_names[(int)Icon.External]  = Catalog.GetString ("External Document");
        }

        public override void NotifyThemeChange ()
        {
            LoadPixbufs ();
        }

        public override void Render (CellContext context, double cellWidth, double cellHeight)
        {
            TrackInfo track = BoundTrack;
            if (track == null) {
                return;
            }

            int icon_index = GetIconIndex (track);
            TooltipMarkup = icon_index == -1 ? null : StatusNames[icon_index];

            if (icon_index < 0 || pixbufs == null || pixbufs[icon_index] == null) {
                return;
            }

            context.Context.Translate (0, 0.5);

            Gdk.Pixbuf render_pixbuf = pixbufs[icon_index];

            Cairo.Rectangle pixbuf_area = new Cairo.Rectangle ((cellWidth - render_pixbuf.Width) / 2,
                (cellHeight - render_pixbuf.Height) / 2, render_pixbuf.Width, render_pixbuf.Height);

            if (!context.Opaque) {
                context.Context.Save ();
            }

            Gdk.CairoHelper.SetSourcePixbuf (context.Context, render_pixbuf, pixbuf_area.X, pixbuf_area.Y);
            context.Context.Rectangle (pixbuf_area);

            if (!context.Opaque) {
                context.Context.Clip ();
                context.Context.PaintWithAlpha (0.5);
                context.Context.Restore ();
            } else {
                context.Context.Fill ();
            }
        }

        public string GetTooltipMarkup (CellContext cellContext, double columnWidth)
        {
            return GetTextAlternative (BoundObject);
        }

        protected TrackInfo BoundTrack {
            get { return BoundObject as TrackInfo; }
        }
    }
}
