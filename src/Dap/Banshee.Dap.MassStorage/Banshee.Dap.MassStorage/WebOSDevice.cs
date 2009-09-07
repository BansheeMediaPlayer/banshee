//
// WebOSDevice.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//   Jeff Wheeler <jeff@jeffwheeler.name>
//
// Copyright (C) 2008 Novell, Inc.
// Copyright (C) 2009 Jeff Wheeler
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
using Mono.Unix;

using Banshee.Base;
using Banshee.Hardware;
using Banshee.Library;
using Banshee.Collection;
using Banshee.Collection.Database;

namespace Banshee.Dap.MassStorage
{
    public class WebOSDevice : CustomMassStorageDevice
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

        // The Pre theoretically supports these formats, but it does not
        // recognize them within the media player.
        private static string [] playlist_formats = new string [] {
            // "audio/x-scpls",
            // "audio/mpegurl",
            // "audio/x-mpegurl"
        };

        private static string [] audio_folders = new string [] {
            "Music/",
            "Videos/",
            "ringtones/",
            "AmazonMP3/"
        };

        private static string [] video_folders = new string [] {
            "Videos/"
        };

        private static string [] icon_names = new string [] {
            DapSource.FallbackIcon
        };

        private AmazonMp3GroupSource amazon_source;
        private string amazon_base_dir;

        private RingtonesGroupSource ringtones_source;

        public override void SourceInitialize ()
        {
            amazon_base_dir = System.IO.Path.Combine (Source.Volume.MountPoint, audio_folders[3]);

            amazon_source = new AmazonMp3GroupSource (Source, "AmazonMP3", amazon_base_dir);
            amazon_source.AutoHide = true;

            ringtones_source = new RingtonesGroupSource (Source);
            ringtones_source.AutoHide = true;
        }

        public override bool LoadDeviceConfiguration ()
        {
            return true;
        }

        public override string Name {
            get { return VendorProductInfo.ProductName; }
        }

        public override string [] AudioFolders {
            get { return audio_folders; }
        }

        public override string [] VideoFolders {
            get { return video_folders; }
        }

        public override string [] PlaybackMimeTypes {
            get { return playback_mime_types; }
        }

        public override int FolderDepth {
            get { return 2; }
        }

        public override string CoverArtFileType {
            get { return "jpeg"; }
        }

        public override int CoverArtSize {
            get { return 320; }
        }

        public override string [] PlaylistFormats {
            get { return playlist_formats; }
        }

        public override string [] GetIconNames ()
        {
            return icon_names;
        }

#region Amazon MP3 Store Purchased Tracks Management

        public override bool DeleteTrackHook (DatabaseTrackInfo track)
        {
            // Do not allow removing purchased tracks if not in the
            // Amazon Purchased Music source; this should prevent
            // accidental deletion of purchased music that may not
            // have been copied from the device yet.
            //
            // TODO: Provide some feedback when a purchased track is
            // skipped from deletion
            //
            // FIXME: unfortunately this does not work due to
            // the cache models being potentially different
            // even though they will always reference the same tracks
            // amazon_source.TrackModel.IndexOf (track) >= 0
            if (!amazon_source.Active && amazon_source.Count > 0 && track.Uri.LocalPath.StartsWith (amazon_base_dir)) {
                return false;
            }

            return true;
        }

#endregion

#region Ringtones Support

    private class RingtonesGroupSource : MediaGroupSource
    {
        // TODO: Support dropping files onto this playlist to copy into the ringtones directory
        public RingtonesGroupSource (DapSource parent)
            : base (parent, Catalog.GetString ("Ringtones"))
        {
            ConditionSql = "(CoreTracks.Uri LIKE \"%ringtones/%\")";
        }
    }

#endregion

    }
}
