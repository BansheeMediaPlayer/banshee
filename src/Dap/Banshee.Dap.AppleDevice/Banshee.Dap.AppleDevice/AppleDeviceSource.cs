//
// AppleDeviceSource.cs
//
// Author:
//   Alan McGovern <amcgovern@novell.com>
//   Phil Trimble <philtrimble@gmail.com>
//   Andres G. Aragoneses <knocte@gmail.com>
//
// Copyright (C) 2010 Novell, Inc.
// Copyright (C) 2012 Phil Trimble
// Copyright (C) 2012 Andres G. Aragoneses
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
using System.Linq;
using System.Collections.Generic;
using System.Threading;

using Hyena;
using Hyena.Query;

using Banshee.Base;
using Banshee.Collection.Database;
using Banshee.ServiceStack;
using Banshee.Library;
using Banshee.Hardware;
using Banshee.Sources;
using Banshee.I18n;
using Banshee.Playlist;
using Banshee.Collection;

namespace Banshee.Dap.AppleDevice
{
    public class AppleDeviceSource : DapSource, IBatchScrobblerSource
    {
        GPod.Device Device {
            get; set;
        }

        IVolume Volume {
            get; set;
        }

        GPod.ITDB MediaDatabase {
            get; set;
        }

        private Dictionary<int, AppleDeviceTrackInfo> tracks_map = new Dictionary<int, AppleDeviceTrackInfo> (); // FIXME: EPIC FAIL

        public event EventHandler<ScrobblingBatchEventArgs> ReadyToScrobble;

#region Device Setup/Dispose

        public override void DeviceInitialize (IDevice device)
        {
            Volume = device as IVolume;

            if (Volume == null) {
                throw new InvalidDeviceException ();
            }

            if (!Volume.IsMounted && device.MediaCapabilities != null && device.MediaCapabilities.IsType ("ipod")) {
                Hyena.Log.Information ("Found potential unmounted iDevice, trying to mount it now");
                Volume.Mount ();
            }

            if (!Volume.IsMounted) {
                Hyena.Log.Information ("AppleDeviceSource is ignoring unmounted volume " + Volume.Name);
                throw new InvalidDeviceException ();
            }
            
            Device = new GPod.Device (Volume.MountPoint);

            if (GPod.ITDB.GetControlPath (Device) == null) {
                throw new InvalidDeviceException ();
            }

            base.DeviceInitialize (device);

            Name = Volume.Name;
            SupportsPlaylists = true;
            SupportsPodcasts = Device.SupportsPodcast;
            SupportsVideo = Device.SupportsVideo;

            Initialize ();
            GPod.ITDB.InitIpod (Volume.MountPoint, Device.IpodInfo == null ? null : Device.IpodInfo.ModelNumber, Name);

            // HACK: ensure that m4a, and mp3 are set as accepted by the device; bgo#633552
            AcceptableMimeTypes = (AcceptableMimeTypes ?? new string [0]).Union (new string [] { "taglib/m4a", "taglib/mp3" }).ToArray ();

            // FIXME: Properly parse the device, color and generation and don't use the fallback strings

            // IpodInfo is null on Macos formated ipods. I don't think we can really do anything with them
            // but they get loaded as UMS devices if we throw an NRE here.
            if (Device.IpodInfo != null) {
                AddDapProperty (Catalog.GetString ("Device"), Device.IpodInfo.ModelString);
                AddDapProperty (Catalog.GetString ("Generation"), Device.IpodInfo.GenerationString);
            }

            // FIXME
            //AddDapProperty (Catalog.GetString ("Color"), "black");
            AddDapProperty (Catalog.GetString ("Capacity"), string.Format ("{0:0.00}GB", BytesCapacity / 1024.0 / 1024.0 / 1024.0));
            AddDapProperty (Catalog.GetString ("Available"), string.Format ("{0:0.00}GB", BytesAvailable / 1024.0 / 1024.0 / 1024.0));
            AddDapProperty (Catalog.GetString ("Serial number"), Volume.Serial);
            //AddDapProperty (Catalog.GetString ("Produced on"), ipod_device.ProductionInfo.DisplayDate);
            //AddDapProperty (Catalog.GetString ("Firmware"), ipod_device.FirmwareVersion);

            //string [] capabilities = new string [ipod_device.ModelInfo.Capabilities.Count];
            //ipod_device.ModelInfo.Capabilities.CopyTo (capabilities, 0);
            //AddDapProperty (Catalog.GetString ("Capabilities"), String.Join (", ", capabilities));
            AddYesNoDapProperty (Catalog.GetString ("Supports cover art"), Device.SupportsArtwork);
            AddYesNoDapProperty (Catalog.GetString ("Supports photos"), Device.SupportsPhoto);
        }

