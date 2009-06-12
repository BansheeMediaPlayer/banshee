//
// IpodSource.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
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
using System.IO;
using System.Threading;
using System.Collections.Generic;
using Mono.Unix;

using IPod;

using Hyena;
using Banshee.Base;
using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Dap;
using Banshee.Hardware;
using Banshee.Collection.Database;
using Banshee.Library;
using Banshee.Playlist;

using Banshee.Dap.Gui;

namespace Banshee.Dap.Ipod
{
    public class IpodSource : DapSource
    {
        private PodSleuthDevice ipod_device;
        internal PodSleuthDevice IpodDevice {
            get { return ipod_device; }
        }
        
        private Dictionary<int, IpodTrackInfo> tracks_map = new Dictionary<int, IpodTrackInfo> (); // FIXME: EPIC FAIL
        private bool database_loaded;
        
        private string name_path;
        internal string NamePath {
            get { return name_path; }
        }
        
        private bool database_supported;
        internal bool DatabaseSupported {
            get { return database_supported; }
        }
        
        private UnsupportedDatabaseView unsupported_view;
        
#region Device Setup/Dispose
        
        public override void DeviceInitialize (IDevice device)
        {
            base.DeviceInitialize (device);
            
            ipod_device = device as PodSleuthDevice;
            if (ipod_device == null) {
                throw new InvalidDeviceException ();
            }

            name_path = Path.Combine (Path.GetDirectoryName (ipod_device.TrackDatabasePath), "BansheeIPodName");
            Name = GetDeviceName ();

            SupportsPlaylists = ipod_device.ModelInfo.DeviceClass != "shuffle";

            // TODO disable this later, but right now it won't disable it in Sync, so might as well
            // leave it enabled
            //SupportsPodcasts = ipod_device.ModelInfo.HasCapability ("podcast");
            SupportsVideo = ipod_device.ModelInfo.DeviceClass == "video" ||
                            ipod_device.ModelInfo.DeviceClass == "classic" ||
                            (ipod_device.ModelInfo.DeviceClass == "nano" && ipod_device.ModelInfo.Generation >= 3);

            Initialize ();

            AddDapProperty (Catalog.GetString ("Device"), ipod_device.ModelInfo.DeviceClass);
            AddDapProperty (Catalog.GetString ("Color"), ipod_device.ModelInfo.ShellColor);
            AddDapProperty (Catalog.GetString ("Generation"), ipod_device.ModelInfo.Generation.ToString ());
            AddDapProperty (Catalog.GetString ("Capacity"), ipod_device.ModelInfo.AdvertisedCapacity);
            AddDapProperty (Catalog.GetString ("Serial number"), ipod_device.Serial);
            AddDapProperty (Catalog.GetString ("Produced on"), ipod_device.ProductionInfo.DisplayDate);
            AddDapProperty (Catalog.GetString ("Firmware"), ipod_device.FirmwareVersion);

            string [] capabilities = new string [ipod_device.ModelInfo.Capabilities.Count];
            ipod_device.ModelInfo.Capabilities.CopyTo (capabilities, 0);
            AddDapProperty (Catalog.GetString ("Capabilities"), String.Join (", ", capabilities));
            AddYesNoDapProperty (Catalog.GetString ("Supports cover art"), ipod_device.ModelInfo.AlbumArtSupported);
            AddYesNoDapProperty (Catalog.GetString ("Supports photos"), ipod_device.ModelInfo.PhotosSupported);
        }

        public override void Dispose ()
        {
            ThreadAssist.ProxyToMain (DestroyUnsupportedView);
            CancelSyncThread ();
            base.Dispose ();
        }

        // WARNING: This will be called from a thread!
        protected override void Eject ()
        {
            base.Eject ();
            CancelSyncThread ();
            if (ipod_device.CanUnmount) {
                ipod_device.Unmount ();
            }

            if (ipod_device.CanEject) {
                ipod_device.Eject ();
            }
            
            Dispose ();
        }
        
        protected override bool CanHandleDeviceCommand (DeviceCommand command)
        {
            try {
                SafeUri uri = new SafeUri (command.DeviceId);
                return IpodDevice.MountPoint.StartsWith (uri.LocalPath);
            } catch {
                return false;
            }
        }
        
        protected override IDeviceMediaCapabilities MediaCapabilities {
            get { return ipod_device.Parent.MediaCapabilities ?? base.MediaCapabilities; }
        }
        
#endregion

#region Database Loading

        // WARNING: This will be called from a thread!
        protected override void LoadFromDevice ()
        {
            LoadIpod ();
            LoadFromDevice (false);
            OnTracksAdded ();
        }

