/***************************************************************************
 *  MassStorageDap.cs
 *
 *  Copyright (C) 2006 Novell and Gabriel Burt
 *  Written by Gabriel Burt (gabriel.burt@gmail.com)
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
using System.Collections;
using System.Collections.Generic;
using Hal;
using Mono.Unix;
using Banshee.Dap;
using Banshee.Base;

public static class PluginModuleEntry
{
    public static Type [] GetTypes()
    {
        return new Type [] {
            typeof(Banshee.Dap.MassStorage.MassStorageDap)
        };
    }
}

namespace Banshee.Dap.MassStorage
{
    [DapProperties(DapType = DapType.Generic)]
    public class MassStorageDap : DapDevice
    {
        private static Gnome.Vfs.VolumeMonitor monitor;

        static MassStorageDap() 
        {
            if(!Gnome.Vfs.Vfs.Initialized) {
                Gnome.Vfs.Vfs.Initialize();
            }
            
            monitor = Gnome.Vfs.VolumeMonitor.Get();
        }

        private bool mounted = false, ui_initialized = false;
        private List<TrackInfo> uncopiedTracks = new List<TrackInfo>();

        protected Hal.Device usb_device = null;
        protected Hal.Device player_device = null;
        protected Hal.Device volume_device = null;
        protected Gnome.Vfs.Volume volume = null;

        public override InitializeResult Initialize(Hal.Device halDevice)
        {
            volume_device = halDevice;

            try {
                player_device = volume_device.Parent;
                usb_device = new Hal.Device(player_device["storage.physical_device"]);
            } catch {
                return InitializeResult.Invalid;
            }

            if(!volume_device.PropertyExists("block.device")) {
                return InitializeResult.Invalid;
            }

            if(!volume_device.PropertyExists("volume.is_mounted") ||
                !volume_device.GetPropertyBoolean("volume.is_mounted")) {
                return WaitForVolumeMount(volume_device);
            }
            
            if(player_device["portable_audio_player.type"] == "ipod") {
                if(File.Exists(IsAudioPlayerPath)) {
                    LogCore.Instance.PushInformation(
                        "Mass Storage Support Loading iPod",
                        "The USB mass storage audio player support is loading an iPod because it has an .is_audio_player file. " +
                        "If you aren't running Rockbox or don't know what you're doing, things might not behave as expected.",
                        false);
                } else {
                    LogCore.Instance.PushInformation(
                        "Mass Storage Support Ignoring iPod",
                        "The USB mass storage audio player support ignored an iPod. " +
                        "Either Banshee's iPod support is broken or missing, or the iPod itself may be corrupted.",
                        false);

                    return InitializeResult.Invalid;
                }
            }

            // Detect player via HAL property or presence of .is_audo_player file in root
            if(player_device["portable_audio_player.access_method"] != "storage" &&
                !File.Exists(IsAudioPlayerPath)) {                
                return InitializeResult.Invalid;
            }

            // Allow the HAL values to be overridden by corresponding key=value pairs in .is_audio_player
            if(File.Exists(IsAudioPlayerPath)) {
                StreamReader reader = null;
                try {
                    reader = new StreamReader(IsAudioPlayerPath);

                    string line;
                    while((line = reader.ReadLine()) != null) {
                        string [] pieces = line.Split('=');
                        if(line.StartsWith("#") || pieces == null || pieces.Length != 2)
                            continue;

                        string key = pieces[0], val = pieces[1];

                        switch(key) {
                        case "audio_folders":
                            AudioFolders = val.Split(',');
                            break;

                        case "output_formats":
                            PlaybackFormats = val.Split(',');
                            break;

                        case "folder_depth":
                            FolderDepth = Int32.Parse(val);
                            break;

                        case "input_formats":
                        case "playlist_format":
                        case "playlist_path":
                        default:
                            Console.WriteLine("Unsupported key: {0}", key);
                            break;
                        }
                    }
                } catch(Exception e) {
                    LogCore.Instance.PushWarning("Error parsing .is_audio_player file", e.ToString(), false);
                } finally {
                    if(reader != null)
                        reader.Close();
                }
            }

            volume = monitor.GetVolumeForPath(MountPoint);
            if(volume == null) {
                // Gnome VFS doesn't know volume is mounted yet
                monitor.VolumeMounted += OnVolumeMounted;
                is_read_only = true;
            } else {
                mounted = true;
                is_read_only = volume.IsReadOnly;
            }

            base.Initialize(usb_device);

            // Configure the extensions and mimetypes this DAP supports
            List<string> extensions = new List<string>();
            List<string> mimetypes = new List<string>();
            foreach(string format in PlaybackFormats) {
                string codec = Banshee.Dap.CodecType.GetCodec(format);
                
                if(codec != null) {
                    extensions.AddRange(CodecType.GetExtensions(codec));
                    mimetypes.AddRange(CodecType.GetMimeTypes(codec));
                }
            }

            SupportedExtensions = extensions.ToArray();
            SupportedPlaybackMimeTypes = mimetypes.ToArray();
 
            // Install properties
 
            if(usb_device.PropertyExists("usb.vendor")) {
                InstallProperty(Catalog.GetString("Vendor"), usb_device["usb.vendor"]);
            } else if(player_device.PropertyExists("info.vendor")) {
                InstallProperty(Catalog.GetString("Vendor"), player_device["info.vendor"]);
            }

            if(AudioFolders.Length > 1 || AudioFolders[0] != "") {
                InstallProperty(String.Format(
                    Catalog.GetPluralString("Audio Folder", "Audio Folders", AudioFolders.Length), AudioFolders.Length),
                    System.String.Join("\n", AudioFolders)
                );
            }

            if(FolderDepth != -1) {
                InstallProperty(Catalog.GetString("Required Folder Depth"), FolderDepth.ToString());
            }

            if(PlaybackFormats.Length > 0) {
                InstallProperty(String.Format(
                    Catalog.GetPluralString("Audio Format", "Audio Formats", PlaybackFormats.Length), PlaybackFormats.Length),
                    System.String.Join(", ", PlaybackFormats)
                );
            }

            // Don't continue until the UI is initialized
            if(!Globals.UIManager.IsInitialized) {
                Globals.UIManager.Initialized += OnUIManagerInitialized;
            } else {
                ui_initialized = true;
            }

            if(ui_initialized && mounted) {
                ReloadDatabase();
            }
            
            // FIXME probably should be able to cancel at some point when you can actually sync
            CanCancelSave = false;
            
            return InitializeResult.Valid;
        }

        public void OnVolumeMounted(object o, Gnome.Vfs.VolumeMountedArgs args)
        {
            if(args.Volume.DevicePath == volume_device["block.device"]) {
                monitor.VolumeMounted -= OnVolumeMounted;
            
                volume = args.Volume;
                is_read_only = volume.IsReadOnly;

                mounted = true;

                if(ui_initialized)
                    ReloadDatabase();
            }
        }

        public override void Dispose()
        {
            // FIXME anything else to do here?
            volume = null;
            base.Dispose();
        }

        private void OnUIManagerInitialized(object o, EventArgs args)
        {
            ui_initialized = true;
            if(mounted)
                ReloadDatabase();
        }
        
        protected override void Reload ()
        {
            ReloadDatabase();
        }
 
        private void ReloadDatabase()
        {
            lock(Source.TracksMutex) {
                ClearTracks(false);
            }

            ImportManager importer = new ImportManager();

            importer.Title = Catalog.GetString("Loading Songs");
            importer.CancelMessage = Catalog.GetString("The audio device song loading process is currently running.  Would you like to stop it?");
            importer.ProgressMessage = Catalog.GetString("Loading {0} of {1}");

            importer.ImportRequested += HandleImportRequested;

            foreach(string music_dir in AudioFolders) {
                importer.QueueSource(System.IO.Path.Combine(MountPoint, music_dir));
            }
        }

        private void HandleImportRequested(object o, ImportEventArgs args)
        {
            try {
                TrackInfo track = new MassStorageTrackInfo(new SafeUri(args.FileName));
                args.ReturnMessage = String.Format("{0} - {1}", track.Artist, track.Title);

                AddTrack(track);
            } catch {
                args.ReturnMessage = Catalog.GetString("Scanning") + "...";
            }

            QueuePropertiesChanged();
        }


        private uint properties_timeout = 0;
        private void QueuePropertiesChanged() {
            if(properties_timeout == 0) {
                properties_timeout = GLib.Timeout.Add(100, DoPropertiesChanged);
            }
        }

        private bool DoPropertiesChanged()
        {
            OnPropertiesChanged();
            properties_timeout = 0;
            return false;
        }

        public override void Synchronize()
        {
            List<TrackInfo> tracksToCopy;
            lock(uncopiedTracks) {
                tracksToCopy = new List<TrackInfo>(uncopiedTracks);
                uncopiedTracks.Clear();
            }

            int count = 1;
            foreach(TrackInfo track in tracksToCopy) {
                UpdateSaveProgress(
                    String.Format(Catalog.GetString("Copying {0} of {1}"), count, tracksToCopy.Count), 
                    String.Format("{0} - {1}", track.DisplayArtist, track.DisplayTitle),
                    count / tracksToCopy.Count);

                CopyTrack(track);
                count++;
            }

            FinishSave();
        }

        public override void Eject()
        {
            UnmapPlayback(typeof(MassStorageTrackInfo));
            Unmount();
        }

        private void HandleStopped(object o, Banshee.MediaEngine.PlayerEngineStateArgs args)
        {
            if(args.State == Banshee.MediaEngine.PlayerEngineState.Idle) {
                PlayerEngineCore.StateChanged -= HandleStopped;
                Unmount();
            }
        }

        private void Unmount()
        {
            if(volume != null)
                volume.Unmount(UnmountCallback);
        }

        private void UnmountCallback(bool succeeded, string error, string detailed_error)
        {
            if(succeeded) {
                if(!player_device.PropertyExists("storage.requires_eject") || 
                    player_device.GetPropertyBoolean("storage.requires_eject")) {
                    volume.Eject(EjectCallback);
                } else {
                   base.Eject();
                }
            } else {
                LogCore.Instance.PushWarning(
                    String.Format(Catalog.GetString("Failed to Unmount {0}"), Name),
                    Catalog.GetString("Make sure no other programs are using it."),
                    true
                );
            }
        }

        private void EjectCallback(bool succeeded, string error, string detailed_error)
        {
            if(succeeded) {
                base.Eject();
            } else {
                LogCore.Instance.PushWarning(
                    String.Format(Catalog.GetString("Failed to Eject {0}"), Name),
                    error,
                    false
                );
            }
        }

        public override void AddTrack(TrackInfo track)
        {
            if(track == null || IsReadOnly && !tracks.Contains(track))
                return;

            lock(Source.TracksMutex) {
                tracks.Add(track);
            }
            OnTrackAdded(track);

            if(!(track is MassStorageTrackInfo)) {
                lock(uncopiedTracks) {
                    uncopiedTracks.Add(track);
                }
            }
        }
        
        private void CopyTrack(TrackInfo track)
        {
            if(track == null)
                return;
            
            string new_path = GetTrackPath(track);
            try {
                Console.WriteLine("have path {0} for track {1}", new_path, track.Uri);

                // If it already is on the device but it's out of date, remove it
                if(File.Exists(new_path) && File.GetLastWriteTime(track.Uri.LocalPath) > File.GetLastWriteTime(new_path))
                    RemoveTrack(new MassStorageTrackInfo(new SafeUri(new_path)));

                if(!File.Exists(new_path)) {
                    Directory.CreateDirectory(Path.GetDirectoryName(new_path));
                    File.Copy(track.Uri.LocalPath, new_path);

                    TrackInfo new_track = new MassStorageTrackInfo(new SafeUri(new_path));

                    // Add the new MassStorageTrackInfo and remove the old TrackInfo from the treeview
                    lock(Source.TracksMutex) {
                        tracks.Add(new_track);
                    }
                    OnTrackAdded(new_track);
                }

                lock(Source.TracksMutex) {
                    tracks.Remove(track);
                }
            } catch (Exception e) {
                LogCore.Instance.PushInformation("Unable to copy file", new_path + e.ToString(), false);
            }
        }

        protected override void OnTrackRemoved(TrackInfo track)
        {
            if(IsReadOnly)
                return;

            Remover.Enqueue(track);
        }


        private void HandleRemoveRequested(object o, QueuedOperationArgs args)
        {
            TrackInfo track = args.Object as TrackInfo;

            args.ReturnMessage = String.Format("{0} - {1}", track.DisplayArtist, track.DisplayTitle);

            if(!(track is MassStorageTrackInfo)) {
                lock(uncopiedTracks) {
                    uncopiedTracks.Remove(track);
                }
            }

            // FIXME shouldn't need to check for this
            // Make sure it's on the drive
            if(track.Uri.LocalPath.IndexOf(MountPoint) == -1)
                return;
            
            try {
                File.Delete(track.Uri.LocalPath);
                //Console.WriteLine("Deleted {0}", track.Uri.LocalPath);
            } catch {
                LogCore.Instance.PushInformation("Could not delete file", track.Uri.LocalPath, false);
            }

            // trim empty parent directories
            try {
                string old_dir = Path.GetDirectoryName(track.Uri.LocalPath);
                while(old_dir != null && old_dir != String.Empty) {
                    Directory.Delete(old_dir);
                    old_dir = Path.GetDirectoryName(old_dir);
                }
            } catch {}
        }

        public override Gdk.Pixbuf GetIcon(int size)
        {
            string prefix = "multimedia-player";
            Gdk.Pixbuf icon = IconThemeUtils.LoadIcon(prefix + ((IconId == null) ? "" : "-" + IconId), size);
            return icon == null ? base.GetIcon(size) : icon;
        }

        private string GetTrackPath(TrackInfo track)
        {
            string file_path = WritePath;

            string artist = FileNamePattern.Escape(track.Artist);
            string album = FileNamePattern.Escape(track.Album);
            string number_title = FileNamePattern.Escape(track.TrackNumberTitle);

            // If the folder_depth property exists, we have to put the files in a hiearchy of
            // the exact given depth (not including the mount point/audio_folder).
            if(FolderDepth != -1) {
                int depth = FolderDepth;

                if(depth == 0) {
                    // Artist - Album - 01 - Title
                    file_path = System.IO.Path.Combine(file_path, String.Format("{0} - {1} - {2}", artist, album, number_title));
                } else if(depth == 1) {
                    // Artist - Album/01 - Title
                    file_path = System.IO.Path.Combine(file_path, String.Format("{0} - {1}", artist, album));
                    file_path = System.IO.Path.Combine(file_path, number_title);
                } else if(depth == 2) {
                    // Artist/Album/01 - Title
                    file_path = System.IO.Path.Combine(file_path, artist);
                    file_path = System.IO.Path.Combine(file_path, album);
                    file_path = System.IO.Path.Combine(file_path, number_title);
                } else {
                    // If the *required* depth is more than 2..go nuts!
                    for(int i = 0; i < depth - 2; i++) {
                        file_path = System.IO.Path.Combine(file_path, artist.Substring(0, Math.Min(i, artist.Length)).Trim());
                    }

                    // Finally add on the Artist/Album/01 - Track
                    file_path = System.IO.Path.Combine(file_path, artist);
                    file_path = System.IO.Path.Combine(file_path, album);
                    file_path = System.IO.Path.Combine(file_path, number_title);
                }
            } else {
                file_path = System.IO.Path.Combine(file_path, FileNamePattern.CreateFromTrackInfo(track));
            }

            file_path += Path.GetExtension(track.Uri.LocalPath);

            return file_path;
        }

        private QueuedOperationManager remover;
        public QueuedOperationManager Remover {
            get {
                if(remover == null) {
                    lock(this) {
                        if(remover == null) {
                            remover = new QueuedOperationManager();
                            remover.ActionMessage = String.Format(Catalog.GetString("Removing songs from {0}"), Name);
                            remover.ProgressMessage = Catalog.GetString("Removing {0} of {1}");
                            remover.UserEvent.Icon = IconThemeUtils.LoadIcon(22, "edit-delete", Gtk.Stock.Delete);
                            remover.OperationRequested += HandleRemoveRequested;
                            remover.Finished += delegate { remover = null; };
                        }
                    }
                }

                return remover;
            }
            set { remover = value; }
        }

        public virtual string IconId {
            get {
                return null;
            }
        }
        
        private string GetDisplayStringProperty(Hal.Device device, string property)
        {
            if(device.PropertyExists(property)) {
                string value = device[property];
                if(String.IsNullOrEmpty(value)) {
                    return null;
                }
                
                return value;
            }
            
            return null;
        }
 
        private Stack<Hal.Device> CollectUsbDeviceStack(Hal.Device device)
        {
            Stack<Hal.Device> device_stack = new Stack<Hal.Device>();
            int usb_vendor_id = -1;
            int usb_product_id = -1;

            Hal.Device tmp_device = device;

            while(tmp_device != null) {
                // Skip the SCSI parents of the player volume if they are in the tree
                if((tmp_device.PropertyExists("info.bus") && tmp_device["info.bus"] == "scsi") ||
                    (tmp_device.PropertyExists("info.category") && tmp_device["info.category"] == "scsi_host")) {
                    device_stack.Push(tmp_device);
                    tmp_device = tmp_device.Parent;
                    continue;
                }

                bool have_usb_ids = false;
                int _usb_vendor_id = -1;
                int _usb_product_id = -1;

                // Figure out the IDs if they exist
                if(tmp_device.PropertyExists("usb.vendor_id") && tmp_device.PropertyExists("usb.product_id")) {
                    _usb_vendor_id = tmp_device.GetPropertyInteger("usb.vendor_id");
                    _usb_product_id = tmp_device.GetPropertyInteger("usb.product_id");
                    have_usb_ids = true;
                } else if(tmp_device.PropertyExists("usb_device.vendor_id") && tmp_device.PropertyExists("usb_device.product_id")) {
                    _usb_vendor_id = tmp_device.GetPropertyInteger("usb_device.vendor_id");
                    _usb_product_id = tmp_device.GetPropertyInteger("usb_device.product_id");
                    have_usb_ids = true;
                }

                if(have_usb_ids) {
                    if(usb_vendor_id == -1 && usb_product_id == -1) {
                        // We found the first raw USB device, remember it
                        usb_vendor_id = _usb_vendor_id;
                        usb_product_id = _usb_product_id;
                    } else if(usb_vendor_id != _usb_vendor_id || usb_product_id != _usb_product_id) {
                        // We are no longer looking at the device we care about (could now be at a hub or something)
                        break;
                    }
                } else if(usb_vendor_id != -1 || usb_product_id != -1) {
                    // We are no longer even looking at USB devices
                    break;
                }

                device_stack.Push(tmp_device);
                tmp_device = tmp_device.Parent;
            }
            
            return device_stack;
        }
 
        private string name = null;
        public override string Name {
            get {
                if(name == null) {
                    Stack<Hal.Device> usb_devices = CollectUsbDeviceStack(player_device);
                    
                    while(usb_devices.Count > 0) {
                        name = GetDisplayStringProperty(usb_devices.Pop(), "info.product");
                        if(name != null) {
                            return name;
                        }
                    }
                
                    name = GetDisplayStringProperty(volume_device, "volume.label");
                    if(name == null) {
                        name = GenericName;
                    }
                }

                return name;
            }
        }
        
        private static string generic_name = Catalog.GetString("Audio Device");
        public override string GenericName {
            get {
                return generic_name;
            }
        }
        
        ulong storage_capacity = 0;
        public override ulong StorageCapacity {
            get {
                if(storage_capacity == 0) {
                    try {
                        if(volume_device.PropertyExists("volume.size")) {
                            storage_capacity = volume_device.GetPropertyUInt64("volume.size");
                        }
                    } catch {}
                }

                return storage_capacity;
            }
        }
        
        public ulong StorageFree {
            get {
                Mono.Unix.Native.Statvfs info;
                Mono.Unix.Native.Syscall.statvfs(MountPoint, out info);

                return (ulong) (info.f_bavail * info.f_bsize);
            }
        }
        
        public override ulong StorageUsed {
            get {
                return Math.Max(StorageCapacity - StorageFree, 0);
            }
        }

        
        private bool is_read_only;
        public override bool IsReadOnly {
            get {
                return is_read_only;
            }
        }
        
        public override bool IsPlaybackSupported {
            get {
                return true;
            }
        }
        
        private string mount_point = null;
        public string MountPoint {
            get {
                if(mount_point == null) {
                    mount_point =  volume_device ["volume.mount_point"];
                }

                return mount_point;
            }
        }

        private int folder_depth = -2;
        public int FolderDepth {
            get {
                if(folder_depth == -2) {
                    if(player_device.PropertyExists("portable_audio_player.folder_depth")) {
                        folder_depth = player_device.GetPropertyInteger("portable_audio_player.folder_depth");
                    } else {
                        folder_depth = -1;
                    }
                }

                return folder_depth;
            }

            protected set { folder_depth = value; }
        }

        private string write_path = null;
        public string WritePath {
            get {
                if(write_path == null) {
                    write_path = MountPoint;
                    // According to the HAL spec, the first folder listed in the audio_folders property
                    // is the folder to write files to.
                    if(AudioFolders != null && AudioFolders.Length > 0) {
                        write_path = System.IO.Path.Combine(write_path, AudioFolders[0]);
                    }
                }

                return write_path;
            }

            protected set { write_path = value; }
        }

        // The path relative to the mount point where music is stored
        private string [] audio_folders = null;
        public string [] AudioFolders {
            get {
                if(audio_folders == null) {
                    if(player_device.PropertyExists("portable_audio_player.audio_folders")) {
                        audio_folders = player_device.GetPropertyStringList("portable_audio_player.audio_folders");
                    } else {
                        audio_folders = new string [] {""};
                    }
                }

                return audio_folders;
            }

            protected set {
                audio_folders = value;
                WritePath = null;
            }
        }

        private string [] playback_formats = null;
        public string [] PlaybackFormats {
            get {
                if(playback_formats == null) {
                    if(player_device.PropertyExists("portable_audio_player.output_formats")) {
                        playback_formats = player_device.GetPropertyStringList("portable_audio_player.output_formats");
                    } else {
                        // If no playback formats are set, default to MP3 and Ogg
                        playback_formats = new string [] {"audio/mp3", "audio/ogg"};
                    }
                }

                return playback_formats;
            }

            protected set { playback_formats = value; }
        }

        protected string IsAudioPlayerPath {
            get { return Path.Combine(MountPoint, ".is_audio_player"); }
        }

    }
}
