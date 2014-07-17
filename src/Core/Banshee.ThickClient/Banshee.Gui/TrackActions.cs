//
// TrackActions.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2007-2008 Novell, Inc.
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
using Hyena.Widgets;

using Banshee.Query;
using Banshee.Sources;
using Banshee.Library;
using Banshee.Playlist;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.ServiceStack;
using Banshee.MediaEngine;
using Banshee.Widgets;
using Banshee.Gui;
using Banshee.Gui.Widgets;
using Hyena.Data;

namespace Banshee.Gui
{
    public class TrackActions : BansheeActionGroup
    {
        private RatingActionProxy selected_tracks_rating_proxy;
        private RatingActionProxy playing_track_rating_proxy;

        private bool chosen_from_playing_track_submenu;

        private static readonly string [] require_selection_actions = new string [] {
            // Selected Track(s) >
            "SelectedTracksAction", "AddSelectedTracksToPlaylistAction", "RateSelectedTracksAction",
            "RemoveSelectedTracksAction", "RemoveTracksFromLibraryAction", "OpenSelectedTracksFolderAction",
            "DeleteSelectedTracksFromDriveAction", "SelectedTracksPropertiesAction",

            // Others >
            "SelectNoneAction", "PlayTrack"
        };

        private static readonly string [] disable_for_filter_actions = new string [] {
            "SelectAllAction", "SelectNoneAction", "SearchMenuAction",
            // FIXME should be able to do this, just need to implement it
            "RateSelectedTracksAction"
        };

        private Hyena.Collections.Selection filter_selection = new Hyena.Collections.Selection ();

        private bool filter_focused;
        public bool FilterFocused {
            get { return filter_focused; }
            set {
                if (filter_focused == value)
                    return;

                filter_focused = value;
                if (value) {
                    Selection = filter_selection;
                    SuppressSelectActions ();
                } else {
                    Selection = current_source.TrackModel.Selection;
                    UnsuppressSelectActions ();
                }

                UpdateActions ();
                OnSelectionChanged ();
            }
        }

        public event EventHandler SelectionChanged;

        public Hyena.Collections.Selection Selection { get; private set; }

        public ModelSelection<TrackInfo> SelectedTracks {
            get {
                return FilterFocused
                    ? new ModelSelection<TrackInfo> (current_source.TrackModel, Selection)
                    : current_source.TrackModel.SelectedItems;
            }
        }

