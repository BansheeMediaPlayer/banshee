//
// DapSource.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
//   Aaron Bockover <abockover@novell.com>
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
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Mono.Unix;

using Hyena;
using Hyena.Data.Sqlite;

using Banshee.Base;
using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Collection;
using Banshee.Playlist;
using Banshee.Collection.Database;
using Banshee.Hardware;
using Banshee.MediaEngine;
using Banshee.MediaProfiles;
using Banshee.Preferences;

using Banshee.Dap.Gui;

namespace Banshee.Dap
{
    public abstract class DapSource : RemovableSource, IDisposable
    {
        private bool flushed = false;
        private DapSync sync;
        private DapInfoBar dap_info_bar;
        private Page page;
        // private DapPropertiesDisplay dap_properties_display;

        private IDevice device;
        internal IDevice Device {
            get { return device; }
        }

        private string addin_id;
        internal string AddinId {
            get { return addin_id; }
            set { addin_id = value; }
        }

        private MediaGroupSource music_group_source;
        protected MediaGroupSource MusicGroupSource {
            get { return music_group_source; }
        }

        private MediaGroupSource video_group_source;
        protected MediaGroupSource VideoGroupSource {
            get { return video_group_source; }
        }

        private MediaGroupSource podcast_group_source;
        protected MediaGroupSource PodcastGroupSource {
            get { return podcast_group_source; }
        }

        protected DapSource ()
        {
        }

        public virtual void DeviceInitialize (IDevice device)
        {
            this.device = device;
            TypeUniqueId = device.Uuid;
        }

        public override void Dispose ()
        {
            Flush ();

            PurgeTemporaryPlaylists ();
            PurgeTracks ();

            if (dap_info_bar != null) {
                var info_bar = dap_info_bar;
                ThreadAssist.ProxyToMain (info_bar.Destroy);
                dap_info_bar = null;
            }

            Properties.Remove ("Nereid.SourceContents.FooterWidget");

            /*Properties.Remove ("Nereid.SourceContents");
            dap_properties_display.Destroy ();
            dap_properties_display = null;*/

            if (sync != null)
                sync.Dispose ();
        }

        private void PurgeTemporaryPlaylists ()
        {
            ServiceManager.DbConnection.Execute (new HyenaSqliteCommand (@"
                BEGIN TRANSACTION;
                    DELETE FROM CoreSmartPlaylistEntries WHERE SmartPlaylistID IN
                        (SELECT SmartPlaylistID FROM CoreSmartPlaylists WHERE PrimarySourceID = ?);
                    DELETE FROM CoreSmartPlaylists WHERE PrimarySourceID = ?;
                COMMIT TRANSACTION",
                DbId, DbId
            ));

            ServiceManager.DbConnection.Execute (new HyenaSqliteCommand (@"
                BEGIN TRANSACTION;
                    DELETE FROM CorePlaylistEntries WHERE PlaylistID IN
                        (SELECT PlaylistID FROM CorePlaylists WHERE PrimarySourceID = ?);
                    DELETE FROM CorePlaylists WHERE PrimarySourceID = ?;
                COMMIT TRANSACTION",
                DbId, DbId
            ));
        }

        internal void RaiseUpdated ()
        {
            OnUpdated ();
        }

        public virtual void SyncPlaylists ()
        {
        }

        public override void Rename (string newName)
        {
            Name = newName;
            base.Rename (newName);
        }

        private bool supports_video = true;
        public bool SupportsVideo {
            get { return supports_video; }
            protected set { supports_video = value; }
        }

        private bool supports_podcasts = true;
        public bool SupportsPodcasts {
            get { return supports_podcasts; }
            protected set { supports_podcasts = value; }
        }

#region Source

