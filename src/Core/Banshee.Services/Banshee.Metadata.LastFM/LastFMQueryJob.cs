// LastFMQueryJob.cs:
//
// Author:
//   Peter de Kraker <peterdk.dev@umito.nl>
//
// Based on RhapsodyQueryJob.cs
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
using System.Web;
using System.Collections.Generic;


using Hyena;
using Banshee.Base;
using Banshee.Collection;
using Banshee.Metadata;
using Banshee.Kernel;
using Banshee.Streaming;
using Banshee.Networking;
using Banshee.ServiceStack;

using Lastfm.Data;

namespace Banshee.Metadata.LastFM
{
    public class LastFMQueryJob : MetadataServiceJob
    {
        public LastFMQueryJob (IBasicTrackInfo track)
        {
            Track = track;
        }

        public override void Run ()
        {
            if (!OnlineMetadataServiceJob.TrackConditionsMet (Track)) {
                return;
            }

            string artwork_id = Track.ArtworkId;

            if (artwork_id == null || CoverArtSpec.CoverExists (artwork_id) || !InternetConnected) {
                return;
            }

            // Lastfm uses double url-encoding in their current 1.2 api for albuminfo.
            string lastfmArtist = HttpUtility.UrlEncode (HttpUtility.UrlEncode (Track.AlbumArtist));
            string lastfmAlbum = HttpUtility.UrlEncode (HttpUtility.UrlEncode (Track.AlbumTitle));

            LastfmAlbumData album = null;

            try {
                album = new LastfmAlbumData (lastfmArtist, lastfmAlbum);
            } catch {
                return;
            }

            // AllUrls is an array with [small,medium,large] coverart url
            string [] album_cover_urls = album.AlbumCoverUrls.AllUrls ();

            // Select the URL for the coverart with highest resolution
            string best_url = String.Empty;
            foreach (string url in album_cover_urls) {
                if (!String.IsNullOrEmpty (url)) {
                    best_url = url;
                }
            }

            // No URL's found
            if (String.IsNullOrEmpty (best_url) || best_url.Contains ("noimage")) {
                //string upload_url = String.Format ("http://www.last.fm/music/{0}/{1}/+images", lastfmArtist, lastfmAlbum);
                //Log.DebugFormat ("No coverart provided by lastfm. (you can upload it here: {0}) - {1} ", upload_url, Track.ArtworkId);
                return;
            }

            // Hack: You can get higher resolution artwork by replacing 130X130 with 300x300 in lastfm hosted albumart
            string high_res_url = null;
            if (best_url.Contains ("130x130")) {
                high_res_url = best_url.Replace ("130x130", "300x300");
            }

            // Hack: You can get higher resolution artwork from Amazon too (lastfm sometimes uses amazon links)
            if (best_url.Contains ("MZZZZZZZ")) {
                high_res_url = best_url.Replace ("MZZZZZZZ", "LZZZZZZZ");
            }

            // Download the cover
            try {
                if ((high_res_url != null && SaveHttpStreamCover (new Uri (high_res_url), artwork_id, null)) ||
                   SaveHttpStreamCover (new Uri (best_url), artwork_id, null)) {
                    if (best_url.Contains ("amazon")) {
                        Log.Debug ("Downloaded cover art from Amazon", artwork_id);
                    } else {
                        Log.Debug ("Downloaded cover art from Lastfm", artwork_id);
                    }

                    StreamTag tag = new StreamTag ();
                    tag.Name = CommonTags.AlbumCoverId;
                    tag.Value = artwork_id;
                    AddTag (tag);
                }
            } catch (Exception e) {
                Log.Error ("Cover art found on Lastfm, but downloading it failed. Probably server under high load or dead link", e);
            }
        }
    }
}
