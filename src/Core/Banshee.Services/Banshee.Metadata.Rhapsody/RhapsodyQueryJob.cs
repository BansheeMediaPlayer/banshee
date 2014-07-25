//
// RhapsodyQueryJob.cs
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
using System.Text.RegularExpressions;
using System.Collections.Generic;

using Hyena;
using Banshee.Base;
using Banshee.Collection;
using Banshee.Metadata;
using Banshee.Kernel;
using Banshee.Streaming;
using Banshee.Networking;

namespace Banshee.Metadata.Rhapsody
{
    public class RhapsodyQueryJob : MetadataServiceJob
    {
        private static Uri base_uri = new Uri("http://www.rhapsody.com/");

        private static Regex filter_regex = new Regex (@"[^A-Za-z0-9]*", RegexOptions.Compiled);
        private static string EscapeUrlPart (string part)
        {
            return filter_regex.Replace (part, "").Replace (' ', '-').ToLower ();
        }

        private static string GetAlbumUrl (IBasicTrackInfo track)
        {
            if (track == null || track.AlbumArtist == null || track.AlbumTitle == null) {
                return null;
            }

            string artist = EscapeUrlPart (track.AlbumArtist);
            string album = EscapeUrlPart (track.AlbumTitle);
            return String.Format ("{0}/{1}", artist, album);
        }

        public RhapsodyQueryJob(IBasicTrackInfo track)
        {
            Track = track;
        }

        public override void Run()
        {
            if (!OnlineMetadataServiceJob.TrackConditionsMet (Track)
                || GetAlbumUrl (Track) == null) {
                return;
            }

            string artwork_id = Track.ArtworkId;

            if(artwork_id == null || CoverArtSpec.CoverExists(artwork_id) || !InternetConnected) {
                return;
            }

            Uri data_uri = new Uri(base_uri, String.Format("/{0}/data.xml", GetAlbumUrl (Track)));

            XmlDocument doc = new XmlDocument();
            HttpWebResponse response = GetHttpStream (data_uri);
            if (response == null) {
                return;
            }

            string [] content_types = response.Headers.GetValues ("Content-Type");
            if (content_types.Length == 0 || content_types[0] != "text/xml") {
                response.Close ();
                return;
            }

            using (Stream stream = response.GetResponseStream ()) {
                doc.Load (stream);
            }

            XmlNode art_node = doc.DocumentElement.SelectSingleNode("/album/art/album-art[@size='large']/img");
            if(art_node != null && art_node.Attributes["src"] != null) {
                // awesome hack to get high resolution cover art from Rhapsody
                string second_attempt = art_node.Attributes["src"].Value;
                string first_attempt = second_attempt.Replace("170x170", "500x500");

                if(SaveHttpStreamCover(new Uri(first_attempt), artwork_id, null) ||
                    SaveHttpStreamCover(new Uri(second_attempt), artwork_id, null)) {
                    Log.Debug ("Downloaded cover art from Rhapsody", artwork_id);
                    StreamTag tag = new StreamTag();
                    tag.Name = CommonTags.AlbumCoverId;
                    tag.Value = artwork_id;

                    AddTag(tag);
                }
            }

            response.Close ();
        }
    }
}