        protected override void Initialize ()
        {
            PurgeTemporaryPlaylists ();

            base.Initialize ();

            Expanded = true;
            Properties.SetStringList ("Icon.Name", GetIconNames ());
            Properties.Set<string> ("SourcePropertiesActionLabel", Catalog.GetString ("Device Properties"));
            Properties.Set<OpenPropertiesDelegate> ("SourceProperties.GuiHandler", delegate {
                new DapPropertiesDialog (this).RunDialog ();
            });

            Properties.Set<bool> ("Nereid.SourceContents.HeaderVisible", false);
            Properties.Set<System.Reflection.Assembly> ("ActiveSourceUIResource.Assembly", System.Reflection.Assembly.GetExecutingAssembly ());
            Properties.SetString ("ActiveSourceUIResource", "ActiveSourceUI.xml");

            sync = new DapSync (this);

            /*dap_properties_display = new DapPropertiesDisplay (this);
            Properties.Set<Banshee.Sources.Gui.ISourceContents> ("Nereid.SourceContents", dap_properties_display);*/

            if (String.IsNullOrEmpty (GenericName)) {
                GenericName = Catalog.GetString ("Media Player");
            }

            if (String.IsNullOrEmpty (Name)) {
                Name = device.Name;
            }

            AddDapProperty (Catalog.GetString ("Product"), device.Product);
            AddDapProperty (Catalog.GetString ("Vendor"), device.Vendor);

            if (acceptable_mimetypes == null) {
                acceptable_mimetypes = HasMediaCapabilities ? MediaCapabilities.PlaybackMimeTypes : null;
                if (acceptable_mimetypes == null || acceptable_mimetypes.Length == 0) {
                    acceptable_mimetypes = new string [] { "taglib/mp3" };
                }
            }

            AddChildSource (music_group_source = new MusicGroupSource (this));
            // We want the group sources to be on top of the list, with Music first
            music_group_source.Order = -30;

            if (SupportsVideo) {
                video_group_source = new VideoGroupSource (this);
                video_group_source.Order = -20;
            }

            if (SupportsPodcasts) {
                podcast_group_source = new PodcastGroupSource (this);
                podcast_group_source.Order = -10;
            }

            BuildPreferences ();

            ThreadAssist.ProxyToMain (delegate {
                dap_info_bar = new DapInfoBar (this);
                Properties.Set<Gtk.Widget> ("Nereid.SourceContents.FooterWidget", dap_info_bar);

                Properties.Set<Banshee.Sources.Gui.ISourceContents> ("Nereid.SourceContents", new DapContent (this));
            });
        }

        private void BuildPreferences ()
        {
            page = new Page ();
            Section main_section = new Section ();
            main_section.Order = -1;

            space_for_data = CreateSchema<long> ("space_for_data", 0, "How much space, in bytes, to reserve for data on the device.", "");
            main_section.Add (space_for_data);
            page.Add (main_section);

            foreach (Section section in sync.PreferenceSections) {
                page.Add (section);
            }
        }

        // Force to zero so that count doesn't show up
        public override int Count {
            get { return 0; }
        }

        public override bool HasProperties {
            get { return true; }
        }

        public override bool CanSearch {
            get { return false; }
        }

        public override void SetStatus (string message, bool can_close, bool is_spinning, string icon_name)
        {
            base.SetStatus (message, can_close, is_spinning, icon_name);
            foreach (Source child in Children) {
                child.SetStatus (message, can_close, is_spinning, icon_name);
            }
        }

        public override void HideStatus ()
        {
            base.HideStatus ();
            foreach (Source child in Children) {
                child.HideStatus ();
            }
        }

#endregion

#region Track Management/Syncing

        public void LoadDeviceContents ()
        {
            ThreadPool.QueueUserWorkItem (ThreadedLoadDeviceContents);
        }

        private void ThreadedLoadDeviceContents (object state)
        {
            try {
                PurgeTracks ();
                SetStatus (String.Format (Catalog.GetString ("Loading {0}"), Name), false);
                LoadFromDevice ();
                HideStatus ();

                sync.DapLoaded ();
                sync.CalculateSync ();
                if (sync.AutoSync) {
                    sync.Sync ();
                }
            } catch (Exception e) {
                Log.Exception (e);
            }
        }