        public override void Dispose ()
        {
            //ThreadAssist.ProxyToMain (DestroyUnsupportedView);
            CancelSyncThread ();
            base.Dispose ();
        }

        // WARNING: This will be called from a thread!
        protected override void Eject ()
        {
            base.Eject ();
            CancelSyncThread ();
            if (Volume.CanUnmount)
                Volume.Unmount ();
            if (Volume.CanEject)
                Volume.Eject ();

            Dispose ();
        }

        protected override bool CanHandleDeviceCommand (DeviceCommand command)
        {
            // Whats this for?
            return false;
//            try {
//                SafeUri uri = new SafeUri (command.DeviceId);
//                return IpodDevice.MountPoint.StartsWith (uri.LocalPath);
//            } catch {
//                return false;
//            }
        }

#endregion

#region Database Loading

        // WARNING: This will be called from a thread!
        protected override void LoadFromDevice ()
        {
            LoadFromDevice (false);
            OnTracksAdded ();
        }

        private void LoadFromDevice (bool refresh)
        {
            tracks_map.Clear ();
            if (refresh || MediaDatabase  == null) {
                if (MediaDatabase != null)
                    MediaDatabase.Dispose ();

                try {
                    MediaDatabase = new GPod.ITDB (Device.Mountpoint);
                } catch (GLib.GException e) {
                    Log.Exception ("iPod database could not be loaded, creating a new one", e);
                    if (GPod.ITDB.InitIpod (Volume.MountPoint, null, Volume.Name)) {
                        // this may throw again. In the future we need to implement some kind of alert
                        // mechanism to let the user know that something more serious is wrong with their
                        // apple device a la the other iPod extension.
                        MediaDatabase = new GPod.ITDB (Device.Mountpoint);
                    } else {
                        Log.Error ("Failed to init iPod database");
                        return;
                    }
                }
            }

            if (MediaDatabase.MasterPlaylist == null) {
                MediaDatabase.Playlists.Add (new GPod.Playlist (Name) {
                    IsMaster = true
                });
            }

            if (SupportsPodcasts && MediaDatabase.PodcastsPlaylist == null) {
                MediaDatabase.Playlists.Add (new GPod.Playlist (Catalog.GetString ("Podcasts")) {
                    IsPodcast = true
                });
            }

            foreach (var ipod_track in MediaDatabase.Tracks) {

                if (String.IsNullOrEmpty (ipod_track.IpodPath)) {
                    invalid_tracks_in_device.Enqueue (ipod_track);
                    continue;
                }

                try {
                    var track = new AppleDeviceTrackInfo (ipod_track);
                    if (!tracks_map.ContainsKey (track.TrackId)) {
                        track.PrimarySource = this;
                        track.Save (false);
                        tracks_map.Add (track.TrackId, track);
                    }
                } catch (Exception e) {
                    Log.Exception (e);
                }
            }

            if (invalid_tracks_in_device.Count > 0) {
                Log.Warning (String.Format ("Found {0} invalid tracks on the device", invalid_tracks_in_device.Count));
            }

            Hyena.Data.Sqlite.HyenaSqliteCommand insert_cmd = new Hyena.Data.Sqlite.HyenaSqliteCommand (
                @"INSERT INTO CorePlaylistEntries (PlaylistID, TrackID)
                    SELECT ?, TrackID FROM CoreTracks WHERE PrimarySourceID = ? AND ExternalID = ?");
            foreach (var playlist in MediaDatabase.Playlists) {
                if (playlist.IsMaster || playlist.IsPodcast)
                    continue;

                PlaylistSource pl_src = new PlaylistSource (playlist.Name, this);
                pl_src.Save ();
                // We use the GPod.Track.DBID here b/c we just shoved it into ExternalID above when we loaded
                // the tracks, however when we sync, the Track.DBID values may/will change.
                foreach (var track in playlist.Tracks) {
                    // DBID will be stored in a long, so we need to cast it. See bgo#650011
                    ServiceManager.DbConnection.Execute (insert_cmd, pl_src.DbId, this.DbId, (long) track.DBID);
                }
                pl_src.UpdateCounts ();
                AddChildSource (pl_src);
            }

            RaiseReadyToScrobble ();
        }

#endregion

#region Source Cosmetics

        internal string [] _GetIconNames ()
        {
            return GetIconNames ();
        }

        protected override string [] GetIconNames ()
        {
            string [] names = new string[4];
            string prefix = "multimedia-player-";
            //string shell_color = "green";

            names[0] = "";
            names[2] = "ipod-standard-color";
            names[3] = "multimedia-player";
            /*
            switch ("grayscale") {
                case "grayscale":
                    names[1] = "ipod-standard-monochrome";
                    break;
                case "color":
                    names[1] = "ipod-standard-color";
                    break;
                case "mini":
                    names[1] = String.Format ("ipod-mini-{0}", shell_color);
                    names[2] = "ipod-mini-silver";
                    break;
                case "shuffle":
                    names[1] = String.Format ("ipod-shuffle-{0}", shell_color);
                    names[2] = "ipod-shuffle";
                    break;
                case "nano":
                case "nano3":
                    names[1] = String.Format ("ipod-nano-{0}", shell_color);
                    names[2] = "ipod-nano-white";
                    break;
                case "video":
                    names[1] = String.Format ("ipod-video-{0}", shell_color);
                    names[2] = "ipod-video-white";
                    break;
                case "classic":
                case "touch":
                case "phone":
                default:
                    break;
            }
          */
            names[1] = names[1] ?? names[2];
            names[1] = prefix + names[1];
            names[2] = prefix + names[2];

            return names;
        }

        public override void Rename (string name)
        {
            if (!CanRename) {
                return;
            }

            try {
                MediaDatabase.MasterPlaylist.Name = name;
                base.Rename (name);
            } catch (Exception e) {
                Log.Exception ("Trying to change iPod name", e);
            }
        }

        public override bool CanRename {
            get { return !(IsAdding || IsDeleting || IsReadOnly); }
        }

        public override long BytesUsed {
            get { return (long) Volume.Capacity - Volume.Available; }
        }

        public override long BytesCapacity {
            get { return (long) Volume.Capacity; }
        }

#endregion

#region Syncing

        public override void UpdateMetadata (DatabaseTrackInfo track)
        {
            lock (sync_mutex) {
                AppleDeviceTrackInfo ipod_track;
                if (!tracks_map.TryGetValue (track.TrackId, out ipod_track)) {
                    return;
                }

                ipod_track.UpdateInfo (track);
                tracks_to_update.Enqueue (ipod_track);
            }
        }

        protected override void OnTracksChanged (params QueryField[] fields)
        {
            if (tracks_to_update.Count > 0 && !Sync.Syncing) {
                QueueSync ();
            }
            base.OnTracksChanged (fields);
        }

        protected override void OnTracksAdded ()
        {
            if (!IsAdding && tracks_to_add.Count > 0 && !Sync.Syncing) {
                QueueSync ();
            }
            base.OnTracksAdded ();
        }

        protected override void OnTracksDeleted ()
        {
            if (!IsDeleting && tracks_to_remove.Count > 0 && !Sync.Syncing) {
                QueueSync ();
            }
            base.OnTracksDeleted ();
        }

        private Queue<AppleDeviceTrackInfo> tracks_to_add = new Queue<AppleDeviceTrackInfo> ();
        private Queue<AppleDeviceTrackInfo> tracks_to_update = new Queue<AppleDeviceTrackInfo> ();
        private Queue<AppleDeviceTrackInfo> tracks_to_remove = new Queue<AppleDeviceTrackInfo> ();
        private Queue<GPod.Track> invalid_tracks_in_device = new Queue<GPod.Track> ();

        private uint sync_timeout_id = 0;
        private object sync_timeout_mutex = new object ();
        private object sync_mutex = new object ();
        private object write_mutex = new object ();
        private Thread sync_thread;
        private AutoResetEvent sync_thread_wait;
        private bool sync_thread_dispose = false;

        public override bool AcceptsInputFromSource (Source source)
        {
            return base.AcceptsInputFromSource (source);
        }
        public override bool CanAddTracks {
            get {
                return base.CanAddTracks;
            }
        }
        public override bool IsReadOnly {
            get { return false; }//!database_supported; }
        }

        public override string BaseDirectory {
            get { return Volume.MountPoint; }
        }

        public override void Import ()
        {
            Banshee.ServiceStack.ServiceManager.Get<LibraryImportManager> ().Enqueue (GPod.ITDB.GetMusicPath (Device));
        }

        public override void CopyTrackTo (DatabaseTrackInfo track, SafeUri uri, BatchUserJob job)
        {
            Banshee.IO.File.Copy (track.Uri, uri, false);
        }

        protected override bool DeleteTrack (DatabaseTrackInfo track)
        {
            lock (sync_mutex) {
                if (!tracks_map.ContainsKey (track.TrackId)) {
                    return true;
                }

                var ipod_track = tracks_map[track.TrackId];
                if (ipod_track != null) {
                    tracks_to_remove.Enqueue (ipod_track);
                }

                return true;
            }
        }


        private void DeleteTrack (GPod.Track track, bool removeFile)
        {
            foreach (var playlist in MediaDatabase.Playlists) {
                playlist.Tracks.Remove (track);
            }

            if (SupportsPodcasts &&
                track.MediaType == GPod.MediaType.Podcast &&
                MediaDatabase.PodcastsPlaylist != null) {
                MediaDatabase.PodcastsPlaylist.Tracks.Remove (track);
            }

            MediaDatabase.MasterPlaylist.Tracks.Remove (track);
            MediaDatabase.Tracks.Remove (track);

            if (removeFile) {
                Banshee.IO.File.Delete (new SafeUri (GPod.ITDB.GetLocalPath (Device, track)));
            }
        }

        protected override void AddTrackToDevice (DatabaseTrackInfo track, SafeUri fromUri)
        {
            lock (sync_mutex) {
                if (track.PrimarySourceId == DbId) {
                    return;
                }

                if (track.Duration.Equals (TimeSpan.Zero)) {
                    throw new Exception (Catalog.GetString ("Track duration is zero"));
                }

                AppleDeviceTrackInfo ipod_track = new AppleDeviceTrackInfo (track);
                ipod_track.Uri = fromUri;
                ipod_track.PrimarySource = this;

                tracks_to_add.Enqueue (ipod_track);
            }
        }

        public override void SyncPlaylists ()
        {
            if (!IsReadOnly && Monitor.TryEnter (sync_mutex)) {
                try {
                    PerformSync ();
                } finally {
                    Monitor.Exit (sync_mutex);
                }
            }
        }

        private void QueueSync ()
        {
            lock (sync_timeout_mutex) {
                if (sync_timeout_id > 0) {
                    Application.IdleTimeoutRemove (sync_timeout_id);
                }

                sync_timeout_id = Application.RunTimeout (150, PerformSync);
            }
        }

        private void CancelSyncThread ()
        {
            Thread thread = sync_thread;
            lock (sync_mutex) {
                if (sync_thread != null && sync_thread_wait != null) {
                    sync_thread_dispose = true;
                    sync_thread_wait.Set ();
                }
            }

            if (thread != null) {
                thread.Join ();
            }
        }

        private bool PerformSync ()
        {
            lock (sync_mutex) {
                if (sync_thread == null) {
                    sync_thread_wait = new AutoResetEvent (false);

                    sync_thread = new Thread (new ThreadStart (PerformSyncThread));
                    sync_thread.Name = "iPod Sync Thread";
                    sync_thread.IsBackground = false;
                    sync_thread.Priority = ThreadPriority.Lowest;
                    sync_thread.Start ();
                }

                sync_thread_wait.Set ();

                lock (sync_timeout_mutex) {
                    sync_timeout_id = 0;
                }

                return false;
            }
        }

        private void PerformSyncThread ()
        {
            try {
                while (true) {
                    sync_thread_wait.WaitOne ();
                    if (sync_thread_dispose) {
                        break;
                    }

                    PerformSyncThreadCycle ();
                }

                lock (sync_mutex) {
                    sync_thread_dispose = false;
                    sync_thread_wait.Close ();
                    sync_thread_wait = null;
                    sync_thread = null;
                }
            } catch (Exception e) {
                Log.Exception (e);
            }
        }

        private void UpdateProgress (UserJob job, string message, int completed, int total)
        {
            job.Status = string.Format (message, completed, total);
            job.Progress = completed / (double) total;
        }

        private void PerformSyncThreadCycle ()
        {
            Hyena.Log.Debug ("Starting AppleDevice sync thread cycle");

            var progressUpdater = new UserJob (Catalog.GetString ("Syncing iPod"),
                                               Catalog.GetString ("Preparing to synchronize..."), GetIconNames ());
            progressUpdater.Register ();
            MediaDatabase.StartSync ();

            SyncTracksToAdd (progressUpdater);

            SyncTracksToUpdate ();

            SyncTracksToRemove (progressUpdater);

            SyncTracksToPlaylists ();

            SyncDatabase (progressUpdater);

            MediaDatabase.StopSync ();
            progressUpdater.Finish ();

            Hyena.Log.Debug ("Ending AppleDevice sync thread cycle");
        }

        void SyncTracksToAdd (UserJob progressUpdater)
        {
            string message = Catalog.GetString ("Adding track {0} of {1}");
            int total = tracks_to_add.Count;
            int i = 0;
            while (tracks_to_add.Count > 0) {
                AppleDeviceTrackInfo track = null;
                lock (sync_mutex) {
                    total = tracks_to_add.Count + i;
                    track = tracks_to_add.Dequeue ();
                }

                try {
                    UpdateProgress (progressUpdater, message, ++i, total);
                    track.CommitToIpod (MediaDatabase);
                    track.Save (false);
                    tracks_map[track.TrackId] = track;
                } catch (Exception e) {
                    Log.Exception ("Cannot save track to the Apple device", e);
                }
            }
            if (total > 0) {
                OnTracksAdded ();
                OnUserNotifyUpdated ();
            }
        }

        void SyncTracksToUpdate ()
        {
            while (tracks_to_update.Count > 0) {
                AppleDeviceTrackInfo track = null;
                lock (sync_mutex) {
                    track = tracks_to_update.Dequeue ();
                }

                try {
                    track.CommitToIpod (MediaDatabase);
                } catch (Exception e) {
                    Log.Exception ("Cannot save track to iPod", e);
                }
            }
        }

        void SyncTracksToRemove (UserJob progressUpdater)
        {
            string message = Catalog.GetString ("Removing track {0} of {1}");
            int total = tracks_to_remove.Count;
            while (tracks_to_remove.Count > 0) {
                AppleDeviceTrackInfo track = null;
                lock (sync_mutex) {
                    track = tracks_to_remove.Dequeue ();
                }

                if (tracks_map.ContainsKey (track.TrackId)) {
                    tracks_map.Remove (track.TrackId);
                }

                try {
                    if (track.IpodTrack != null) {
                        UpdateProgress (progressUpdater, message, total - tracks_to_remove.Count, total);

                        DeleteTrack (track.IpodTrack, true);
                    } else {
                        Log.Error ("The ipod track was null");
                    }
                } catch (Exception e) {
                    Log.Exception ("Cannot remove track from iPod", e);
                }
            }

            SyncRemovalOfInvalidTracks ();
        }

        void SyncRemovalOfInvalidTracks ()
        {
            while (invalid_tracks_in_device.Count > 0) {
                try {
                    DeleteTrack (invalid_tracks_in_device.Dequeue (), false);
                } catch (Exception e) {
                    Log.Exception ("Cannot remove invalid track from iPod", e);
                }
            }
        }

        void SyncTracksToPlaylists ()
        {
            if (!SupportsPlaylists) {
                return;
            }

            // Remove playlists on the device
            var device_playlists = new List<GPod.Playlist> (MediaDatabase.Playlists);
            foreach (var playlist in device_playlists) {
                if (!playlist.IsMaster && !playlist.IsPodcast) {
                    MediaDatabase.Playlists.Remove (playlist);
                }
            }

            // Add playlists from Banshee to the device
            foreach (Source child in Children) {
                PlaylistSource from = child as PlaylistSource;
                if (from != null && from.Count > 0) {
                    var playlist = new GPod.Playlist (from.Name);
                    MediaDatabase.Playlists.Add (playlist);
                    foreach (int track_id in ServiceManager.DbConnection.QueryEnumerable<int> (String.Format (
                        "SELECT CoreTracks.TrackID FROM {0} WHERE {1}",
                        from.DatabaseTrackModel.ConditionFromFragment, from.DatabaseTrackModel.Condition)))
                    {
                        if (tracks_map.ContainsKey (track_id)) {
                            playlist.Tracks.Add (tracks_map[track_id].IpodTrack);
                        }
                    }
                }
            }
        }

        void SyncDatabase (UserJob progressUpdater)
        {
            try {
                string message = Catalog.GetString ("Writing media database");
                UpdateProgress (progressUpdater, message, 1, 1);

                lock (write_mutex) {
                    MediaDatabase.Write ();
                }

                Log.Information ("Wrote iPod database");
            } catch (Exception e) {
                Log.Exception ("Failed to save iPod database", e);
            }
        }

        public bool SyncNeeded {
            get {
                lock (sync_mutex) {
                    return tracks_to_add.Count > 0 ||
                        tracks_to_update.Count > 0 ||
                        tracks_to_remove.Count > 0;
                }
            }
        }

        public override bool HasEditableTrackProperties {
            get {
                // we want child sources to be able to edit metadata and the
                // savetrackmetadataservice to take in account this source
                return true;
            }
        }

#endregion

#region Scrobbling

        private void RaiseReadyToScrobble ()
        {
            var handler = ReadyToScrobble;
            if (handler != null) {
                var recent_plays = new ScrobblingBatchEventArgs {
                    ScrobblingBatch = GatherRecentPlayInfo ()
                };
                if (recent_plays.ScrobblingBatch.Count != 0) {
                    handler (this, recent_plays);

                    // We must perform a write to clear out the recent playcount information so we do not
                    // submit duplicate plays on subsequent invocations.
                    lock (write_mutex) {
                        MediaDatabase.Write ();
                    }
                }
            }
        }

        private IDictionary<TrackInfo, IList<DateTime>> GatherRecentPlayInfo ()
        {
            var recent_plays = new Dictionary <TrackInfo, IList<DateTime>> ();

            foreach (var ipod_track in MediaDatabase.Tracks) {

                if (String.IsNullOrEmpty (ipod_track.IpodPath) || ipod_track.RecentPlayCount == 0) {
                    continue;
                }

                IList<DateTime> playtimes = GenerateFakePlaytimes (ipod_track);

                recent_plays [new AppleDeviceTrackInfo (ipod_track)] = playtimes;
            }

            return recent_plays;
        }

        // Apple products do not save DateTime info for each track play, only a total
        // sum of number of plays (playcount) of each track.
        private IList<DateTime> GenerateFakePlaytimes (GPod.Track track)
        {
            IList<DateTime> playtimes = new List<DateTime> ();

            //FIXME: avoid sequences of overlapping playtimes?
            DateTime current_playtime = track.TimePlayed;
            for (int i = 0; i < track.RecentPlayCount; i++) {
                playtimes.Add (current_playtime);
                current_playtime -= TimeSpan.FromMilliseconds (track.TrackLength);
            }

            return playtimes;
        }

#endregion

    }
}