        public TrackActions () : base ("Track")
        {
            Add (new ActionEntry [] {

                /*
                 * Playing Track ActionsEntries
                 */

                new ActionEntry ("PlayingTrackAction", null,
                    Catalog.GetString ("Playing Track"), "",
                    Catalog.GetString ("Options for playing track"),
                    (o, e) => { ResetRating (); }),

                new ActionEntry ("AddPlayingTrackToPlaylistAction", null,
                    Catalog.GetString ("Add _to Playlist"), "",
                    Catalog.GetString ("Append playing items to playlist or create new playlist from playing track"),
                    OnAddPlayingTrackToPlaylistMenu),

                new ActionEntry ("RatePlayingTrackAction", null,
                    String.Empty, null, null, OnRatePlayingTrack),

                new ActionEntry ("PlayingTrackEditorAction", Stock.Edit,
                    Catalog.GetString ("_Edit Track Information"), "E",
                    Catalog.GetString ("Edit information on playing track"), OnPlayingTrackEditor),

                new ActionEntry ("RemovePlayingTrackAction", Stock.Remove,
                    Catalog.GetString ("_Remove"), "",
                    Catalog.GetString ("Remove playing track from this source"), OnRemovePlayingTrack),

                new ActionEntry ("DeletePlayingTrackFromDriveAction", null,
                    Catalog.GetString ("_Delete From Drive"), "",
                    Catalog.GetString ("Permanently delete playing item from medium"), OnDeletePlayingTrackFromDrive),

                new ActionEntry ("OpenPlayingTrackFolderAction", null,
                    Catalog.GetString ("_Open Containing Folder"), "",
                    Catalog.GetString ("Open the folder that contains playing item"), OnOpenPlayingTrackFolder),

                new ActionEntry ("PlayingTrackPropertiesAction", Stock.Properties,
                    Catalog.GetString ("Properties"), "",
                    Catalog.GetString ("View information on playing track"), OnPlayingTrackProperties),

                /*
                 * Selected Track(s) ActionEntries
                 */

                new ActionEntry ("SelectedTracksAction", null,
                    Catalog.GetString ("Selected Track(s)"), "",
                    Catalog.GetString ("Options for selected track(s)"),
                    (o, e) => { ResetRating (); }),

                new ActionEntry ("AddSelectedTracksToPlaylistAction", null,
                    Catalog.GetString ("Add _to Playlist"), "",
                    Catalog.GetString ("Append selected items to playlist or create new playlist from selection"),
                    OnAddSelectedTracksToPlaylistMenu),

                new ActionEntry ("RateSelectedTracksAction", null,
                    String.Empty, null, null, OnRateSelectedTracks),

                new ActionEntry ("SelectedTracksEditorAction", Stock.Edit,
                    Catalog.GetString ("_Edit Track Information"), "",
                    Catalog.GetString ("Edit information on selected tracks"), OnSelectedTracksEditor),

                new ActionEntry ("RemoveSelectedTracksAction", Stock.Remove,
                    Catalog.GetString ("_Remove"), "Delete",
                    Catalog.GetString ("Remove selected track(s) from this source"), OnRemoveSelectedTracks),

                new ActionEntry ("RemoveTracksFromLibraryAction", null,
                    Catalog.GetString ("Remove From _Library"), "",
                    Catalog.GetString ("Remove selected track(s) from library"), OnRemoveTracksFromLibrary),

                new ActionEntry ("DeleteSelectedTracksFromDriveAction", null,
                    Catalog.GetString ("_Delete From Drive"), "",
                    Catalog.GetString ("Permanently delete selected item(s) from medium"), OnDeleteSelectedTracksFromDrive),

                new ActionEntry ("OpenSelectedTracksFolderAction", null,
                    Catalog.GetString ("_Open Containing Folder"), "",
                    Catalog.GetString ("Open the folder that contains the selected item"), OnOpenSelectedTracksFolder),

                new ActionEntry ("SelectedTracksPropertiesAction", Stock.Properties,
                    Catalog.GetString ("Properties"), "",
                    Catalog.GetString ("View information on selected tracks"), OnSelectedTracksProperties),

                /*
                 * Others
                 */

                new ActionEntry ("AddToNewPlaylistAction", Stock.New,
                    Catalog.GetString ("New Playlist"), null,
                    Catalog.GetString ("Create new playlist from selected tracks"),
                    OnAddToNewPlaylist),

                new ActionEntry("SelectAllAction", null,
                    Catalog.GetString("Select _All"), "<control>A",
                    Catalog.GetString("Select all tracks"), OnSelectAll),

                new ActionEntry("SelectNoneAction", null,
                    Catalog.GetString("Select _None"), "<control><shift>A",
                    Catalog.GetString("Unselect all tracks"), OnSelectNone),

                new ActionEntry("TrackContextMenuAction", null,
                    String.Empty, null, null, OnTrackContextMenu),

                new ActionEntry ("PlayTrack", null,
                    Catalog.GetString ("_Play"), "",
                    Catalog.GetString ("Play the selected item"), OnPlayTrack),

                new ActionEntry ("SearchMenuAction", Stock.Find,
                    Catalog.GetString ("_Search"), null,
                    Catalog.GetString ("Search for items matching certain criteria"), null),

                new ActionEntry ("SearchForSameAlbumAction", null,
                    Catalog.GetString ("By Matching _Album"), "",
                    Catalog.GetString ("Search all songs of this album"), OnSearchForSameAlbum),

                new ActionEntry ("SearchForSameArtistAction", null,
                    Catalog.GetString ("By Matching A_rtist"), "",
                    Catalog.GetString ("Search all songs of this artist"), OnSearchForSameArtist),
            });

            Actions.UIManager.ActionsChanged += HandleActionsChanged;

            Actions.GlobalActions["EditMenuAction"].Activated += HandleEditMenuActivated;
            ServiceManager.SourceManager.ActiveSourceChanged += HandleActiveSourceChanged;
            ServiceManager.PlayerEngine.ConnectEvent (OnPlayerEvent, PlayerEvent.StateChange);

            this["AddPlayingTrackToPlaylistAction"].HideIfEmpty   = false;
            this["AddSelectedTracksToPlaylistAction"].HideIfEmpty = false;
            this["PlayTrack"].StockId = Gtk.Stock.MediaPlay;
        }

#region State Event Handlers

