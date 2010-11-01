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

using Hyena;

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
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

        // When importing new cover art, if not JPEGs, then we use the .cover extension
        // as a signal to the ArtworkManager that it needs to convert it to JPEG first.
        public static string GetPathForNewFile (string aaid, string imagePath)
        {
            string extension = "cover";
            if (imagePath.EndsWith ("jpg", true, System.Globalization.CultureInfo.InvariantCulture) ||
                imagePath.EndsWith ("jpeg", true, System.Globalization.CultureInfo.InvariantCulture)) {
                extension = "jpg";
            }

            return System.IO.Path.ChangeExtension (GetPath (aaid), extension);
        }

        public static string CreateArtistAlbumId (string artist, string album)
        {
            if (album == null || album == unknown_album || album == unknown_album_tr) {
                // do not attempt to group unknown album tracks together
                return null;
            }

            if (artist == null || artist == unknown_artist || artist == unknown_artist_tr) {
                return null;
            }

            string digestible = String.Format ("{0}\t{1}", artist, album);
            return String.Format ("album-{0}", Digest (digestible));
        }

        public static string Digest (string str)
        {
            if (String.IsNullOrEmpty (str)) {
                return null;
            }

            str = str.Normalize (NormalizationForm.FormKD);
            return Hyena.CryptoUtil.Md5Encode (str, Encoding.UTF8);
        }

        static CoverArtSpec () {
            Hyena.Log.DebugFormat ("Album artwork path set to {0}", root_path);
        }

        private static string root_path = Path.Combine (XdgBaseDirectorySpec.GetUserDirectory (
            "XDG_CACHE_HOME", ".cache"),  "media-art");

        public static string RootPath {
            get { return root_path; }
        }

        #region Old spec

        private static string legacy_root_path = Path.Combine (XdgBaseDirectorySpec.GetUserDirectory (
            "XDG_CACHE_HOME", ".cache"),  "album-art");

        public static string LegacyRootPath {
            get { return legacy_root_path; }
        }

        public static string CreateLegacyArtistAlbumId (string artist, string album)
        {
            if (artist == unknown_artist || artist == unknown_artist_tr || album == unknown_album || album == unknown_album_tr) {
                return null;
            }

            string sm_artist = LegacyEscapePart (artist);
            string sm_album = LegacyEscapePart (album);

            return String.IsNullOrEmpty (sm_artist) || String.IsNullOrEmpty (sm_album)
                ? null
                : String.Format ("{0}{1}{2}", sm_artist, "-", sm_album);
        }

        private static Regex legacy_filter_regex = new Regex (@"[^A-Za-z0-9]*", RegexOptions.Compiled);
        public static string LegacyEscapePart (string part)
        {
            if (String.IsNullOrEmpty (part)) {
                return null;
            }

            int lp_index = part.LastIndexOf ('(');
            if (lp_index > 0) {
                part = part.Substring (0, lp_index);
            }
            return legacy_filter_regex.Replace (part, "").ToLower ();
        }

        #endregion Old spec
    }
}
