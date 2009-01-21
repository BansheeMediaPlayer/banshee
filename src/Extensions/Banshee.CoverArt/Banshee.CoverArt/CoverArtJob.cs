//
// CoverArtJob.cs
//
// Authors:
//   James Willcox <snorp@novell.com>
//   Gabriel Burt <gburt@novell.com>
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
using System.Collections.Generic;
using System.IO;
using System.Threading;

using Mono.Unix;
using Gtk;

using Hyena;
using Hyena.Data.Sqlite;

using Banshee.Base;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Collection.Gui;
using Banshee.Kernel;
using Banshee.Metadata;
using Banshee.ServiceStack;
using Banshee.Library;

namespace Banshee.CoverArt
{
    public class CoverArtJob : UserJob, IJob
    {
        private const int BatchSize = 10;
        
        private DateTime last_scan = DateTime.MinValue;
        private TimeSpan retry_every = TimeSpan.FromDays (7);

        private static HyenaSqliteCommand count_query = new HyenaSqliteCommand (@"
            SELECT count(DISTINCT CoreTracks.AlbumID)
            FROM CoreTracks, CoreArtists, CoreAlbums
            WHERE
                CoreTracks.PrimarySourceID = ? AND
                CoreTracks.DateUpdatedStamp > ? AND
                CoreTracks.AlbumID = CoreAlbums.AlbumID AND 
                CoreAlbums.ArtistID = CoreArtists.ArtistID AND
                CoreTracks.AlbumID NOT IN (
                    SELECT AlbumID FROM CoverArtDownloads WHERE
                        LastAttempt > ? OR Downloaded = 1)");

        private static HyenaSqliteCommand select_query = new HyenaSqliteCommand (@"
            SELECT DISTINCT CoreAlbums.AlbumID, CoreAlbums.Title, CoreArtists.Name, CoreTracks.Uri, CoreTracks.UriType
            FROM CoreTracks, CoreArtists, CoreAlbums
            WHERE
                CoreTracks.PrimarySourceID = ? AND
                CoreTracks.DateUpdatedStamp > ? AND
                CoreTracks.AlbumID = CoreAlbums.AlbumID AND 
                CoreAlbums.ArtistID = CoreArtists.ArtistID AND
                CoreTracks.AlbumID NOT IN (
                    SELECT AlbumID FROM CoverArtDownloads WHERE
                        LastAttempt > ? OR Downloaded = 1)
            GROUP BY CoreTracks.AlbumID LIMIT ?");
        
        public CoverArtJob (DateTime lastScan) : base (Catalog.GetString ("Downloading Cover Art"))
        {
            last_scan = lastScan;

            // Since we do last_scan - retry_every, avoid out-of-range error by ensuring
            // the last_scan date isn't already MinValue
            if (last_scan == DateTime.MinValue) {
                last_scan = DateTime.Now - TimeSpan.FromDays (300);
            }

            CanCancel = true;
            DelayShow = true;

            CancelRequested += delegate {
                Finish ();
            };
        }
        
        public void Start ()
        {
            Register ();
            Scheduler.Schedule (this, JobPriority.Lowest);
        }

        private HyenaDataReader RunQuery ()
        {
            return new HyenaDataReader (ServiceManager.DbConnection.Query (select_query,
                ServiceManager.SourceManager.MusicLibrary.DbId, last_scan, last_scan - retry_every, BatchSize
            ));
        }
        
        public void Run ()
        {
            Status = Catalog.GetString ("Preparing...");
            IconNames = new string [] {Stock.Network};
            
            int current = 0;
            int total = 0;

            try {
                DatabaseTrackInfo track = new DatabaseTrackInfo ();
                while (true) {
                    total = current + ServiceManager.DbConnection.Query<int> (count_query, ServiceManager.SourceManager.MusicLibrary.DbId, last_scan, last_scan - retry_every);
                    if (total == 0 || total <= current) {
                        break;
                    }

                    using (HyenaDataReader reader = RunQuery ()) {
                        while (reader.Read ()) {
                            if (IsCancelRequested) {
                                Finish ();
                                return;
                            }
                            
                            track.AlbumTitle = reader.Get<string> (1);
                            track.ArtistName = reader.Get<string> (2);
                            track.PrimarySource = ServiceManager.SourceManager.MusicLibrary;
                            track.Uri = track.PrimarySource.UriAndTypeToSafeUri (reader.Get<TrackUriType> (4), reader.Get<string> (3));
                            track.AlbumId = reader.Get<int> (0);
                            //Console.WriteLine ("have album {0}/{1} for track uri {2}", track.AlbumId, track.AlbumTitle, track.Uri);

                            Progress = (double) current / (double) total;
                            Status = String.Format (Catalog.GetString ("{0} - {1}"), track.ArtistName, track.AlbumTitle);

                            FetchForTrack (track);
                            current++;
                        }
                    }
                }
            } catch (Exception e) {
                Log.Exception (e);
            }
 
            Finish ();
        }

        private void FetchForTrack (DatabaseTrackInfo track)
        {
            try {
                if (String.IsNullOrEmpty (track.AlbumTitle) || track.AlbumTitle == Catalog.GetString ("Unknown Album") ||
                    String.IsNullOrEmpty (track.ArtistName) || track.ArtistName == Catalog.GetString ("Unknown Artist")) {
                    // Do not try to fetch album art for these
                } else {
                    IMetadataLookupJob job = MetadataService.Instance.CreateJob (track);
                    job.Run ();
                }
            } catch (Exception e) {
                Log.Exception (e);
            } finally {
                bool have_cover_art = CoverArtSpec.CoverExists (track.ArtistName, track.AlbumTitle);
                ServiceManager.DbConnection.Execute (
                    "INSERT OR REPLACE INTO CoverArtDownloads (AlbumID, Downloaded, LastAttempt) VALUES (?, ?, ?)",
                    track.AlbumId, have_cover_art, DateTime.Now);
            }
        }
    }
}