        private ITrackModelSource current_source;
        private void HandleActiveSourceChanged (SourceEventArgs args)
        {
            FilterFocused = false;

            if (current_source != null && current_source.TrackModel != null) {
                current_source.TrackModel.Reloaded -= OnReloaded;
                current_source.TrackModel.Selection.Changed -= HandleSelectionChanged;
                current_source = null;
            }

            ITrackModelSource new_source = ActiveSource as ITrackModelSource;
            if (new_source != null) {
                new_source.TrackModel.Selection.Changed += HandleSelectionChanged;
                new_source.TrackModel.Reloaded += OnReloaded;
                current_source = new_source;
                Selection = new_source.TrackModel.Selection;
            }

            ThreadAssist.ProxyToMain (UpdateActions);
        }

        private void OnReloaded (object sender, EventArgs args)
        {
            ThreadAssist.ProxyToMain (delegate {
                UpdateActions ();
            });
        }

        private void OnPlayerEvent (PlayerEventArgs args)
        {
            ThreadAssist.ProxyToMain (() => {
                UpdateActions ();
            });
        }

        private void HandleActionsChanged (object sender, EventArgs args)
        {
            if (Actions.UIManager.GetAction ("/MainMenu/EditMenu/SelectedTracks") != null &&
                Actions.UIManager.GetAction ("/MainMenu/EditMenu/PlayingTrack") != null) {

                selected_tracks_rating_proxy = new RatingActionProxy (Actions.UIManager, this["RateSelectedTracksAction"]);
                playing_track_rating_proxy = new RatingActionProxy (Actions.UIManager, this["RatePlayingTrackAction"]);

                playing_track_rating_proxy.AddPath ("/MainMenu/EditMenu/PlayingTrack", "AddToPlaylist");
                selected_tracks_rating_proxy.AddPath ("/MainMenu/EditMenu/SelectedTracks", "AddToPlaylist");
                selected_tracks_rating_proxy.AddPath ("/TrackContextMenu", "AddToPlaylist");

                Actions.UIManager.ActionsChanged -= HandleActionsChanged;
            }
        }

        private void HandleSelectionChanged (object sender, EventArgs args)
        {
            ThreadAssist.ProxyToMain (delegate {
                OnSelectionChanged ();
                UpdateActions ();
            });
        }

        private void HandleEditMenuActivated (object sender, EventArgs args)
        {
            // inside the "Edit" menu it's a bit redundant to have a label that starts as "Edit Track..."
            this["PlayingTrackEditorAction"].Label = Catalog.GetString ("Track _Information");
            this["SelectedTracksEditorAction"].Label = Catalog.GetString ("Track _Information");
            if (Selection.Count > 1) {
                this ["SelectedTracksAction"].Label = Catalog.GetString ("Selected Tracks");
            } else {
                this ["SelectedTracksAction"].Label = Catalog.GetString ("Selected Track");
            }
        }

        private void OnSelectionChanged ()
        {
            EventHandler handler = SelectionChanged;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }

#endregion

#region Utility Methods

