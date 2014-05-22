//
// InternetRadioSource.cs
//
// Author:
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
using System.Collections.Generic;
using Mono.Unix;
using Gtk;

using Hyena;

using Banshee.Sources;
using Banshee.Streaming;
using Banshee.ServiceStack;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.PlaybackController;

using Banshee.Gui;
using Banshee.Sources.Gui;

namespace Banshee.InternetRadio
{
    public class InternetRadioSource : PrimarySource, IDisposable, IBasicPlaybackController
    {
        private uint ui_id;

        public InternetRadioSource () : base (Catalog.GetString ("Radio"), Catalog.GetString ("Radio"), "internet-radio", 52)
        {
            Properties.SetString ("Icon.Name", "radio");
            TypeUniqueId = "internet-radio";
            IsLocal = false;

            AfterInitialized ();

            InterfaceActionService uia_service = ServiceManager.Get<InterfaceActionService> ();
            uia_service.GlobalActions.Add (
                new ActionEntry ("AddRadioStationAction", Stock.Add,
                    Catalog.GetString ("Add Station"), null,
                    Catalog.GetString ("Add a new Internet Radio station or playlist"),
                    OnAddStation)
            );
            uia_service.GlobalActions["AddRadioStationAction"].IsImportant = false;

            ui_id = uia_service.UIManager.AddUiFromResource ("GlobalUI.xml");

            Properties.SetString ("ActiveSourceUIResource", "ActiveSourceUI.xml");
            Properties.Set<bool> ("ActiveSourceUIResourcePropagate", true);
            Properties.Set<System.Reflection.Assembly> ("ActiveSourceUIResource.Assembly", typeof(InternetRadioSource).Assembly);

            Properties.SetString ("GtkActionPath", "/InternetRadioContextMenu");

            Properties.Set<bool> ("Nereid.SourceContentsPropagate", true);
            Properties.Set<ISourceContents> ("Nereid.SourceContents", new LazyLoadSourceContents<InternetRadioSourceContents> ());

            Properties.Set<string> ("SearchEntryDescription", Catalog.GetString ("Search your stations"));
            Properties.SetString ("RemovePlayingTrackActionLabel", Catalog.GetString ("Remove From Library"));
            Properties.SetString ("DeletePlayingTrackFromDriveActionLabel", Catalog.GetString ("Delete From Drive"));
            Properties.SetString ("SelectedTracksEditorActionLabel", Catalog.GetString ("Edit Station"));
            Properties.Set<InvokeHandler> ("SelectedTracksEditorActionHandler", delegate {
                var track_actions = ServiceManager.Get<InterfaceActionService> ().TrackActions;
                var tracks = track_actions.SelectedTracks;
                if (tracks == null || tracks.Count <= 0) {
                    return;
                }

                foreach (var track in tracks) {
                    var station_track = track as DatabaseTrackInfo;
                    if (station_track != null) {
                        EditStation (station_track);
                        return;
                    }
                }
            });

            Properties.SetString ("TrackView.ColumnControllerXml", String.Format (@"
                <column-controller>
                  <!--<column modify-default=""IndicatorColumn"">
                    <renderer type=""Banshee.Podcasting.Gui.ColumnCellPodcastStatusIndicator"" />
                  </column>-->
                  <add-default column=""IndicatorColumn"" />
                  <add-default column=""GenreColumn"" />
                  <column modify-default=""GenreColumn"">
                    <visible>false</visible>
                  </column>
                  <add-default column=""TitleColumn"" />
                  <column modify-default=""TitleColumn"">
                    <title>{0}</title>
                    <long-title>{0}</long-title>
                  </column>
                  <add-default column=""ArtistColumn"" />
                  <column modify-default=""ArtistColumn"">
                    <title>{1}</title>
                    <long-title>{1}</long-title>
                  </column>
                  <add-default column=""CommentColumn"" />
                  <column modify-default=""CommentColumn"">
                    <title>{2}</title>
                    <long-title>{2}</long-title>
                  </column>
                  <add-default column=""RatingColumn"" />
                  <add-default column=""PlayCountColumn"" />
                  <add-default column=""LastPlayedColumn"" />
                  <add-default column=""LastSkippedColumn"" />
                  <add-default column=""DateAddedColumn"" />
                  <add-default column=""UriColumn"" />
                  <sort-column direction=""asc"">genre</sort-column>
                </column-controller>",
                Catalog.GetString ("Station"),
                Catalog.GetString ("Creator"),
                Catalog.GetString ("Description")
            ));

            ServiceManager.PlayerEngine.TrackIntercept += OnPlayerEngineTrackIntercept;
            //ServiceManager.PlayerEngine.ConnectEvent (OnTrackInfoUpdated, Banshee.MediaEngine.PlayerEvent.TrackInfoUpdated);

            TrackEqualHandler = (DatabaseTrackInfo a, TrackInfo b) => {
                RadioTrackInfo radio_track = b as RadioTrackInfo;
                return radio_track != null && DatabaseTrackInfo.TrackEqual (
                    radio_track.ParentTrack as DatabaseTrackInfo, a);
            };

            TrackIsPlayingHandler = ServiceManager.PlayerEngine.IsPlaying;

            var migrator = new XspfMigrator (this);
            if (migrator.Migrate () | migrator.LoadDefaults ()) {
                Reload ();
            }
        }

        public override string GetPluralItemCountString (int count)
        {
            return Catalog.GetPluralString ("{0} station", "{0} stations", count);
        }

