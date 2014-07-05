//
// MassStorageSource.cs
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
using Mono.Unix;

using Hyena;
using Hyena.Collections;

using Banshee.IO;
using Banshee.Dap;
using Banshee.Base;
using Banshee.ServiceStack;
using Banshee.Library;
using Banshee.Query;
using Banshee.Sources;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Hardware;

using Banshee.Playlists.Formats;
using Banshee.Playlist;

namespace Banshee.Dap.MassStorage
{
    public class MassStorageSource : DapSource
    {
        private Banshee.Collection.Gui.ArtworkManager artwork_manager
            = ServiceManager.Get<Banshee.Collection.Gui.ArtworkManager> ();

        private MassStorageDevice ms_device;
        private IVolume volume;
        private IUsbDevice usb_device;

        public override void DeviceInitialize (IDevice device, bool force)
        {
            base.DeviceInitialize (device, force);

            volume = device as IVolume;

            if (volume == null || (usb_device = volume.ResolveRootUsbDevice ()) == null) {
                throw new InvalidDeviceException ();
            }

            if (!volume.IsMounted && !volume.CanMount) {
                throw new InvalidDeviceException ();
            } else if (!volume.IsMounted) {
                volume.Mount ();
            }

            ms_device = DeviceMapper.Map (this);
            try {
                if (ms_device.ShouldIgnoreDevice () || !ms_device.LoadDeviceConfiguration ()) {
                    ms_device = null;
                }
            } catch (Exception e) {
                Log.Error (e);

                ms_device = null;
            }

            if (!HasMediaCapabilities && ms_device == null) {
                throw new InvalidDeviceException ();
            }

            // Ignore iPods, except ones with .is_audio_player files
            if (MediaCapabilities != null && MediaCapabilities.IsType ("ipod")) {
                if (ms_device != null && ms_device.HasIsAudioPlayerFile) {
                    Log.Information (
                        "Mass Storage Support Loading iPod",
                        "The USB mass storage audio player support is loading an iPod because it has an .is_audio_player file. " +
                        "If you aren't running Rockbox or don't know what you're doing, things might not behave as expected."
                    );
                } else {
                    throw new InvalidDeviceException ();
                }
            }

            Name = ms_device == null ? volume.Name : ms_device.Name;
            mount_point = volume.MountPoint;

            Initialize ();

            if (ms_device != null) {
                ms_device.SourceInitialize ();
            }

            AddDapProperties ();

            // TODO differentiate between Audio Players and normal Disks, and include the size, eg "2GB Audio Player"?
            //GenericName = Catalog.GetString ("Audio Player");
        }

        private void AddDapProperties ()
        {
            if (AudioFolders.Length > 0 && !String.IsNullOrEmpty (AudioFolders[0])) {
                AddDapProperty (String.Format (
                    Catalog.GetPluralString ("Audio Folder", "Audio Folders", AudioFolders.Length), AudioFolders.Length),
                    System.String.Join ("\n", AudioFolders)
                );
            }

            if (VideoFolders.Length > 0 && !String.IsNullOrEmpty (VideoFolders[0])) {
                AddDapProperty (String.Format (
                    Catalog.GetPluralString ("Video Folder", "Video Folders", VideoFolders.Length), VideoFolders.Length),
                    System.String.Join ("\n", VideoFolders)
                );
            }

            if (FolderDepth != -1) {
                AddDapProperty (Catalog.GetString ("Required Folder Depth"), FolderDepth.ToString ());
            }

            AddYesNoDapProperty (Catalog.GetString ("Supports Playlists"), PlaylistTypes.Count > 0);

            /*if (AcceptableMimeTypes.Length > 0) {
                AddDapProperty (String.Format (
                    Catalog.GetPluralString ("Audio Format", "Audio Formats", PlaybackFormats.Length), PlaybackFormats.Length),
                    System.String.Join (", ", PlaybackFormats)
                );
            }*/
        }

        private System.Threading.ManualResetEvent import_reset_event;
        private DatabaseImportManager importer;
        // WARNING: This will be called from a thread!
        protected override void LoadFromDevice ()
        {
            import_reset_event = new System.Threading.ManualResetEvent (false);

            importer = new DatabaseImportManager (this) {
                KeepUserJobHidden = true,
                SkipHiddenChildren = false
            };
            importer.Finished += OnImportFinished;

            foreach (string audio_folder in BaseDirectories) {
                importer.Enqueue (audio_folder);
            }

            import_reset_event.WaitOne ();
        }