        private void LoadIpod ()
        {
            database_supported = false;
            
            try {
                if (File.Exists (ipod_device.TrackDatabasePath)) { 
                    ipod_device.LoadTrackDatabase (false);
                } else {
                    int count = CountMusicFiles ();
                    Log.DebugFormat ("Found {0} files in /iPod_Control/Music", count);
                    if (CountMusicFiles () > 5) {
                        throw new DatabaseReadException ("No database, but found a lot of music files");
                    }
                }
                database_supported = true;
                ThreadAssist.ProxyToMain (DestroyUnsupportedView);
            } catch (DatabaseReadException e) {
                Log.Exception ("Could not read iPod database", e);
                ipod_device.LoadTrackDatabase (true);
                
                ThreadAssist.ProxyToMain (delegate {
                    DestroyUnsupportedView ();
                    unsupported_view = new UnsupportedDatabaseView (this);
                    unsupported_view.Refresh += OnRebuildDatabaseRefresh;
                    Properties.Set<Banshee.Sources.Gui.ISourceContents> ("Nereid.SourceContents", unsupported_view);
                });
            } catch (Exception e) {
                Log.Exception (e);
            }
            
            database_loaded = true;

            Name = GetDeviceName ();
        }
        
        private int CountMusicFiles ()
        {
            try {
                int file_count = 0;
                
                DirectoryInfo m_dir = new DirectoryInfo (Path.Combine (ipod_device.ControlPath, "Music"));
                foreach (DirectoryInfo f_dir in m_dir.GetDirectories ()) {
                    file_count += f_dir.GetFiles().Length;
                }
                
                return file_count;
            } catch {
                return 0;
            }
        }
        
