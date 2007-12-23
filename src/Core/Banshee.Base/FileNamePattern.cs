/***************************************************************************
 *  FileNamePattern.cs
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@abock.org>
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */
 
using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using Mono.Unix;

using Banshee.Configuration.Schema;

namespace Banshee.Base
{
    public static class FileNamePattern
    {
        public delegate string ExpandTokenHandler(TrackInfo track, object replace);
        public delegate string FilterHandler(string path);
        
        private static string invalid_path_characters = "\"\\:'~`!@#$%^&*_-+|?/><[]";
        private static Regex invalid_path_regex;
        
        public static FilterHandler Filter;
        
        public struct Conversion
        {
            private string token;
            private string name;
            private ExpandTokenHandler handler;
            
            public Conversion(string token, string name, ExpandTokenHandler handler)
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

        public static void AddConversion(string token, string name, ExpandTokenHandler handler)
        {
            conversion_table.Add(token, new Conversion(token, name, handler));
        }
        
        static FileNamePattern()
        {
            conversion_table = new SortedList<string, Conversion>();
            
            AddConversion("artist", Catalog.GetString("Artist"),  
                delegate(TrackInfo t, object r) {
                    return Escape(t == null ? (string)r : t.DisplayArtist);
            });
            
            AddConversion("album", Catalog.GetString("Album"),  
                delegate(TrackInfo t, object r) {
                    return Escape(t == null ? (string)r : t.DisplayAlbum);
            });
            
            AddConversion("title", Catalog.GetString("Title"),  
                delegate(TrackInfo t, object r) {
                    return Escape(t == null ? (string)r : t.DisplayTitle);
            });
             
            AddConversion("track_count", Catalog.GetString("Count"),  
                delegate(TrackInfo t, object r) {
                    return String.Format("{0:00}", t == null ? (uint)r : t.TrackCount);
            });
             
            AddConversion("track_number", Catalog.GetString("Number"),  
                delegate(TrackInfo t, object r) {
                    return String.Format("{0:00}", t == null ? (uint)r : t.TrackNumber);
            });
             
            AddConversion("track_count_nz", Catalog.GetString("Count (unsorted)"),  
                delegate(TrackInfo t, object r) {
                    return String.Format("{0}", t == null ? (uint)r : t.TrackCount);
            });
             
            AddConversion("track_number_nz", Catalog.GetString("Number (unsorted)"),  
                delegate(TrackInfo t, object r) {
                    return String.Format("{0}", t == null ? (uint)r : t.TrackNumber);
            });

            AddConversion("year", Catalog.GetString("Year"),
                delegate(TrackInfo t, object r) {
                    return String.Format("{0}", t == null ? (int)r : t.Year);
            });
            
            AddConversion("path_sep", Path.DirectorySeparatorChar.ToString(),
                delegate(TrackInfo t, object r) {
                    return Path.DirectorySeparatorChar.ToString();
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
            get { return CreateFolderFilePattern(DefaultFolder, DefaultFile); }
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
        
        private static string OnFilter(string input)
        {
            string repl_pattern = input;
            
            FilterHandler filter_handler = Filter;
            if(filter_handler != null) {
                repl_pattern = filter_handler(repl_pattern);
            }
            
            return repl_pattern;
        }

        public static string CreateFolderFilePattern(string folder, string file)
        {
            return String.Format("{0}%path_sep%{1}", folder, file);
        }

        public static string CreatePatternDescription(string pattern)
        {
            string repl_pattern = pattern;
            foreach(Conversion conversion in PatternConversions) {
                repl_pattern = repl_pattern.Replace("%" + conversion.Token + "%", conversion.Name);
            }
            return OnFilter(repl_pattern);
        }

        public static string CreateFromTrackInfo(TrackInfo track)
        {
            string pattern = null;

            try {
                pattern = CreateFolderFilePattern(
                    LibrarySchema.FolderPattern.Get(),
                    LibrarySchema.FilePattern.Get()
                );
            } catch {
            }

            return CreateFromTrackInfo(pattern, track);
        }

        public static string CreateFromTrackInfo(string pattern, TrackInfo track)
        {
            string repl_pattern;

            if(pattern == null || pattern.Trim() == String.Empty) {
                repl_pattern = DefaultPattern;
            } else {
                repl_pattern = pattern;
            }

            foreach(Conversion conversion in PatternConversions) {
                repl_pattern = repl_pattern.Replace("%" + conversion.Token + "%", 
                    conversion.Handler(track, null));
            }
            
            return OnFilter(repl_pattern);
        }

        public static string BuildFull(TrackInfo track, string ext)
        {
            if(ext == null || ext.Length < 1) {
                ext = String.Empty;
            } else if(ext[0] != '.') {
                ext = String.Format(".{0}", ext);
            }
            
            string songpath = CreateFromTrackInfo(track) + ext;
            string dir = Path.GetFullPath(Globals.Library.Location + 
                Path.DirectorySeparatorChar + 
                Path.GetDirectoryName(songpath));
            string filename = dir + Path.DirectorySeparatorChar + 
                Path.GetFileName(songpath);
                
            if(!Banshee.IO.IOProxy.Directory.Exists(dir)) {
                Banshee.IO.IOProxy.Directory.Create(dir);
            }
            
            return filename;
        }

        public static string Escape(string input)
        {
            if(invalid_path_regex == null) {
                string regex_str = "[";
                for(int i = 0; i < invalid_path_characters.Length; i++) {
                    regex_str += "\\" + invalid_path_characters[i];
                }
                regex_str += "]+";
                
                invalid_path_regex = new Regex(regex_str);
            }
            
            return invalid_path_regex.Replace(input, String.Empty);
        }
    }
}
