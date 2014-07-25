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
using Hyena.Jobs;
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
    public class CoverArtJob : DbIteratorJob
    {
        private DateTime last_scan = DateTime.MinValue;
        private TimeSpan retry_every = TimeSpan.FromDays (7);

        public CoverArtJob (DateTime lastScan) : base (Catalog.GetString ("Downloading Cover Art"))
        {
            last_scan = lastScan;

            // Since we do last_scan - retry_every, avoid out-of-range error by ensuring
            // the last_scan date isn't already MinValue
            if (last_scan == DateTime.MinValue) {
                last_scan = DateTime.Now - TimeSpan.FromDays (365*50);
            }

            CountCommand = new HyenaSqliteCommand (@"
                SELECT count(DISTINCT CoreTracks.AlbumID)
                    FROM CoreTracks, CoreArtists, CoreAlbums
                    WHERE
                        CoreTracks.PrimarySourceID = ? AND
                        CoreTracks.DateUpdatedStamp > ? AND
                        CoreTracks.AlbumID = CoreAlbums.AlbumID AND
                        CoreAlbums.ArtistID = CoreArtists.ArtistID AND
                        CoreTracks.AlbumID NOT IN (
                            SELECT AlbumID FROM CoverArtDownloads WHERE
                                LastAttempt > ? OR Downloaded = 1)",
                ServiceManager.SourceManager.MusicLibrary.DbId, last_scan, last_scan - retry_every
            );

            SelectCommand = new HyenaSqliteCommand (String.Format (@"
                SELECT DISTINCT CoreAlbums.AlbumID, CoreAlbums.Title, CoreArtists.Name, {0}, CoreTracks.TrackID
                    FROM CoreTracks, CoreArtists, CoreAlbums
                    WHERE
                        CoreTracks.PrimarySourceID = ? AND
                        CoreTracks.DateUpdatedStamp > ? AND
                        CoreTracks.AlbumID = CoreAlbums.AlbumID AND
                        CoreAlbums.ArtistID = CoreArtists.ArtistID AND
                        CoreTracks.AlbumID NOT IN (
                            SELECT AlbumID FROM CoverArtDownloads WHERE
                                LastAttempt > ? OR Downloaded = 1)
                    GROUP BY CoreTracks.AlbumID ORDER BY CoreTracks.DateUpdatedStamp DESC LIMIT ?",
                Banshee.Query.BansheeQuery.UriField.Column),
                ServiceManager.SourceManager.MusicLibrary.DbId, last_scan, last_scan - retry_every, 1
            );

            SetResources (Resource.Database);
            PriorityHints = PriorityHints.LongRunning;

            IsBackground = true;
            CanCancel = true;
            DelayShow = true;
        }

        public void Start ()
        {
            Register ();
        }

        private class CoverartTrackInfo : DatabaseTrackInfo
        {
            public long DbId {
                set { TrackId = value; }
            }
        }

        protected override void IterateCore (HyenaDataReader reader)
        {
            var track = new CoverartTrackInfo () {
                AlbumTitle = reader.Get<string> (1),
                ArtistName = reader.Get<string> (2),
                PrimarySource = ServiceManager.SourceManager.MusicLibrary,
                Uri = new SafeUri (reader.Get<string> (3)),
                DbId = reader.Get<long> (4),
                AlbumId = reader.Get<long> (0)
            };

            Status = String.Format (Catalog.GetString ("{0} - {1}"), track.ArtistName, track.AlbumTitle);
            FetchForTrack (track);
        }

        private void FetchForTrack (DatabaseTrackInfo track)
        {
            bool save = true;
            try {
                IMetadataLookupJob job = MetadataService.Instance.CreateJob (track);
                job.Run ();
            } catch (System.Threading.ThreadAbortException) {
                save = false;
                throw;
            } catch (Exception e) {
                Log.Error (e);
            } finally {
                if (save) {
                    bool have_cover_art = CoverArtSpec.CoverExists (track.ArtistName, track.AlbumTitle);
                    ServiceManager.DbConnection.Execute (
                        "INSERT OR REPLACE INTO CoverArtDownloads (AlbumID, Downloaded, LastAttempt) VALUES (?, ?, ?)",
                        track.AlbumId, have_cover_art, DateTime.Now);
                }
            }
        }

        protected override void OnCancelled ()
        {
            AbortThread ();
        }
    }
}
