/*************************************************************************** 
 *  PodcastItem.cs
 *
 *  Copyright (C) 2008 Michael C. Urbanski
 *  Written by Mike Urbanski <michael.c.urbanski@gmail.com>
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
using System.Text;
using Mono.Unix;

using System.Collections.Generic;

using Hyena.Data.Sqlite;

using Banshee.MediaEngine;
using Banshee.ServiceStack;

using Banshee.Database;
using Banshee.Collection;
using Banshee.Collection.Database;

using Migo.Syndication;

namespace Banshee.Podcasting.Data 
{
    public enum PodcastItemActivity : int {
        Downloading = 0,
        DownloadPending = 1,        
        DownloadFailed = 2,
        DownloadPaused = 3,        
        //NewPodcastItem = 4,
        //Video = 5,
        Downloaded = 6,
        None = 7
    }

    public class PodcastTrackInfo
    {
        public static PodcastTrackInfo From (TrackInfo track)
        {
            if (track != null) {
                PodcastTrackInfo pi = track.ExternalObject as PodcastTrackInfo;
                if (pi != null) {
                    track.ReleaseDate = pi.PublishedDate;
                    track.Enabled = !pi.Item.IsRead;
                }
                return pi;
            }
            return null;
        }

        public static IEnumerable<PodcastTrackInfo> From (IEnumerable<TrackInfo> tracks)
        {
            foreach (TrackInfo track in tracks) {
                PodcastTrackInfo pi = From (track);
                if (pi != null) {
                    yield return pi;
                }
            }
        }
        
        private int position;
        private DatabaseTrackInfo track;

#region Properties

        public DatabaseTrackInfo Track {
            get { return track; }
        }

        public Feed Feed {
            get { return Item.Feed; }
        }
        
        private FeedItem item;
        public FeedItem Item {
            get {
                if (item == null && track.ExternalId > 0) {
                    item = FeedItem.Provider.FetchSingle (track.ExternalId);
                }
                return item;
            }
            set { item = value; track.ExternalId = value.DbId; }
        }
        
        public DateTime PublishedDate {
            get { return Item.PubDate; }
        }

        public string Description {
            get { return Item.StrippedDescription; }
        }
        
        public bool IsNew {
            get { return !Item.IsRead; }
        }
        
        public bool IsDownloaded {
            get { return !String.IsNullOrEmpty (Enclosure.LocalPath); }
        }
        
        public int Position {
            get { return position; }
            set { position = value; }
        }

        public DateTime ReleaseDate {
            get { return Item.PubDate; }
        }
        
        public FeedEnclosure Enclosure {
            get { return (Item == null) ? null : Item.Enclosure; }
        }

        public PodcastItemActivity Activity {
            get {
                switch (Item.Enclosure.DownloadStatus) {
                case FeedDownloadStatus.Downloaded:
                    return PodcastItemActivity.Downloaded;
               
                case FeedDownloadStatus.DownloadFailed:
                    return PodcastItemActivity.Downloaded;
                    
                case FeedDownloadStatus.Downloading:
                    return PodcastItemActivity.Downloading;
                    
                case FeedDownloadStatus.Pending:
                    return PodcastItemActivity.DownloadPending;
                    
                case FeedDownloadStatus.Paused:
                    return PodcastItemActivity.DownloadPaused;

                default:
                    return PodcastItemActivity.None;   
                }
            }
        }

#endregion

#region Constructors
    
        public PodcastTrackInfo (DatabaseTrackInfo track) : base ()
        {
            this.track = track;
        }
        
        public PodcastTrackInfo (DatabaseTrackInfo track, FeedItem feed_item) : this (track)
        {
            Item = feed_item;
            SyncWithFeedItem ();
        }

#endregion

        static PodcastTrackInfo ()
        {
            TrackInfo.PlaybackFinished += OnPlaybackFinished;
        }

        private static void OnPlaybackFinished (TrackInfo track, double percentCompleted)
        {
            if (percentCompleted > 0.5 && track.PlayCount > 0) {
                PodcastTrackInfo pi = PodcastTrackInfo.From (track);
                if (pi != null && !pi.Item.IsRead) {
                    pi.Item.IsRead = true;
                    pi.Item.Save ();
                }
            }   
        }
        
        public void SyncWithFeedItem ()
        {
            //Console.WriteLine ("Syncing item, enclosure == null? {0}", Item.Enclosure == null);
            track.ArtistName = Item.Author;
            track.AlbumTitle = Item.Feed.Title;
            track.TrackTitle = Item.Title;
            track.Year = Item.PubDate.Year;
            track.CanPlay = true;
            track.Genre = track.Genre ?? "Podcast";
            track.ReleaseDate = Item.PubDate;
            track.MimeType = Item.Enclosure.MimeType;
            track.Duration = Item.Enclosure.Duration;
            track.FileSize = Item.Enclosure.FileSize;
            track.LicenseUri = Item.LicenseUri;
            track.Uri = new Banshee.Base.SafeUri (Item.Enclosure.LocalPath ?? Item.Enclosure.Url);
            
            if (!String.IsNullOrEmpty (Item.Enclosure.LocalPath)) {
                try {
                    TagLib.File file = Banshee.Streaming.StreamTagger.ProcessUri (track.Uri);
                    Banshee.Streaming.StreamTagger.TrackInfoMerge (track, file, true);
                } catch {}
            }

            track.MediaAttributes |= TrackMediaAttributes.Podcast;
        }
    }
}
