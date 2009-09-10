//
// PlayQueueSource.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//   Alexander Kojevnikov <alexander@kojevnikov.com>
//
// Copyright (C) 2008 Novell, Inc.
// Copyright (C) 2009 Alexander Kojevnikov
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
using System.Linq;

using Mono.Unix;

using Hyena.Collections;
using Hyena.Data.Sqlite;

using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Configuration;
using Banshee.Database;
using Banshee.Gui;
using Banshee.Library;
using Banshee.MediaEngine;
using Banshee.PlaybackController;
using Banshee.Playlist;
using Banshee.Preferences;
using Banshee.ServiceStack;
using Banshee.Sources;

namespace Banshee.PlayQueue
{
    public class PlayQueueSource : PlaylistSource, IBasicPlaybackController, IPlayQueue, IDBusExportable, IDisposable
    {
        private static string special_playlist_name = "Play Queue";

        private ITrackModelSource prior_playback_source;
        private DatabaseTrackInfo current_track;
        private long offset;
        private TrackInfo prior_playback_track;
        private PlayQueueActions actions;
        private bool was_playing = false;
        protected DateTime source_set_at = DateTime.MinValue;
        private HeaderWidget header_widget;

        private SourcePage pref_page;
        private Section pref_section;

        private PlaybackShuffleMode populate_mode = (PlaybackShuffleMode) PopulateModeSchema.Get ();
        private string populate_from_name = PopulateFromSchema.Get ();
        private ITrackModelSource populate_from = null;
        private int played_songs_number = PlayedSongsNumberSchema.Get ();
        private int upcoming_songs_number = UpcomingSongsNumberSchema.Get ();
        
        public PlayQueueSource () : base (Catalog.GetString ("Play Queue"), null)
        {
            BindToDatabase ();
            TypeUniqueId = DbId.ToString ();
            Initialize ();
            AfterInitialized ();
            
            Order = 20;
            Properties.SetString ("Icon.Name", "source-playlist");
            Properties.SetString ("RemoveTracksActionLabel", Catalog.GetString ("Remove From Play Queue"));

            DatabaseTrackModel.ForcedSortQuery = "CorePlaylistEntries.ViewOrder ASC, CorePlaylistEntries.EntryID ASC";
            DatabaseTrackModel.CanReorder = true;

            ServiceManager.PlayerEngine.ConnectEvent (OnPlayerEvent);
            ServiceManager.PlaybackController.TrackStarted += OnTrackStarted;

            ServiceManager.SourceManager.AddSource (this);
            
            // TODO change this Gtk.Action code so that the actions can be removed.  And so this
            // class doesn't depend on Gtk/ThickClient.
            actions = new PlayQueueActions (this);
            
            Properties.SetString ("ActiveSourceUIResource", "ActiveSourceUI.xml");
            Properties.SetString ("GtkActionPath", "/PlayQueueContextMenu");

            // TODO listen to all primary sources, and handle transient primary sources
            ServiceManager.SourceManager.MusicLibrary.TracksChanged += HandleTracksChanged;
            ServiceManager.SourceManager.MusicLibrary.TracksDeleted += HandleTracksDeleted;
            ServiceManager.SourceManager.VideoLibrary.TracksChanged += HandleTracksChanged;
            ServiceManager.SourceManager.VideoLibrary.TracksDeleted += HandleTracksDeleted;
            
            populate_from = ServiceManager.SourceManager.Sources.FirstOrDefault (
                source => source.Name == populate_from_name) as ITrackModelSource;
            if (populate_from != null) {
                populate_from.Reload ();
            }

            TrackModel.Reloaded += HandleReloaded;

            Offset = CurrentOffsetSchema.Get ();
        }

        protected override void Initialize ()
        {
            base.Initialize ();

            InstallPreferences ();

            header_widget = new HeaderWidget (populate_mode, populate_from_name);
            header_widget.ModeChanged += delegate(object sender, ModeChangedEventArgs e) {
                populate_mode = e.Value;
                PopulateModeSchema.Set ((int) e.Value);
                UpdatePlayQueue ();
                OnUpdated ();
            };
            header_widget.SourceChanged += delegate(object sender, SourceChangedEventArgs e) {
                populate_from = e.Value;
                populate_from_name = e.Value.Name;
                PopulateFromSchema.Set (e.Value.Name);
                source_set_at = DateTime.Now;
                populate_from.Reload ();
                Refresh ();
            };
            header_widget.ShowAll ();

            Properties.Set<Gtk.Widget> ("Nereid.SourceContents.HeaderWidget", header_widget);
        }

#region IPlayQueue, IDBusExportable

