/***************************************************************************
 *  DapSource.cs
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@abock.org>
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
using Mono.Unix;
using Gtk;
using Gdk;

using Banshee.Base;
using Banshee.Dap;

namespace Banshee.Sources
{
    public class DapSource : Source, IImportable, IImportSource
    {
        private Banshee.Dap.DapDevice device;
        private EventBox syncing_container;
        private Gtk.Image dap_syncing_image = new Gtk.Image();
        
        public bool NeedSync;
        
        public DapSource(Banshee.Dap.DapDevice device) : base(device.Name, 400)
        {
            this.device = device;
            device.SaveStarted += OnDapSaveStarted;
            device.SaveFinished += OnDapSaveFinished;
            device.Reactivate += OnDapReactivate;
            CanRename = device.CanSetName;
        }
        
        protected override bool UpdateName(string oldName, string newName)
        {
            if(!device.CanSetName) {
                return true;
            }
        
            if(oldName == null || !oldName.Equals(newName)) {
                device.SetName(newName);
                Name = newName;
            }
            
            return true;
        }
        
        public override void AddTrack(TrackInfo track)
        {
            device.Activate();
            
            if(track is LibraryTrackInfo) {
                device.AddTrack(track);
            }
        }
        
        public override void RemoveTrack(TrackInfo track)
        {
            device.RemoveTrack(track);
        }
        
        public void SetSourceName(string name)
        {
            Name = name;
        }
        
        private void OnDapSaveStarted(object o, EventArgs args)
        {
            if(IsSyncing) {
                if(syncing_container == null) {
                    syncing_container = new EventBox();
                    HBox syncing_box = new HBox();
                    syncing_container.Add(syncing_box);
                    syncing_box.Spacing = 20;
                    syncing_box.PackStart(dap_syncing_image, false, false, 0);
                    Label syncing_label = new Label();
                                                    
                    syncing_container.ModifyBg(StateType.Normal, new Color(0, 0, 0));
                    syncing_label.ModifyFg(StateType.Normal, new Color(160, 160, 160));
                
                    syncing_label.Markup = "<big><b>" + GLib.Markup.EscapeText(
                        Catalog.GetString("Synchronizing your Device, Please Wait...")) + "</b></big>";
                    syncing_box.PackStart(syncing_label, false, false, 0);
                    syncing_container.ShowAll ();
                }
                
                dap_syncing_image.Pixbuf = device.GetIcon(128);
                Globals.ActionManager.DapActions.Sensitive = false;
            }
            
            OnViewChanged();
        }
        
        private void OnDapSaveFinished(object o, EventArgs args)
        {
            if(!IsSyncing) {
                Globals.ActionManager.DapActions.Sensitive = true;
            }
            
            OnViewChanged();
        }
        
        private void OnDapReactivate(object o, EventArgs args)
        {
            OnViewChanged();
            OnUpdated();
        }
        
        public override void Activate()
        {
            device.Activate();
            base.Activate();
        }
        
        public override bool Unmap()
        {
            device.Eject();
            return true;
        }

        public void Import()
        {
            Import(Tracks);
        }

        public override void SourceDrop(Source source)
        {
            if (!device.IsReadOnly) {
                if (device is IPlaylistCapable && (source is AbstractPlaylistSource || source is Banshee.SmartPlaylist.SmartPlaylistSource)) {
                    DapPlaylistSource dap_playlist = null;
                    foreach (ChildSource child in Children) {
                        if (child.Name == source.Name) {
                            dap_playlist = child as DapPlaylistSource;
                            break;
                        }
                    }

                    if (dap_playlist == null) {
                        AddChildSource((device as IPlaylistCapable).AddPlaylist(source));
                    } else {
                        dap_playlist.AddTrack(source.Tracks);
                    }
                } else if (source is LibrarySource) {
                    Console.WriteLine("Dragging Library onto DAP not yet supported.");
                }
            }
        }
        
        private QueuedOperationManager import_manager;
        
        public void Import(IEnumerable<TrackInfo> tracks, PlaylistSource playlist)
        {
            if(device is IImportable) {
                (device as IImportable).Import(tracks, playlist);
                return;
            }
        
            if(playlist != null && playlist.Count == 0) {
                playlist.Rename(PlaylistUtil.GoodUniqueName(tracks));
                playlist.Commit();
            }
        
            if(import_manager == null) {
                import_manager = new QueuedOperationManager();
                import_manager.HandleActveUserEvent = false;
                import_manager.ActionMessage = Catalog.GetString("Importing");
                import_manager.UserEvent.CancelMessage = String.Format(Catalog.GetString(
                    "You are currently importing from {0}. Would you like to stop it?"), Name);
                import_manager.UserEvent.Icon = Icon;
                import_manager.UserEvent.Header = String.Format(Catalog.GetString("Copying from {0}"), Name);
                import_manager.UserEvent.Message = Catalog.GetString("Scanning...");
                import_manager.OperationRequested += OnImportOperationRequested;
                import_manager.Finished += delegate {
                    import_manager = null;
                };
            }
            
            foreach(TrackInfo track in tracks) {
                if(import_manager == null) {
                    continue;
                }
                
                if(playlist == null) {
                    import_manager.Enqueue(track);
                } else {
                    import_manager.Enqueue(new KeyValuePair<TrackInfo, PlaylistSource>(track, playlist));
                }
            }
        }
        
        public void Import(IEnumerable<TrackInfo> tracks)
        {
            Import(tracks, null);
        }
        
        private void OnImportOperationRequested(object o, QueuedOperationArgs args)
        {
            TrackInfo track = null;
            PlaylistSource playlist = null;
            
            if(args.Object is TrackInfo) {
                track = args.Object as TrackInfo;
            } else if(args.Object is KeyValuePair<TrackInfo, PlaylistSource>) {
                KeyValuePair<TrackInfo, PlaylistSource> container = 
                    (KeyValuePair<TrackInfo, PlaylistSource>)args.Object;
                track = container.Key;
                playlist = container.Value;
            }
            
            import_manager.UserEvent.Progress = import_manager.ProcessedCount / (double)import_manager.TotalCount;
            import_manager.UserEvent.Message = String.Format("{0} - {1}", track.Artist, track.Title);
            
            string from = track.Uri.LocalPath;
            string to = FileNamePattern.BuildFull(track, Path.GetExtension(from));
            
            try {
                if(File.Exists(to)) {
                    FileInfo from_info = new FileInfo(from);
                    FileInfo to_info = new FileInfo(to);
                    
                    // probably already the same file
                    if(from_info.Length == to_info.Length) {
                        try {
                            new LibraryTrackInfo(new SafeUri(to, false), track);
                        } catch {
                            // was already in the library
                        }
                        
                        return;
                    }
                }
            
                using(FileStream from_stream = new FileStream(from, FileMode.Open, FileAccess.Read)) {
                    long total_bytes = from_stream.Length;
                    long bytes_read = 0;
                    
                    using(FileStream to_stream = new FileStream(to, FileMode.Create, FileAccess.ReadWrite)) {
                        byte [] buffer = new byte[8192];
                        int chunk_bytes_read = 0;
                        
                        DateTime last_message_pump = DateTime.MinValue;
                        TimeSpan message_pump_delay = TimeSpan.FromMilliseconds(500);
                        
                        while((chunk_bytes_read = from_stream.Read(buffer, 0, buffer.Length)) > 0) {
                            to_stream.Write(buffer, 0, chunk_bytes_read);
                            bytes_read += chunk_bytes_read;
                            
                            if(DateTime.Now - last_message_pump < message_pump_delay) {
                                continue;
                            }
                            
                            if(import_manager == null || import_manager.UserEvent == null) {
                                continue;
                            }
                            
                            double tracks_processed = (double)import_manager.ProcessedCount;
                            double tracks_total = (double)import_manager.TotalCount;
                            
                            import_manager.UserEvent.Progress = (tracks_processed / tracks_total) +
                                ((bytes_read / (double)total_bytes) / tracks_total);

                            last_message_pump = DateTime.Now;
                        }
                    }
                }
                
                try {
                    LibraryTrackInfo library_track = new LibraryTrackInfo(new SafeUri(to, false), track);
                    if(playlist != null) {
                        playlist.AddTrack(library_track);
                        playlist.Commit();
                    }
                } catch {
                    // song already in library
                }
            } catch(Exception e) {
                try {
                    File.Delete(to);
                } catch {
                }
                
                if(e is QueuedOperationManager.OperationCanceledException) {
                    return;
                }
                
                args.Abort = true;
                
                LogCore.Instance.PushError(String.Format(Catalog.GetString("Cannot import track from {0}"), 
                    Name), e.Message);
                Console.Error.WriteLine(e);
            } 
        }

        public override int Count {
            get { return device.TrackCount; }
        }        
        
        public Banshee.Dap.DapDevice Device {
            get { return device; }
        }
        
        public override IEnumerable<TrackInfo> Tracks {
            get { return device.Tracks; }
        }
        
        public double DiskUsageFraction {
            get { return (double)device.StorageUsed / (double)device.StorageCapacity; }
        }

        public string DiskUsageString {
            get {
                // Translators: iPod disk usage. Each {N} is something like "100 MB"
                return String.Format(
                    Catalog.GetString("{0} of {1}"),
                    Utilities.BytesToString(device.StorageUsed),
                    Utilities.BytesToString(device.StorageCapacity));
            }
        }

        public string DiskAvailableString {
            get {
                // Translators: iPod disk usage. {0} is something like "100 MB"
                return String.Format(
                    Catalog.GetString("({0} Remaining)"),
                    Utilities.BytesToString(device.StorageCapacity - device.StorageUsed));
            }
        }
        
        public bool IsSyncing {
            get { return device.IsSyncing; }
        }

        public override void ShowPropertiesDialog()
        {
            DapPropertiesDialog properties_dialog = new DapPropertiesDialog(this);
            IconThemeUtils.SetWindowIcon(properties_dialog);
            properties_dialog.Run();
            properties_dialog.Destroy();
              
            if(properties_dialog.Edited && !Device.IsReadOnly) {
                if(properties_dialog.UpdatedName != null) {
                    Device.SetName(properties_dialog.UpdatedName);
                    SetSourceName(Device.Name);
                }
                  
                if(properties_dialog.UpdatedOwner != null) {
                    Device.SetOwner(properties_dialog.UpdatedOwner);
                }
            }
        }
        
        public override Gdk.Pixbuf Icon {
            get { return device.GetIcon(22); }
        }

        public override string UnmapIcon {
            get { return "media-eject"; }
        }

        public override string UnmapLabel {
            get { return String.Format(Catalog.GetString("Eject {0}"), GenericName); }
        }

        public override Gtk.Widget ViewWidget {
            get { return IsSyncing ? syncing_container : Device.ViewWidget; }
        }
        
        public override bool ShowPlaylistHeader {
            get { return !IsSyncing; }
        }
        
        public override bool SearchEnabled {
            get { return !IsSyncing && Device.ViewWidget == null; }
        }

        public override string GenericName {
            get { return Device != null ? Device.GenericName : Catalog.GetString("Device"); }
        }
        
        public override string SourcePropertiesLabel {
            get { return String.Format(Catalog.GetString("{0} Properties"), GenericName); }
        }
    }
}
