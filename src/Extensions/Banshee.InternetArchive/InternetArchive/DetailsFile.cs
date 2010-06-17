//
// DetailsFile.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2009 Novell, Inc.
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
using System.Linq;

using Mono.Unix;

using Hyena.Json;

namespace InternetArchive
{
    public class DetailsFile
    {
        JsonObject file;
        string location_root;
        string object_key;

        public DetailsFile (JsonObject file, string location_root, string objectKey)
        {
            this.file = file;
            this.location_root = location_root;
            this.object_key = objectKey;
        }

        private string location;
        public string Location {
            get {
                if (location == null) {
                    string loc = file.Get<string> ("location");
                    if (String.IsNullOrEmpty (loc)) {
                        loc = object_key ?? "";
                    }

                    location = location_root + loc;
                }

                return location;
            }
        }

        public long Size {
            get { return file.Get<long> ("size"); }
        }

        public int Track {
            get {
                string track = file.Get<string> ("track");
                if (track == null)
                    return 0;

                var bits = track.Split ('/', '-');
                return Int32.Parse (bits[0]);
            }
        }

        public string Creator {
            get { return file.Get<string> ("creator"); }
        }

        public string OriginalFile {
            get { return file.Get<string> ("original"); }
        }

        public string Title {
            get { return file.Get<string> ("title"); }
        }

        public int BitRate {
            get { return file.Get<int> ("bitrate"); }
        }

        public string Format {
            get { return file.Get<string> ("format"); }
        }

        public TimeSpan Length {
            get { return file.Get<TimeSpan> ("length"); }
        }
    }
}
