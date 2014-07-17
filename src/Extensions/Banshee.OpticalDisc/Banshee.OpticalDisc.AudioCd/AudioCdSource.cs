//
// AudioCdSource.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//   Alex Launi <alex.launi@canonical.com>
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
using Banshee.Base;
using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Library;
using Banshee.Collection;
using Banshee.Collection.Database;

using Gtk;
using Banshee.Gui;

namespace Banshee.OpticalDisc.AudioCd
{
    public class AudioCdSource : DiscSource, IImportSource,
        IDurationAggregator, IFileSizeAggregator
    {
        private SourceMessage query_message;

        public AudioCdSource (AudioCdService service, AudioCdDiscModel discModel)
            : base ((DiscService) service, (DiscModel) discModel, Catalog.GetString ("Audio CD"), discModel.Title, 59)
        {
            TypeUniqueId = "";
            Properties.SetString ("TrackView.ColumnControllerXml", String.Format (@"
                <column-controller>
                  <column>
                    <renderer type=""Hyena.Data.Gui.ColumnCellCheckBox"" property=""RipEnabled""/>
                  </column>
                  <add-all-defaults />
                </column-controller>
            "));

            Model.MetadataQueryStarted += OnMetadataQueryStarted;
            Model.MetadataQueryFinished += OnMetadataQueryFinished;
            Model.EnabledCountChanged += OnEnabledCountChanged;
            Model.LoadModelFromDisc ();

            SetupGui ();
        }

        public TimeSpan Duration {
            get { return Model.Duration; }
        }

        public long FileSize {
            get { return Model.FileSize; }
        }

        public new AudioCdDiscModel Model {
            get { return (AudioCdDiscModel) base.DiscModel; }
            set { base.DiscModel = value; }
        }

        public override void Dispose ()
        {
            StopPlayingDisc ();
            ClearMessages ();
            Model.MetadataQueryStarted -= OnMetadataQueryStarted;
            Model.MetadataQueryFinished -= OnMetadataQueryFinished;
            Model.EnabledCountChanged -= OnEnabledCountChanged;
            Service = null;
            Model = null;
        }

        private void OnEnabledCountChanged (object o, EventArgs args)
        {
            UpdateActions ();
        }

        private void OnMetadataQueryStarted (object o, EventArgs args)
        {
            if (query_message != null) {
                DestroyQueryMessage ();
            }

            query_message = new SourceMessage (this);
            query_message.FreezeNotify ();
            query_message.CanClose = false;
            query_message.IsSpinning = true;
            query_message.Text = Catalog.GetString ("Searching for track information...");
            query_message.ThawNotify ();

            PushMessage (query_message);
        }

        private void OnMetadataQueryFinished (object o, EventArgs args)
        {
            if (Model.Title != Name) {
                Name = Model.Title;
                OnUpdated ();
            }

            if (Model.MetadataQuerySuccess) {
                DestroyQueryMessage ();
                if (DiscIsPlaying) {
                    ServiceManager.PlayerEngine.TrackInfoUpdated ();
                }

                if (AudioCdService.AutoRip.Get ()) {
                    BeginAutoRip ();
                }

                return;
            }

            if (query_message == null) {
                return;
            }

            query_message.FreezeNotify ();
            query_message.IsSpinning = false;
            query_message.SetIconName ("dialog-error");
            query_message.Text = Catalog.GetString ("Could not fetch track information");
            query_message.CanClose = true;
            query_message.ThawNotify ();
        }

        private void DestroyQueryMessage ()
        {
            if (query_message != null) {
                RemoveMessage (query_message);
                query_message = null;
            }
        }

        private void BeginAutoRip ()
        {
            // Make sure the album isn't already in the Library
            TrackInfo track = Model[0];
            int count = ServiceManager.DbConnection.Query<int> (String.Format (
                @"SELECT Count(*) FROM CoreTracks, CoreArtists, CoreAlbums WHERE
                    CoreTracks.PrimarySourceID = ? AND
                    CoreTracks.ArtistID = CoreArtists.ArtistID AND
                    CoreTracks.AlbumID = CoreAlbums.AlbumID AND
                    CoreArtists.Name = ? AND CoreAlbums.Title = ? AND ({0} = ? OR {0} = 0)",
                    Banshee.Query.BansheeQuery.DiscNumberField.Column),
                    ServiceManager.SourceManager.MusicLibrary.DbId,
                    track.ArtistName, track.AlbumTitle, track.DiscNumber
            );

            if (count > 0) {
                SetStatus (Catalog.GetString ("Automatic import off since this album is already in the Music Library."), true, false, null);
                return;
            }

            Log.DebugFormat ("Beginning auto rip of {0}", Name);
            ImportDisc ();
        }

        internal void ImportDisc ()
        {
            AudioCdRipper ripper = null;

            try {
                if (AudioCdRipper.Supported) {
                    ripper = new AudioCdRipper (this);
                    ripper.Finished += OnRipperFinished;
                    ripper.Start ();
                }
            } catch (Exception e) {
                Log.Error (Catalog.GetString ("Could not import CD"), e.Message, true);
                Log.Error (e);

                if (ripper != null) {
                    ripper.Dispose ();
                }
            }
        }

        private void OnRipperFinished (object o, EventArgs args)
        {
            if (AudioCdService.EjectAfterRipped.Get ()) {
                Unmap ();
            }
        }

        internal void DuplicateDisc ()
        {
            try {
                AudioCdDuplicator.Duplicate (Model);
            } catch (Exception e) {
                Hyena.Log.Error (Catalog.GetString ("Could not duplicate audio CD"), e.Message, true);
                Log.Error (e);
            }
        }

        internal void LockAllTracks ()
        {
            StopPlayingDisc ();

            foreach (AudioCdTrackInfo track in Model) {
                track.CanPlay = false;
            }

            Model.NotifyUpdated ();
        }

        internal void UnlockAllTracks ()
        {
            foreach (AudioCdTrackInfo track in Model) {
                track.CanPlay = true;
            }

            Model.NotifyUpdated ();
        }

        internal void UnlockTrack (AudioCdTrackInfo track)
        {
            track.CanPlay = true;
            Model.NotifyUpdated ();
        }

#region DiscSource

        public override bool CanRepeat {
            get { return true;}
        }

        public override bool CanShuffle {
            get { return true; }
        }

#endregion

#region Source Overrides

        public override int Count {
            get { return Model.Count; }
        }

        public override string PreferencesPageId {
            get { return "audio-cd"; }
        }

        public override bool HasEditableTrackProperties {
            get { return true; }
        }

        public override bool HasViewableTrackProperties {
            get { return true; }
        }

#endregion

#region GUI/ThickClient

        private bool actions_loaded = false;

        private void SetupGui ()
        {
            Properties.SetStringList ("Icon.Name", "media-optical-cd-audio", "media-optical-cd", "media-optical", "gnome-dev-cdrom-audio", "source-cd-audio");
            Properties.SetString ("SourcePreferencesActionLabel", Catalog.GetString ("Audio CD Preferences"));
            Properties.SetString ("UnmapSourceActionLabel", Catalog.GetString ("Eject Disc"));
            Properties.SetString ("UnmapSourceActionIconName", "media-eject");
            Properties.SetString ("ActiveSourceUIResource", "ActiveSourceUI.xml");
            Properties.SetString ("GtkActionPath", "/AudioCdContextMenu");

            actions_loaded = true;

            UpdateActions ();
        }

        private void UpdateActions ()
        {
            InterfaceActionService uia_service = ServiceManager.Get<InterfaceActionService> ();
            if (uia_service == null) {
                return;
            }

            Gtk.Action rip_action = uia_service.GlobalActions["RipDiscAction"];
            if (rip_action != null) {
                string title = Model.Title;
                int max_title_length = 20;
                title = title.Length > max_title_length
                    ? String.Format ("{0}\u2026", title.Substring (0, max_title_length).Trim ())
                    : title;
                rip_action.Label = String.Format (Catalog.GetString ("Import \u201f{0}\u201d"), title);
                rip_action.ShortLabel = Catalog.GetString ("Import CD");
                rip_action.IconName = "media-import-audio-cd";
                rip_action.Sensitive = AudioCdRipper.Supported && Model.EnabledCount > 0;
            }

            Gtk.Action duplicate_action = uia_service.GlobalActions["DuplicateDiscAction"];
            if (duplicate_action != null) {
                duplicate_action.IconName = "media-optical";
                duplicate_action.Visible = AudioCdDuplicator.Supported;
            }
        }

        protected override void OnUpdated ()
        {
            if (actions_loaded) {
                UpdateActions ();
            }

            base.OnUpdated ();
        }

#endregion

#region IImportSource

        void IImportSource.Import ()
        {
            ImportDisc ();
        }

        string [] IImportSource.IconNames {
            get { return Properties.GetStringList ("Icon.Name"); }
        }

        bool IImportSource.CanImport {
            get { return true; }
        }

        int IImportSource.SortOrder {
            get { return -10; }
        }

        string IImportSource.ImportLabel {
            get { return null; }
        }

#endregion

    }
}