        private bool select_actions_suppressed = false;
        private void SuppressSelectActions ()
        {
            if (!select_actions_suppressed) {
                this ["SelectAllAction"].DisconnectAccelerator ();
                this ["SelectNoneAction"].DisconnectAccelerator ();
                select_actions_suppressed = true;
            }
        }

        private void UnsuppressSelectActions ()
        {
            if (select_actions_suppressed) {
                this ["SelectAllAction"].ConnectAccelerator ();
                this ["SelectNoneAction"].ConnectAccelerator ();
                select_actions_suppressed = false;
            }
        }

        public void UpdateActions ()
        {
            Source source = ServiceManager.SourceManager.ActiveSource;
            if (source == null) {
                Sensitive = Visible = false;
                return;
            }

            bool in_database = source is DatabaseSource;
            PrimarySource primary_source = (source as PrimarySource) ?? (source.Parent as PrimarySource);

            var track_source = source as ITrackModelSource;
            if (track_source != null) {
                if (FilterFocused) {
                    if (Selection == filter_selection) {
                        filter_selection.MaxIndex = track_source.TrackModel.Selection.MaxIndex;
                        filter_selection.Clear (false);
                        filter_selection.SelectAll ();
                    } else {
                        Log.Error (new Exception ("Filter focused, but selection is not filter selection!"));
                    }
                } else {
                    UpdateActions (true, true, disable_for_filter_actions);
                }

                var selection = Selection;
                var playing_track = ServiceManager.PlayerEngine.CurrentTrack;
                var playback_source = (DatabaseSource)ServiceManager.PlaybackController.Source;
                int count = selection.Count;
                Sensitive = Visible = true;
                bool has_selection = count > 0;
                bool has_single_selection = count == 1;
                bool is_playing_or_paused = ServiceManager.PlayerEngine.CurrentState == PlayerState.Playing ||
                                            ServiceManager.PlayerEngine.CurrentState == PlayerState.Paused;
                bool is_idle = ServiceManager.PlayerEngine.CurrentState == PlayerState.Idle;

                foreach (string action in require_selection_actions) {
                    this[action].Sensitive = has_selection;
                }

                UpdateActions (source.CanSearch && !FilterFocused, has_single_selection,
                   "SearchMenuAction", "SearchForSameArtistAction", "SearchForSameAlbumAction"
                );

                this["SelectAllAction"].Sensitive = track_source.Count > 0 && !selection.AllSelected;

                UpdateAction ("PlayingTrackAction", !is_idle && playing_track is DatabaseTrackInfo, is_playing_or_paused, null);
                UpdateAction ("AddPlayingTrackToPlaylistAction", source is MusicLibrarySource, is_playing_or_paused, null);
                UpdateAction ("RatePlayingTrackAction", playback_source.HasEditableTrackProperties, is_playing_or_paused, null);
                UpdateAction ("PlayingTrackPropertiesAction", playback_source.HasViewableTrackProperties, is_playing_or_paused, source);
                UpdateAction ("PlayingTrackEditorAction", playback_source.HasEditableTrackProperties, is_playing_or_paused, source);
                UpdateAction ("RemovePlayingTrackAction", playback_source.CanRemoveTracks, is_playing_or_paused, source);
                UpdateAction ("DeletePlayingTrackFromDriveAction", playback_source.CanDeleteTracks, is_playing_or_paused, source);
                UpdateAction ("OpenPlayingTrackFolderAction", playback_source.CanDeleteTracks, is_playing_or_paused, source);

                UpdateAction ("SelectedTracksAction", has_selection, has_selection, null);
                UpdateAction ("AddSelectedTracksToPlaylistAction", in_database && primary_source != null &&
                    primary_source.SupportsPlaylists && !primary_source.PlaylistsReadOnly, has_selection, null);
                UpdateAction ("RateSelectedTracksAction", source.HasEditableTrackProperties, has_selection, null);
                UpdateAction ("SelectedTracksEditorAction", source.HasEditableTrackProperties, has_selection, source);
                UpdateAction ("RemoveSelectedTracksAction", track_source.CanRemoveTracks, has_selection, source);
                UpdateAction ("RemoveTracksFromLibraryAction", source.Parent is LibrarySource, has_selection, null);
                UpdateAction ("DeleteSelectedTracksFromDriveAction", track_source.CanDeleteTracks, has_selection, source);
                //if it can delete tracks, most likely it can open their folder
                UpdateAction ("OpenSelectedTracksFolderAction", track_source.CanDeleteTracks, has_single_selection, source);
                UpdateAction ("SelectedTracksPropertiesAction", source.HasViewableTrackProperties, has_selection, source);

                if (primary_source != null &&
                    !(primary_source is LibrarySource) &&
                    primary_source.StorageName != null) {
                    this["DeleteSelectedTracksFromDriveAction"].Label = String.Format (
                        Catalog.GetString ("_Delete From \"{0}\""), primary_source.StorageName);
                    this["DeletePlayingTrackFromDriveAction"].Label = String.Format (
                        Catalog.GetString ("_Delete From \"{0}\""), primary_source.StorageName);
                }

                if (FilterFocused) {
                    UpdateActions (false, false, disable_for_filter_actions);
                }
            } else {
                Sensitive = Visible = false;
            }
        }