        private void OnImportFinished (object o, EventArgs args)
        {
            importer.Finished -= OnImportFinished;

            if (CanSyncPlaylists) {
                var insert_cmd = new Hyena.Data.Sqlite.HyenaSqliteCommand (
                    "INSERT INTO CorePlaylistEntries (PlaylistID, TrackID) VALUES (?, ?)");
                foreach (string playlist_path in PlaylistFiles) {
                    // playlist_path has a file:// prefix, and GetDirectoryName messes it up,
                    // so we need to convert it to a regular path
                    string base_folder = ms_device.RootPath != null ? BaseDirectory :
                        System.IO.Path.GetDirectoryName (SafeUri.UriToFilename (playlist_path));

                    IPlaylistFormat loaded_playlist = PlaylistFileUtil.Load (playlist_path,
                                                                             new Uri (base_folder),
                                                                             ms_device.RootPath);
                    if (loaded_playlist == null)
                        continue;

                    string name = System.IO.Path.GetFileNameWithoutExtension (SafeUri.UriToFilename (playlist_path));
                    PlaylistSource playlist = new PlaylistSource (name, this);
                    playlist.Save ();
                    //Hyena.Data.Sqlite.HyenaSqliteCommand.LogAll = true;
                    foreach (PlaylistElement element in loaded_playlist.Elements) {
                        string track_path = element.Uri.LocalPath;
                        long track_id = DatabaseTrackInfo.GetTrackIdForUri (new SafeUri (track_path), DbId);
                        if (track_id == 0) {
                            Log.DebugFormat ("Failed to find track {0} in DAP library to load it into playlist {1}", track_path, playlist_path);
                        } else {
                            ServiceManager.DbConnection.Execute (insert_cmd, playlist.DbId, track_id);
                        }
                    }
                    //Hyena.Data.Sqlite.HyenaSqliteCommand.LogAll = false;
                    playlist.UpdateCounts ();
                    AddChildSource (playlist);
                }
            }

            import_reset_event.Set ();
        }

        public override void CopyTrackTo (DatabaseTrackInfo track, SafeUri uri, BatchUserJob job)
        {
            if (track.PrimarySourceId == DbId) {
                Banshee.IO.File.Copy (track.Uri, uri, false);
            }
        }

        public override void Import ()
        {
            var importer = new LibraryImportManager (true) {
                SkipHiddenChildren = false
            };

            foreach (string audio_folder in BaseDirectories) {
                importer.Enqueue (audio_folder);
            }
        }

        public IVolume Volume {
            get { return volume; }
        }

        public IUsbDevice UsbDevice {
            get { return usb_device; }
        }

        private string mount_point;
        public override string BaseDirectory {
            get { return mount_point; }
        }

        protected override IDeviceMediaCapabilities MediaCapabilities {
            get {
                return ms_device ?? (
                    volume.Parent == null
                        ? base.MediaCapabilities
                        : volume.Parent.MediaCapabilities ?? base.MediaCapabilities
                );
            }
        }

#region Properties and Methods for Supporting Syncing of Playlists

        private string [] playlists_paths;
        private string [] PlaylistsPaths {
            get {
                if (playlists_paths == null) {
                    if (MediaCapabilities == null || MediaCapabilities.PlaylistPaths == null
                        || MediaCapabilities.PlaylistPaths.Length == 0) {
                        playlists_paths = new string [] { WritePath };
                    } else {
                        playlists_paths = new string [MediaCapabilities.PlaylistPaths.Length];
                        for (int i = 0; i < MediaCapabilities.PlaylistPaths.Length; i++) {
                            playlists_paths[i] = Paths.Combine (BaseDirectory, MediaCapabilities.PlaylistPaths[i]);
                            playlists_paths[i] = playlists_paths[i].Replace ("%File", String.Empty);
                        }
                    }
                }
                return playlists_paths;
            }
        }

        private string playlists_write_path;
        private string PlaylistsWritePath {
            get {
                if (playlists_write_path == null) {
                    playlists_write_path = WritePath;
                    // We write playlists to the first folder listed in the PlaylistsPath property
                    if (PlaylistsPaths.Length > 0) {
                        playlists_write_path = PlaylistsPaths[0];
                    }

                    if (!Directory.Exists (playlists_write_path)) {
                        Directory.Create (playlists_write_path);
                    }

                }
                return playlists_write_path;
            }
        }

