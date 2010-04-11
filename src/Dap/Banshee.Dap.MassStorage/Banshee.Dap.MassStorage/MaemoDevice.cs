//
// MaemoDevice.cs
//
// Author:
//   Pavel Antonov <pavelantonov@richmd.ru>
//
// Copyright (C) 2009 Pavel Antonov
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
//n900 vendor product id
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using Mono.Unix;

using Banshee.Base;
using Banshee.Hardware;
using Banshee.Library;
using Banshee.Collection;
using Banshee.Collection.Database;

namespace Banshee.Dap.MassStorage
{
    public class MaemoDevice : CustomMassStorageDevice
    {
        private static string [] playback_mime_types = new string [] {
            // Video
            "video/mp4-generic",
            "video/quicktime",
            "video/mp4",
            "video/mpeg4",
            "video/3gp",
            "video/3gpp2",
            "application/sdp",

            // Audio
            "audio/3gpp",
            "audio/3ga",
            "audio/3gpp2",
            "audio/amr",
            "audio/x-amr",
            "audio/mpa",
            "audio/mp3",
            "audio/x-mp3",
            "audio/x-mpg",
            "audio/mpeg",
            "audio/mpeg3",
            "audio/mpg3",
            "audio/mpg",
            "audio/mp4",
            "audio/m4a",
            "audio/aac",
            "audio/x-aac",
            "audio/mp4a-latm",
            "audio/wav"
        };

        private static string [] playlist_formats = new string [] {
            "audio/x-scpls",
            "audio/mpegurl",
            "audio/x-mpegurl"
        };

        private static string [] audio_folders = new string [] {
            "Music/",
            ".videos/"
        };

        private static string [] video_folders = new string [] {
            "Video/",
            ".videos/"
        };

        private static string [] icon_names = new string [] {
            "phone-nokia-n900", DapSource.FallbackIcon
        };

        private static string playlists_path = "Playlists/";


        public override void SourceInitialize ()
        {
        }

        public override bool LoadDeviceConfiguration ()
        {
            LoadConfig ();
            return true;
        }

        protected override string DefaultName {
            get { return VendorProductInfo.ProductName; }
        }

        protected override string[] DefaultAudioFolders {
            get { return audio_folders; }
        }

        protected override string [] DefaultVideoFolders {
            get { return video_folders; }
        }

        protected override string [] DefaultPlaybackMimeTypes {
            get { return playback_mime_types; }
        }

        protected override int DefaultFolderDepth {
            get { return 2; }
        }

        protected override string DefaultCoverArtFileName {
            get { return "cover.jpg"; }
        }

        protected override string DefaultCoverArtFileType {
            get { return "jpeg"; }
        }

        protected override int DefaultCoverArtSize {
            get { return 200; }
        }

        protected override string [] DefaultPlaylistFormats {
            get { return playlist_formats; }
        }

        protected override string DefaultPlaylistPath {
            get { return playlists_path; }
        }

        public override string [] GetIconNames ()
        {
            return icon_names;
        }

        public override bool DeleteTrackHook (DatabaseTrackInfo track)
        {
            return true;
        }
    }
}
