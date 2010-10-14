//
// EditorTrackInfo.cs
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
using System.Reflection;
using System.Collections.Generic;

using TagLib;

using Hyena;
using Banshee.Streaming;
using Banshee.Collection;

namespace Banshee.Gui.TrackEditor
{
    public class EditorTrackInfo : TrackInfo
    {
        public EditorTrackInfo (TrackInfo sourceTrack)
        {
            source_track = sourceTrack;
            TrackInfo.ExportableMerge (source_track, this);
        }

        public void GenerateDiff ()
        {
            diff_count = 0;

            foreach (KeyValuePair<string, PropertyInfo> iter in GetExportableProperties (typeof (TrackInfo))) {
                try {
                    PropertyInfo property = iter.Value;
                    if (property.CanWrite && property.CanRead) {
                        object old_value = property.GetValue (source_track, null);
                        object new_value = property.GetValue (this, null);
                        if (!Object.Equals (old_value, new_value)) {
                            diff_count++;
                            Log.DebugFormat ("Track field changed: {0} (old={1}, new={2})",
                                property.Name, old_value, new_value);
                        }
                    }
                } catch (Exception e) {
                    Log.Exception (e);
                }
            }
        }

        private int diff_count;
        public int DiffCount {
            get { return diff_count; }
        }

        private int editor_index;
        public int EditorIndex {
            get { return editor_index; }
            set { editor_index = value; }
        }

        private int editor_count;
        public int EditorCount {
            get { return editor_count; }
            set { editor_count = value; }
        }

        private TrackInfo source_track;
        public TrackInfo SourceTrack {
            get { return source_track; }
        }

        public TagLib.File GetTaglibFile ()
        {
            if (Uri.Scheme == "http" || Uri.Scheme == "https")
                return null;

            try {
                return StreamTagger.ProcessUri (Uri);
            } catch (Exception e) {
                if (Uri.Scheme == "file") {
                    Hyena.Log.Exception ("Cannot load TagLib file", e);
                }
            }

            return null;
        }
    }
}
