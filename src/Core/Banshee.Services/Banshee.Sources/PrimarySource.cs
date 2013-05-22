//
// PrimarySource.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
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

using Hyena;
using Hyena.Jobs;
using Hyena.Data;
using Hyena.Query;
using Hyena.Data.Sqlite;
using Hyena.Collections;

using Banshee.Base;
using Banshee.Preferences;
using Banshee.ServiceStack;
using Banshee.Configuration;
using Banshee.Sources;
using Banshee.Playlist;
using Banshee.SmartPlaylist;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Query;

namespace Banshee.Sources
{
    public class TrackEventArgs : EventArgs
    {
        private DateTime when;
        public DateTime When {
            get { return when; }
        }

        private QueryField [] changed_fields;
        public QueryField [] ChangedFields {
            get { return changed_fields; }
        }

        public TrackEventArgs ()
        {
            when = DateTime.Now;
        }

        public TrackEventArgs (params QueryField [] fields) : this ()
        {
            changed_fields = fields;
        }
    }

    public delegate bool TrackEqualHandler (DatabaseTrackInfo a, TrackInfo b);
    public delegate object TrackExternalObjectHandler (DatabaseTrackInfo a);
    public delegate string TrackArtworkIdHandler (DatabaseTrackInfo a);

    public abstract class PrimarySource : DatabaseSource, IDisposable
    {
        #region Functions that let us override some behavior of our DatabaseTrackInfos

        public TrackEqualHandler TrackEqualHandler { get; protected set; }
        public TrackInfo.IsPlayingHandler TrackIsPlayingHandler { get; protected set; }
        public TrackExternalObjectHandler TrackExternalObjectHandler { get; protected set; }
        public TrackArtworkIdHandler TrackArtworkIdHandler { get; protected set; }

        #endregion

        protected ErrorSource error_source;
        protected bool error_source_visible = false;

        protected string remove_range_sql = @"
            INSERT INTO CoreRemovedTracks (DateRemovedStamp, TrackID, Uri)
                SELECT ?, TrackID, " + BansheeQuery.UriField.Column + @"
                FROM CoreTracks WHERE TrackID IN (SELECT {0});
            DELETE FROM CoreTracks WHERE TrackID IN (SELECT {0})";