        public void RemovePlaylists ()
        {
            // First remove any playlists on the device
            List<Source> children = new List<Source> (sync.Dap.Children);
            foreach (Source child in children) {
                if (child is AbstractPlaylistSource && !(child is MediaGroupSource)) {
                    (child as IUnmapableSource).Unmap ();
                }
            }
        }

        protected virtual void LoadFromDevice ()
        {
        }

        protected override void Eject ()
        {
            Flush ();
        }

        private void Flush ()
        {
            if (!flushed) {
                flushed = true;
                if (!Sync.Enabled) {
                    // If sync isn't enabled, then make sure we've written saved our playlists
                    // Track transfers happen immediately, but playlists saves don't
                    SyncPlaylists ();
                }
            }
        }

        HyenaSqliteCommand track_on_dap_query = new HyenaSqliteCommand (
            "SELECT TrackID FROM CoreTracks WHERE PrimarySourceID = ? AND MetadataHash = ? LIMIT 1");

        private void AttemptToAddTrackToDevice (DatabaseTrackInfo track, SafeUri fromUri)
        {
            if (!Banshee.IO.File.Exists (fromUri)) {
                throw new FileNotFoundException (Catalog.GetString ("File not found"), fromUri.ToString ());
            }

            // Ensure there's enough space
            if (BytesAvailable - Banshee.IO.File.GetSize (fromUri) >= 0) {
                // Ensure it's not already on the device
                if (ServiceManager.DbConnection.Query<int> (track_on_dap_query, DbId, track.MetadataHash) == 0) {
                    AddTrackToDevice (track, fromUri);
                }
            }
        }

        protected abstract void AddTrackToDevice (DatabaseTrackInfo track, SafeUri fromUri);

        protected bool TrackNeedsTranscoding (TrackInfo track)
        {
            string extension = ServiceManager.MediaProfileManager.GetExtensionForMimeType (track.MimeType);
            if (extension != null) {
                foreach (string mimetype in AcceptableMimeTypes) {
                    if (extension == ServiceManager.MediaProfileManager.GetExtensionForMimeType (mimetype)) {
                        return false;
                    }
                }
            }

            return true;
        }

        public struct DapProperty {
            public string Name;
            public string Value;
            public DapProperty (string k, string v) { Name = k; Value = v; }
        }

        private List<DapProperty> dap_properties = new List<DapProperty> ();
        protected void AddDapProperty (string key, string val)
        {
            dap_properties.Add (new DapProperty (key, val));
        }

        protected void AddYesNoDapProperty (string key, bool val)
        {
            AddDapProperty (key, val ? Catalog.GetString ("Yes") : Catalog.GetString ("No"));
        }

        public IEnumerable<DapProperty> DapProperties {
            get { return dap_properties; }
        }

        protected override void AddTrackAndIncrementCount (DatabaseTrackInfo track)
        {
            if (!TrackNeedsTranscoding (track)) {
                AttemptToAddTrackToDevice (track, track.Uri);
                IncrementAddedTracks ();
                return;
            }

            // If it's a video and needs transcoding, we don't support that yet
            // TODO have preferred profiles for Audio and Video separately
            if (PreferredConfiguration == null || (track.MediaAttributes & TrackMediaAttributes.VideoStream) != 0) {
                string format = System.IO.Path.GetExtension (track.Uri.LocalPath);
                format = String.IsNullOrEmpty (format) ? Catalog.GetString ("Unknown") : format.Substring (1);
                throw new ApplicationException (String.Format (Catalog.GetString (
                    "The {0} format is not supported by the device, and no converter was found to convert it"), format));
            }

            TranscoderService transcoder = ServiceManager.Get<TranscoderService> ();
            if (transcoder == null) {
                throw new ApplicationException (Catalog.GetString (
                    "File format conversion support is not available"));
            }

            transcoder.Enqueue (track, PreferredConfiguration, OnTrackTranscoded, OnTrackTranscodeCancelled, OnTrackTranscodeError);
        }

