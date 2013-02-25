//
// PathPattern.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//   Alexander Kojevnikov <alexander@kojevnikov.com>
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2005-2010 Novell, Inc.
// Copyright (C) 2009 Alexander Kojevnikov
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
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using Mono.Unix;

using Banshee.Configuration;
using Banshee.Configuration.Schema;
using Banshee.Collection;

namespace Banshee.Base
{
    public class PathPattern
    {
        public FileNamePattern.FilterHandler Filter;

        private SortedList<string, FileNamePattern.Conversion> conversion_table = new SortedList<string, FileNamePattern.Conversion> ();

        public PathPattern ()
        {
        }

        public void AddConversion (string token, string name, FileNamePattern.ExpandTokenHandler handler)
        {
            conversion_table.Add (token, new FileNamePattern.Conversion (token, name, handler));
        }

        public IEnumerable<FileNamePattern.Conversion> PatternConversions {
            get { return conversion_table.Values; }
        }

        public virtual IEnumerable<TrackInfo> SampleTracks {
            get { yield return new SampleTrackInfo (); }
        }

        public string DefaultFolder { get; set; }
        public string DefaultFile   { get; set; }

        public string DefaultPattern {
            get { return CreateFolderFilePattern (DefaultFolder, DefaultFile); }
        }

        public SchemaEntry<string> FileSchema { get; set; }
        public SchemaEntry<string> FolderSchema { get; set; }

        public string [] SuggestedFolders { get; set; }
        public string [] SuggestedFiles   { get; set; }

        private string OnFilter (string input)
        {
            string repl_pattern = input;

            FileNamePattern.FilterHandler filter_handler = Filter;
            if (filter_handler != null) {
                repl_pattern = filter_handler (repl_pattern);
            }

            return repl_pattern;
        }

        public string CreateFolderFilePattern (string folder, string file)
        {
            return String.Format ("{0}%path_sep%{1}", folder, file);
        }

        public string CreatePatternDescription (string pattern)
        {
            pattern = Convert (pattern, conversion => conversion.Name);
            return OnFilter (pattern);
        }

        public string CreateFromTrackInfo (TrackInfo track)
        {
            string pattern = null;

            try {
                pattern = CreateFolderFilePattern (FolderSchema.Get (), FileSchema.Get ());
            } catch {}

            return CreateFromTrackInfo (pattern, track);
        }

        public string CreateFromTrackInfo (string pattern, TrackInfo track)
        {
            if (pattern == null || pattern.Trim () == String.Empty) {
                pattern = DefaultPattern;
            }

            pattern = Convert (pattern, conversion => conversion.Handler (track, null));

            return OnFilter (pattern);
        }

        private static Regex optional_tokens_regex = new Regex ("{([^}]*)}", RegexOptions.Compiled);

        public string Convert (string pattern, Func<FileNamePattern.Conversion, string> handler)
        {
            if (String.IsNullOrEmpty (pattern)) {
                return null;
            }

            pattern = optional_tokens_regex.Replace (pattern, delegate (Match match) {
                var sub_pattern = match.Groups[1].Value;
                foreach (var conversion in PatternConversions) {
                    var token_string = conversion.TokenString;
                    if (!sub_pattern.Contains (token_string)) {
                        continue;
                    }
                    var replacement = handler (conversion);
                    if (String.IsNullOrEmpty (replacement)) {
                        sub_pattern = String.Empty;
                        break;
                    }
                    sub_pattern = sub_pattern.Replace (token_string, replacement);
                }
                return sub_pattern;
            });

            foreach (FileNamePattern.Conversion conversion in PatternConversions) {
                pattern = pattern.Replace (conversion.TokenString, handler (conversion));
            }

            return pattern;
        }

        public string BuildFull (string base_dir, TrackInfo track)
        {
            return BuildFull (base_dir, track, Path.GetExtension (track.Uri.ToString ()));
        }

        public string BuildFull (string base_dir, TrackInfo track, string ext)
        {
            if (ext == null || ext.Length < 1) {
                ext = String.Empty;
            } else if (ext[0] != '.') {
                ext = String.Format (".{0}", ext);
            }

            string track_path = CreateFromTrackInfo (track);
            track_path = Hyena.StringUtil.EscapePath (track_path);

            string dir = Path.GetFullPath (Path.Combine (base_dir,
                Path.GetDirectoryName (track_path)));
            if (!Banshee.IO.Directory.Exists (dir)) {
                Banshee.IO.Directory.Create (dir);
            }

            string filename = Path.GetFileName (track_path);
            // Most filesystems have a filename length limit of 255 characters or bytes
            // We use a limit of 200 characters as a compromise
            if (filename.Length > 200) {
                filename = filename.Remove (200);
            }

            return Path.Combine (dir, filename + ext);
        }

        public static string Escape (string input)
        {
            return Hyena.StringUtil.EscapeFilename (input);
        }
    }
}
