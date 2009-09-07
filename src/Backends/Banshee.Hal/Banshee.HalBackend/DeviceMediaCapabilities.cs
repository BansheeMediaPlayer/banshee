//
// DeviceMediaCapabilities.cs
//
// Author:
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
using System.Collections.Generic;

using Banshee.Hardware;

namespace Banshee.HalBackend
{
    public class DeviceMediaCapabilities : IDeviceMediaCapabilities
    {
        private Hal.Device device;

        public DeviceMediaCapabilities (Hal.Device device)
        {
            this.device = device;
        }

        private int? cover_art_size;
        public int CoverArtSize {
            get {
                if (cover_art_size == null) {
                    cover_art_size = SafelyGetInt ("portable_audio_player.cover_art_size", -1);
                }
                return cover_art_size.Value;
            }
        }		

        private int? folder_depth;
        public int FolderDepth {
            get {
                if (folder_depth == null) {
                    folder_depth = SafelyGetInt ("portable_audio_player.folder_depth", -1);
                }
                return folder_depth.Value;
            }
        }

        private string [] audio_folders;
        public string [] AudioFolders {
            get {
                if (audio_folders == null) {
                    audio_folders = SafelyGetStringList ("portable_audio_player.audio_folders");
                }
                return audio_folders;
            }
        }

        private string [] video_folders;
        public string [] VideoFolders {
            get {
                if (video_folders == null) {
                    video_folders = SafelyGetStringList ("portable_audio_player.video_folders");
                }
                return video_folders;
            }
        }

        private string cover_art_file_name;
        public string CoverArtFileName {
            get {
                if (cover_art_file_name == null) {
                    cover_art_file_name = SafelyGetString ("portable_audio_player.cover_art_file_name");
                }
                return cover_art_file_name;
            }
        }

        private string cover_art_file_type;
        public string CoverArtFileType {
            get {
                if (cover_art_file_type == null) {
                    cover_art_file_type = SafelyGetString ("portable_audio_player.cover_art_file_type");
                }
                return cover_art_file_type;
            }
        }

        private string [] playlist_formats;
        public string [] PlaylistFormats {
            get {
                if (playlist_formats == null) {
                    playlist_formats = SafelyGetStringList ("portable_audio_player.playlist_format");
                }
                return playlist_formats;
            }
        }

        private string playlist_path;
        public string PlaylistPath {
            get {
                if (playlist_path == null) {
                    playlist_path = SafelyGetString ("portable_audio_player.playlist_path");
                }
                return playlist_path;
            }
        }

        private string [] playback_formats;
        public string [] PlaybackMimeTypes {
            get {
                if (playback_formats == null) {
                    playback_formats = SafelyGetStringList ("portable_audio_player.output_formats");
                }
                return playback_formats;
            }
        }

        private string SafelyGetString (string name)
        {
            try {
                if (device.PropertyExists (name)) {
                    return device[name];
                }
            } catch {}
            return null;
        }

        private string[] SafelyGetStringList (string name)
        {
            try {
                if (device.PropertyExists (name)) {
                    return device.GetPropertyStringList (name);
                }
            } catch {}
            return new string [0];
        }

        private int SafelyGetInt (string name, int def)
        {
            try {
                if (device.PropertyExists (name)) {
                    return device.GetPropertyInteger (name);
                }
            } catch {}
            return def;
        }

        public bool IsType (string type)
        {
            if (device.PropertyExists ("portable_audio_player.type")) {
                if (device ["portable_audio_player.type"] == type) {
                    return true;
                }
            }

            if (device.PropertyExists ("portable_audio_player.access_method.protocols")) {
                if (Array.IndexOf (device.GetPropertyStringList ("portable_audio_player.access_method.protocols"), type) != -1) {
                    return true;
                }
            }

            return false;
        }
    }
}