        private void ResetRating ()
        {
            if (current_source != null) {
                int rating = 0;

                // If there is only one track, get the preset rating
                if (Selection.Count == 1) {
                    foreach (TrackInfo track in SelectedTracks) {
                        rating = track.Rating;
                    }
                }
                selected_tracks_rating_proxy.Reset (rating);

                var playing_track = ServiceManager.PlayerEngine.CurrentTrack as TrackInfo;
                if (playing_track != null) {
                    rating = playing_track.Rating;
                    playing_track_rating_proxy.Reset (rating);
                }
            }
        }

#endregion

#region Action Handlers

        private void OnSelectAll (object o, EventArgs args)
        {
            if (current_source != null)
                current_source.TrackModel.Selection.SelectAll ();
        }

        private void OnSelectNone (object o, EventArgs args)
        {
            if (current_source != null)
                current_source.TrackModel.Selection.Clear ();
        }

        private void OnTrackContextMenu (object o, EventArgs args)
        {
            ResetRating ();
            UpdateActions ();
            ShowContextMenu ("/TrackContextMenu");
        }

        private bool RunSourceOverrideHandler (string sourceOverrideHandler)
        {
            Source source = current_source as Source;
            InvokeHandler handler = source != null
                ? source.GetInheritedProperty<InvokeHandler> (sourceOverrideHandler)
                : null;

            if (handler != null) {
                handler ();
                return true;
            }

            return false;
        }

        private void OnPlayingTrackProperties (object o, EventArgs args)
        {
            var track = ServiceManager.PlayerEngine.CurrentTrack as TrackInfo;
            if (track != null && current_source != null && !RunSourceOverrideHandler ("PlayingTrackPropertiesActionHandler")) {
                var s = current_source as Source;
                var readonly_tabs = s != null && !s.HasEditableTrackProperties;
                TrackEditor.TrackEditorDialog.RunView (track, readonly_tabs);
            }
        }

        private void OnSelectedTracksProperties (object o, EventArgs args)
        {
            if (current_source != null && !RunSourceOverrideHandler ("SelectedTracksPropertiesActionHandler")) {
                var s = current_source as Source;
                var readonly_tabs = s != null && !s.HasEditableTrackProperties;
                TrackEditor.TrackEditorDialog.RunView (current_source.TrackModel, Selection, readonly_tabs);
            }
        }

        private void OnPlayingTrackEditor (object o, EventArgs args)
        {
            var track = ServiceManager.PlayerEngine.CurrentTrack as TrackInfo;
            if (track != null && current_source != null && !RunSourceOverrideHandler ("PlayingTrackEditorActionHandler")) {
                TrackEditor.TrackEditorDialog.RunEdit (track);
            }
        }