        public void EnqueueUri (string uri)
        {
            EnqueueUri (uri, false);
        }

        public void EnqueueUri (string uri, bool prepend)
        {
            EnqueueId (DatabaseTrackInfo.GetTrackIdForUri (uri), prepend, false);
        }
        
        public void EnqueueTrack (TrackInfo track, bool prepend)
        {
            DatabaseTrackInfo db_track = track as DatabaseTrackInfo;
            if (db_track != null) {
                EnqueueId (db_track.TrackId, prepend, false);
            } else {
                EnqueueUri (track.Uri.AbsoluteUri, prepend);
            }
        }
        
        private void EnqueueId (int trackId, bool prepend, bool generated)
        {
            if (trackId <= 0) {
                return;
            }

            long view_order;
            if (prepend && current_track != null) {
                // We are going to prepend the track to the play queue, which means
                // adding it after the current_track. Now find the corresponding view_order.
                view_order = ServiceManager.DbConnection.Query<long> (@"
                    SELECT ViewOrder + 1
                    FROM CorePlaylistEntries
                    WHERE PlaylistID = ? AND EntryID = ?",
                    DbId, Convert.ToInt64 (current_track.CacheEntryId)
                );
            } else {
                if (generated) {
                    // view_order will point after the last track in the queue.
                    view_order = MaxViewOrder;
                }
                else {
                    // view_order will point after the last non-generated track in the queue.
                    view_order = ServiceManager.DbConnection.Query<long> (@"
                        SELECT MAX(ViewOrder) + 1
                        FROM CorePlaylistEntries
                        WHERE PlaylistID = ? AND Generated = 0",
                        DbId
                    );
                }
            }

            // Increment the order of all tracks after view_order
            ServiceManager.DbConnection.Execute (@"
                UPDATE CorePlaylistEntries
                SET ViewOrder = ViewOrder + 1
                WHERE PlaylistID = ? AND ViewOrder >= ?",
                DbId, view_order
            );

            // Add the track to the queue using the view order calculated above.
            ServiceManager.DbConnection.Execute (@"
                INSERT INTO CorePlaylistEntries
                (PlaylistID, TrackID, ViewOrder, Generated)
                VALUES (?, ?, ?, ?)",
                DbId, trackId, view_order, generated ? 1 : 0
            );

            OnTracksAdded ();
            NotifyUser ();
        }
        
        IDBusExportable IDBusExportable.Parent {
            get { return ServiceManager.SourceManager; }
        }
        
        string IService.ServiceName {
            get { return "PlayQueue"; }
        }

#endregion

        public override bool AddSelectedTracks (Source source)
        {
            if ((Parent == null || source == Parent || source.Parent == Parent) && AcceptsInputFromSource (source)) {
                DatabaseTrackListModel model = (source as ITrackModelSource).TrackModel as DatabaseTrackListModel;
                if (model == null) {
                    return false;
                }

                // Get the ViewOrder of the current_track
                long current_view_order = current_track == null ?
                    ServiceManager.DbConnection.Query<long> (@"
                        SELECT MAX(ViewOrder) + 1
                        FROM CorePlaylistEntries
                        WHERE PlaylistID = ?",
                        DbId
                    ) :
                    ServiceManager.DbConnection.Query<long> (@"
                        SELECT ViewOrder
                        FROM CorePlaylistEntries
                        WHERE PlaylistID = ? AND EntryID = ?",
                        DbId, Convert.ToInt64 (current_track.CacheEntryId)
                );

                // If the current_track is not playing, insert before it.
                int index = -1;
                if (current_track != null && !ServiceManager.PlayerEngine.IsPlaying (current_track)) {
                    current_view_order--;
                    index = TrackModel.IndexOf (current_track);
                }

                // view_order will point to the last pending non-generated track in the queue
                // or to the current_track if all tracks are generated. We want to insert tracks after it.
                long view_order = Math.Max(current_view_order, ServiceManager.DbConnection.Query<long> (@"
                    SELECT MAX(ViewOrder)
                    FROM CorePlaylistEntries
                    WHERE PlaylistID = ? AND ViewOrder > ? AND Generated = 0",
                    DbId, current_view_order
                ));

                // Add the tracks to the end of the queue.
                WithTrackSelection (model, AddTrackRange);

                // Shift generated tracks to the end of the queue.
                ServiceManager.DbConnection.Execute (@"
                    UPDATE CorePlaylistEntries
                    SET ViewOrder = ViewOrder - ? + ?
                    WHERE PlaylistID = ? AND ViewOrder > ? AND Generated = 1",
                    view_order, MaxViewOrder, DbId, view_order
                );

                OnTracksAdded ();
                OnUserNotifyUpdated ();

                // If the current_track was not playing, and there were no non-generated tracks,
                // mark the first added track as current.
                if (view_order == current_view_order && current_track != null &&
                    !ServiceManager.PlayerEngine.IsPlaying (current_track)) {
                    SetCurrentTrack (TrackModel[index] as DatabaseTrackInfo);
                }
                return true;
            }
            return false;
        }

