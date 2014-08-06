//
// MtpSource.cs
//
// Author:
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
using System.Collections.Generic;
using System.Threading;
using Mono.Unix;

using Hyena;
using Mtp;
using MTP = Mtp;

using Banshee.Dap;
using Banshee.Collection.Gui;
using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Playlist;
using Banshee.Configuration;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Hardware;

namespace Banshee.Dap.Mtp
{
    public class MtpSource : DapSource
    {
        protected override object InternalLock {
            get { return mtp_device; }
        }
        private MtpDevice mtp_device;

        //private bool supports_jpegs = false;
        private Dictionary<long, Track> track_map;

        private Dictionary<string, Album> album_cache = new Dictionary<string, Album> ();

        private bool supports_jpegs = false;
        private bool can_sync_albumart = NeverSyncAlbumArtSchema.Get () == false;
        private int thumb_width = AlbumArtWidthSchema.Get ();

        public override void DeviceInitialize (IDevice device, bool force)
        {
            base.DeviceInitialize (device, force);

            var portInfo = device.ResolveUsbPortInfo ();
            if (portInfo == null || portInfo.DeviceNumber == 0) {
                throw new InvalidDeviceException ();
            }

            //int busnum = portInfo.BusNumber;
            int devnum = portInfo.DeviceNumber;

            List<RawMtpDevice> devices = null;
            try {
                devices = MtpDevice.Detect ();
            } catch (TypeInitializationException e) {
                Log.Error (e); // even if this is not a generic-catch block, e.InnerException should probably be checked here
                Log.Error (
                    Catalog.GetString ("Error Initializing MTP Device Support"),
                    Catalog.GetString ("There was an error initializing MTP device support."), true
                );
                throw new InvalidDeviceException ();
            } catch (Exception e) {
                Log.Error (e);
                //ShowGeneralExceptionDialog (e);
                throw new InvalidDeviceException ();
            }

            IVolume volume = device as IVolume;
            foreach (var v in devices) {
                // Using the HAL hardware backend, HAL says the busnum is 2, but libmtp says it's 0, so disabling that check
                //if (v.BusNumber == busnum && v.DeviceNumber == devnum) {
                if (v.DeviceNumber == devnum) {
                    // If gvfs-gphoto has it mounted, unmount it
                    if (volume != null && volume.IsMounted && force) {
                        Log.DebugFormat ("MtpSource: attempting to unmount {0}", volume.Name);
                        volume.Unmount ();
                    }

                    if (volume != null && volume.IsMounted) {
                        throw new InvalidDeviceStateException ();
                    }

                    mtp_device = MtpDevice.Connect (v);

                    if (mtp_device == null) {
                        Log.DebugFormat ("Failed to connect to mtp device {0}", device.Name);
                        throw new InvalidDeviceStateException ();
                    }
                }
            }

            if (mtp_device == null) {
                throw new InvalidDeviceException ();
            }

            // libmtp sometimes returns '?????'. I assume this is if the device does
            // not supply a friendly name. In this case show the model name.
            if (string.IsNullOrEmpty (mtp_device.Name) || mtp_device.Name == "?????")
                Name = mtp_device.ModelName;
            else
                Name = mtp_device.Name;

            Initialize ();

            List<string> mimetypes = new List<string> ();
            foreach (FileType format in mtp_device.GetFileTypes ()) {
                if (format == FileType.JPEG) {
                    supports_jpegs = true;
                } else {
                    string mimetype = MtpDevice.GetMimeTypeFor (format);
                    if (mimetype != null) {
                        mimetypes.Add (mimetype);
                    }
                }
            }
            AcceptableMimeTypes = mimetypes.ToArray ();

            AddDapProperty (Catalog.GetString ("Serial number"), mtp_device.SerialNumber);
            AddDapProperty (Catalog.GetString ("Version"), mtp_device.Version);
            try {
                AddDapProperty (Catalog.GetString ("Battery level"), String.Format ("{0:0%}", mtp_device.BatteryLevel/100.0));
            } catch (LibMtpException e) {
                Log.Warning ("Unable to get battery level from MTP device", e);
            }
        }

