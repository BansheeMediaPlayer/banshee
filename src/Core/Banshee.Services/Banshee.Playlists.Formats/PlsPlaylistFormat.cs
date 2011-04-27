//
// PlsPlaylistFormat.cs
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

using Hyena;

using Banshee.Base;
using Banshee.Collection;
using Banshee.Sources;

namespace Banshee.Playlists.Formats
{
    public class PlsPlaylistFormat : PlaylistFormatBase
    {
        private enum PlsType {
            Unknown,
            File,
            Title,
            Length
        }

        public static readonly PlaylistFormatDescription FormatDescription = new PlaylistFormatDescription(
            typeof(PlsPlaylistFormat), MagicHandler, Catalog.GetString("Shoutcast Playlist version 2 (*.pls)"),
            "pls", new string [] {"audio/x-scpls"});

        public static bool MagicHandler(StreamReader reader)
        {
            string line = reader.ReadLine();
            if(line == null) {
                return false;
            }

            return line.Trim().ToLower() == "[playlist]";
        }

        public PlsPlaylistFormat()
        {
        }

        public override void Load(StreamReader reader, bool validateHeader)
        {
            string line;

            if(validateHeader && !MagicHandler(reader)) {
                throw new InvalidPlaylistException();
            }

            while((line = reader.ReadLine()) != null) {
                line = line.Trim();

                if(line.Length == 0) {
                    continue;
                }

                int eq_offset = line.IndexOf('=');
                int index_offset = 0;

                if(eq_offset <= 0) {
                    continue;
                }

                PlsType element_type = PlsType.Unknown;

                if(line.StartsWith("File")) {
                    element_type = PlsType.File;
                    index_offset = 4;
                } else if(line.StartsWith("Title")) {
                    element_type = PlsType.Title;
                    index_offset = 5;
                } else if(line.StartsWith("Length")) {
                    element_type = PlsType.Length;
                    index_offset = 6;
                } else {
                    continue;
                }

                try {
                    int index = Int32.Parse(line.Substring(index_offset, eq_offset - index_offset),
                        ApplicationContext.InternalCultureInfo.NumberFormat) - 1;
                    string value_string = line.Substring(eq_offset + 1).Trim();
                    Dictionary<string, object> element = index < Elements.Count
                        ? Elements[index]
                        : AddElement();

                    switch(element_type) {
                        case PlsType.File:
                            element["uri"] = ResolveUri(value_string);
                            break;
                        case PlsType.Title:
                            element["title"] = value_string;
                            break;
                        case PlsType.Length:
                            element["duration"] = SecondsStringToTimeSpan(value_string);
                            break;
                    }
                } catch {
                    continue;
                }
            }
        }

        public override void Save(Stream stream, ITrackModelSource source)
        {
            using(StreamWriter writer = new StreamWriter(stream)) {
                int count = 0;

                writer.WriteLine("[playlist]");

                TrackInfo track;
                for (int i = 0; i < source.TrackModel.Count; i++) {
                    track = source.TrackModel[i];
                    count++;

                    writer.WriteLine("File{0}={1}", count, ExportUri(track.Uri));
                    writer.WriteLine("Title{0}={1} - {2}", count, track.DisplayArtistName, track.DisplayTrackTitle);
                    writer.WriteLine("Length{0}={1}", count, (int)Math.Round(track.Duration.TotalSeconds));
                }

                writer.WriteLine("NumberOfEntries={0}", count);
                writer.WriteLine("Version=2");
            }
        }
    }
}