        private void SetAsPlaybackSourceUnlessPlaying ()
        {
            if (current_track != null && ServiceManager.PlaybackController.Source != this) {
                PriorSource = ServiceManager.PlaybackController.Source;
                ServiceManager.PlaybackController.NextSource = this;
            }
        }

        public void Clear ()
        {
            ServiceManager.DbConnection.Execute (@"
                DELETE FROM CorePlaylistEntries
                WHERE PlaylistID = ?", DbId
            );
            offset = 0;
            SetCurrentTrack (null);

            if (this == ServiceManager.PlaybackController.Source && ServiceManager.PlayerEngine.IsPlaying ()) {
                ServiceManager.PlayerEngine.Close();
            }

            Reload ();
        }

        public void Dispose ()
        {
            int track_index = current_track == null ? Count : Math.Max (0, TrackModel.IndexOf (current_track));
            CurrentTrackSchema.Set (track_index);

            ServiceManager.PlayerEngine.DisconnectEvent (OnPlayerEvent);
            ServiceManager.PlaybackController.TrackStarted -= OnTrackStarted;

            if (actions != null) {
                actions.Dispose ();
            }

            UninstallPreferences ();

            Properties.Remove ("Nereid.SourceContents.HeaderWidget");

            if (header_widget != null) {
                header_widget.Destroy ();
                header_widget = null;
            }

            if (!Populate && ClearOnQuitSchema.Get ()) {
                Clear ();
            }
        }
        