        protected HyenaSqliteCommand remove_list_command = new HyenaSqliteCommand (String.Format (@"
            INSERT INTO CoreRemovedTracks (DateRemovedStamp, TrackID, Uri)
                SELECT ?, TrackID, {0} FROM CoreTracks WHERE TrackID IN (SELECT ItemID FROM CoreCache WHERE ModelID = ?);
            DELETE FROM CoreTracks WHERE TrackID IN (SELECT ItemID FROM CoreCache WHERE ModelID = ?)
        ", BansheeQuery.UriField.Column));

        protected HyenaSqliteCommand prune_artists_albums_command = new HyenaSqliteCommand (@"
            DELETE FROM CoreAlbums WHERE AlbumID NOT IN (SELECT AlbumID FROM CoreTracks);
            DELETE FROM CoreArtists WHERE
                    NOT EXISTS (SELECT 1 FROM CoreTracks WHERE CoreTracks.ArtistID = CoreArtists.ArtistID)
                AND NOT EXISTS (SELECT 1 FROM CoreAlbums WHERE CoreAlbums.ArtistID = CoreArtists.ArtistID)
        ");

        protected HyenaSqliteCommand purge_tracks_command = new HyenaSqliteCommand (@"
            DELETE FROM CoreTracks WHERE PrimarySourceId = ?
        ");

        private SchemaEntry<bool> expanded_schema;
        public SchemaEntry<bool> ExpandedSchema {
            get { return expanded_schema; }
        }

        private int dbid;
        public int DbId {
            get {
                if (dbid > 0) {
                    return dbid;
                }

                dbid = ServiceManager.DbConnection.Query<int> ("SELECT PrimarySourceID FROM CorePrimarySources WHERE StringID = ?", UniqueId);
                if (dbid == 0) {
                    dbid = ServiceManager.DbConnection.Execute ("INSERT INTO CorePrimarySources (StringID, IsTemporary) VALUES (?, ?)", UniqueId, IsTemporary);
                } else {
                    SavedCount = ServiceManager.DbConnection.Query<int> ("SELECT CachedCount FROM CorePrimarySources WHERE PrimarySourceID = ?", dbid);
                    IsTemporary = ServiceManager.DbConnection.Query<bool> ("SELECT IsTemporary FROM CorePrimarySources WHERE PrimarySourceID = ?", dbid);
                }

                if (dbid == 0) {
                    throw new ApplicationException ("dbid could not be resolved, this should never happen");
                }

                return dbid;
            }
        }

        private bool supports_playlists = true;
        public virtual bool SupportsPlaylists {
            get { return supports_playlists; }
            protected set { supports_playlists = value; }
        }

        public virtual bool PlaylistsReadOnly {
            get { return false; }
        }

        public ErrorSource ErrorSource {
            get {
                if (error_source == null) {
                    error_source = new ErrorSource (Catalog.GetString ("Errors"));
                    ErrorSource.Updated += OnErrorSourceUpdated;
                    OnErrorSourceUpdated (null, null);
                }
                return error_source;
            }
        }


        private bool is_local = false;
        public bool IsLocal {
            get { return is_local; }
            protected set { is_local = value; }
        }

        private static SourceSortType[] sort_types = new SourceSortType[] {
            SortNameAscending,
            SortSizeAscending,
            SortSizeDescending
        };

        public override SourceSortType[] ChildSortTypes {
            get { return sort_types; }
        }

        public override SourceSortType DefaultChildSort {
            get { return SortNameAscending; }
        }

        public delegate void TrackEventHandler (Source sender, TrackEventArgs args);

        public event TrackEventHandler TracksAdded;
        public event TrackEventHandler TracksChanged;
        public event TrackEventHandler TracksDeleted;

        private static Dictionary<int, PrimarySource> primary_sources = new Dictionary<int, PrimarySource> ();
        public static PrimarySource GetById (int id)
        {
            return (primary_sources.ContainsKey (id)) ? primary_sources[id] : null;
        }

        public virtual string BaseDirectory {
            get { return null; }
            protected set { base_dir_with_sep = null; }
        }

        private string base_dir_with_sep;
        public string BaseDirectoryWithSeparator {
            get { return base_dir_with_sep ?? (base_dir_with_sep = BaseDirectory + System.IO.Path.DirectorySeparatorChar); }
        }

        protected PrimarySource (string generic_name, string name, string id, int order) : this (generic_name, name, id, order, false)
        {
        }

        protected PrimarySource (string generic_name, string name, string id, int order, bool is_temp) : base (generic_name, name, id, order)
        {
            Properties.SetString ("SortChildrenActionLabel", Catalog.GetString ("Sort Playlists By"));
            IsTemporary = is_temp;
            PrimarySourceInitialize ();
        }

        protected PrimarySource () : base ()
        {
        }

        // Translators: this is a noun, referring to the harddisk
        private string storage_name = Catalog.GetString ("Drive");
        public string StorageName {
            get { return storage_name; }
            protected set { storage_name = value; }
        }

        public override bool? AutoExpand {
            get { return ExpandedSchema.Get (); }
        }

        public override bool Expanded {
            get { return ExpandedSchema.Get (); }
            set {
                ExpandedSchema.Set (value);
                base.Expanded = value;
            }
        }

        public PathPattern PathPattern { get; private set; }

        protected void SetFileNamePattern (PathPattern pattern)
        {
            PathPattern = pattern;

            var file_system = PreferencesPage.FindOrAdd (new Section ("file-system", Catalog.GetString ("File Organization"), 5));
            file_system.Add (new SchemaPreference<string> (pattern.FolderSchema, Catalog.GetString ("Folder hie_rarchy")));
            file_system.Add (new SchemaPreference<string> (pattern.FileSchema, Catalog.GetString ("File _name")));
        }

        public virtual void Dispose ()
        {
            PurgeSelfIfTemporary ();

            if (Application.ShuttingDown)
                return;

            DatabaseTrackInfo track = ServiceManager.PlayerEngine.CurrentTrack as DatabaseTrackInfo;
            if (track != null && track.PrimarySourceId == this.DbId) {
                ServiceManager.PlayerEngine.Close ();
            }

            ClearChildSources ();
            Remove ();
        }

        protected override void Initialize ()
        {
            base.Initialize ();
            PrimarySourceInitialize ();
        }

        private void PrimarySourceInitialize ()
        {
            // Scope the tracks to this primary source
            DatabaseTrackModel.AddCondition (String.Format ("CoreTracks.PrimarySourceID = {0}", DbId));

            primary_sources[DbId] = this;

            // If there was a crash, tracks can be left behind, for example in DaapSource.
            // Temporary playlists are cleaned up by the PlaylistSource.LoadAll call below
            if (IsTemporary && SavedCount > 0) {
                PurgeTracks ();
            }

            // Load our playlists and smart playlists
            foreach (PlaylistSource pl in PlaylistSource.LoadAll (this)) {
                AddChildSource (pl);
            }

            int sp_count = 0;
            foreach (SmartPlaylistSource pl in SmartPlaylistSource.LoadAll (this)) {
                AddChildSource (pl);
                sp_count++;
            }

            // Create default smart playlists if we haven't done it ever before, and if the
            // user has zero smart playlists.
            if (!HaveCreatedSmartPlaylists) {
                if (sp_count == 0) {
                    foreach (SmartPlaylistDefinition def in DefaultSmartPlaylists) {
                        SmartPlaylistSource pl = def.ToSmartPlaylistSource (this);
                        pl.Save ();
                        AddChildSource (pl);
                        pl.RefreshAndReload ();
                        sp_count++;
                    }
                }

                // Only save it if we already had some smart playlists, or we actually created some (eg not
                // if we didn't have any and the list of default ones is empty atm).
                if (sp_count > 0)
                    HaveCreatedSmartPlaylists = true;

            }

            expanded_schema = new SchemaEntry<bool> (
                String.Format ("sources.{0}", ParentConfigurationId), "expanded", true, "Is source expanded", "Is source expanded"
            );
        }

        private bool HaveCreatedSmartPlaylists {
            get { return DatabaseConfigurationClient.Client.Get<bool> ("HaveCreatedSmartPlaylists", UniqueId, false); }
            set { DatabaseConfigurationClient.Client.Set<bool> ("HaveCreatedSmartPlaylists", UniqueId, value); }
        }

        public override void Save ()
        {
            ServiceManager.DbConnection.Execute (
                "UPDATE CorePrimarySources SET CachedCount = ? WHERE PrimarySourceID = ?",
                Count, DbId
            );
        }

        public virtual void UpdateMetadata (DatabaseTrackInfo track)
        {
        }

        public virtual void CopyTrackTo (DatabaseTrackInfo track, SafeUri uri, BatchUserJob job)
        {
            Log.WarningFormat ("CopyTrackTo not implemented for source {0}", this);
        }

        internal void NotifyTracksAdded ()
        {
            OnTracksAdded ();
        }

        internal void NotifyTracksChanged (params QueryField [] fields)
        {
            OnTracksChanged (fields);
        }

        // TODO replace this public method with a 'transaction'-like system
        public void NotifyTracksChanged ()
        {
            OnTracksChanged ();
        }

        public void NotifyTracksDeleted ()
        {
            OnTracksDeleted ();
        }

        protected void OnErrorSourceUpdated (object o, EventArgs args)
        {
            lock (error_source) {
                if (error_source.Count > 0 && !error_source_visible) {
                    error_source_visible = true;
                    AddChildSource (error_source);
                } else if (error_source.Count <= 0 && error_source_visible) {
                    error_source_visible = false;
                    RemoveChildSource (error_source);
                }
            }
        }

        public virtual IEnumerable<SmartPlaylistDefinition> DefaultSmartPlaylists {
            get { yield break; }
        }

        public virtual IEnumerable<SmartPlaylistDefinition> NonDefaultSmartPlaylists {
            get { yield break; }
        }

        public IEnumerable<SmartPlaylistDefinition> PredefinedSmartPlaylists {
            get {
                foreach (SmartPlaylistDefinition def in DefaultSmartPlaylists)
                    yield return def;

                foreach (SmartPlaylistDefinition def in NonDefaultSmartPlaylists)
                    yield return def;
            }
        }

        public override bool CanSearch {
            get { return true; }
        }

        protected override void OnTracksAdded ()
        {
            ThreadAssist.SpawnFromMain (delegate {
                Reload ();

                TrackEventHandler handler = TracksAdded;
                if (handler != null) {
                    handler (this, new TrackEventArgs ());
                }
            });
        }

        protected override void OnTracksChanged (params QueryField [] fields)
        {
            ThreadAssist.SpawnFromMain (delegate {
                if (NeedsReloadWhenFieldsChanged (fields)) {
                    Reload ();
                } else {
                    InvalidateCaches ();
                }

                System.Threading.Thread.Sleep (150);

                TrackEventHandler handler = TracksChanged;
                if (handler != null) {
                    handler (this, new TrackEventArgs (fields));
                }
            });
        }

        protected override void OnTracksDeleted ()
        {
            ThreadAssist.SpawnFromMain (delegate {
                PruneArtistsAlbums ();
                Reload ();

                TrackEventHandler handler = TracksDeleted;
                if (handler != null) {
                    handler (this, new TrackEventArgs ());
                }

                SkipTrackIfRemoved ();
            });
        }

        protected override void OnTracksRemoved ()
        {
            OnTracksDeleted ();
        }

        protected virtual void PurgeSelfIfTemporary ()
        {
            if (!IsTemporary) {
                return;
            }

            PlaylistSource.ClearTemporary (this);
            PurgeTracks ();

            ServiceManager.DbConnection.Execute (new HyenaSqliteCommand (@"
                DELETE FROM CorePrimarySources WHERE PrimarySourceId = ?"),
                DbId);
        }

        protected virtual void PurgeTracks ()
        {
            ServiceManager.DbConnection.Execute (purge_tracks_command, DbId);
        }

        protected override void RemoveTrackRange (DatabaseTrackListModel model, RangeCollection.Range range)
        {
            ServiceManager.DbConnection.Execute (
                String.Format (remove_range_sql, model.TrackIdsSql),
                DateTime.Now,
                model.CacheId, range.Start, range.End - range.Start + 1,
                model.CacheId, range.Start, range.End - range.Start + 1
            );
        }

        public void DeleteAllTracks (AbstractPlaylistSource source)
        {
            if (source.PrimarySource != this) {
                Log.WarningFormat ("Cannot delete all tracks from {0} via primary source {1}", source, this);
                return;
            }

            if (source.Count < 1)
                return;

            var list = CachedList<DatabaseTrackInfo>.CreateFromModel (source.DatabaseTrackModel);
            ThreadAssist.SpawnFromMain (delegate {
                DeleteTrackList (list);
            });
        }

        public override void DeleteTracks (DatabaseTrackListModel model, Selection selection)
        {
            if (model == null || model.Count < 1) {
                return;
            }

            var list = CachedList<DatabaseTrackInfo>.CreateFromModelAndSelection (model, selection);
            ThreadAssist.SpawnFromMain (delegate {
                DeleteTrackList (list);
            });
        }

        protected virtual void DeleteTrackList (CachedList<DatabaseTrackInfo> list)
        {
            is_deleting = true;
            DeleteTrackJob.Total += (int) list.Count;
            List<DatabaseTrackInfo> skip_deletion = null;

            // Remove from file system
            foreach (DatabaseTrackInfo track in list) {
                if (track == null) {
                    DeleteTrackJob.Completed++;
                    continue;
                }

                try {
                    DeleteTrackJob.Status = String.Format ("{0} - {1}", track.ArtistName, track.TrackTitle);
                    if (!DeleteTrack (track)) {
                        if (skip_deletion == null) {
                            skip_deletion = new List<DatabaseTrackInfo> ();
                        }
                        skip_deletion.Add (track);
                    }
                } catch (Exception e) {
                    Log.Exception (e);
                    ErrorSource.AddMessage (e.Message, track.Uri.ToString ());
                }

                DeleteTrackJob.Completed++;
                if (DeleteTrackJob.Completed % 10 == 0 && !DeleteTrackJob.IsFinished) {
                    OnTracksDeleted ();
                }
            }

            is_deleting = false;

            if (DeleteTrackJob.Total == DeleteTrackJob.Completed) {
                delete_track_job.Finish ();
                delete_track_job = null;
            }

            if (skip_deletion != null) {
                list.Remove (skip_deletion);
                skip_deletion.Clear ();
                skip_deletion = null;
            }

            // Remove from database
            if (list.Count > 0) {
                ServiceManager.DbConnection.Execute (remove_list_command, DateTime.Now, list.CacheId, list.CacheId);
            }

            ThreadAssist.ProxyToMain (delegate {
                OnTracksDeleted ();
                OnUserNotifyUpdated ();
                OnUpdated ();
            });
        }

        protected virtual bool DeleteTrack (DatabaseTrackInfo track)
        {
            if (!track.Uri.IsLocalPath)
                throw new Exception ("Cannot delete a non-local resource: " + track.Uri.Scheme);

            try {
                Banshee.IO.Utilities.DeleteFileTrimmingParentDirectories (track.Uri);
            } catch (System.IO.FileNotFoundException) {
            } catch (System.IO.DirectoryNotFoundException) {
            }

            return true;
        }

        public override bool AcceptsInputFromSource (Source source)
        {
            return base.AcceptsInputFromSource (source) && source.Parent != this
                && (source.Parent is PrimarySource || source is PrimarySource)
                && !(source.Parent is Banshee.Library.LibrarySource);
        }

        public override bool AddSelectedTracks (Source source, Selection selection)
        {
            if (!AcceptsInputFromSource (source))
                return false;

            DatabaseTrackListModel model = (source as ITrackModelSource).TrackModel as DatabaseTrackListModel;

            // Store a snapshot of the current selection
            CachedList<DatabaseTrackInfo> cached_list = CachedList<DatabaseTrackInfo>.CreateFromModelAndSelection (model, selection);
            if (ThreadAssist.InMainThread) {
                System.Threading.ThreadPool.QueueUserWorkItem (AddTrackList, cached_list);
            } else {
                AddTrackList (cached_list);
            }
            return true;
        }

        public int GetTrackIdForUri (string uri)
        {
            return DatabaseTrackInfo.GetTrackIdForUri (new SafeUri (uri), DbId);
        }

        private bool is_adding;
        public bool IsAdding {
            get { return is_adding; }
        }

        private bool is_deleting;
        public bool IsDeleting {
            get { return is_deleting; }
        }

        protected virtual void AddTrackAndIncrementCount (DatabaseTrackInfo track)
        {
            AddTrackJob.Status = String.Format ("{0} - {1}", track.ArtistName, track.TrackTitle);
            AddTrack (track);
            IncrementAddedTracks ();
        }

        protected virtual void AddTrackList (object cached_list)
        {
            CachedList<DatabaseTrackInfo> list = cached_list as CachedList<DatabaseTrackInfo>;
            is_adding = true;
            AddTrackJob.Total += (int) list.Count;

            foreach (DatabaseTrackInfo track in list) {
                if (AddTrackJob.IsCancelRequested) {
                    AddTrackJob.Finish ();
                    IncrementAddedTracks ();
                    break;
                }

                if (track == null) {
                    IncrementAddedTracks ();
                    continue;
                }

                try {
                    AddTrackJob.Status = String.Format ("{0} - {1}", track.ArtistName, track.TrackTitle);
                    AddTrackAndIncrementCount (track);
                } catch (Exception e) {
                    IncrementAddedTracks ();
                    Log.Exception (e);
                    ErrorSource.AddMessage (e.Message, track.Uri.ToString ());
                }
            }
            if (!AddTrackJob.IsFinished) {
                AddTrackJob.Finish ();
            }
            add_track_job = null;
            is_adding = false;
        }

        protected void IncrementAddedTracks ()
        {
            bool finished = false, notify = false;

            lock (this) {
                AddTrackJob.Completed++;

                if (add_track_job.IsFinished) {
                    finished = true;
                } else {
                    if (add_track_job.Completed % 10 == 0)
                        notify = true;
                }
            }

            if (finished) {
                is_adding = false;
            }

            if (notify || finished) {
                OnTracksAdded ();
                if (finished) {
                    ThreadAssist.ProxyToMain (OnUserNotifyUpdated);
                }
            }
        }

        private bool delay_add_job = true;
        protected bool DelayAddJob {
            get { return delay_add_job; }
            set { delay_add_job = value; }
        }

        private bool delay_delete_job = true;
        protected bool DelayDeleteJob {
            get { return delay_delete_job; }
            set { delay_delete_job = value; }
        }

        private BatchUserJob add_track_job;
        protected BatchUserJob AddTrackJob {
            get {
                lock (this) {
                    if (add_track_job == null) {
                        add_track_job = new BatchUserJob (String.Format (Catalog.GetString (
                            "Adding {0} of {1} to {2}"), "{0}", "{1}", Name),
                            Properties.GetStringList ("Icon.Name"));
                        add_track_job.SetResources (Resource.Cpu, Resource.Database, Resource.Disk);
                        add_track_job.PriorityHints = PriorityHints.SpeedSensitive | PriorityHints.DataLossIfStopped;
                        add_track_job.DelayShow = DelayAddJob;
                        add_track_job.CanCancel = true;
                        add_track_job.Register ();
                    }
                }
                return add_track_job;
            }
        }

        private BatchUserJob delete_track_job;
        protected BatchUserJob DeleteTrackJob {
            get {
                lock (this) {
                    if (delete_track_job == null) {
                        delete_track_job = new BatchUserJob (String.Format (Catalog.GetString (
                            "Deleting {0} of {1} From {2}"), "{0}", "{1}", Name),
                            Properties.GetStringList ("Icon.Name"));
                        delete_track_job.SetResources (Resource.Cpu, Resource.Database);
                        delete_track_job.PriorityHints = PriorityHints.SpeedSensitive | PriorityHints.DataLossIfStopped;
                        delete_track_job.DelayShow = DelayDeleteJob;
                        delete_track_job.Register ();
                    }
                }
                return delete_track_job;
            }
        }

        protected override void PruneArtistsAlbums ()
        {
            ServiceManager.DbConnection.Execute (prune_artists_albums_command);
            base.PruneArtistsAlbums ();
            DatabaseAlbumInfo.Reset ();
            DatabaseArtistInfo.Reset ();
        }
    }
}
