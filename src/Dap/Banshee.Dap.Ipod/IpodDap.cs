/***************************************************************************
 *  IpodDap.cs
 *
 *  Copyright (C) 2005-2007 Novell, Inc.
 *  Written by Aaron Bockvoer <abockover@novell.com>
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
using System.Reflection;
using System.Diagnostics;
using Mono.Unix;
using Gtk;
using Hal;
using IPod;
using IPod.HalClient;

using Banshee.Base;
using Banshee.Dap;
using Banshee.Widgets;
using Banshee.Metadata;

public static class PluginModuleEntry
{
    public static Type [] GetTypes()
    {
        return new Type [] {
            typeof(Banshee.Dap.Ipod.IpodDap)
        };
    }
}

namespace Banshee.Dap.Ipod
{
    [DapProperties(DapType = DapType.NonGeneric, PipelineName="Ipod")]
    [SupportedCodec(CodecType.Mp3)]
    [SupportedCodec(CodecType.Mp4)]
    public sealed class IpodDap : DapDevice
    {
        private HalDevice device;
        private Hal.Device hal_device;
        private bool database_supported;
        private UnsupportedDatabaseView db_unsupported_container;
        private bool metadata_provider_initialized = false;
        private string name_path;
        
        internal string NamePath {
            get { return name_path; }
        }
    
        public override InitializeResult Initialize(Hal.Device halDevice)
        {
            if(!metadata_provider_initialized) {
                MetadataService.Instance.AddProvider(0, new IpodMetadataProvider());
                metadata_provider_initialized = true;
            }
            
            hal_device = halDevice;
            
            if(!hal_device.PropertyExists("block.device") || 
                !hal_device.PropertyExists("block.is_volume") ||
                !hal_device.GetPropertyBoolean("block.is_volume") ||
                hal_device.Parent["portable_audio_player.type"] != "ipod") {
                return InitializeResult.Invalid;
            } else if(!hal_device.GetPropertyBoolean("volume.is_mounted")) {
                return WaitForVolumeMount(hal_device);
            }
            
            if(LoadIpod() == InitializeResult.Invalid) {
                return InitializeResult.Invalid;
            }
            
            base.Initialize(halDevice);
            
            if(device.ModelInfo.IsUnknown) {
                InstallProperty(Catalog.GetString("Model"), Catalog.GetString("Unknown"));
            } else {
                string device_class = device.ModelInfo.DeviceClass;
                InstallProperty(Catalog.GetString("Model"), String.Format("{0}{1}, {2}G", Char.ToUpper(device_class[0]), 
                    device_class.Substring(1), device.ModelInfo.Generation));
            }
            
            InstallProperty(Catalog.GetString("Advertised Capacity"), device.ModelInfo.AdvertisedCapacity);
                
            if(device.ProductionInfo.Year > 0) {
            	InstallProperty(Catalog.GetString("Manufactured In"), device.ProductionInfo.DisplayDate);
           	}
           	
            InstallProperty("Serial Number", device.ProductionInfo.SerialNumber);
            InstallProperty("Firmware Version", device.FirmwareVersion);
            InstallProperty("Database Version", device.TrackDatabase.Version.ToString());
            
            if(device.ShouldAskIfUnknown) {
                GLib.Timeout.Add(5000, AskAboutUnknown);
            }

            ReloadDatabase(false);
            CanCancelSave = false;
            
            if(Globals.Debugging) {
                device.Dump();
            }
            
            return InitializeResult.Valid;
        }
        
        private InitializeResult LoadIpod()
        {
            try {
                device = new HalDevice(hal_device.Volume);
                name_path = Path.Combine(Path.GetDirectoryName(device.TrackDatabasePath), "BansheeIPodName");
                
                if(File.Exists(device.TrackDatabasePath)) { 
                    device.LoadTrackDatabase();
                } else {
                    int count = CountMusicFiles();
                    Console.WriteLine("Found {0} files in /iPod_Control/Music", count);
                    if(CountMusicFiles() > 5) {
                        throw new DatabaseReadException("No database, but found a lot of music files");
                    }
                }
                database_supported = true;
            } catch(DatabaseReadException) {
                device.LoadTrackDatabase(true);
                database_supported = false;
            } catch(Exception e) {
                Console.WriteLine(e);
                return InitializeResult.Invalid;
            }
            
            return InitializeResult.Valid;
        }
        
        private int CountMusicFiles()
        {
            try {
                int file_count = 0;
                
                DirectoryInfo m_dir = new DirectoryInfo(Path.Combine(device.ControlPath, "Music"));
                foreach(DirectoryInfo f_dir in m_dir.GetDirectories()) {
                    file_count += f_dir.GetFiles().Length;
                }
                
                return file_count;
            } catch {
                return 0;
            }
        }
        
        private bool AskAboutUnknown()
        {
            HigMessageDialog dialog = new HigMessageDialog(null, Gtk.DialogFlags.Modal,
                Gtk.MessageType.Warning, Gtk.ButtonsType.None,
                Catalog.GetString("Your iPod could not be identified"),
                Catalog.GetString("Please consider submitting information about your iPod " +
                    "to the Banshee Project so your iPod may be more fully identified in the future.\n"));
        
            CheckButton do_not_ask = new CheckButton(Catalog.GetString("Do not ask me again"));
            do_not_ask.Show();
            dialog.LabelVBox.PackStart(do_not_ask, false, false, 0);
            
            dialog.AddButton("gtk-cancel", Gtk.ResponseType.Cancel, false);
            dialog.AddButton(Catalog.GetString("Go to Web Site"), Gtk.ResponseType.Ok, false);
            
            try {
                if(dialog.Run() == (int)ResponseType.Ok) {
                    do_not_ask.Active = true;
                    Banshee.Web.Browser.Open(device.UnknownIpodUrl);
                }
            } finally {
                dialog.Destroy();
            }
            
            if(do_not_ask.Active) {
                device.DoNotAskIfUnknown();
            }
            
            return false;
        }
        
        public override void AddTrack(TrackInfo track)
        {
            AddTrack(track, false);
        }

        private void AddTrack(TrackInfo track, bool loading)
        {
            if(track == null || (IsReadOnly && !loading)) {
                return;
			}
			
            TrackInfo new_track = (track is IpodDapTrackInfo)
            	? track
            	: new IpodDapTrackInfo(track, device.TrackDatabase);
            	
            // FIXME: only add a new track if we don't have it already

            if(new_track != null) {
                tracks.Add(new_track);
                OnTrackAdded(new_track);
            }
        }

        protected override void OnTrackRemoved(TrackInfo track)
        {
            if(!(track is IpodDapTrackInfo)) {
                return;
            }
            
            try {
                IpodDapTrackInfo ipod_track = (IpodDapTrackInfo)track;
                device.TrackDatabase.RemoveTrack(ipod_track.Track);
            } catch(Exception) {
            }
        }
        
        private void ReloadDatabase(bool refresh)
        {
            bool previous_database_supported = database_supported;
            
            ClearTracks(false);
            
            if(refresh) {
                device.TrackDatabase.Reload();
            }
            
            if(database_supported || (device.HasTrackDatabase && device.ModelInfo.DeviceClass == "shuffle")) {
                foreach(Track track in device.TrackDatabase.Tracks) {
                    IpodDapTrackInfo ti = new IpodDapTrackInfo(track);
                    AddTrack(ti, true);
                }
            } else {
                BuildDatabaseUnsupportedWidget();
            }
            
            if(previous_database_supported != database_supported) {
                OnPropertiesChanged();
            }
        }
        
        private delegate void AsyncEjectHandler(Hal.Device device);
        
        private static void AsyncEject(Hal.Device device)
        {
            try {
                LogCore.Instance.PushDebug("Ejecting iPod", device.Udi);
                device.Volume.Eject();
                LogCore.Instance.PushDebug("Finished Ejecting iPod", device.Udi);
            } catch(Exception e) {
                LogCore.Instance.PushError(Catalog.GetString("Could not eject iPod"), e.Message);
            }
        }
        
        public override void Eject()
        {
            UnmapPlayback(typeof(IpodDapTrackInfo));
            base.Eject();
            new AsyncEjectHandler(AsyncEject).BeginInvoke(hal_device, null, null);
        }
        
        public override void SetName(string name)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(name_path));
            
            using(StreamWriter writer = new StreamWriter(File.Open(name_path, FileMode.Create), System.Text.Encoding.Unicode)) {
                writer.Write(name);
            }
            
            device.Name = name;
        }
        
        public override void Synchronize()
        {
            UpdateSaveProgress(
                Catalog.GetString("Synchronizing iPod"), 
                Catalog.GetString("Pre-processing tracks"),
                0.0);
            
            foreach(IpodDapTrackInfo track in Tracks) {
                if(track.Track == null) {
                    CommitTrackToDevice(track);
                } else {
                    track.Track.Uri = new Uri(track.Uri.AbsoluteUri);
                }
            }
            
            device.TrackDatabase.SaveProgressChanged += delegate(object o, TrackSaveProgressArgs args)
            {
                double progress = args.CurrentTrack == null ? 0.0 : args.TotalProgress;
                string message = args.CurrentTrack == null 
                    ? Catalog.GetString("Waiting for Media")
                    : args.CurrentTrack.Artist + " - " + args.CurrentTrack.Title;
                    
                UpdateSaveProgress(Catalog.GetString("Synchronizing iPod"), message, progress);
            };

            try {
                device.Save();
                try {
                    File.Delete(name_path);
                } catch {
                }
            } catch(Exception e) {
                Console.Error.WriteLine (e);
                LogCore.Instance.PushError(Catalog.GetString("Failed to synchronize iPod"), e.Message);
            } finally {
                ReloadDatabase(true);
                FinishSave();
            }
        }
        
        private void CommitTrackToDevice(IpodDapTrackInfo ti)
        {
            Track track = device.TrackDatabase.CreateTrack();

            try {
                track.Uri = new Uri(ti.Uri.AbsoluteUri);
            } catch {
                device.TrackDatabase.RemoveTrack (track);
                return;
            }
        
            if(ti.Album != null) {
                track.Album = ti.Album;
            }
            
            if(ti.Artist != null) {
                track.Artist = ti.Artist;
            }
            
            if(ti.Title != null) {
                track.Title = ti.Title;
            }
            
            if(ti.Genre != null) {
                track.Genre = ti.Genre;
            }
            
            track.Duration = ti.Duration;
            track.TrackNumber = (int)ti.TrackNumber;
            track.TotalTracks = (int)ti.TrackCount;
            track.Year = (int)ti.Year;
            track.LastPlayed = ti.LastPlayed;
            
            switch(ti.Rating) {
                case 1: track.Rating = TrackRating.Zero; break;
                case 2: track.Rating = TrackRating.Two; break;
                case 3: track.Rating = TrackRating.Three; break;
                case 4: track.Rating = TrackRating.Four; break;
                case 5: track.Rating = TrackRating.Five; break;
                default: track.Rating = TrackRating.Zero; break;
            }
            
            if(track.Artist == null) {
                track.Artist = String.Empty;
            }
            
            if(track.Album == null) {
                track.Album = String.Empty;
            }
            
            if(track.Title == null) {
                track.Title = String.Empty;
            }
            
            if(track.Genre == null) {
                track.Genre = String.Empty;
            }

            SetCoverArt(track, ti.CoverArtFileName);
        }
        
        internal void SetCoverArt(Track track, string path)
        {
            if(path == null || !File.Exists(path)) {
                return;
            }
            
            try {
                Gdk.Pixbuf pixbuf = new Gdk.Pixbuf(path);
                if(pixbuf != null) {
                    SetCoverArt(track, ArtworkUsage.Cover, pixbuf);
                    pixbuf.Dispose();
                }
            } catch(Exception e) {
                Console.Error.WriteLine("Failed to set cover art from {0}: {1}", path, e);
            }
        }

        private void SetCoverArt(Track track, ArtworkUsage usage, Gdk.Pixbuf pixbuf)
        {
            foreach(ArtworkFormat format in device.LookupArtworkFormats(usage)) {
                if(!track.HasCoverArt(format)) {
                    track.SetCoverArt(format, ArtworkHelpers.ToBytes(format, pixbuf));
                }
            }
        }
        
        private Dictionary<int, Gdk.Pixbuf> icon_cache = new Dictionary<int, Gdk.Pixbuf>();
        
        public override Gdk.Pixbuf GetIcon(int size)
        {
        	Gdk.Pixbuf icon = null;
        	
        	if(icon_cache.ContainsKey(size)) {
        		icon = icon_cache[size];
        	}
        	
        	if(icon == null) {
        		icon = LookupIcon(size);
        	} else {
        		return icon;
        	}
        	
        	if(icon == null) {
        		return base.GetIcon(size);
        	}
        	
        	icon_cache.Add(size, icon);
        	return icon;
        }
        
        private Gdk.Pixbuf LookupIcon(int size)
        {
            string prefix = "multimedia-player-";
            string id = null;
            string fallback_id = null;

            Gdk.Pixbuf icon = IconThemeUtils.LoadIcon(device.ModelInfo.IconName, size);
            if(icon != null) {
            	return icon;
           	}
           	
           	switch(device.ModelInfo.DeviceClass) {
           		case "grayscale": id = "ipod-standard-monochrome"; break;
           		case "color": id="ipod-standard-color"; break;
           		case "mini": 
           			id = String.Format("ipod-mini-{0}", device.ModelInfo.ShellColor);
           			fallback_id = "ipod-mini-silver";
           			break;
           		case "shuffle": 
           			id = String.Format("ipod-shuffle-{0}", device.ModelInfo.ShellColor);
           			fallback_id = "ipod-shuffle";
           			break;
           		case "nano":
           		case "nano3":
           			id = String.Format("ipod-nano-{0}", device.ModelInfo.ShellColor);
           			fallback_id = "ipod-nano-white";
           			break;
           		case "video":
           			id = String.Format("ipod-video-{0}", device.ModelInfo.ShellColor);
           			fallback_id = "ipod-video-white";
           			break;
           		case "classic":
           		case "touch":
           		case "phone":
           		default:
           			id = "ipod-standard-color";
           			break;
           	}
           	
            return IconThemeUtils.LoadIcon(prefix + id, size) ??
            	IconThemeUtils.LoadIcon(prefix + fallback_id, size);
        }
        
        private void BuildDatabaseUnsupportedWidget()
        {
            db_unsupported_container = new UnsupportedDatabaseView(this);
            db_unsupported_container.Show();
            db_unsupported_container.Refresh += delegate {
                LoadIpod();
                ReloadDatabase(false);
                OnReactivate();
            };
        }
        
        public override string Name {
            get {
                string name = null;
                
                if(File.Exists(name_path)) {
                    using(StreamReader reader = new StreamReader(name_path, System.Text.Encoding.Unicode)) {
                        name = reader.ReadLine();
                    }
                }
                
                if(String.IsNullOrEmpty(name)) {
                    name = device.Name;
                }
                    
                if(!String.IsNullOrEmpty(name)) {
                    return name;
                } else if(hal_device.PropertyExists("volume.label")) {
                    return hal_device["volume.label"];
                } else if(hal_device.PropertyExists("info.product")) {
                    return hal_device["info.product"];
                }
                
                return "iPod";
            }
        }
        
        public override ulong StorageCapacity {
            get { return device.VolumeInfo.Size; }
        }
        
        public override ulong StorageUsed {
            get { return device.VolumeInfo.SpaceUsed; }
        }
        
        public override bool IsReadOnly {
            get { return device.VolumeInfo.IsMountedReadOnly; }
        }
        
        public override bool IsPlaybackSupported {
            get { return true; }
        }
        
        public override string GenericName {
            get { return "iPod"; }
        }
        
        public override Gtk.Widget ViewWidget {
            get { return !database_supported ? db_unsupported_container : null; }
        }
        
        internal IPod.Device Device {
            get { return device; }
        }
    }
}