        private void BindToDatabase ()
        {
            int result = ServiceManager.DbConnection.Query<int> (
                "SELECT PlaylistID FROM CorePlaylists WHERE Special = 1 AND Name = ? LIMIT 1",
                special_playlist_name
            );
            
            if (result != 0) {
                DbId = result;
            } else {
                DbId = ServiceManager.DbConnection.Execute (new HyenaSqliteCommand (@"
                    INSERT INTO CorePlaylists (PlaylistID, Name, SortColumn, SortType, Special) VALUES (NULL, ?, -1, 0, 1)
                ", special_playlist_name));
            }
        }

        protected override void OnTracksAdded ()
        {
            int old_count = Count;

            base.OnTracksAdded ();

            if (current_track == null && old_count < Count) {
                SetCurrentTrack (TrackModel[old_count] as DatabaseTrackInfo);
            }

            SetAsPlaybackSourceUnlessPlaying ();
        }

        protected override void OnTracksRemoved ()
        {
            base.OnTracksRemoved ();

            if (this == ServiceManager.PlaybackController.Source &&
                ServiceManager.PlayerEngine.IsPlaying () &&
                TrackModel.IndexOf (ServiceManager.PlayerEngine.CurrentTrack) == -1) {
                if (ServiceManager.PlayerEngine.CurrentState == PlayerState.Paused || current_track == null) {
                    ServiceManager.PlayerEngine.Close();
                } else {
                    ServiceManager.PlayerEngine.OpenPlay (current_track);
                }
            }
            UpdatePlayQueue ();
        }

        protected override void RemoveTrackRange (DatabaseTrackListModel model, RangeCollection.Range range)
        {
            base.RemoveTrackRange (model, range);

            model.Selection.UnselectRange (range.Start, range.End);

            int index = TrackModel.IndexOf (current_track);
            if (range.Start <= index && index <= range.End) {
                SetCurrentTrack (range.End + 1 < Count ? TrackModel[range.End + 1] as DatabaseTrackInfo : null);
            }
        }

        private void HandleReloaded(object sender, EventArgs e)
        {
            int track_index = CurrentTrackSchema.Get ();
            if (track_index < Count) {
                SetCurrentTrack (TrackModel[track_index] as DatabaseTrackInfo);
            }

            SetAsPlaybackSourceUnlessPlaying ();

            TrackModel.Reloaded -= HandleReloaded;
        }

        public override void ReorderSelectedTracks (int drop_row)
        {
            // If the current_track is not playing, make the first pending unselected track the current one.
            if (current_track != null && !ServiceManager.PlayerEngine.IsPlaying (current_track)) {
                int current_index = TrackModel.IndexOf (current_track);
                int new_index = -1;
                for (int index = current_index; index < TrackModel.Count; index++) {
                    if (!TrackModel.Selection.Contains (index)) {
                        new_index = index;
                        break;
                    }
                }
                if (new_index != current_index) {
                    SetCurrentTrack (new_index == -1 ? null : TrackModel[new_index] as DatabaseTrackInfo);
                }
            }

            base.ReorderSelectedTracks (drop_row);
        }

        private void OnPlayerEvent (PlayerEventArgs args)
        {
            if (args.Event == PlayerEvent.EndOfStream) {
                if (this == ServiceManager.PlaybackController.Source &&
                    TrackModel.IndexOf (current_track) == Count - 1) {
                    SetCurrentTrack (null);
                    UpdatePlayQueue ();
                    if (was_playing) {
                        ServiceManager.PlaybackController.PriorTrack = prior_playback_track;
                    } else {
                        ServiceManager.PlaybackController.StopWhenFinished = true;
                    }
                }
            } else if (args.Event == PlayerEvent.StartOfStream) {
                if (TrackModel.IndexOf (ServiceManager.PlayerEngine.CurrentTrack) != -1) {
                    SetCurrentTrack (ServiceManager.PlayerEngine.CurrentTrack as DatabaseTrackInfo);
                    SetAsPlaybackSourceUnlessPlaying ();
                    UpdatePlayQueue ();
                } else {
                    prior_playback_track = ServiceManager.PlayerEngine.CurrentTrack;
                }
            }
        }

        public override void Reload ()
        {
            enabled_cache.Clear ();
            base.Reload ();

            if (current_track == null) {
                if (this == ServiceManager.PlaybackController.Source ||
                    this == ServiceManager.PlaybackController.NextSource) {
                    ServiceManager.PlaybackController.NextSource = PriorSource;
                }
            }
        }

        protected override DatabaseTrackListModel CreateTrackModelFor (DatabaseSource src)
        {
            return new PlayQueueTrackListModel (ServiceManager.DbConnection, DatabaseTrackInfo.Provider, (PlayQueueSource) src);
        }

        bool IBasicPlaybackController.First ()
        {
            return ((IBasicPlaybackController)this).Next (false);
        }
        
        bool IBasicPlaybackController.Next (bool restart)
        {
            if (current_track != null && ServiceManager.PlayerEngine.CurrentTrack == current_track) {
                int index = TrackModel.IndexOf (current_track) + 1;
                SetCurrentTrack (index < Count ? TrackModel[index] as DatabaseTrackInfo : null);
            }
            if (current_track == null) {
                UpdatePlayQueue ();
                ServiceManager.PlaybackController.Source = PriorSource;
                if (was_playing) {
                    ServiceManager.PlaybackController.PriorTrack = prior_playback_track;
                    ServiceManager.PlaybackController.Next (restart);
                } else {
                    ServiceManager.PlayerEngine.Close ();
                }
                return true;
            }

            ServiceManager.PlayerEngine.OpenPlay (current_track);
            return true;
        }
        
        bool IBasicPlaybackController.Previous (bool restart)
        {
            if (current_track != null && ServiceManager.PlayerEngine.CurrentTrack == current_track) {
                int index = TrackModel.IndexOf (current_track);
                if (index > 0) {
                    SetCurrentTrack (TrackModel[index - 1] as DatabaseTrackInfo);
                }
                ServiceManager.PlayerEngine.OpenPlay (current_track);
            }
            return true;
        }
        
        private void UpdatePlayQueue ()
        {
            // Find the ViewOrder of the current_track.
            long view_order;
            if (current_track == null) {
                view_order = ServiceManager.DbConnection.Query<long> (@"
                    SELECT MAX(ViewOrder) + 1
                    FROM CorePlaylistEntries
                    WHERE PlaylistID = ?", DbId
                );
            }
            else {
                view_order = ServiceManager.DbConnection.Query<long> (@"
                    SELECT ViewOrder
                    FROM CorePlaylistEntries
                    WHERE PlaylistID = ? AND EntryID = ?",
                    DbId, Convert.ToInt64 (current_track.CacheEntryId)
                );
            }

            // Offset the model so that no more than played_songs_number tracks are shown before the current_track.
            Offset = played_songs_number == 0 ? view_order : ServiceManager.DbConnection.Query<long> (@"
                SELECT MIN(ViewOrder)
                FROM (
                    SELECT ViewOrder
                    FROM CorePlaylistEntries
                    WHERE PlaylistID = ? AND ViewOrder < ?
                    ORDER BY ViewOrder DESC
                    LIMIT ?
                )", DbId, view_order, played_songs_number
            );

            // Check if we need to add more tracks.
            int tracks_to_add = upcoming_songs_number -
                (current_track == null ? 0 : Count - TrackModel.IndexOf (current_track) - 1);

            // If the current track is not playing count it as well.
            if (current_track != null && !ServiceManager.PlayerEngine.IsPlaying (current_track)) {
                tracks_to_add--;
            }

            if (tracks_to_add > 0 && Populate && populate_from != null) {
                // Add songs from the selected source, skip if all tracks need to be populated.
                bool skip = tracks_to_add == upcoming_songs_number;
                for (int i = 0; i < tracks_to_add; i++) {
                    var track = populate_from.TrackModel.GetRandom (
                        source_set_at, populate_mode, false, skip && i == 0) as DatabaseTrackInfo;
                    if (track != null) {
                        track.LastPlayed = DateTime.Now;
                        // track.Save() is quite slow, update LastPlayedStamp directly in the database.
                        ServiceManager.DbConnection.Execute (@"
                            UPDATE CoreTracks
                            SET LastPlayedStamp = ?
                            WHERE TrackID = ?",
                            Hyena.DateTimeUtil.ToTimeT (track.LastPlayed), track.TrackId
                        );
                        EnqueueId (track.TrackId, false, true);
                    }
                }
                OnTracksAdded ();
                if (current_track == null && Count > 0) {
                    // If the queue was empty, make the first added track the current one.
                    SetCurrentTrack (TrackModel[0] as DatabaseTrackInfo);
                    ServiceManager.PlayerEngine.OpenPlay (current_track);
                }
            }
        }

        private readonly Dictionary<int, bool> enabled_cache = new Dictionary<int, bool> ();
        public bool IsTrackEnabled (int index)
        {
            if (!enabled_cache.ContainsKey (index)) {
                int current_index = current_track == null ? Count : TrackModel.IndexOf (current_track);
                enabled_cache.Add (index, index >= current_index);
            }
            return enabled_cache[index];
        }

        private void SetCurrentTrack (DatabaseTrackInfo track)
        {
            enabled_cache.Clear ();
            current_track = track;
        }

        public long Offset {
            get { return offset; }
            protected set {
                if (value != offset) {
                    offset = value;
                    CurrentOffsetSchema.Set ((int) offset);
                    Reload ();
                }
            }
        }

        public void Refresh ()
        {
            int index = current_track == null ? Count : TrackModel.IndexOf (current_track);

            // If the current track is not playing refresh it too.
            if (current_track != null && !ServiceManager.PlayerEngine.IsPlaying (current_track)) {
                index--;
            }

            if (index + 1 < Count) {
                // Get the ViewOrder of the current_track
                long current_view_order = current_track == null ?
                    ServiceManager.DbConnection.Query<long> (@"
                        SELECT MAX(ViewOrder) + 1
                        FROM CorePlaylistEntries
                        WHERE PlaylistID = ?",
                        DbId
                    ) :
                    ServiceManager.DbConnection.Query<long> (@"
                        SELECT ViewOrder
                        FROM CorePlaylistEntries
                        WHERE PlaylistID = ? AND EntryID = ?",
                        DbId, Convert.ToInt64 (current_track.CacheEntryId)
                );
                // Get the list of generated tracks.
                var generated = new HashSet<long> ();
                foreach(long trackID in ServiceManager.DbConnection.QueryEnumerable<long> ( @"
                    SELECT TrackID
                    FROM CorePlaylistEntries
                    WHERE PlaylistID = ? AND Generated = 1 AND ViewOrder >= ?",
                    DbId, current_view_order)) {

                    generated.Add (trackID);
                }

                // Collect the indices of all generated tracks.
                var ranges = new RangeCollection ();
                for (int i = index + 1; i < Count; i++) {
                    if (generated.Contains (((DatabaseTrackInfo)TrackModel[i]).TrackId)) {
                        ranges.Add (i);
                    }
                }

                bool removed = false;
                foreach (var range in ranges.Ranges) {
                    RemoveTrackRange (DatabaseTrackModel, range);
                    removed = true;
                }

                if (removed) {
                    OnTracksRemoved ();
                }
            }
            else if (Count == 0 || current_track == null) {
                UpdatePlayQueue ();
            }
        }

        private void OnTrackStarted(object sender, EventArgs e)
        {
            SetAsPlaybackSourceUnlessPlaying ();
        }

        public bool Populate {
            get { return populate_mode != PlaybackShuffleMode.Linear; }
        }

        private ITrackModelSource PriorSource {
            get {
                if (prior_playback_source == null || prior_playback_source == this) {
                    return (ITrackModelSource)ServiceManager.SourceManager.DefaultSource;
                }
                return prior_playback_source;
            }
            set {
                if (value == null || value == this) {
                    return;
                }
                prior_playback_source = value;
                was_playing = ServiceManager.PlayerEngine.IsPlaying ();
            }
        }

        public override bool CanRename {
            get { return false; }
        }

        public override bool CanSearch {
            get { return false; }
        }

        public override bool ShowBrowser {
            get { return false; }
        }

        protected override bool HasArtistAlbum {
            get { return false; }
        }

        public override bool ConfirmRemoveTracks {
            get { return false; }
        }

        public override bool CanRepeat {
            get { return false; }
        }

        public override bool CanShuffle {
            get { return false; }
        }

        public override bool CanUnmap {
            get { return false; }
        }

        public override string PreferencesPageId {
            get { return "play-queue"; }
        }

        private void InstallPreferences ()
        {
            pref_page = new Banshee.Preferences.SourcePage (PreferencesPageId, Name, "source-playlist", 500);

            pref_section = pref_page.Add (new Section ());
            pref_section.ShowLabel = false;
            pref_section.Add (new SchemaPreference<int> (PlayedSongsNumberSchema,
                Catalog.GetString ("Number of _played songs to show"), null, delegate {
                    played_songs_number = PlayedSongsNumberSchema.Get ();
                    UpdatePlayQueue ();
                }
            ));
            pref_section.Add (new SchemaPreference<int> (UpcomingSongsNumberSchema,
                Catalog.GetString ("Number of _upcoming songs to show"), null, delegate {
                    upcoming_songs_number = UpcomingSongsNumberSchema.Get ();
                    UpdatePlayQueue ();
                }
            ));
        }

        private void UninstallPreferences ()
        {
            pref_page.Dispose ();
            pref_page = null;
            pref_section = null;
        }

        public static readonly SchemaEntry<bool> ClearOnQuitSchema = new SchemaEntry<bool> (
            "plugins.play_queue", "clear_on_quit",
            false,
            "Clear on Quit",
            "Clear the play queue when quitting"
        );

        public static readonly SchemaEntry<int> CurrentTrackSchema = new SchemaEntry<int> (
            "plugins.play_queue", "current_track",
            0,
            "Current Track",
            "Current track in the Play Queue"
        );

        public static readonly SchemaEntry<int> CurrentOffsetSchema = new SchemaEntry<int> (
            "plugins.play_queue", "current_offset",
            0,
            "Current Offset",
            "Current offset of the Play Queue"
        );

        public static readonly SchemaEntry<int> PopulateModeSchema = new SchemaEntry<int> (
            "plugins.play_queue", "populate_mode",
            (int) PlaybackShuffleMode.Linear,
            "Play Queue population mode",
            "How (and if) the Play Queue should be randomly populated"
        );

        public static readonly SchemaEntry<string> PopulateFromSchema = new SchemaEntry<string> (
            "plugins.play_queue", "populate_from",
            ServiceManager.SourceManager.MusicLibrary.Name,
            "Source to poplulate from",
            "Name of the source to populate the the Play Queue from"
        );

        public static readonly SchemaEntry<int> PlayedSongsNumberSchema = new SchemaEntry<int> (
            "plugins.play_queue", "played_songs_number",
            10, 0, 100,
            "Played Songs Number",
            "Number of played songs to show in the Play Queue"
        );

        public static readonly SchemaEntry<int> UpcomingSongsNumberSchema = new SchemaEntry<int> (
            "plugins.play_queue", "upcoming_songs_number",
            10, 1, 100,
            "Upcoming Songs Number",
            "Number of upcoming songs to show in the Play Queue"
        );
    }
}