        private void OnSelectedTracksEditor (object o, EventArgs args)
        {
            if (current_source != null && !RunSourceOverrideHandler ("SelectedTracksEditorActionHandler")) {
                TrackEditor.TrackEditorDialog.RunEdit (current_source.TrackModel, Selection);
            }
        }

        private void OnPlayTrack (object o, EventArgs args)
        {
            var source = ServiceManager.SourceManager.ActiveSource as ITrackModelSource;
            if (source != null) {
                var track = source.TrackModel [FilterFocused ? 0 : source.TrackModel.Selection.FocusedIndex];
                if (track.HasAttribute (TrackMediaAttributes.ExternalResource)) {
                    System.Diagnostics.Process.Start (track.Uri);
                } else {
                    ServiceManager.PlaybackController.Source = source;
                    ServiceManager.PlayerEngine.OpenPlay (track);
                }
            }
        }

        // TODO This function works only for music library source now
        //      but it should act on a source where a track is playing.
        private void OnAddPlayingTrackToPlaylistMenu (object o, EventArgs args)
        {
            List<Source> children;
            chosen_from_playing_track_submenu = true;
            lock (ServiceManager.SourceManager.MusicLibrary.Children) {
                children = new List<Source> (ServiceManager.SourceManager.MusicLibrary.Children);
            }
            OnAddToPlaylistMenu (o, children);
        }

        private void OnAddSelectedTracksToPlaylistMenu (object o, EventArgs args)
        {
            List<Source> children;
            chosen_from_playing_track_submenu = false;
            lock (ActivePrimarySource.Children) {
                children = new List<Source> (ActivePrimarySource.Children);
            }
            OnAddToPlaylistMenu (o, children);
        }

        // Called when the Add to Playlist action is highlighted.
        // Generates the menu of playlists to which you can add the selected tracks.
        private void OnAddToPlaylistMenu (object o, List<Source> children)
        {
            Source active_source = ServiceManager.SourceManager.ActiveSource;

            // TODO find just the menu that was activated instead of modifying all proxies
            foreach (Widget proxy_widget in ((Gtk.Action)o).Proxies) {
                MenuItem menu = proxy_widget as MenuItem;
                if (menu == null)
                    continue;

                Menu submenu = new Menu ();
                menu.Submenu = submenu;

                submenu.Append (this ["AddToNewPlaylistAction"].CreateMenuItem ());
                bool separator_added = false;

                foreach (Source child in children) {
                    PlaylistSource playlist = child as PlaylistSource;
                    if (playlist != null) {
                        if (!separator_added) {
                            submenu.Append (new SeparatorMenuItem ());
                            separator_added = true;
                        }

                        PlaylistMenuItem item = new PlaylistMenuItem (playlist);
                        item.Image = new Gtk.Image ("playlist-source", IconSize.Menu);
                        item.Activated += OnAddToExistingPlaylist;
                        item.Sensitive = playlist != active_source;
                        submenu.Append (item);
                    }
                }

                submenu.ShowAll ();
            }
        }

        private void OnAddToNewPlaylist (object o, EventArgs args)
        {
            // TODO generate name based on the track selection, or begin editing it
            PlaylistSource playlist = new PlaylistSource (Catalog.GetString ("New Playlist"), ActivePrimarySource);
            playlist.Save ();
            playlist.PrimarySource.AddChildSource (playlist);
            AddToPlaylist (playlist);
        }

        private void OnAddToExistingPlaylist (object o, EventArgs args)
        {
            AddToPlaylist (((PlaylistMenuItem)o).Playlist);
        }

        private void AddToPlaylist (PlaylistSource playlist)
        {
            if (!FilterFocused) {
                var track = ServiceManager.PlayerEngine.CurrentTrack as DatabaseTrackInfo;
                if (chosen_from_playing_track_submenu && track != null) {
                    playlist.AddTrack (track);
                } else {
                    playlist.AddSelectedTracks (ActiveSource);
                }
            } else {
                playlist.AddAllTracks (ActiveSource);
            }
        }