        protected override IEnumerable<IFilterListModel> CreateFiltersFor (DatabaseSource src)
        {
            var genre_model = new DatabaseQueryFilterModel<string> (src, src.DatabaseTrackModel, ServiceManager.DbConnection,
                        Catalog.GetString ("All Genres ({0})"), src.UniqueId, Banshee.Query.BansheeQuery.GenreField, "Genre");

            if (this == src) {
                this.genre_model = genre_model;
            }

            yield return genre_model;
        }

        public override void Dispose ()
        {
            base.Dispose ();

            //ServiceManager.PlayerEngine.DisconnectEvent (OnTrackInfoUpdated);

            InterfaceActionService uia_service = ServiceManager.Get<InterfaceActionService> ();
            if (uia_service == null) {
                return;
            }

            if (ui_id > 0) {
                uia_service.UIManager.RemoveUi (ui_id);
                uia_service.GlobalActions.Remove ("AddRadioStationAction");
                ui_id = 0;
            }

            ServiceManager.PlayerEngine.TrackIntercept -= OnPlayerEngineTrackIntercept;
        }

        // TODO the idea with this is to grab and display cover art when we get updated info
        // for a radio station (eg it changes song and lets us know).  The problem is I'm not sure
        // if that info ever/usually includes the album name, and also we would probably want to mark
        // such downloaded/cached cover art as temporary.
        /*private void OnTrackInfoUpdated (Banshee.MediaEngine.PlayerEventArgs args)
        {
            RadioTrackInfo radio_track = ServiceManager.PlaybackController.CurrentTrack as RadioTrackInfo;
            if (radio_track != null) {
                Banshee.Metadata.MetadataService.Instance.Lookup (radio_track);
            }
        }*/

        private bool OnPlayerEngineTrackIntercept (TrackInfo track)
        {
            DatabaseTrackInfo station = track as DatabaseTrackInfo;
            if (station == null || station.PrimarySource != this) {
                return false;
            }

            new RadioTrackInfo (station).Play ();

            return true;
        }

        private void OnAddStation (object o, EventArgs args)
        {
            EditStation (null);
        }

        private void EditStation (DatabaseTrackInfo track)
        {
            StationEditor editor = new StationEditor (track);
            editor.Response += OnStationEditorResponse;
            editor.Show ();
        }

        private void OnStationEditorResponse (object o, ResponseArgs args)
        {
            StationEditor editor = (StationEditor)o;
            bool destroy = true;

            try {
                if (args.ResponseId == ResponseType.Ok) {
                    DatabaseTrackInfo track = editor.Track ?? new DatabaseTrackInfo ();
                    track.PrimarySource = this;
                    track.IsLive = true;

                    try {
                        track.Uri = new SafeUri (editor.StreamUri);
                    } catch {
                        destroy = false;
                        editor.ErrorMessage = Catalog.GetString ("Please provide a valid station URI");
                    }

                    if (!String.IsNullOrEmpty (editor.StationCreator)) {
                        track.ArtistName = editor.StationCreator;
                    }

                    track.Comment = editor.Description;

                    if (!String.IsNullOrEmpty (editor.Genre)) {
                        track.Genre = editor.Genre;
                    } else {
                        destroy = false;
                        editor.ErrorMessage = Catalog.GetString ("Please provide a station genre");
                    }

                    if (!String.IsNullOrEmpty (editor.StationTitle)) {
                        track.TrackTitle = editor.StationTitle;
                        track.AlbumTitle = editor.StationTitle;
                    } else {
                        destroy = false;
                        editor.ErrorMessage = Catalog.GetString ("Please provide a station title");
                    }

                    track.Rating = editor.Rating;

                    if (destroy) {
                        track.Save ();
                    }
                }
            } finally {
                if (destroy) {
                    editor.Response -= OnStationEditorResponse;
                    editor.Destroy ();

                    Reload ();
                }
            }
        }

        #region IBasicPlaybackController implementation

        public bool First ()
        {
            return false;
        }

        public bool Next (bool restart, bool changeImmediately)
        {
            /*
             * TODO: It should be technically possible to handle changeImmediately=False
             * correctly here, but the current implementation is quite hostile.
             * For the moment, just SetNextTrack (null), and go on to OpenPlay if
             * the engine isn't currently playing.
             */
            if (!changeImmediately) {
                ServiceManager.PlayerEngine.SetNextTrack ((SafeUri)null);
                if (ServiceManager.PlayerEngine.IsPlaying ()) {
                    return true;
                }
            }
            RadioTrackInfo radio_track = ServiceManager.PlaybackController.CurrentTrack as RadioTrackInfo;
            return radio_track != null && radio_track.PlayNextStream ();
        }

        public bool Previous (bool restart)
        {
            RadioTrackInfo radio_track = ServiceManager.PlaybackController.CurrentTrack as RadioTrackInfo;
            return radio_track != null && radio_track.PlayPreviousStream ();
        }

        #endregion

        public override bool AcceptsInputFromSource (Source source)
        {
            return false;
        }

        public override bool CanDeleteTracks {
            get { return false; }
        }

        public override bool ShowBrowser {
            get { return true; }
        }

        public override bool CanRename {
            get { return false; }
        }

        protected override bool HasArtistAlbum {
            get { return false; }
        }

        public override bool HasViewableTrackProperties {
            get { return false; }
        }

        public override bool HasEditableTrackProperties {
            get { return true; }
        }
    }
}
