//
// RescanPipeline.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
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

using Mono.Unix;

using Hyena;
using Hyena.Jobs;
using Hyena.Collections;
using Hyena.Data.Sqlite;

using Banshee.Base;
using Banshee.Sources;
using Banshee.Collection.Database;
using Banshee.Library;
using Banshee.Metadata;
using Banshee.ServiceStack;

namespace Banshee.Collection
{
    // Goals:
    // 1. Add new files that are on disk but not in the library
    // 2. Find the updated location of files that were moved
    // 3. Update metadata for files that were changed since we last scanned/imported
    // 4. Remove tracks that aren't on disk and weren't found to have moved
    //
    // Approach:
    // 1. For each file in the source's directory, find orphaned db track if any, or add if new
    //    and update if modified; update the LastScannedAt stamp
    // 2. Remove all db tracks from the database that weren't scanned (LastScannedAt < scan_started)
    public class RescanPipeline : QueuePipeline<string>
    {
        private DateTime scan_started;
        private PrimarySource psource;
        private BatchUserJob job;
        private TrackSyncPipelineElement track_sync;
        private Func<int, bool> remove_confirmation;
        private readonly int MAX_NOWARN_TRACKS_REMOVAL = 50;

        public RescanPipeline (LibrarySource psource, Func<int, bool> removeConfirmation) : base ()
        {
            this.psource = psource;
            scan_started = DateTime.Now;
            remove_confirmation = removeConfirmation;

            AddElement (new Banshee.IO.DirectoryScannerPipelineElement ());
            AddElement (track_sync = new TrackSyncPipelineElement (psource, scan_started));
            Finished += OnFinished;

            BuildJob ();
            Enqueue (psource.BaseDirectory);
        }

        private void BuildJob ()
        {
            job = new BatchUserJob (Catalog.GetString ("Rescanning {0} of {1}"), "system-search", "gtk-find");
            job.SetResources (Resource.Cpu, Resource.Disk, Resource.Database);
            job.PriorityHints = PriorityHints.SpeedSensitive;
            job.CanCancel = true;
            job.CancelRequested += delegate { cancelled = true; Cancel (); };
            track_sync.ProcessedItem += delegate {
                job.Total = track_sync.TotalCount;
                job.Completed = track_sync.ProcessedCount;
                job.Status = track_sync.Status;
            };
            job.Register ();
        }

        private bool cancelled = false;
        private void OnFinished (object o, EventArgs args)
        {
            job.Finish ();

            if (cancelled || remove_confirmation == null) {
                return;
            }

            //Hyena.Log.DebugFormat ("Have {0} items before delete", ServiceManager.DbConnection.Query<int>("select count(*) from coretracks where primarysourceid=?", psource.DbId));

            // Delete tracks that are under the BaseDirectory and that weren't rescanned just now
            string condition = String.Format (
                @"WHERE PrimarySourceID = ? AND {0} LIKE ? ESCAPE '\' AND {1} IS NOT NULL AND {1} < ?",
                Banshee.Query.BansheeQuery.UriField.Column, "CoreTracks.LastSyncedStamp"
            );
            string uri = Hyena.StringUtil.EscapeLike (new SafeUri (psource.BaseDirectoryWithSeparator).AbsoluteUri) + "%";

            int remove_count = ServiceManager.DbConnection.Query<int> (
                String.Format (@"SELECT COUNT('x') FROM CoreTracks {0}", condition),
                psource.DbId, uri, scan_started);

            if (remove_count > MAX_NOWARN_TRACKS_REMOVAL) {

                if (!remove_confirmation (remove_count)) {
                    Refresh ();
                    return;
                }
            }

            if (remove_count > 0) {

                ServiceManager.DbConnection.Execute (String.Format (@"BEGIN;
                        DELETE FROM CorePlaylistEntries WHERE TrackID IN (SELECT TrackID FROM CoreTracks {0});
                        DELETE FROM CoreSmartPlaylistEntries WHERE TrackID IN (SELECT TrackID FROM CoreTracks {0});
                        DELETE FROM CoreTracks {0}; COMMIT",
                    condition),
                    psource.DbId, uri, scan_started,
                    psource.DbId, uri, scan_started,
                    psource.DbId, uri, scan_started
                );
            }

            // TODO prune artists/albums
            Refresh ();
            //Hyena.Log.DebugFormat ("Have {0} items after delete", ServiceManager.DbConnection.Query<int>("select count(*) from coretracks where primarysourceid=?", psource.DbId));
        }

