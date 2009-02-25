//
// FileNamePattern.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
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
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using Mono.Unix;

using Banshee.Collection;
using Banshee.Configuration.Schema;

namespace Banshee.Base
{
    public static class FileNamePattern
    {
        public delegate string ExpandTokenHandler (ITrackInfo track, object replace);
        public delegate string FilterHandler (string path);
        
        public static FilterHandler Filter;
        
        public struct Conversion
        {
            private string token;
            private string name;
            private ExpandTokenHandler handler;
            
            public Conversion (string token, string name, ExpandTokenHandler handler)
            {
                this.token = token;
                this.name = name;
                this.handler = handler;
            }
            
            public string Token {
                get { return token; }
            }
            
            public string Name {
                get { return name; }
            }
            
            public ExpandTokenHandler Handler {
                get { return handler; }
            }
        }
    
        private static SortedList<string, Conversion> conversion_table;

        public static void AddConversion (string token, string name, ExpandTokenHandler handler)
        {
            conversion_table.Add (token, new Conversion (token, name, handler));
        }
        
        static FileNamePattern ()
        {
            conversion_table = new SortedList<string, Conversion> ();
            
            AddConversion ("artist", Catalog.GetString ("Artist"),  
                delegate (ITrackInfo t, object r) {
                    return Escape (t == null ? (string)r : t.DisplayArtistName);
            });
                        
            AddConversion ("genre", Catalog.GetString ("Genre"),  
                delegate (ITrackInfo t, object r) {
                    return Escape (t == null ? (string)r : t.DisplayGenre);
            });

            AddConversion ("album", Catalog.GetString ("Album"),  
                delegate (ITrackInfo t, object r) {
                    return Escape (t == null ? (string)r : t.DisplayAlbumTitle);
            });
            
            AddConversion ("title", Catalog.GetString ("Title"),  
                delegate (ITrackInfo t, object r) {
                    return Escape (t == null ? (string)r : t.DisplayTrackTitle);
            });
             
            AddConversion ("year", Catalog.GetString ("Year"),  
                delegate (ITrackInfo t, object r) {
                    return String.Format ("{0}", t == null ? (int)r : t.Year);
            });
             
            AddConversion ("track_count", Catalog.GetString ("Count"),  
                delegate (ITrackInfo t, object r) {
                    return String.Format ("{0:00}", t == null ? (int)r : t.TrackCount);
            });
             
            AddConversion ("track_number", Catalog.GetString ("Number"),  
                delegate (ITrackInfo t, object r) {
                    return String.Format ("{0:00}", t == null ? (int)r : t.TrackNumber);
            });
             
            AddConversion ("track_count_nz", Catalog.GetString ("Count (unsorted)"),  
                delegate (ITrackInfo t, object r) {
                    return String.Format ("{0}", t == null ? (int)r : t.TrackCount);
            });
             
            AddConversion ("track_number_nz", Catalog.GetString ("Number (unsorted)"),  
                delegate (ITrackInfo t, object r) {
                    return String.Format ("{0}", t == null ? (int)r : t.TrackNumber);
            });
            
            AddConversion ("path_sep", Path.DirectorySeparatorChar.ToString (),
                delegate (ITrackInfo t, object r) {
                    return Path.DirectorySeparatorChar.ToString ();
            });
        }
        
        public static IEnumerable<Conversion> PatternConversions {
            get { return conversion_table.Values; }
        }
        
        public static string DefaultFolder {
            get { return "%artist%%path_sep%%album%"; }
        }
        
        public static string DefaultFile {
            get { return "%track_number%. %title%"; }
        }
        
        public static string DefaultPattern {
            get { return CreateFolderFilePattern (DefaultFolder, DefaultFile); }
        }
        
        private static string [] suggested_folders = new string [] {
            DefaultFolder,
            "%artist%%path_sep%%artist% - %album%",
            "%artist%%path_sep%%album% (%year%)",
            "%artist% - %album%",
            "%album%",
            "%artist%"
        };
        
        public static string [] SuggestedFolders {
            get { return suggested_folders; }
        }
    
        private static string [] suggested_files = new string [] {
            DefaultFile,
            "%track_number%. %artist% - %title%",
            "%artist% - %title%",
            "%artist% - %track_number% - %title%",
            "%artist% (%album%) - %track_number% - %title%",
            "%title%"
        };
        
        public static string [] SuggestedFiles {
            get { return suggested_files; }
        }
        
        private static string OnFilter (string input)
        {
            string repl_pattern = input;
            
            FilterHandler filter_handler = Filter;
            if (filter_handler != null) {
                repl_pattern = filter_handler (repl_pattern);
            }
            
            return repl_pattern;
        }

        public static string CreateFolderFilePattern (string folder, string file)
        {
            return String.Format ("{0}%path_sep%{1}", folder, file);
        }

        public static string CreatePatternDescription (string pattern)
        {
            string repl_pattern = pattern;
            foreach (Conversion conversion in PatternConversions) {
                repl_pattern = repl_pattern.Replace ("%" + conversion.Token + "%", conversion.Name);
            }
            return OnFilter (repl_pattern);
        }

        public static string CreateFromTrackInfo (ITrackInfo track)
        {
            string pattern = null;

            try {
                pattern = CreateFolderFilePattern (
                    LibrarySchema.FolderPattern.Get (),
                    LibrarySchema.FilePattern.Get ()
                );
            } catch {
            }

            return CreateFromTrackInfo (pattern, track);
        }

        public static string CreateFromTrackInfo (string pattern, ITrackInfo track)
        {
            string repl_pattern;

            if (pattern == null || pattern.Trim () == String.Empty) {
                repl_pattern = DefaultPattern;
            } else {
                repl_pattern = pattern;
            }

            foreach (Conversion conversion in PatternConversions) {
                repl_pattern = repl_pattern.Replace ("%" + conversion.Token + "%", 
                    conversion.Handler (track, null));
            }
            
            return OnFilter (repl_pattern);
        }

        public static string BuildFull (TrackInfo track)
        {
            return BuildFull (track, Path.GetExtension (track.Uri.ToString ()));
        }

        public static string BuildFull (ITrackInfo track, string ext)
        {
            if (ext == null || ext.Length < 1) {
                ext = String.Empty;
            } else if (ext[0] != '.') {
                ext = String.Format (".{0}", ext);
            }
            
            string songpath = CreateFromTrackInfo (track) + ext;
            string dir = Path.GetFullPath (Path.Combine (Paths.LibraryLocation, 
                Path.GetDirectoryName (songpath)));
            string filename = Path.Combine (dir, Path.GetFileName (songpath));
                
            if (!Banshee.IO.Directory.Exists (dir)) {
                Banshee.IO.Directory.Create (dir);
            }
            
            return filename;
        }

        public static string Escape (string input)
        {
            return Hyena.StringUtil.EscapeFilename (input);
        }
    }
}