        private void OnRemovePlayingTrack (object o, EventArgs args)
        {
            var playback_src = ServiceManager.PlaybackController.Source as DatabaseSource;
            var track = ServiceManager.PlayerEngine.CurrentTrack as DatabaseTrackInfo;

            if (playback_src != null && track != null) {
                if (!ConfirmRemove (playback_src, false, 1)) {
                    return;
                }
                if (playback_src != null && playback_src.CanRemoveTracks) {
                    playback_src.RemoveTrack (track);
                }
            }
        }

        private void OnRemoveSelectedTracks (object o, EventArgs args)
        {
            ITrackModelSource source = ActiveSource as ITrackModelSource;

            if (!ConfirmRemove (source, false, Selection.Count))
                return;

            if (source != null && source.CanRemoveTracks) {
                source.RemoveTracks (Selection);
            }
        }

        private void OnRemoveTracksFromLibrary (object o, EventArgs args)
        {
            ITrackModelSource source = ActiveSource as ITrackModelSource;

            if (source != null) {
                LibrarySource library = source.Parent as LibrarySource;
                if (library != null) {
                    if (!ConfirmRemove (library, false, Selection.Count)) {
                        return;
                    }

                    ThreadAssist.SpawnFromMain (delegate {
                        library.RemoveTracks (source.TrackModel as DatabaseTrackListModel, Selection);
                    });
                }
            }
        }

        private void OnOpenPlayingTrackFolder (object o, EventArgs args)
        {
            var track = ServiceManager.PlayerEngine.CurrentTrack as TrackInfo;
            if (track != null) {
                var path = System.IO.Path.GetDirectoryName (track.Uri.AbsolutePath);
                OpenContainingFolder (path);
            }
        }

        private void OnOpenSelectedTracksFolder (object o, EventArgs args)
        {
            var source = ActiveSource as ITrackModelSource;
            if (source == null || source.TrackModel == null)
                return;

            var items = SelectedTracks;
            if (items == null || items.Count != 1) {
                Log.Error ("Could not open containing folder");
                return;
            }

            foreach (var track in items) {
                var path = System.IO.Path.GetDirectoryName (track.Uri.AbsolutePath);
                OpenContainingFolder (path);
            }
        }

        private void OpenContainingFolder (String path)
        {
            if (Banshee.IO.Directory.Exists (path)) {
                System.Diagnostics.Process.Start (path);
                return;
            }

            var md = new HigMessageDialog (
                ServiceManager.Get<GtkElementsService> ().PrimaryWindow,
                DialogFlags.DestroyWithParent, MessageType.Warning,
                ButtonsType.None, Catalog.GetString ("The folder could not be found."),
                Catalog.GetString ("Please check that the track's location is accessible by the system.")
            );
            md.AddButton ("gtk-ok", ResponseType.Ok, true);

            try {
                md.Run ();
            } finally {
                md.Destroy ();
            }
        }

        private void OnDeletePlayingTrackFromDrive (object o, EventArgs args)
        {
            var playback_src = ServiceManager.PlaybackController.Source as DatabaseSource;
            var track = ServiceManager.PlayerEngine.CurrentTrack as DatabaseTrackInfo;

            if (playback_src != null && track != null) {
                if (!ConfirmRemove (playback_src, true, 1)) {
                    return;
                }

                if (playback_src != null && playback_src.CanDeleteTracks) {
                    var selection = new Hyena.Collections.Selection ();
                    selection.Select (playback_src.TrackModel.IndexOf (track));
                    playback_src.DeleteTracks (selection);
                }
            }
        }

        private void OnDeleteSelectedTracksFromDrive (object o, EventArgs args)
        {
            ITrackModelSource source = ActiveSource as ITrackModelSource;

            if (!ConfirmRemove (source, true, Selection.Count))
                return;

            if (source != null && source.CanDeleteTracks) {
                source.DeleteTracks (Selection);
            }
        }