        private void Refresh ()
        {
            psource.Reload ();
            psource.NotifyTracksChanged ();
        }
    }

    public class TrackSyncPipelineElement : QueuePipelineElement<string>
    {
        private PrimarySource psource;
        private DateTime scan_started;
        private HyenaSqliteCommand fetch_command, fetch_similar_command;

        private string status;
        public string Status {
            get { return status; }
        }

        public TrackSyncPipelineElement (PrimarySource psource, DateTime scan_started) : base ()
        {
            this.psource = psource;
            this.scan_started = scan_started;

            fetch_command = DatabaseTrackInfo.Provider.CreateFetchCommand (String.Format (
                "CoreTracks.PrimarySourceID = ? AND {0} = ? LIMIT 1",
                Banshee.Query.BansheeQuery.UriField.Column));

            fetch_similar_command = DatabaseTrackInfo.Provider.CreateFetchCommand (
                "CoreTracks.PrimarySourceID = ? AND CoreTracks.LastSyncedStamp < ? AND CoreTracks.MetadataHash = ?");
        }

        protected override string ProcessItem (string file_path)
        {
            if (!DatabaseImportManager.IsWhiteListedFile (file_path)) {
                return null;
            }

            // Hack to ignore Podcast files
            if (file_path.Contains ("Podcasts")) {
                return null;
            }

            //Hyena.Log.DebugFormat ("Rescanning item {0}", file_path);
            try {
                SafeUri uri = new SafeUri (file_path);

                using (var reader = ServiceManager.DbConnection.Query (fetch_command, psource.DbId, uri.AbsoluteUri)) {
                    if (reader.Read () ) {
                        //Hyena.Log.DebugFormat ("Found it in the db!");
                        DatabaseTrackInfo track = DatabaseTrackInfo.Provider.Load (reader);

                        MergeIfModified (track);

                        // Either way, update the LastSyncStamp
                        track.LastSyncedStamp = DateTime.Now;
                        track.Save (false);
                        status = String.Format ("{0} - {1}", track.DisplayArtistName, track.DisplayTrackTitle);
                    } else {
                        // This URI is not in the database - try to find it based on MetadataHash in case it was simply moved
                        DatabaseTrackInfo track = new DatabaseTrackInfo ();
                        Banshee.Streaming.StreamTagger.TrackInfoMerge (track, uri);

                        using (var similar_reader = ServiceManager.DbConnection.Query (
                            fetch_similar_command, psource.DbId, scan_started, track.MetadataHash)) {
                            DatabaseTrackInfo similar_track = null;
                            while (similar_reader.Read ()) {
                                similar_track = DatabaseTrackInfo.Provider.Load (similar_reader);
                                if (!Banshee.IO.File.Exists (similar_track.Uri)) {
                                    //Hyena.Log.DebugFormat ("Apparently {0} was moved to {1}", similar_track.Uri, file_path);
                                    similar_track.Uri = uri;
                                    MergeIfModified (similar_track);
                                    similar_track.LastSyncedStamp = DateTime.Now;
                                    similar_track.Save (false);
                                    status = String.Format ("{0} - {1}", similar_track.DisplayArtistName, similar_track.DisplayTrackTitle);
                                    break;
                                }
                                similar_track = null;
                            }

                            // If we still couldn't find it, try to import it
                            if (similar_track == null) {
                                //Hyena.Log.DebugFormat ("Couldn't find it, so queueing to import it");
                                status = System.IO.Path.GetFileNameWithoutExtension (file_path);
                                ServiceManager.Get<Banshee.Library.LibraryImportManager> ().ImportTrack (uri);
                            }
                        }
                    }
                }
            } catch (Exception e) {
                Hyena.Log.Error (e);
            }
            return null;
        }

        private void MergeIfModified (TrackInfo track)
        {
            long mtime = Banshee.IO.File.GetModifiedTime (track.Uri);

            // If the file was modified since we last scanned, parse the file's metadata
            if (mtime > track.FileModifiedStamp) {
                using (var file = Banshee.Streaming.StreamTagger.ProcessUri (track.Uri)) {
                    Banshee.Streaming.StreamTagger.TrackInfoMerge (track, file, false,
                        SaveTrackMetadataService.WriteRatingsEnabled.Value,
                        SaveTrackMetadataService.WritePlayCountsEnabled.Value);
                }
            }
        }
    }
}
