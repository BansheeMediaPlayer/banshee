/***************************************************************************
 *  MusicBrainzQueryJob.cs
 *
 *  Copyright (C) 2006-2007 Novell, Inc.
 *  Written by James Willcox <snorp@novell.com>
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
using System.IO;
using System.Net;
using System.Xml;
using System.Text;
using System.Collections.Generic;

using Banshee.Base;
using Banshee.Metadata;
using Banshee.Kernel;

namespace Banshee.Metadata.MusicBrainz
{
    public class MusicBrainzQueryJob : MetadataServiceJob
    {
        private enum MBQueryType {
            Unknown,
            Release,
            Track
        }

        private static string AmazonUriFormat = "http://images.amazon.com/images/P/{0}.01._SCLZZZZZZZ_.jpg";
    
        private TrackInfo track;
        private string asin;
        private string artist_album_id;
        private string artist_name;
        private string album_title;
        
        public MusicBrainzQueryJob(IBasicTrackInfo track, MetadataSettings settings)
        {
            Track = track;
            
            this.track = track as TrackInfo;
            this.artist_name = track.Artist;
            this.album_title = track.Album;
            
            Settings = settings;
        }
        
        public MusicBrainzQueryJob(IBasicTrackInfo track, MetadataSettings settings, string asin) : this(track, settings)
        {
            this.asin = asin;
        }
        
        public override void Run()
        {
            Lookup();
        }
        
        public bool Lookup()
        {
            if(track == null || (!track.IsLive && track.CoverArtFileName != null)) {
                return false;
            }
            
            artist_album_id = TrackInfo.CreateArtistAlbumID(artist_name, album_title, false);
            
            if(!track.IsLive && artist_album_id == null) {
                return false;
            } else if(!track.IsLive && File.Exists(Paths.GetCoverArtPath(artist_album_id))) {
                return false;
            } else if(!Settings.NetworkConnected) {
                return false;
            }
            
            RunQueries();
            
            return false;
        }

        private void RunQueries()
        {
            Uri uri = null;
            MBQueryType query_type = MBQueryType.Unknown;
            
            if(!String.IsNullOrEmpty(track.Artist) && !String.IsNullOrEmpty(track.Album)) {
                query_type = MBQueryType.Release;
                uri = new Uri(String.Format("http://musicbrainz.org/ws/1/release/?type=xml&artist={0}&title={1}",
                    track.Artist, track.Album));
            } else if(!String.IsNullOrEmpty(track.Artist) && !String.IsNullOrEmpty(track.Title)) {
                query_type = MBQueryType.Track;
                uri = new Uri(String.Format("http://musicbrainz.org/ws/1/track/?type=xml&artist={0}&title={1}&inc=release-rels",
                    track.Artist, track.Title));
            }

            if(uri == null || query_type == MBQueryType.Unknown) {
                return;
            }

            XmlTextReader reader = new XmlTextReader(GetHttpStream(uri));

            switch(query_type) {
                case MBQueryType.Release:
                    ParseRelease(track, reader, true);
                    break;
                case MBQueryType.Track:
                    ParseTrack(track, reader);
                    break;
            }
        }

        private void ParseRelease(TrackInfo track, XmlTextReader reader, bool respectScore)
        {
            bool have_match = !respectScore;

            while(reader.Read()) {
                if(reader.NodeType != XmlNodeType.Element) {
                    continue;
                }
                
                switch(reader.LocalName) {
                    case "release":
                        if(respectScore) {
                            have_match = reader["ext:score"] == "100";
                        }
                        
                        break;
                    case "title":
                        if(!track.IsLive) {
                            break;
                        }
                        
                        album_title = reader.ReadString();
                        if(!String.IsNullOrEmpty(album_title)) {
                            StreamTag tag = new StreamTag();
                            tag.Name = CommonTags.Album;
                            tag.Value = album_title;
                            AddTag(tag);
                        }
                        
                        break;
                    case "asin":
                        if(have_match && asin == null) {
                            asin = reader.ReadString();
                            artist_album_id = TrackInfo.CreateArtistAlbumID(artist_name, album_title, false);
                            
                            if(artist_album_id != null && SaveHttpStreamPixbuf(new Uri(String.Format(AmazonUriFormat, asin)), 
                                artist_album_id, new string [] { "image/gif" })) {
                                StreamTag tag = new StreamTag();
                                tag.Name = CommonTags.AlbumCoverID;
                                tag.Value = artist_album_id;
                                AddTag(tag);
                            }
                        }
                        
                        break;
                }
            }
        }

        private void ParseTrack(TrackInfo track, XmlTextReader reader)
        {
            bool have_match = false;
            string release_id = null;

            while(reader.Read()) {
                if(reader.NodeType != XmlNodeType.Element) {
                    continue;
                }
                
                switch(reader.LocalName) {
                    case "track":
                        if(have_match) {
                            return;
                        }
                        have_match = reader["ext:score"] == "100";
                        break;
                    case "title":
                        if(!track.IsLive) {
                            break;
                        }
                        
                        string title = reader.ReadString();
                        if(!String.IsNullOrEmpty(title)) {
                            StreamTag tag = new StreamTag();
                            tag.Name = CommonTags.Title;
                            tag.Value = title;
                            AddTag(tag);
                        }
                        
                        break;
                    case "release":
                        release_id = reader["id"];
                        break;
                }

                if(release_id != null) {
                    break;
                }
            }
            
            if(release_id == null) {
                return;
            }
            
            Uri uri = new Uri(String.Format("http://musicbrainz.org/ws/1/release/{0}?type=xml", release_id));
            ParseRelease(track, new XmlTextReader(GetHttpStream(uri)), false);
        }
    }
}
