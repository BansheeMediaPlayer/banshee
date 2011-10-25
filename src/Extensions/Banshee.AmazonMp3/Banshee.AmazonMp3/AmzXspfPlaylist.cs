// 
// AmzXspfPlaylist.cs
//  
// Author:
//   Aaron Bockover <abockover@novell.com>
// 
// Copyright 2010 Novell, Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOvoidT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;

using Xspf = Media.Playlists.Xspf;

namespace Banshee.AmazonMp3
{
    public class AmzXspfPlaylist : Xspf.Playlist
    {
        /*
            Decrypt/Encrypt an AMZ file. To encrypt, remove the -d option
            and invert the -in/-out values.

            openssl enc -a -d -des-cbc \
                -K 29AB9D18B2449E31 \
                -iv 5E72D79A11B34FEE \
                -in input.amz \
                -out output.xml
        */

        private static byte [] amz_key = new byte []
            { 0x29, 0xAB, 0x9D, 0x18, 0xB2, 0x44, 0x9E, 0x31 };

        private static byte [] amz_iv = new byte []
            { 0x5E, 0x72, 0xD7, 0x9A, 0x11, 0xB3, 0x4F, 0xEE };

        private List<Xspf.Track> downloadable_tracks = new List<Xspf.Track> ();

        public AmzXspfPlaylist (string path)
        {
            using (var stream = File.OpenRead (path)) {
                Decrypt (stream);
            }
        }

        public AmzXspfPlaylist (Stream stream)
        {
            Decrypt (stream);
        }

        private void Decrypt (Stream stream)
        {
            // Amazon sometimes sends an unencrypted XSPF file, which doesn't start
            // with <?xml... but with a <playlist> tag
            var magic = new byte [10];
            if (stream.Read (magic, 0, 10) == 10) {
                string start = System.Text.Encoding.UTF8.GetString (magic);

                if (start.Contains ("<?xml") || start.Contains ("<playlist")) {
                    stream.Seek (0, SeekOrigin.Begin);
                    Load (stream);
                    AmzLoad ();
                    return;
                }
            }

            stream.Seek (0, SeekOrigin.Begin);

            var crypto_service = new DESCryptoServiceProvider () {
                Key = amz_key,
                IV = amz_iv,
                Mode = CipherMode.CBC
            };

            using (var encrypted_stream = new MemoryStream (Base64Decode (stream))) {
                using (var decrypted_stream = new CryptoStream (encrypted_stream,
                    crypto_service.CreateDecryptor (), CryptoStreamMode.Read)) {
                    using (var reader = new StreamReader (decrypted_stream)) {
                        Load (decrypted_stream);
                        AmzLoad ();
                    }
                }
            }
        }

        private byte [] Base64Decode (Stream input)
        {
            using (var reader = new StreamReader (input)) {
                return Convert.FromBase64String (reader.ReadToEnd ());
            }
        }

        private void AmzLoad ()
        {
            var downloadable_track_meta = new Xspf.MetaEntry (
                new Uri ("http://www.amazon.com/dmusic/productTypeName"),
                "DOWNLOADABLE_MUSIC_TRACK");

            foreach (var track in Tracks) {
                if (track.Meta.Contains (downloadable_track_meta)) {
                    downloadable_tracks.Add (track);
                }
            }
        }

        protected override void LoadExtensionNode (XmlNode extensionNode, XmlNamespaceManager xmlns)
        {
            // Digital Booklets (pdf) are stuffed under extension/deluxe, so we need
            // to support this XSPF extension that is specific to the .amz format.
            foreach (XmlNode node in extensionNode.SelectNodes ("xspf:deluxe/xspf:trackList", xmlns)) {
                LoadTrackListFromNode (node, xmlns);
            }
        }

        public ReadOnlyCollection<Xspf.Track> DownloadableTracks {
            get { return new ReadOnlyCollection<Xspf.Track> (downloadable_tracks); }
        }

        public int DownloadableTrackCount {
            get { return downloadable_tracks.Count; }
        }
    }
}