        // FIXME filter
        private void OnRateSelectedTracks (object o, EventArgs args)
        {
            ThreadAssist.SpawnFromMain (delegate {
                ((DatabaseSource)ActiveSource).RateSelectedTracks (selected_tracks_rating_proxy.LastRating);
            });
        }

        private void OnRatePlayingTrack (object o, EventArgs args)
        {
            var track = ServiceManager.PlayerEngine.CurrentTrack as DatabaseTrackInfo;
            if (track != null) {
                track.SavedRating = playing_track_rating_proxy.LastRating;
            }
        }

        private void OnSearchForSameArtist (object o, EventArgs args)
        {
            if (current_source != null) {
                foreach (TrackInfo track in current_source.TrackModel.SelectedItems) {
                    if (!String.IsNullOrEmpty (track.ArtistName)) {
                        ActiveSource.FilterQuery = BansheeQuery.ArtistField.ToTermString (":", track.ArtistName);
                    }
                    break;
                }
            }
        }

        private void OnSearchForSameAlbum (object o, EventArgs args)
        {
            if (current_source != null) {
                foreach (TrackInfo track in current_source.TrackModel.SelectedItems) {
                    if (!String.IsNullOrEmpty (track.AlbumTitle)) {
                        ActiveSource.FilterQuery = BansheeQuery.AlbumField.ToTermString (":", track.AlbumTitle);
                    }
                    break;
                }
            }
        }

#endregion

        private static bool ConfirmRemove (ITrackModelSource source, bool delete, int selCount)
        {
            if (!source.ConfirmRemoveTracks) {
                return true;
            }

            bool ret = false;
            string header = null;
            string message = null;
            string button_label = null;

            if (delete) {
                header = String.Format (
                    Catalog.GetPluralString (
                        "Are you sure you want to permanently delete this item?",
                        "Are you sure you want to permanently delete the selected {0} items?", selCount
                    ), selCount
                );
                message = Catalog.GetString ("If you delete the selection, it will be permanently lost.");
                button_label = "gtk-delete";
            } else {
                header = String.Format (Catalog.GetString ("Remove selection from {0}?"), source.Name);
                message = String.Format (
                    Catalog.GetPluralString (
                        "Are you sure you want to remove the selected item from your {1}?",
                        "Are you sure you want to remove the selected {0} items from your {1}?", selCount
                    ), selCount, source.GenericName
                );
                button_label = "gtk-remove";
            }

            HigMessageDialog md = new HigMessageDialog (
                ServiceManager.Get<GtkElementsService> ().PrimaryWindow,
                DialogFlags.DestroyWithParent, delete ? MessageType.Warning : MessageType.Question,
                ButtonsType.None, header, message
            );
            // Delete from Disk defaults to Cancel and the others to OK/Confirm.
            md.AddButton ("gtk-cancel", ResponseType.No, delete);
            md.AddButton (button_label, ResponseType.Yes, !delete);

            try {
                if (md.Run () == (int) ResponseType.Yes) {
                    ret = true;
                }
            } finally {
                md.Destroy ();
            }
            return ret;
        }

        public static bool ConfirmRemove (string header)
        {
            string message = Catalog.GetString ("Are you sure you want to continue?");

            bool remove_tracks = false;
            ThreadAssist.BlockingProxyToMain (() => {

                var md = new HigMessageDialog (
                    ServiceManager.Get<GtkElementsService> ().PrimaryWindow,
                    DialogFlags.DestroyWithParent, MessageType.Warning,
                    ButtonsType.None, header, message
                );
                md.AddButton ("gtk-cancel", ResponseType.No, true);
                md.AddButton (Catalog.GetString ("Remove tracks"), ResponseType.Yes, false);

                try {
                    if (md.Run () == (int) ResponseType.Yes) {
                        remove_tracks = true;
                    }
                } finally {
                    md.Destroy ();
                }
            });
            return remove_tracks;
        }

    }
}
