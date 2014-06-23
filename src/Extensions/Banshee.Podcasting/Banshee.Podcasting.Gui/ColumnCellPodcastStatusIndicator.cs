//
// ColumnCellPodcastStatusIndicator.cs
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
using Mono.Unix;

using Hyena.Data.Gui;
using Banshee.Gui;
using Banshee.Collection.Gui;

using Banshee.Collection;
using Banshee.Podcasting.Data;

namespace Banshee.Podcasting.Gui
{
    public class ColumnCellPodcastStatusIndicator : ColumnCellStatusIndicator
    {
        public ColumnCellPodcastStatusIndicator (string property) : base (property)
        {
        }

        public ColumnCellPodcastStatusIndicator (string property, bool expand) : base (property, expand)
        {
        }

        protected override int PixbufCount {
            get { return base.PixbufCount + 2; }
        }

        protected override void LoadPixbufs ()
        {
            base.LoadPixbufs ();

            // Downloading
            Pixbufs[base.PixbufCount + 0] = IconThemeUtils.LoadIcon (PixbufSize, "document-save", "go-bottom");
            StatusNames[base.PixbufCount + 0] = Catalog.GetString ("Downloading");

            // Podcast is Downloaded
            Pixbufs[base.PixbufCount + 1] = IconThemeUtils.LoadIcon (PixbufSize, "document-save");
            StatusNames[base.PixbufCount + 1] = Catalog.GetString ("Downloaded");
        }

        protected override int GetIconIndex (TrackInfo track)
        {
            int i = -1;
            PodcastTrackInfo podcast = PodcastTrackInfo.From (track);
            if (track != null) {
                switch (podcast.Activity) {
                    case PodcastItemActivity.Downloading:
                    case PodcastItemActivity.DownloadPending:
                        i = base.PixbufCount + 0;
                        break;
                    default:
                        i = podcast.IsDownloaded ? base.PixbufCount + 1 : -1;
                        break;
                }
            }

            return i;
        }

        public override void Render (CellContext context, double cellWidth, double cellHeight)
        {
            PodcastTrackInfo podcast = PodcastTrackInfo.From (BoundTrack);
            if (podcast != null) {
                if (podcast.Activity == PodcastItemActivity.DownloadPending) {
                    context.Opaque = false;
                }
            }

            base.Render (context, cellWidth, cellHeight);
        }
    }
}