        private string [] playlist_formats;
        private string [] PlaylistFormats {
            get {
                if (playlist_formats == null && MediaCapabilities != null) {
                    playlist_formats = MediaCapabilities.PlaylistFormats;
                }
                return playlist_formats;
            }
            set { playlist_formats = value; }
        }

        private List<PlaylistFormatDescription> playlist_types;
        private IList<PlaylistFormatDescription> PlaylistTypes  {
            get {
                if (playlist_types == null) {
                    playlist_types = new List<PlaylistFormatDescription> ();
                    if (PlaylistFormats != null) {
                        foreach (PlaylistFormatDescription desc in Banshee.Playlist.PlaylistFileUtil.ExportFormats) {
                            foreach (string mimetype in desc.MimeTypes) {
                                if (Array.IndexOf (PlaylistFormats, mimetype) != -1) {
                                    playlist_types.Add (desc);
                                    break;
                                }
                            }
                        }
                    }
                }

                SupportsPlaylists &= CanSyncPlaylists;
                return playlist_types;
            }
        }

        private IEnumerable<string> PlaylistFiles {
            get {
                foreach (string folder_name in PlaylistsPaths) {
                    if (!Directory.Exists (folder_name)) {
                        continue;
                    }
                    foreach (string file_name in Directory.GetFiles (folder_name)) {
                        foreach (PlaylistFormatDescription desc in playlist_types) {
                            if (file_name.EndsWith (desc.FileExtension)) {
                                yield return file_name;
                                break;
                            }
                        }
                    }
                }
            }
        }

        private bool CanSyncPlaylists {
            get {
                return PlaylistsWritePath != null && playlist_types.Count > 0;
            }
        }

        public override void SyncPlaylists ()
        {
            if (!CanSyncPlaylists) {
                return;
            }

            foreach (string file_name in PlaylistFiles) {
                try {
                    Banshee.IO.File.Delete (new SafeUri (file_name));
                } catch (Exception e) {
                    Log.Error (e);
                }
            }

            // Add playlists from Banshee to the device
            PlaylistFormatBase playlist_format = null;
            List<Source> children = new List<Source> (Children);
            foreach (Source child in children) {
                PlaylistSource from = child as PlaylistSource;
                string escaped_name = StringUtil.EscapeFilename (child.Name);
                if (from != null && !String.IsNullOrEmpty (escaped_name)) {
                    from.Reload ();
                    if (playlist_format == null) {
                        playlist_format = Activator.CreateInstance (PlaylistTypes[0].Type) as PlaylistFormatBase;
                        playlist_format.FolderSeparator = MediaCapabilities.FolderSeparator;
                    }

                    SafeUri playlist_path = new SafeUri (System.IO.Path.Combine (
                        PlaylistsWritePath, String.Format ("{0}.{1}", escaped_name, PlaylistTypes[0].FileExtension)));

                    System.IO.Stream stream = null;
                    try {
                        stream = Banshee.IO.File.OpenWrite (playlist_path, true);

                        if (ms_device.RootPath == null) {
                            playlist_format.BaseUri = new Uri (PlaylistsWritePath);
                        } else {
                            playlist_format.RootPath = ms_device.RootPath;
                            playlist_format.BaseUri = new Uri (BaseDirectory);
                        }

                        playlist_format.Save (stream, from);
                    } catch (Exception e) {
                        Log.Error (e);
                    } finally {
                        stream.Close ();
                    }
                }
            }
        }

#endregion

        protected override string [] GetIconNames ()
        {
            string [] names = ms_device != null ? ms_device.GetIconNames () : null;
            return names == null ? base.GetIconNames () : names;
        }

        public override long BytesUsed {
            get { return BytesCapacity - volume.Available; }
        }

        public override long BytesCapacity {
            get { return (long) volume.Capacity; }
        }

        private bool had_write_error = false;
        public override bool IsReadOnly {
            get { return volume.IsReadOnly || had_write_error; }
        }

        private string write_path = null;
        public string WritePath {
            get {
                if (write_path == null) {
                    write_path = BaseDirectory;
                    // According to the HAL spec, the first folder listed in the audio_folders property
                    // is the folder to write files to.
                    if (AudioFolders.Length > 0) {
                        write_path = Hyena.Paths.Combine (write_path, AudioFolders[0]);
                    }
                }
                return write_path;
            }

            set { write_path = value; }
        }

