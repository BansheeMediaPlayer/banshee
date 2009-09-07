//
// VideoLibrarySource.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2005-2008 Novell, Inc.
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

using Mono.Unix;

using Banshee.SmartPlaylist;
using Banshee.Collection;

namespace Banshee.Library
{
    public class VideoLibrarySource : LibrarySource
    {
        // Catalog.GetString ("Video Library")
        public VideoLibrarySource () : base (Catalog.GetString ("Videos"), "VideoLibrary", 50)
        {
            MediaTypes = TrackMediaAttributes.VideoStream;
            NotMediaTypes = TrackMediaAttributes.Podcast;
            Properties.SetStringList ("Icon.Name", "video-x-generic", "video", "source-library");
            Properties.SetString ("TrackView.ColumnControllerXml", String.Format (@"
                <column-controller>
                  <add-all-defaults />
                  <remove-default column=""DiscColumn"" />
                  <remove-default column=""AlbumColumn"" />
                  <remove-default column=""ComposerColumn"" />
                  <remove-default column=""AlbumArtistColumn"" />
                  <remove-default column=""ConductorColumn"" />
                  <remove-default column=""ComposerColumn"" />
                  <remove-default column=""BpmColumn"" />
                  <sort-column direction=""asc"">track_title</sort-column>
                  <column modify-default=""ArtistColumn"">
                    <title>{0}</title>
                    <long-title>{0}</long-title>
                  </column>
                </column-controller>
            ", Catalog.GetString ("Produced By")));
        }

        public override bool ShowBrowser {
            get { return false; }
        }
        
        protected override bool HasArtistAlbum {
            get { return false; }
        }
        
        public override string DefaultBaseDirectory {
            get { return Banshee.Base.XdgBaseDirectorySpec.GetUserDirectory ("XDG_VIDEOS_DIR", "Videos"); }
        }

        public override IEnumerable<SmartPlaylistDefinition> DefaultSmartPlaylists {
            get { return default_smart_playlists; }
        }

        private static SmartPlaylistDefinition [] default_smart_playlists = new SmartPlaylistDefinition [] {
            new SmartPlaylistDefinition (
                Catalog.GetString ("Favorites"),
                Catalog.GetString ("Videos rated four and five stars"),
                "rating>=4"),

            new SmartPlaylistDefinition (
                Catalog.GetString ("Unwatched"),
                Catalog.GetString ("Videos that haven't been played yet"),
                "plays=0"),
        };
    }
}
