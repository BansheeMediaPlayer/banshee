/***************************************************************************
 *  Dap.cs
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

using Banshee.Gui;
using Banshee.Base;
using Banshee.Widgets;
using Banshee.Sources;
using Banshee.Plugins;
using Banshee.AudioProfiles;

namespace Banshee.Dap
{    
    public abstract class DapDevice : IPlugin, IEnumerable<TrackInfo>, IEnumerable
    {
        public class Property
        {
            private string name;
            private string val;
            
            public string Name {
                get { return name; }
                internal set { name = value; }
            }
            
            public string Value {
                get { return val; }
                internal set { val = value; }
            }
        }
        
        public class PropertyTable : IEnumerable
        {
            private ArrayList properties = new ArrayList();
            
            private Property Find(string name)
            {
                foreach(Property property in properties) {
                    if(property.Name == name) {
                        return property;
                    }
                }
                
                return null;
            }
            
            public void Add(string name, string value)
            {
                if(value == null || value.Trim() == String.Empty) {
                    return;
                }
                
                Property property = Find(name);
                if(property != null) {
                    property.Value = value;
                    return;
                } 
                
                property = new Property();
                property.Name = name;
                property.Value = value;
            
                properties.Add(property);
            }
            
            public string this [string name] {
                get {
                    Property property = Find(name);
                    return property == null ? null : property.Value;
                }
            }
            
            public IEnumerator GetEnumerator()
            {
                foreach(Property property in properties) {
                    yield return property;
                }
            }
        }
        
        private static uint current_uid = 1;
        private static uint NextUid {
            get { return current_uid++; }
        }
        
        private uint uid;
        private PropertyTable properties = new PropertyTable();
        private ActiveUserEvent save_report_event;
        private bool is_syncing = false;
        private bool can_cancel_save = true;
        private DapProperties type_properties;

        protected List<TrackInfo> tracks = new List<TrackInfo>(); 
        
        public event DapTrackListUpdatedHandler TrackAdded;
        public event DapTrackListUpdatedHandler TrackRemoved;
        public event EventHandler TracksCleared;
        public event EventHandler PropertiesChanged;
        public event EventHandler Ejected;
        public event EventHandler SaveStarted;
        public event EventHandler SaveFinished;
        public event EventHandler Reactivate;
        
        public Hal.Device HalDevice;
        private DapSource source;
        
        public virtual InitializeResult Initialize(Hal.Device halDevice)
        {
            Attribute [] dap_attrs = Attribute.GetCustomAttributes(GetType(), typeof(DapProperties));
            if(dap_attrs != null && dap_attrs.Length >= 1) {
                type_properties = dap_attrs[0] as DapProperties;
            }
            
            uid = NextUid;
            
            source = new DapSource(this);
            SourceManager.AddSource(source);
            
            return InitializeResult.Valid;
        }
        
        protected InitializeResult WaitForVolumeMount(Hal.Device halDevice)
        {
            DapCore.QueueWaitForVolumeMount(halDevice);
            return InitializeResult.WaitForVolumeMount;
        }
        
        public uint Uid {
            get { return uid; }
        }

        public DapSource Source {
            get { return source; }
        }
        
        IEnumerator IEnumerable.GetEnumerator()
        {
            return tracks.GetEnumerator();
        }
        
        public IEnumerator<TrackInfo> GetEnumerator()
        {
            return tracks.GetEnumerator();
        }
        
        protected void OnPropertiesChanged()
        {
            EventHandler handler = PropertiesChanged;
            if(handler != null) {
                handler(this, new EventArgs());
            }
        }
        
        protected void OnReactivate()
        {
            EventHandler handler = Reactivate;
            if(handler != null) {
                handler(this, new EventArgs());
            }
        }
        
        protected void InstallProperty(string name, string value)
        {
            properties.Add(name, value);
            OnPropertiesChanged();
        }
        
        public virtual void AddTrack(TrackInfo track)
        {
            if(track == null) {
                return;
            } 
            
            tracks.Add(track);
            OnTrackAdded(track);
        }
        
        public void RemoveTrack(TrackInfo track)
        {
            tracks.Remove(track);
            OnTrackRemoved(track);
            
            DapTrackListUpdatedHandler handler = TrackRemoved;
            if(handler != null) {
                handler(this, new DapTrackListUpdatedArgs(track));
            }
        }
        
        public virtual void Dispose()
        {
            if(source != null) {
                SourceManager.RemoveSource(source);
            }
        }
        
        public void ClearTracks()
        {
            ClearTracks(true);
        }
        
        protected void ClearTracks(bool notify)
        {
            tracks.Clear();
            if(notify) {
                OnTracksCleared();
                EventHandler handler = TracksCleared;
                if(handler != null) {
                    handler(this, new EventArgs());
                }
            }
        }
        
        protected virtual void OnTrackAdded (TrackInfo track)
        {
            DapTrackListUpdatedHandler handler = TrackAdded;
            if(handler != null) {
                handler(this, new DapTrackListUpdatedArgs(track));
            }
        }
        
        protected virtual void OnTrackRemoved(TrackInfo track)
        {
        }
        
        protected virtual void OnTracksCleared()
        {
        }
        
        public virtual void Activate()
        {
        }

        public virtual void Eject()
        {
            EventHandler handler = Ejected;
            if(handler != null) {
                handler(this, new EventArgs());
            }
        }
        
        private string ToLower(string str)
        {
            return str == null ? null : str.ToLower();
        }
        
        private bool TrackCompare(TrackInfo a, TrackInfo b)
        {
            return ToLower(a.Title) == ToLower(b.Title) && 
                ToLower(a.Album) == ToLower(b.Album) &&
                ToLower(a.Artist) == ToLower(b.Artist) &&
                a.Year == b.Year &&
                a.TrackNumber == b.TrackNumber;
        }
        
        protected bool TrackExistsInList(TrackInfo track, ICollection<TrackInfo> list)
        {
            try {
                foreach(TrackInfo track_b in list) {
                    if(TrackCompare(track, track_b)) {
                        return true;
                    }
                }
            } catch(Exception) {
            }
            
            return false;
        }

        public void Save(ICollection<TrackInfo> library)
        {
            Queue remove_queue = new Queue();
            
            foreach(TrackInfo ti in Tracks) {
                if(TrackExistsInList(ti, library)) {
                    continue;
                }
                
                remove_queue.Enqueue(ti);
            }
            
            while(remove_queue.Count > 0) {
                RemoveTrack(remove_queue.Dequeue() as TrackInfo);
            }
            
            foreach(TrackInfo ti in library) {
                if(TrackExistsInList(ti, Tracks) || ti.Uri == null) {
                    continue;
                }
                
                AddTrack(ti);
            }
            
            Save();
        }
        
        public void Save()
        {
            is_syncing = true;
            
            EventHandler handler = SaveStarted;
            if(handler != null) {
                handler(this, new EventArgs());
            }
            
            string aue_name = Name == null || Name == String.Empty ? Catalog.GetString("Device") : Name; 
            string aue_title = String.Format(Catalog.GetString("Synchronizing {0}"), aue_name); 
                
            save_report_event = new ActiveUserEvent(aue_title);
            save_report_event.Header = aue_title;
            save_report_event.Message = Catalog.GetString("Waiting for transcoder...");
            save_report_event.Icon = GetIcon(22);
            save_report_event.CanCancel = can_cancel_save;

            ThreadAssist.Spawn(Transcode);
        }
        
        protected bool ShouldCancelSave {
            get { return save_report_event.IsCancelRequested; }
        }
        
        protected bool CanCancelSave {
            set { can_cancel_save = value; }
        }
        
        protected void UpdateSaveProgress(string header, string message, double progress)
        {
            save_report_event.Header = header;
            save_report_event.Message = message;
            save_report_event.Progress = progress;
        }
        
        protected void FinishSave()
        {
            is_syncing = false;
            
            ThreadAssist.ProxyToMain(delegate {
                save_report_event.Dispose();
                save_report_event = null;
                
                EventHandler handler = SaveFinished;
                if(handler != null) {
                    handler(this, new EventArgs());
                }
            });
        }

        private BatchTranscoder encoder = null;

        private void Transcode()
        {
            Profile profile = Globals.AudioProfileManager.GetConfiguredActiveProfile(ID, SupportedPlaybackMimeTypes);
            
            Queue remove_queue = new Queue();
            
            foreach(TrackInfo track in Tracks) {
                if(track == null || track.Uri == null || !track.Uri.IsFile) {
                    continue;
                }
            
                string cached_filename = GetCachedSongFileName(track.Uri);
                
                if(cached_filename == null) {
                    if(profile == null) {
                        remove_queue.Enqueue(track);
                        continue;
                    }

                    SafeUri old_uri = track.Uri;
                    try {
                        track.Uri = ConvertSongFileName(track.Uri, profile.OutputFileExtension);
                    } catch {
                        continue;
                    }

                    if(encoder == null) {
                        encoder = new BatchTranscoder(profile, type_properties.PipelineName);
                        encoder.FileFinished += OnFileEncodeComplete;
                        encoder.BatchFinished += OnFileEncodeBatchFinished;
                        encoder.Canceled += OnFileEncodeCanceled;
                    }
                    
                    encoder.AddTrack(old_uri, track.Uri);
                } else {
                    if(System.IO.File.Exists(cached_filename)) {
                        track.Uri = new SafeUri(cached_filename);
                    } else {
                        remove_queue.Enqueue(track);
                    }
                }
            }
            
            while(remove_queue.Count > 0) {
                RemoveTrack(remove_queue.Dequeue() as TrackInfo);
            }
            
            if(encoder == null) {
                save_report_event.Message = Catalog.GetString("Processing...");
                Synchronize();
            } else {
                encoder.Start();
            }
        }
        
        private bool encoder_canceled = false;
        
        private void OnFileEncodeCanceled(object o, EventArgs args)
        {
            encoder_canceled = true;
        }
        
        private void OnFileEncodeComplete(object o, FileCompleteArgs args)
        {
        }
        
        private void OnFileEncodeBatchFinished(object o, EventArgs args)
        {
            if(!encoder_canceled) {
                save_report_event.Message = Catalog.GetString("Processing...");
                
                BatchTranscoder encoder = o as BatchTranscoder;
                if(encoder.ErrorCount > 0) {
                    ThreadAssist.ProxyToMain(delegate {
                        HandleTranscodeErrors(encoder);
                    });
                } else {
                    ChainSynchronize();
                }
            } else {
                FinishSave();
            }
        }
        
        private void HandleTranscodeErrors(BatchTranscoder encoder)
        {
            ErrorListDialog dialog = new ErrorListDialog();
            dialog.IconNameStock = Gtk.Stock.DialogError;
            dialog.Header = Catalog.GetString("Could not encode some files");
            dialog.Message = Catalog.GetString(
                "Some files could not be encoded to the proper format. " +
                "They will not be saved to the device if you continue."
            );
            
            dialog.AddStockButton(Gtk.Stock.Cancel, Gtk.ResponseType.Cancel);
            dialog.AddButton(Catalog.GetString("Continue synchronizing"), Gtk.ResponseType.Ok);
            
            foreach(BatchTranscoder.QueueItem item in encoder.ErrorList) {
                if(item.Source is TrackInfo) {
                    TrackInfo track = item.Source as TrackInfo;
                    dialog.AppendString(String.Format("{0} - {1}", track.Artist, track.Title));
                } else if(item.Source is SafeUri) {
                    SafeUri uri = item.Source as SafeUri;
                    dialog.AppendString(System.IO.Path.GetFileName(uri.LocalPath));
                } else {
                    dialog.AppendString(item.Source.ToString());
                }
            }
            
            try {
                if(dialog.Run() == Gtk.ResponseType.Ok) {
                    ChainSynchronize();
                } else {
                    FinishSave();
                }
            } finally {
                dialog.Destroy();
            }   
        }
        
        private void ChainSynchronize()
        {
            if(ThreadAssist.InMainThread) {
                ThreadAssist.Spawn(Synchronize);
            } else {
                Synchronize();
            }
        }
        
        private string [] supported_extensions = null;
        protected string [] SupportedExtensions {
            get {        
                if(supported_extensions != null) {
                    return supported_extensions;
                }
            
                Attribute [] attrs = Attribute.GetCustomAttributes(GetType(), typeof(SupportedCodec));
                ArrayList extensions = new ArrayList();
                
                foreach(SupportedCodec codec in attrs) {
                    foreach(string extension in CodecType.GetExtensions(codec.CodecType)) {
                        extensions.Add(extension);
                    }
                }
                
                supported_extensions = extensions.ToArray(typeof(string)) as string [];
                return supported_extensions;
            }

            set {
                supported_extensions = value;
            }
        }
        
        protected string PipelineCodecFilter {
            get {
                string filter = String.Empty;
                for(int i = 0, n = SupportedExtensions.Length; i < n; i++) {
                    filter += String.Format("{0}{1}", SupportedExtensions[i], (i < n - 1) ? "," : "");
                }
                return filter;
            }
        }
        
        private bool ValidSongFormat(string filename)
        {
            string ext = Path.GetExtension(filename).ToLower().Trim();

            foreach(string vext in SupportedExtensions) {
                if(ext == "." + vext) {
                    return true;
                }
            }
            
            return false;
        }
        
        private string GetCachedSongFileName(SafeUri uri)
        {
            string filename = uri.LocalPath;
        
            if(ValidSongFormat(filename)) {
                return filename;
            }
                
            string path = PathUtil.MakeFileNameKey(uri);
            string dir = Path.GetDirectoryName(path);
            string file = Path.GetFileNameWithoutExtension(filename);
            
            foreach(string vext in SupportedExtensions) {
                string newfile = dir + Path.DirectorySeparatorChar + ".banshee-dap-" + file + "." + vext;
                
                if(File.Exists(newfile)) {
                    return newfile;
                }
            }
            
            foreach(string vext in SupportedExtensions) {
                string newfile = path + "." + vext;
                
                if(File.Exists(newfile)) {
                    return newfile;
                }
            }   
                 
            return null;
        }
        
        private SafeUri ConvertSongFileName(SafeUri uri, string newext)
        {
            string filename = uri.LocalPath;
            
            string path = PathUtil.MakeFileNameKey(uri);
            string dir = Path.GetDirectoryName(path);
            string file = Path.GetFileNameWithoutExtension(filename);
            
            return new SafeUri(dir + Path.DirectorySeparatorChar 
                + ".banshee-dap-" + file + "." + newext);
        }

        public virtual Gdk.Pixbuf GetIcon(int size)
        {
            Gdk.Pixbuf pixbuf = IconThemeUtils.LoadIcon("multimedia-player", size);
            if(pixbuf == null) {
                return IconThemeUtils.LoadIcon("gnome-dev-ipod", size);
            }
            
            return pixbuf;
        }
        
        public virtual void SetName(string name)
        {
        }
        
        public virtual void SetOwner(string owner)
        {
        }
        
        public PropertyTable Properties {
            get { return properties; }
        }
        
        public TrackInfo this [int index] {
            get { return tracks[index]; }
        }
        
        public int TrackCount { 
            get { return tracks.Count; }
        }
        
        public IList<TrackInfo> Tracks {
            get { return tracks; }
        }
        
        public bool IsSyncing {
            get { return is_syncing; }
        }
        
        public virtual string GenericName {
            get { return "DAP"; }
        }
        
        public string HalUdi {
            get { return HalDevice.Udi; }
        }
        
        public bool CanSetName {
            get { return ReflectionUtil.IsVirtualMethodImplemented(GetType(), "SetName"); }
        }
        
        public bool CanSetOwner {
            get { return ReflectionUtil.IsVirtualMethodImplemented(GetType(), "SetOwner"); }
        }
        
        public virtual bool CanSynchronize { 
            get { return true; }
        }
        
        public virtual string Owner {
            get { return Catalog.GetString("Unknown"); }
        }
        
        public virtual bool ShowOwner {
            get { return false; }
        }
        
        public virtual Gtk.Widget ViewWidget {
            get { return null; }
        }
        
        private string [] supported_playback_mime_types;
        public string [] SupportedPlaybackMimeTypes {
            get {
                if(supported_playback_mime_types == null) {
                    List<string> types = new List<string>();
                    
                    foreach(object attr in GetType().GetCustomAttributes(true)) {
                        if(attr is SupportedCodec) {
                            foreach(string mimetype in (attr as SupportedCodec).MimeTypes) {
                                types.Add(mimetype);
                            }
                        }
                    }
                    
                    supported_playback_mime_types = types.ToArray();
                }
                
                return supported_playback_mime_types;
            }

            protected set {
                supported_playback_mime_types = value;
            }
        }
        
        private string id;
        public string ID { 
            get { 
                if(id == null) {
                    string [] parts = HalUdi.Split('/');
                    id = parts[parts.Length - 1];
                }
                
                return id;
            }
        }
        
        public abstract void Synchronize();
        public abstract string Name { get; }
        public abstract ulong StorageCapacity { get; }
        public abstract ulong StorageUsed { get; }
        public abstract bool IsReadOnly { get; }
        public abstract bool IsPlaybackSupported { get; }
    }
}