        private string write_path_video = null;
        public string WritePathVideo {
            get {
                if (write_path_video == null) {
                    write_path_video = BaseDirectory;
                    // Some Devices May Have a Separate Video Directory
                    if (VideoFolders.Length > 0) {
                        write_path_video = Hyena.Paths.Combine (write_path_video, VideoFolders[0]);
                    } else if (AudioFolders.Length > 0) {
                        write_path_video = Hyena.Paths.Combine (write_path_video, AudioFolders[0]);
                        write_path_video = Hyena.Paths.Combine (write_path_video, "Videos");
                    }
                }
                return write_path_video;
            }

            set { write_path_video = value; }
        }

        private string [] audio_folders;
        protected string [] AudioFolders {
            get {
                if (audio_folders == null) {
                    audio_folders = HasMediaCapabilities ? MediaCapabilities.AudioFolders : new string[0];
                }
                return audio_folders;
            }
            set { audio_folders = value; }
        }

        private string [] video_folders;
        protected string [] VideoFolders {
            get {
                if (video_folders == null) {
                    video_folders = HasMediaCapabilities ? MediaCapabilities.VideoFolders : new string[0];
                }
                return video_folders;
            }
            set { video_folders = value; }
        }

        protected IEnumerable<string> BaseDirectories {
            get {
                if (AudioFolders.Length == 0) {
                    yield return BaseDirectory;
                } else {
                    foreach (string audio_folder in AudioFolders) {
                        yield return Paths.Combine (BaseDirectory, audio_folder);
                    }
                }
            }
        }

        private int folder_depth = -1;
        protected int FolderDepth {
            get {
                if (folder_depth == -1) {
                    folder_depth = HasMediaCapabilities ? MediaCapabilities.FolderDepth : -1;
                }
                return folder_depth;
            }
            set { folder_depth = value; }
        }

        private int cover_art_size = -1;
        protected int CoverArtSize {
            get {
                if (cover_art_size == -1) {
                    cover_art_size = HasMediaCapabilities ? MediaCapabilities.CoverArtSize : 0;
                }
                return cover_art_size;
            }
            set { cover_art_size = value; }
        }

        private string cover_art_file_name = null;
        protected string CoverArtFileName {
            get {
                if (cover_art_file_name == null) {
                    cover_art_file_name = HasMediaCapabilities ? MediaCapabilities.CoverArtFileName : null;
                }
                return cover_art_file_name;
            }
            set { cover_art_file_name = value; }
        }

        private string cover_art_file_type = null;
        protected string CoverArtFileType {
            get {
                if (cover_art_file_type == null) {
                    cover_art_file_type = HasMediaCapabilities ? MediaCapabilities.CoverArtFileType : null;
            }
                return cover_art_file_type;
            }
            set { cover_art_file_type = value; }
        }

        public override void UpdateMetadata (DatabaseTrackInfo track)
        {
            SafeUri new_uri = new SafeUri (GetTrackPath (track, System.IO.Path.GetExtension (track.Uri)));

            if (new_uri.ToString () != track.Uri.ToString ()) {
                Directory.Create (System.IO.Path.GetDirectoryName (new_uri.LocalPath));
                Banshee.IO.File.Move (track.Uri, new_uri);

                //to remove the folder if it's not needed anymore:
                DeleteTrackFile (track);

                track.Uri = new_uri;
                track.Save (true, BansheeQuery.UriField);
            }

            base.UpdateMetadata (track);
        }