        protected override void LoadFromDevice ()
        {
            track_map = new Dictionary<long, Track> ();
            try {
                List<Track> files = null;
                lock (mtp_device) {
                    files = mtp_device.GetAllTracks (delegate (ulong current, ulong total, IntPtr data) {
                        //user_event.Progress = (double)current / total;
                        // Translators: {0} is the name of the MTP audio device (eg Gabe's Zen Player), {1} is the
                        // track currently being loaded, and {2} is the total # of tracks that will be loaded.
                        SetStatus (String.Format (Catalog.GetString ("Loading {0} - {1} of {2}"), Name, current, total), false);
                        return 0;
                    });
                }

                /*if (user_event.IsCancelRequested) {
                    return;
                }*/

                // Delete any empty albums
                lock (mtp_device) {
                    foreach (Album album in mtp_device.GetAlbums ()) {
                        if (album.Count == 0) {
                            album.Remove ();
                        }
                    }
                }

                foreach (Track mtp_track in files) {
                    long track_id;
                    if ((track_id = DatabaseTrackInfo.GetTrackIdForUri (MtpTrackInfo.GetPathFromMtpTrack (mtp_track), DbId )) > 0) {
                        track_map[track_id] = mtp_track;
                    } else {
                        MtpTrackInfo track = new MtpTrackInfo (mtp_device, mtp_track);
                        track.PrimarySource = this;
                        track.Save (false);
                        track_map[track.TrackId] = mtp_track;
                    }
                }

                Hyena.Data.Sqlite.HyenaSqliteCommand insert_cmd = new Hyena.Data.Sqlite.HyenaSqliteCommand (
                    @"INSERT INTO CorePlaylistEntries (PlaylistID, TrackID)
                        SELECT ?, TrackID FROM CoreTracks WHERE PrimarySourceID = ? AND ExternalID = ?");

                lock (mtp_device) {
                    var playlists = mtp_device.GetPlaylists ();
                    if (playlists != null) {
                        foreach (MTP.Playlist playlist in playlists) {
                            PlaylistSource pl_src = new PlaylistSource (playlist.Name, this);
                            pl_src.Save ();
                            // TODO a transaction would make sense here (when the threading issue is fixed)
                            foreach (uint id in playlist.TrackIds) {
                                ServiceManager.DbConnection.Execute (insert_cmd, pl_src.DbId, this.DbId, id);
                            }
                            pl_src.UpdateCounts ();
                            AddChildSource (pl_src);
                        }
                    }
                }

            } catch (Exception e) {
                Log.Error (e);
            }
            OnTracksAdded ();
        }

        public override void Import ()
        {
            Log.Information ("Import to Library is not implemented for MTP devices yet", true);
            //new LibraryImportManager (true).QueueSource (BaseDirectory);
        }

        public override void CopyTrackTo (DatabaseTrackInfo track, SafeUri uri, BatchUserJob job)
        {
            if (track_map.ContainsKey (track.TrackId)) {
                track_map[track.TrackId].Download (uri.LocalPath, delegate (ulong current, ulong total, IntPtr data) {
                    job.DetailedProgress = (double) current / total;
                    return 0;
                });
            } else {
                throw new Exception ("Error copying track from MTP device");
            }
        }

        public override void SyncPlaylists ()
        {
            lock (mtp_device) {
                List<MTP.Playlist> device_playlists = new List<MTP.Playlist> (mtp_device.GetPlaylists ());
                foreach (MTP.Playlist playlist in device_playlists) {
                    playlist.Remove ();
                }
                device_playlists.Clear ();

                // Add playlists from Banshee to the device
                List<Source> children = new List<Source> (Children);
                foreach (Source child in children) {
                    PlaylistSource from = child as PlaylistSource;
                    if (from != null && from.Count > 0) {
                        MTP.Playlist playlist = new MTP.Playlist (mtp_device, from.Name);
                        foreach (uint track_id in ServiceManager.DbConnection.QueryEnumerable<uint> (String.Format (
                            "SELECT CoreTracks.ExternalID FROM {0} WHERE {1}",
                            from.DatabaseTrackModel.ConditionFromFragment, from.DatabaseTrackModel.Condition)))
                        {
                            playlist.AddTrack (track_id);
                        }
                        playlist.Save ();
                    }
                }
            }
        }


        public override bool CanRename {
            get { return !(IsAdding || IsDeleting); }
        }

        private SafeUri empty_file = new SafeUri (Paths.Combine (Paths.ApplicationCache, "mtp.mp3"));
        protected override void OnTracksDeleted ()
        {
            // Hack to get the disk usage indicate to be accurate, which seems to
            // only be updated when tracks are added, not removed.
            try {
                lock (mtp_device) {
                    using (System.IO.TextWriter writer = new System.IO.StreamWriter (Banshee.IO.File.OpenWrite (empty_file, true))) {
                        writer.Write ("foo");
                    }
                    Track mtp_track = new Track (System.IO.Path.GetFileName (empty_file.LocalPath), 3, mtp_device);

                    mtp_device.UploadTrack (empty_file.AbsolutePath, mtp_track, mtp_device.MusicFolder);
                    mtp_device.Remove (mtp_track);
                    Banshee.IO.File.Delete (empty_file);
                }
            } catch {}
            base.OnTracksDeleted ();
        }

        public override void Rename (string newName)
        {
            base.Rename (newName);
            lock (mtp_device) {
                mtp_device.Name = newName;
            }
        }

        private long bytes_used;
        public override long BytesUsed {
            get {
                if (mtp_device != null && Monitor.TryEnter (mtp_device)) {
                    try {
                        bytes_used = 0;
                        foreach (DeviceStorage s in mtp_device.GetStorage ()) {
                            bytes_used += (long) s.MaxCapacity - (long) s.FreeSpaceInBytes;
                        }
                    } finally {
                        Monitor.Exit (mtp_device);
                    }
                }
                return bytes_used;
            }
        }

        private long bytes_capacity;
        public override long BytesCapacity {
            get {
                if (mtp_device != null && Monitor.TryEnter (mtp_device)) {
                    try {
                        bytes_capacity = 0;
                        foreach (DeviceStorage s in mtp_device.GetStorage ()) {
                            bytes_capacity += (long) s.MaxCapacity;
                        }
                    } finally {
                        Monitor.Exit (mtp_device);
                    }
                }
                return bytes_capacity;
            }
        }

        public override bool IsReadOnly {
            get { return false; }
        }

        protected override void AddTrackToDevice (DatabaseTrackInfo track, SafeUri fromUri)
        {
            if (track.PrimarySourceId == DbId)
                return;

            lock (mtp_device) {
                Track mtp_track = TrackInfoToMtpTrack (track, fromUri);
                bool video = track.HasAttribute (TrackMediaAttributes.VideoStream);
                mtp_device.UploadTrack (fromUri.LocalPath, mtp_track, GetFolderForTrack (track), OnUploadProgress);

                // Add/update album art
                if (!video) {
                    string key = MakeAlbumKey (track.AlbumArtist, track.AlbumTitle);
                    if (!album_cache.ContainsKey (key)) {
                        // LIBMTP 1.0.3 BUG WORKAROUND
                        // In libmtp.c the 'LIBMTP_Create_New_Album' function invokes 'create_new_abstract_list'.
                        // The latter calls strlen on the 'name' parameter without null checking. If AlbumTitle is
                        // null, this causes a sigsegv. Lets be safe and always pass non-null values.
                        Album album = new Album (mtp_device, track.AlbumTitle ?? "", track.AlbumArtist ?? "", track.Genre ?? "", track.Composer ?? "");
                        album.AddTrack (mtp_track);

                        if (supports_jpegs && can_sync_albumart) {
                            ArtworkManager art = ServiceManager.Get<ArtworkManager> ();
                            Exception ex = null;
                            Gdk.Pixbuf pic = null;
                            byte[] bytes = null;
                            uint width = 0, height = 0;
                            ThreadAssist.BlockingProxyToMain (() => {
                                try {
                                    pic = art.LookupScalePixbuf (track.ArtworkId, thumb_width);
                                    if (pic != null) {
                                        bytes = pic.SaveToBuffer ("jpeg");
                                        width = (uint) pic.Width;
                                        height = (uint) pic.Height;
                                    }
                                } catch (Exception e) {
                                    ex = e;
                                }
                            });

                            try {
                                if (ex != null) {
                                    throw ex;
                                }
                                if (bytes != null) {
                                    ArtworkManager.DisposePixbuf (pic);
                                    album.Save (bytes, width, height);
                                    album_cache [key] = album;
                                }
                            } catch (Exception e) {
                                Log.Debug ("Failed to create MTP Album", e.Message);
                            }
                        } else {
                            album.Save ();
                            album_cache[key] = album;
                        }
                    } else {
                        Album album = album_cache[key];
                        album.AddTrack (mtp_track);
                        album.Save ();
                    }
                }

                MtpTrackInfo new_track = new MtpTrackInfo (mtp_device, mtp_track);
                new_track.PrimarySource = this;
                new_track.Save (false);
                track_map[new_track.TrackId] = mtp_track;
            }
        }

        private Folder GetFolderForTrack (TrackInfo track)
        {
            if (track.HasAttribute (TrackMediaAttributes.VideoStream)) {
                return mtp_device.VideoFolder;
            } else if (track.HasAttribute (TrackMediaAttributes.Podcast)) {
                return mtp_device.PodcastFolder;
            } else {
                return mtp_device.MusicFolder;
            }
        }

        private int OnUploadProgress (ulong sent, ulong total, IntPtr data)
        {
            AddTrackJob.DetailedProgress = (double) sent / (double) total;
            return 0;
        }

        protected override bool DeleteTrack (DatabaseTrackInfo track)
        {
            lock (mtp_device) {
                Track mtp_track = track_map [track.TrackId];
                track_map.Remove (track.TrackId);

                // Remove from device
                mtp_device.Remove (mtp_track);

                // Remove track from album, and remove album from device if it no longer has tracks
                string key = MakeAlbumKey (track.ArtistName, track.AlbumTitle);
                if (album_cache.ContainsKey (key)) {
                    Album album = album_cache[key];
                    album.RemoveTrack (mtp_track);
                    if (album.Count == 0) {
                        album.Remove ();
                        album_cache.Remove (key);
                    }
                }

                return true;
            }
        }

        public Track TrackInfoToMtpTrack (TrackInfo track, SafeUri fromUri)
        {
            Track f = new Track (System.IO.Path.GetFileName (fromUri.LocalPath), (ulong) Banshee.IO.File.GetSize (fromUri), mtp_device);
            MtpTrackInfo.ToMtpTrack (track, f);
            return f;
        }

        private bool disposed = false;
        public override void Dispose ()
        {
            if (disposed)
                return;

            disposed = true;
            base.Dispose ();

            if (mtp_device != null) {
                lock (mtp_device) {
                    mtp_device.Dispose ();
                }
            }

            mtp_device = null;
        }

        private static string MakeAlbumKey (string album_artist, string album)
        {
            return String.Format ("{0}_{1}", album_artist, album);
        }

        public static readonly SchemaEntry<bool> NeverSyncAlbumArtSchema = new SchemaEntry<bool>(
            "plugins.mtp", "never_sync_albumart",
            false,
            "Album art disabled",
            "Regardless of device's capabilities, do not sync album art"
        );

        public static readonly SchemaEntry<int> AlbumArtWidthSchema = new SchemaEntry<int>(
            "plugins.mtp", "albumart_max_width",
            170,
            "Album art max width",
            "The maximum width to allow for album art."
        );
    }
}
