//
// PlayQueueSource.cs
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

using Mono.Unix;

using Hyena.Data.Sqlite;

using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Playlist;
using Banshee.Library;
using Banshee.Database;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.PlaybackController;
using Banshee.MediaEngine;
using Banshee.Configuration;
using Banshee.Gui;

namespace Banshee.PlayQueue
{
    public class PlayQueueSource : PlaylistSource, IBasicPlaybackController, IPlayQueue, IDBusExportable, IDisposable
    {
        private static string special_playlist_name = "Play Queue";//typeof (PlayQueueSource).ToString ();

        private ITrackModelSource prior_playback_source;
        private DatabaseTrackInfo playing_track;
        private TrackInfo prior_playback_track;
        private PlayQueueActions actions;
        private bool was_playing = false;
        
        protected override bool HasArtistAlbum {
            get { return false; }
        }
        
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
            ServiceManager.PlaybackController.Transition += OnCanonicalPlaybackControllerTransition;

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
            
            TrackModel.Reloaded += delegate {
                if (Count == 0) {
                    if (this == ServiceManager.PlaybackController.Source || this == ServiceManager.PlaybackController.NextSource) {
                        ServiceManager.PlaybackController.NextSource = ServiceManager.PlaybackController.Source = PriorSource;
                    }
                }
            };
            
            Reload ();
            SetAsPlaybackSourceUnlessPlaying ();
        }
        
#region IPlayQueue, IDBusExportable

        public void EnqueueUri (string uri)
        {
            EnqueueUri (uri, false);
        }

        public void EnqueueUri (string uri, bool prepend)
        {
            EnqueueId (DatabaseTrackInfo.GetTrackIdForUri (uri), prepend);
        }
        
        public void EnqueueTrack (TrackInfo track, bool prepend)
        {
            DatabaseTrackInfo db_track = track as DatabaseTrackInfo;
            if (db_track != null) {
                EnqueueId (db_track.TrackId, prepend);
            } else {
                EnqueueUri (track.Uri.AbsoluteUri, prepend);
            }
        }
        
        private void EnqueueId (int trackId, bool prepend)
        {
            if (trackId <= 0) {
                return;
            }
            
            if (prepend) {
                ServiceManager.DbConnection.Execute ("UPDATE CorePlaylistEntries SET ViewOrder = ViewOrder + 1 WHERE PlaylistID = ?", DbId);
            }

            HyenaSqliteCommand insert_command = new HyenaSqliteCommand (String.Format (
                @"INSERT INTO CorePlaylistEntries (PlaylistID, TrackID, ViewOrder) VALUES ({0}, ?, {1})", 
                    DbId, prepend ? 0 : MaxViewOrder));
            ServiceManager.DbConnection.Execute (insert_command, trackId);

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
        
        private void SetAsPlaybackSourceUnlessPlaying ()
        {
            if (Count > 0 && ServiceManager.PlaybackController.Source != this) {
                PriorSource = ServiceManager.PlaybackController.Source;
                ServiceManager.PlaybackController.NextSource = this;
            }
        }

        public void Clear ()
        {
            playing_track = null;
            RemoveTrackRange (DatabaseTrackModel, new Hyena.Collections.RangeCollection.Range (0, Count));
            Reload ();
        }

        public void Dispose ()
        {
            ServiceManager.PlayerEngine.DisconnectEvent (OnPlayerEvent);

            if (actions != null) {
                actions.Dispose ();
            }

            if (ClearOnQuitSchema.Get ()) {
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
            base.OnTracksAdded ();
            SetAsPlaybackSourceUnlessPlaying ();
        }
        
        private void OnCanonicalPlaybackControllerTransition (object o, EventArgs args)
        {
            if (Count > 0) {
                PriorSource = ServiceManager.PlaybackController.Source;
                ServiceManager.PlaybackController.Source = this;
            }
        }
        
        private void OnPlayerEvent (PlayerEventArgs args)
        {
            if (args.Event == PlayerEvent.EndOfStream) {
                if (RemovePlayingTrack () && Count == 0) {
                    if (was_playing) {
                        ServiceManager.PlaybackController.PriorTrack = prior_playback_track;
                    } else {
                        ServiceManager.PlaybackController.StopWhenFinished = true;
                    }
                }
            } else if (args.Event == PlayerEvent.StartOfStream) {
                if (this == ServiceManager.PlaybackController.Source) {
                    playing_track = ServiceManager.PlayerEngine.CurrentTrack as DatabaseTrackInfo;
                } else {
                    playing_track = null;
                    prior_playback_track = ServiceManager.PlayerEngine.CurrentTrack;
                }
            }
        }

        bool IBasicPlaybackController.First ()
        {
            return ((IBasicPlaybackController)this).Next (false);
        }
        
        bool IBasicPlaybackController.Next (bool restart)
        {
            RemovePlayingTrack ();
            
            if (Count == 0) {
                ServiceManager.PlaybackController.Source = PriorSource;
                if (was_playing) {
                    ServiceManager.PlaybackController.PriorTrack = prior_playback_track;
                    ServiceManager.PlaybackController.Next (restart);
                } else {
                    ServiceManager.PlayerEngine.Close ();
                }
                return true;
            }
            
            ServiceManager.PlayerEngine.OpenPlay ((DatabaseTrackInfo)TrackModel[0]);
            return true;
        }
        
        bool IBasicPlaybackController.Previous (bool restart)
        {
            return true;
        }
        
        private bool RemovePlayingTrack ()
        {
            if (playing_track != null) {
                RemoveTrack (playing_track);
                playing_track = null;
                return true;
            }
            return false;
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
        
        public override bool ShowBrowser {
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
        
        public static readonly SchemaEntry<bool> ClearOnQuitSchema = new SchemaEntry<bool> (
            "plugins.play_queue", "clear_on_quit",
            false,
            "Clear on Quit",
            "Clear the play queue when quitting"
        );

        // Reserve strings in preparation for the forthcoming string freeze.
        public void Strings ()
        {
            Catalog.GetString ("manual");
            Catalog.GetString ("by song");
            Catalog.GetString ("by album");
            Catalog.GetString ("by artist");
            Catalog.GetString ("by rating");
            Catalog.GetString ("by score");
            Catalog.GetString ("_Fill");
            Catalog.GetString ("f_rom");
            Catalog.GetString ("Refresh");
            Catalog.GetString ("Refresh random tracks in the play queue");
            Catalog.GetString ("Number of _played songs to show");
            Catalog.GetString ("Number of _upcoming songs to show");
        }
    }
}