        protected override void AddTrackToDevice (DatabaseTrackInfo track, SafeUri fromUri)
        {
            if (track.PrimarySourceId == DbId)
                return;

            SafeUri new_uri = new SafeUri (GetTrackPath (track, System.IO.Path.GetExtension (fromUri)));
            // If it already is on the device but it's out of date, remove it
            //if (File.Exists(new_uri) && File.GetLastWriteTime(track.Uri.LocalPath) > File.GetLastWriteTime(new_uri))
                //RemoveTrack(new MassStorageTrackInfo(new SafeUri(new_uri)));

            if (!File.Exists (new_uri)) {
                Directory.Create (System.IO.Path.GetDirectoryName (new_uri.LocalPath));
                File.Copy (fromUri, new_uri, false);

                DatabaseTrackInfo copied_track = new DatabaseTrackInfo (track);
                copied_track.PrimarySource = this;
                copied_track.Uri = new_uri;

                // Write the metadata in db to the file on the DAP if it has changed since file was modified
                // to ensure that when we load it next time, it's data will match what's in the database
                // and the MetadataHash will actually match.  We do this by comparing the time
                // stamps on files for last update of the db metadata vs the sync to file.
                // The equals on the inequality below is necessary for podcasts who often have a sync and
                // update time that are the same to the second, even though the album metadata has changed in the
                // DB to the feedname instead of what is in the file.  It should be noted that writing the metadata
                // is a small fraction of the total copy time anyway.

                if (track.LastSyncedStamp >= Hyena.DateTimeUtil.ToDateTime (track.FileModifiedStamp)) {
                    Log.DebugFormat ("Copying Metadata to File Since Sync time >= Updated Time");
                    bool write_metadata = Metadata.SaveTrackMetadataService.WriteMetadataEnabled.Value;
                    bool write_ratings = Metadata.SaveTrackMetadataService.WriteRatingsEnabled.Value;
                    bool write_playcounts = Metadata.SaveTrackMetadataService.WritePlayCountsEnabled.Value;
                    Banshee.Streaming.StreamTagger.SaveToFile (copied_track, write_metadata, write_ratings, write_playcounts);
                }

                copied_track.Save (false);
            }

            if (CoverArtSize > -1 && !String.IsNullOrEmpty (CoverArtFileType) &&
                    !String.IsNullOrEmpty (CoverArtFileName) && (FolderDepth == -1 || FolderDepth > 0)) {
                SafeUri cover_uri = new SafeUri (System.IO.Path.Combine (System.IO.Path.GetDirectoryName (new_uri.LocalPath),
                                                                         CoverArtFileName));
                string coverart_id = track.ArtworkId;

                if (!File.Exists (cover_uri) && CoverArtSpec.CoverExists (coverart_id)) {
                    Gdk.Pixbuf pic = null;

                    if (CoverArtSize == 0) {
                        if (CoverArtFileType == "jpg" || CoverArtFileType == "jpeg") {
                            SafeUri local_cover_uri = new SafeUri (Banshee.Base.CoverArtSpec.GetPath (coverart_id));
                            Banshee.IO.File.Copy (local_cover_uri, cover_uri, false);
                        } else {
                            pic = artwork_manager.LookupPixbuf (coverart_id);
                        }
                    } else {
                        pic = artwork_manager.LookupScalePixbuf (coverart_id, CoverArtSize);
                    }

                    if (pic != null) {
                        try {
                            byte [] bytes = pic.SaveToBuffer (CoverArtFileType);
                            System.IO.Stream cover_art_file = File.OpenWrite (cover_uri, true);
                            cover_art_file.Write (bytes, 0, bytes.Length);
                            cover_art_file.Close ();
                        } catch (GLib.GException){
                            Log.DebugFormat ("Could not convert cover art to {0}, unsupported filetype?", CoverArtFileType);
                        } finally {
                            Banshee.Collection.Gui.ArtworkManager.DisposePixbuf (pic);
                        }
                    }
                }
            }
        }

        protected override bool DeleteTrack (DatabaseTrackInfo track)
        {
            if (ms_device != null && !ms_device.DeleteTrackHook (track)) {
                return false;
            }
            DeleteTrackFile (track);
            return true;
        }

        private void DeleteTrackFile (DatabaseTrackInfo track)
        {
            try {
                string track_file = System.IO.Path.GetFileName (track.Uri.LocalPath);
                string track_dir = System.IO.Path.GetDirectoryName (track.Uri.LocalPath);
                int files = 0;

                // Count how many files remain in the track's directory,
                // excluding self or cover art
                foreach (string file in System.IO.Directory.GetFiles (track_dir)) {
                    string relative = System.IO.Path.GetFileName (file);
                    if (relative != track_file && relative != CoverArtFileName) {
                        files++;
                    }
                }

                // If we are the last track, go ahead and delete the artwork
                // to ensure that the directory tree can get trimmed away too
                if (files == 0 && CoverArtFileName != null) {
                    System.IO.File.Delete (Paths.Combine (track_dir, CoverArtFileName));
                }

                if (Banshee.IO.File.Exists (track.Uri)) {
                    Banshee.IO.Utilities.DeleteFileTrimmingParentDirectories (track.Uri);
                } else {
                    Banshee.IO.Utilities.TrimEmptyDirectories (track.Uri);
                }
            } catch (System.IO.FileNotFoundException) {
            } catch (System.IO.DirectoryNotFoundException) {
            }
        }