        private void OnTrackTranscoded (TrackInfo track, SafeUri outputUri)
        {
            AddTrackJob.Status = String.Format ("{0} - {1}", track.ArtistName, track.TrackTitle);

            try {
                AttemptToAddTrackToDevice ((DatabaseTrackInfo)track, outputUri);
            } catch (Exception e) {
                Log.Exception (e);
            }

            IncrementAddedTracks ();
        }

        private void OnTrackTranscodeCancelled ()
        {
            IncrementAddedTracks ();
        }

        private void OnTrackTranscodeError (TrackInfo track)
        {
            ErrorSource.AddMessage (Catalog.GetString ("Error converting file"), track.Uri.ToString ());
            IncrementAddedTracks ();
        }

#endregion

#region Device Properties

        protected virtual string [] GetIconNames ()
        {
            string vendor = device.Vendor;
            string product = device.Product;

            vendor = vendor != null ? vendor.Trim () : null;
            product = product != null ? product.Trim () : null;

            if (!String.IsNullOrEmpty (vendor) && !String.IsNullOrEmpty (product)) {
                return new string [] {
                    String.Format ("multimedia-player-{0}-{1}", vendor, product).Replace (' ', '-').ToLower (),
                    FallbackIcon
                };
            } else {
                return new string [] { FallbackIcon };
            }
        }

        public static string FallbackIcon {
            get { return "multimedia-player"; }
        }

        protected virtual bool HasMediaCapabilities {
            get { return MediaCapabilities != null; }
        }

        protected virtual IDeviceMediaCapabilities MediaCapabilities {
            get { return device.MediaCapabilities; }
        }

        public Page Preferences {
            get { return page; }
        }

        public DapSync Sync {
            get { return sync; }
        }

        private ProfileConfiguration preferred_config;
        private ProfileConfiguration PreferredConfiguration {
            get {
                if (preferred_config != null) {
                    return preferred_config;
                }

                MediaProfileManager manager = ServiceManager.MediaProfileManager;
                if (manager == null) {
                    return null;
                }

                preferred_config = manager.GetActiveProfileConfiguration (UniqueId, acceptable_mimetypes);
                return preferred_config;
            }
        }

        internal protected virtual bool CanHandleDeviceCommand (DeviceCommand command)
        {
            return false;
        }

        private string [] acceptable_mimetypes;
        public string [] AcceptableMimeTypes {
            get { return acceptable_mimetypes; }
            protected set { acceptable_mimetypes = value; }
        }

        public long BytesVideo {
            get { return VideoGroupSource == null ? 0 : VideoGroupSource.BytesUsed; }
        }

        public long BytesMusic {
            get { return MusicGroupSource == null ? 0 : MusicGroupSource.BytesUsed; }
        }

        public long BytesData {
            get { return BytesUsed - BytesVideo - BytesMusic; }
        }

        public long BytesReserved {
            get { return space_for_data.Get (); }
            set { space_for_data.Set (value); }
        }

        public override long BytesAvailable {
            get { return BytesCapacity - BytesUsed - Math.Max (0, BytesReserved - BytesData); }
        }

        public override bool CanRemoveTracks {
            get { return base.CanRemoveTracks; }
        }

        public override bool CanDeleteTracks {
            get { return base.CanDeleteTracks; }
        }

        public override bool CanAddTracks {
            get { return base.CanAddTracks; }
        }

        public override bool PlaylistsReadOnly {
            get { return IsReadOnly; }
        }

        private Banshee.Configuration.SchemaEntry<long> space_for_data;
#endregion

    }
}
