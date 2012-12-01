//
// M3uPlaylistFormat.cs
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
using System.IO;
using System.Collections.Generic;

using Mono.Unix;

using Banshee.Base;
using Banshee.Collection;
using Banshee.Sources;
using Hyena;

namespace Banshee.Playlists.Formats
{
    public class M3uPlaylistFormat : PlaylistFormatBase
    {
        public static readonly PlaylistFormatDescription FormatDescription = new PlaylistFormatDescription(
            typeof(M3uPlaylistFormat), MagicHandler, Catalog.GetString("MPEG Version 3.0 Extended (*.m3u)"),
            "m3u", new string [] {"audio/x-mpegurl", "audio/m3u", "audio/mpeg-url"});

        public static bool MagicHandler(StreamReader reader)
        {
            string line = reader.ReadLine();
            if(line == null) {
                return false;
            }

            line = line.Trim();
            return line == "#EXTM3U" || line.StartsWith("http") || Banshee.Collection.Database.DatabaseImportManager.IsWhiteListedFile (line);
        }

        public M3uPlaylistFormat()
        {
        }

        public override void Load(StreamReader reader, bool validateHeader)
        {
            string line;
            PlaylistElement element = null;

            while((line = reader.ReadLine()) != null) {
                line = line.Trim();

                if(line.Length == 0) {
                    continue;
                }

                bool extinf = line.StartsWith("#EXTINF:");

                if(!extinf && line[0] == '#') {
                    continue;
                } else if(extinf) {
                    element = AddElement();
                    try {
                        ParseExtended(element, line);
                    } catch {
                    }
                    continue;
                } else if(element == null) {
                    element = AddElement();
                }

                try {
                    element.Uri = ResolveUri (line);
                } catch {
                    Elements.Remove(element);
                }

                element = null;
            }
        }

        private void ParseExtended (PlaylistElement element, string line)
        {
            string split = line.Substring(8).TrimStart(',');
            string [] parts = split.Split(new char [] { ',' }, 2);

            if(parts.Length == 2) {
                element.Duration = SecondsStringToTimeSpan (parts[0]);
                element.Title = parts[1].Trim ();
            } else {
                element.Title = split.Trim ();
            }
        }

        public override void Save(Stream stream, ITrackModelSource source)
        {
            using(StreamWriter writer = new StreamWriter(stream)) {
                writer.WriteLine("#EXTM3U");
                TrackInfo track;
                for (int i = 0; i < source.TrackModel.Count; i++) {
                    track = source.TrackModel[i];
                    int duration = (int)Math.Round(track.Duration.TotalSeconds);
                    if(duration <= 0) {
                        duration = -1;
                    }

                    writer.WriteLine("#EXTINF:{0},{1} - {2}", duration, track.DisplayArtistName, track.DisplayTrackTitle);
                    string trackpath = ExportUri (track.Uri);
                    if (FolderSeparator == Paths.Folder.DosSeparator) {
                        trackpath = Paths.NormalizeToDos (trackpath);
                    }
                    writer.WriteLine( trackpath );
                }
            }
        }
    }
}