        private void LoadFromDevice (bool refresh)
        {
            // bool previous_database_supported = database_supported;
            
            if (refresh) {
                ipod_device.TrackDatabase.Reload ();
            }
            
            tracks_map.Clear ();

            if (database_supported || (ipod_device.HasTrackDatabase && 
                ipod_device.ModelInfo.DeviceClass == "shuffle")) {
                foreach (Track ipod_track in ipod_device.TrackDatabase.Tracks) {
                    try {
                        IpodTrackInfo track = new IpodTrackInfo (ipod_track);
                        track.PrimarySource = this;
                        track.Save (false);
                        tracks_map.Add (track.TrackId, track);
                    } catch (Exception e) {
                        Log.Exception (e);
                    }
                }

                Hyena.Data.Sqlite.HyenaSqliteCommand insert_cmd = new Hyena.Data.Sqlite.HyenaSqliteCommand (
                    @"INSERT INTO CorePlaylistEntries (PlaylistID, TrackID)
                        SELECT ?, TrackID FROM CoreTracks WHERE PrimarySourceID = ? AND ExternalID = ?");
                foreach (IPod.Playlist playlist in ipod_device.TrackDatabase.Playlists) {
                    if (playlist.IsOnTheGo) { // || playlist.IsPodcast) {
                        Console.WriteLine ("have playlist {0} with {1} items but ignoring b/c otg or podcast", playlist.Name, playlist.Tracks.Count);
                        continue;
                    }
                    PlaylistSource pl_src = new PlaylistSource (playlist.Name, this);
                    pl_src.Save ();
                    // We use the IPod.Track.Id here b/c we just shoved it into ExternalID above when we loaded
                    // the tracks, however when we sync, the Track.Id values may/will change.
                    foreach (IPod.Track track in playlist.Tracks) {
                        ServiceManager.DbConnection.Execute (insert_cmd, pl_src.DbId, this.DbId, track.Id);
                    }
                    pl_src.UpdateCounts ();
                    AddChildSource (pl_src);
                }
            }
            
            /*else {
                BuildDatabaseUnsupportedWidget ();
            }*/
            
            /*if(previous_database_supported != database_supported) {
                OnPropertiesChanged();
            }*/
        }
        
        private void OnRebuildDatabaseRefresh (object o, EventArgs args)
        {
            ServiceManager.SourceManager.SetActiveSource (MusicGroupSource);
            base.LoadDeviceContents ();
        }
        
        private void DestroyUnsupportedView ()
        {
            if (unsupported_view != null) {
                unsupported_view.Refresh -= OnRebuildDatabaseRefresh;
                unsupported_view.Destroy ();
                unsupported_view = null;
            }
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
            string shell_color = ipod_device.ModelInfo.ShellColor;
            
            names[0] = ipod_device.ModelInfo.IconName;
            names[2] = "ipod-standard-color";
            names[3] = "multimedia-player";
            
            switch (ipod_device.ModelInfo.DeviceClass) {
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
                if (name_path != null) {
                    Directory.CreateDirectory (Path.GetDirectoryName (name_path));
                
                    using (StreamWriter writer = new StreamWriter (File.Open (name_path, FileMode.Create), 
                        System.Text.Encoding.Unicode)) {
                        writer.Write (name);
                    }
                }
            } catch (Exception e) {
                Log.Exception (e);
            }
            
            ipod_device.Name = name;
            base.Rename (name);
        }
        
        private string GetDeviceName ()
        {
            string name = null;
            if (File.Exists (name_path)) {
                using (StreamReader reader = new StreamReader (name_path, System.Text.Encoding.Unicode)) {
                    name = reader.ReadLine ();
                }
            }
            
            if (String.IsNullOrEmpty (name) && database_loaded && database_supported) {
                name = ipod_device.Name;
            }
                
            if (!String.IsNullOrEmpty (name)) {
                return name;
            } else if (ipod_device.PropertyExists ("volume.label")) {
                name = ipod_device.GetPropertyString ("volume.label");
            } else if (ipod_device.PropertyExists ("info.product")) {
                name = ipod_device.GetPropertyString ("info.product");
            } else {
                name = ((IDevice)ipod_device).Name ?? "iPod";
            }
            
            return name;
        }
        
        public override bool CanRename {
            get { return !(IsAdding || IsDeleting || IsReadOnly); }
        }
        
        public override long BytesUsed {
            get { return (long)ipod_device.VolumeInfo.SpaceUsed; }
        }
        
        public override long BytesCapacity {
            get { return (long)ipod_device.VolumeInfo.Size; }
        }
        
#endregion

#region Syncing

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
        
        private Queue<IpodTrackInfo> tracks_to_add = new Queue<IpodTrackInfo> ();
        private Queue<IpodTrackInfo> tracks_to_remove = new Queue<IpodTrackInfo> ();
        
        private uint sync_timeout_id = 0;
        private object sync_timeout_mutex = new object ();
        private object sync_mutex = new object ();
        private Thread sync_thread;
        private AutoResetEvent sync_thread_wait;
        private bool sync_thread_dispose = false;

        public override bool IsReadOnly {
            get { return ipod_device.IsReadOnly || !database_supported; }
        }
        
        public override void Import ()
        {
            Banshee.ServiceStack.ServiceManager.Get<LibraryImportManager> ().Enqueue (Path.Combine (ipod_device.ControlPath, "Music"));
        }

        /*public override void CopyTrackTo (DatabaseTrackInfo track, SafeUri uri, BatchUserJob job)
        {
            throw new Exception ("Copy to Library is not implemented for iPods yet");
        }*/

        protected override bool DeleteTrack (DatabaseTrackInfo track)
        {
            lock (sync_mutex) {
                if (!tracks_map.ContainsKey (track.TrackId)) {
                    return true;
                }
                
                IpodTrackInfo ipod_track = tracks_map[track.TrackId];
                if (ipod_track != null) {
                    tracks_to_remove.Enqueue (ipod_track);
                }
                
                return true;
            }
        }
        
        protected override void AddTrackToDevice (DatabaseTrackInfo track, SafeUri fromUri)
        {
            lock (sync_mutex) {
                if (track.PrimarySourceId == DbId) {
                    return;
                }
                
                IpodTrackInfo ipod_track = new IpodTrackInfo (track);
                ipod_track.Uri = fromUri;
                ipod_track.PrimarySource = this;
                ipod_track.Save (false);
            
                tracks_to_add.Enqueue (ipod_track);
            }
        }

        public override void SyncPlaylists ()
        {
            if (!IsReadOnly && Monitor.TryEnter (sync_mutex)) {
                PerformSync ();
                Monitor.Exit (sync_mutex);
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
        
        private void PerformSyncThreadCycle ()
        {
            while (tracks_to_add.Count > 0) {
                IpodTrackInfo track = null;
                lock (sync_mutex) {
                    track = tracks_to_add.Dequeue ();
                }
                
                try {
                    track.CommitToIpod (ipod_device);
                    tracks_map[track.TrackId] = track;
                } catch (Exception e) {
                    Log.Exception ("Cannot save track to iPod", e);
                }
            }

            // TODO sync updated metadata to changed tracks
            
            while (tracks_to_remove.Count > 0) {
                IpodTrackInfo track = null;
                lock (sync_mutex) {
                    track = tracks_to_remove.Dequeue ();
                }
                
                if (tracks_map.ContainsKey (track.TrackId)) {
                    tracks_map.Remove (track.TrackId);
                }
                
                try {
                    if (track.IpodTrack != null) {
                        ipod_device.TrackDatabase.RemoveTrack (track.IpodTrack);
                    }
                } catch (Exception e) {
                    Log.Exception ("Cannot remove track from iPod", e);
                }
            }

            // Remove playlists on the device
            List<IPod.Playlist> device_playlists = new List<IPod.Playlist> (ipod_device.TrackDatabase.Playlists);
            foreach (IPod.Playlist playlist in device_playlists) {
                if (!playlist.IsOnTheGo) { // && !playlist.IsPodcast) {
                    ipod_device.TrackDatabase.RemovePlaylist (playlist);
                }
            }
            device_playlists.Clear ();
            
            if (SupportsPlaylists) {
                // Add playlists from Banshee to the device
                foreach (Source child in Children) {
                    PlaylistSource from = child as PlaylistSource;
                    if (from != null && from.Count > 0) {
                        IPod.Playlist playlist = ipod_device.TrackDatabase.CreatePlaylist (from.Name);
                        foreach (int track_id in ServiceManager.DbConnection.QueryEnumerable<int> (String.Format (
                            "SELECT CoreTracks.TrackID FROM {0} WHERE {1}",
                            from.DatabaseTrackModel.ConditionFromFragment, from.DatabaseTrackModel.Condition)))
                        {
                            playlist.AddTrack (tracks_map[track_id].IpodTrack);
                        }
                    }
                }
            }
    
            // Sync podcast playlist
            /*IPod.Playlist podcast_playlist = GetPodcastPlaylist ();
            podcast_playlist.Clear ();
            foreach (int track_id in ServiceManager.DbConnection.QueryEnumerable<int> (
                "SELECT CoreTracks.TrackID FROM CoreTracks WHERE PrimarySourceID = ? AND (Attributes & ?) != 0",
                DbId, (int)Banshee.Collection.TrackMediaAttributes.Podcast))
            {
                podcast_playlist.AddTrack (tracks_map[track_id].IpodTrack);
            }*/
            
            try {
                ipod_device.TrackDatabase.SaveStarted += OnIpodDatabaseSaveStarted;
                ipod_device.TrackDatabase.SaveEnded += OnIpodDatabaseSaveEnded;
                ipod_device.TrackDatabase.SaveProgressChanged += OnIpodDatabaseSaveProgressChanged;
                ipod_device.Save ();
            } catch (InsufficientSpaceException) {
                ErrorSource.AddMessage (Catalog.GetString ("Out of space on device"), Catalog.GetString ("Please manually remove some songs"));
            } catch (Exception e) {
                Log.Exception ("Failed to save iPod database", e);
            } finally {
                ipod_device.TrackDatabase.SaveStarted -= OnIpodDatabaseSaveStarted;
                ipod_device.TrackDatabase.SaveEnded -= OnIpodDatabaseSaveEnded;
                ipod_device.TrackDatabase.SaveProgressChanged -= OnIpodDatabaseSaveProgressChanged;
            }
        }

        /*private IPod.Playlist GetPodcastPlaylist ()
        {
            foreach (IPod.Playlist playlist in ipod_device.TrackDatabase.Playlists) {
                if (playlist.IsPodcast) {
                    return playlist;
                }
            }

            IPod.Playlist podcast_playlist= ipod_device.TrackDatabase.CreatePlaylist (Catalog.GetString ("Podcasts"));
            podcast_playlist.IsPodcast = true;
            return podcast_playlist;
        }*/
        
        private UserJob sync_user_job;
        
        private void OnIpodDatabaseSaveStarted (object o, EventArgs args)
        {
            DisposeSyncUserJob ();
            
            sync_user_job = new UserJob (Catalog.GetString ("Syncing iPod"), 
                Catalog.GetString ("Preparing to synchronize..."), GetIconNames ());
            sync_user_job.Register ();
        }
        
        private void OnIpodDatabaseSaveEnded (object o, EventArgs args)
        {
            DisposeSyncUserJob ();
        }
        
        private void DisposeSyncUserJob ()
        {
            if (sync_user_job != null) {
                sync_user_job.Finish ();
                sync_user_job = null;
            }
        }
        
        private void OnIpodDatabaseSaveProgressChanged (object o, IPod.TrackSaveProgressArgs args)
        {
            double progress = args.CurrentTrack == null ? 0.0 : args.TotalProgress;
            string message = args.CurrentTrack == null 
                    ? Catalog.GetString ("Updating...")
                    : String.Format ("{0} - {1}", args.CurrentTrack.Artist, args.CurrentTrack.Title);
             
             if (progress >= 0.99) {
                 sync_user_job.Status = Catalog.GetString ("Flushing to disk...");
                 sync_user_job.Progress = 0;
             } else {
                 sync_user_job.Status = message;
                 sync_user_job.Progress = progress;
             }
        }
        
        public bool SyncNeeded {
            get {
                lock (sync_mutex) {
                    return tracks_to_add.Count > 0 || tracks_to_remove.Count > 0;
                }
            }
        }

#endregion
        
    }
}
