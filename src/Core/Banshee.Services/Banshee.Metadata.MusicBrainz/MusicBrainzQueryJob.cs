//
// MusicBrainzQueryJob.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2006-2008 Novell, Inc.
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
using System.Net;
using System.Xml;
using System.Text;
using System.Collections.Generic;
using System.Web;

using Hyena;
using Banshee.Base;
using Banshee.Metadata;
using Banshee.Kernel;
using Banshee.Collection;
using Banshee.Streaming;
using Banshee.Networking;

namespace Banshee.Metadata.MusicBrainz
{
    public class MusicBrainzQueryJob : MetadataServiceJob
    {
        private static string AmazonUriFormat = "http://images.amazon.com/images/P/{0}.01._SCLZZZZZZZ_.jpg";

        private string asin;

        public MusicBrainzQueryJob (IBasicTrackInfo track)
        {
            Track = track;
        }

        public MusicBrainzQueryJob (IBasicTrackInfo track, string asin) : this (track)
        {
            this.asin = asin;
        }

        public override void Run ()
        {
            Lookup ();
        }

        public bool Lookup ()
        {
            if (Track == null || (Track.MediaAttributes & TrackMediaAttributes.Podcast) != 0) {
                return false;
            }

            string artwork_id = Track.ArtworkId;

            if (artwork_id == null) {
                return false;
            } else if (CoverArtSpec.CoverExists (artwork_id)) {
                return false;
            } else if (!InternetConnected) {
                return false;
            }

            if (asin == null) {
                asin = FindAsin ();
                if (asin == null) {
                    return false;
                }
            }

            if (SaveHttpStreamCover (new Uri (String.Format (AmazonUriFormat, asin)), artwork_id,
                new string [] { "image/gif" })) {
                Log.Debug ("Downloaded cover art from Amazon", artwork_id);
                StreamTag tag = new StreamTag ();
                tag.Name = CommonTags.AlbumCoverId;
                tag.Value = artwork_id;

                AddTag (tag);

                return true;
            }

            return false;
        }

        // MusicBrainz has this new XML API, so I'm using that here
        // instead of using the stuff in the MusicBrainz namespace
        // which sucks and doesn't even appear to work anymore.

        private string FindAsin ()
        {
            string album_artist = HttpUtility.UrlEncode (Track.AlbumArtist);
            string album_title = HttpUtility.UrlEncode (Track.AlbumTitle);
            Uri uri = new Uri (String.Format ("http://musicbrainz.org/ws/1/release/?type=xml&artist={0}&title={1}",
                album_artist, album_title));

            HttpWebResponse response = GetHttpStream (uri);
            if (response == null) {
                return null;
            }

            using (Stream stream = response.GetResponseStream ()) {
                XmlTextReader reader = new XmlTextReader (stream);

                bool haveMatch = false;

                while (reader.Read ()) {
                    if (reader.NodeType == XmlNodeType.Element) {
                        switch (reader.LocalName) {
                            case "release":
                                haveMatch = reader["ext:score"] == "100";
                                break;
                            case "asin":
                                if (haveMatch) {
                                    return reader.ReadString ();
                                }
                            break;
                        default:
                            break;
                        }
                    }
                }
            }

            return null;
        }
    }
}