        protected override void Eject ()
        {
            base.Eject ();
            if (volume.CanUnmount) {
                volume.Unmount ();
            }

            if (volume.CanEject) {
                volume.Eject ();
            }
        }

        protected override bool CanHandleDeviceCommand (DeviceCommand command)
        {
            try {
                SafeUri uri = new SafeUri (command.DeviceId);
                return BaseDirectory.StartsWith (uri.LocalPath);
            } catch (Exception e) {
                Log.Error (e);

                return false;
            }
        }

        private string GetTrackPath (TrackInfo track, string ext)
        {
            string file_path = null;

            if (track.HasAttribute (TrackMediaAttributes.Podcast)) {
                string album = FileNamePattern.Escape (track.DisplayAlbumTitle);
                string title = FileNamePattern.Escape (track.DisplayTrackTitle);
                file_path = System.IO.Path.Combine ("Podcasts", album);
                file_path = System.IO.Path.Combine (file_path, title);
            } else if (track.HasAttribute (TrackMediaAttributes.VideoStream)) {
                string album = FileNamePattern.Escape (track.DisplayAlbumTitle);
                string title = FileNamePattern.Escape (track.DisplayTrackTitle);
                file_path = System.IO.Path.Combine (album, title);
            } else if (ms_device == null || !ms_device.GetTrackPath (track, out file_path)) {
                // If the folder_depth property exists, we have to put the files in a hiearchy of
                // the exact given depth (not including the mount point/audio_folder).
                if (FolderDepth != -1) {
                    int depth = FolderDepth;

                    bool is_album_unknown = String.IsNullOrEmpty (track.AlbumTitle);

                    string album_artist = FileNamePattern.Escape (track.DisplayAlbumArtistName);
                    string track_album  = FileNamePattern.Escape (track.DisplayAlbumTitle);
                    string track_number = FileNamePattern.Escape (String.Format ("{0:00}", track.TrackNumber));
                    string track_title  = FileNamePattern.Escape (track.DisplayTrackTitle);

                    if (depth == 0) {
                        // Artist - Album - 01 - Title
                        string track_artist = FileNamePattern.Escape (track.DisplayArtistName);
                        file_path = is_album_unknown ?
                            String.Format ("{0} - {1} - {2}", track_artist, track_number, track_title) :
                            String.Format ("{0} - {1} - {2} - {3}", track_artist, track_album, track_number, track_title);
                    } else if (depth == 1) {
                        // Artist - Album/01 - Title
                        file_path = is_album_unknown ?
                            album_artist :
                            String.Format ("{0} - {1}", album_artist, track_album);
                        file_path = System.IO.Path.Combine (file_path, String.Format ("{0} - {1}", track_number, track_title));
                    } else if (depth == 2) {
                        // Artist/Album/01 - Title
                        file_path = album_artist;
                        if (!is_album_unknown || ms_device.MinimumFolderDepth == depth) {
                            file_path = System.IO.Path.Combine (file_path, track_album);
                        }
                        file_path = System.IO.Path.Combine (file_path, String.Format ("{0} - {1}", track_number, track_title));
                    } else {
                        // If the *required* depth is more than 2..go nuts!
                        for (int i = 0; i < depth - 2; i++) {
                            if (i == 0) {
                                file_path = album_artist.Substring (0, Math.Min (i+1, album_artist.Length)).Trim ();
                            } else {
                                file_path = System.IO.Path.Combine (file_path, album_artist.Substring (0, Math.Min (i+1, album_artist.Length)).Trim ());
                            }

                        }

                        // Finally add on the Artist/Album/01 - Track
                        file_path = System.IO.Path.Combine (file_path, album_artist);
                        file_path = System.IO.Path.Combine (file_path, track_album);
                        file_path = System.IO.Path.Combine (file_path, String.Format ("{0} - {1}", track_number, track_title));
                    }
                } else {
                    file_path = MusicLibrarySource.MusicFileNamePattern.CreateFromTrackInfo (track);
                }
            }

            if (track.HasAttribute (TrackMediaAttributes.VideoStream)) {
              file_path = System.IO.Path.Combine (WritePathVideo, file_path);
            } else {
              file_path = System.IO.Path.Combine (WritePath, file_path);
            }
            file_path += ext;

            return file_path;
        }

        public override bool HasEditableTrackProperties {
            get { return true; }
        }
    }
}
