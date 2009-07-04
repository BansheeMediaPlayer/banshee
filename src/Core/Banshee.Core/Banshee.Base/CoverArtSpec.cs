//
// CoverArtSpec.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2007-2008 Novell, Inc.
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
using System.Text.RegularExpressions;

using Mono.Unix;

namespace Banshee.Base
{
    public static class CoverArtSpec
    {
        private static string unknown_artist_tr = Catalog.GetString ("Unknown Artist");
        private static string unknown_artist = "Unknown Artist";
        private static string unknown_album_tr = Catalog.GetString ("Unknown Album");
        private static string unknown_album = "Unknown Album";

        public static bool CoverExists (string artist, string album)
        {
            return CoverExists (CreateArtistAlbumId (artist, album));
        }
    
        public static bool CoverExists (string aaid)
        {
            return CoverExistsForSize (aaid, 0);
        }
        
        public static bool CoverExistsForSize (string aaid, int size)
        {
            return aaid == null ? false : File.Exists (GetPathForSize (aaid, size));
        }
        
        public static string GetPath (string aaid)
        {
            return GetPathForSize (aaid, 0);
        }
    
        public static string GetPathForSize (string aaid, int size)
        {
            return size == 0
                ? Path.Combine (RootPath, String.Format ("{0}.jpg", aaid))
                : Path.Combine (RootPath, Path.Combine (size.ToString (), String.Format ("{0}.jpg", aaid))); 
        }
    
        public static string CreateArtistAlbumId (string artist, string album)
        {
            return CreateArtistAlbumId (artist, album, false);
        }

        public static string CreateArtistAlbumId (string artist, string album, bool asUriPart)
        {
            if (artist == unknown_artist || artist == unknown_artist_tr || album == unknown_album || album == unknown_album_tr) {
                return null;
            }

            string sm_artist = EscapePart (artist);
            string sm_album = EscapePart (album);

            return String.IsNullOrEmpty (sm_artist) || String.IsNullOrEmpty (sm_album)
                ? null 
                : String.Format ("{0}{1}{2}", sm_artist, asUriPart ? "/" : "-", sm_album); 
        }

        private static Regex filter_regex = new Regex (@"[^A-Za-z0-9]*", RegexOptions.Compiled);
        public static string EscapePart (string part)
        {
            if (String.IsNullOrEmpty (part)) {
                return null;
            }
            
            int lp_index = part.LastIndexOf ('(');
            if (lp_index > 0) {
                part = part.Substring (0, lp_index);
            }
            
            return filter_regex.Replace (part, "").ToLower ();
        }
        
        private static string root_path = Path.Combine (XdgBaseDirectorySpec.GetUserDirectory (
            "XDG_CACHE_HOME", ".cache"),  "album-art");
            
        static CoverArtSpec () {
            Hyena.Log.DebugFormat ("Album artwork path set to {0}", root_path);
        }
            
        public static string RootPath {
            get { return root_path; }
        }
    }
}
