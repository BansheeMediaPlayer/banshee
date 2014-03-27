//
// DatabaseSource.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2005-2007 Novell, Inc.
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
using System.Text;
using System.Collections.Generic;

using Mono.Unix;

using Hyena;
using Hyena.Data;
using Hyena.Query;
using Hyena.Data.Sqlite;
using Hyena.Collections;

using Banshee.Base;
using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Configuration;
using Banshee.Query;

namespace Banshee.Sources
{
    public abstract class DatabaseSource : Source, ITrackModelSource, IFilterableSource, IDurationAggregator, IFileSizeAggregator
    {
        public event EventHandler FiltersChanged;

        protected delegate void TrackRangeHandler (DatabaseTrackListModel model, RangeCollection.Range range);

        protected DatabaseTrackListModel track_model;
        protected DatabaseAlbumListModel album_model;
        protected DatabaseFilterListModel<DatabaseArtistInfo, ArtistInfo> artist_model;
        protected DatabaseQueryFilterModel<string> genre_model;

        private RateLimiter reload_limiter;

        public DatabaseSource (string generic_name, string name, string id, int order) : this (generic_name, name, id, order, null)
        {
        }

        public DatabaseSource (string generic_name, string name, string id, int order, Source parent) : base (generic_name, name, order, id)
        {
            if (parent != null) {
                SetParentSource (parent);
            }
            DatabaseSourceInitialize ();
        }

        protected DatabaseSource () : base ()
        {
        }

        public void UpdateCounts ()
        {
            DatabaseTrackModel.UpdateUnfilteredAggregates ();
            ever_counted = true;
            OnUpdated ();
        }

        public abstract void Save ();

        protected override void Initialize ()
        {
            base.Initialize ();
            DatabaseSourceInitialize ();
        }

        protected virtual bool HasArtistAlbum {
            get { return true; }
        }

        public DatabaseTrackListModel DatabaseTrackModel {
            get { return track_model ?? (track_model = (Parent as DatabaseSource ?? this).CreateTrackModelFor (this)); }
            protected set { track_model = value; }
        }

        private IDatabaseTrackModelCache track_cache;
        protected IDatabaseTrackModelCache TrackCache {
            get {
                return track_cache ?? (track_cache = new DatabaseTrackModelCache<DatabaseTrackInfo> (
                    ServiceManager.DbConnection, UniqueId, DatabaseTrackModel, TrackProvider));
            }
            set { track_cache = value; }
        }

        protected DatabaseTrackModelProvider<DatabaseTrackInfo> TrackProvider {
            get { return DatabaseTrackInfo.Provider; }
        }

        private void DatabaseSourceInitialize ()
        {
            InitializeTrackModel ();

            current_filters_schema = CreateSchema<string[]> ("current_filters");

            DatabaseSource filter_src = Parent as DatabaseSource ?? this;
            foreach (IFilterListModel filter in filter_src.CreateFiltersFor (this)) {
                AvailableFilters.Add (filter);
                DefaultFilters.Add (filter);
            }

            reload_limiter = new RateLimiter (RateLimitedReload);
        }

        protected virtual DatabaseTrackListModel CreateTrackModelFor (DatabaseSource src)
        {
            return new DatabaseTrackListModel (ServiceManager.DbConnection, TrackProvider, src);
        }

        protected virtual IEnumerable<IFilterListModel> CreateFiltersFor (DatabaseSource src)
        {
            if (!HasArtistAlbum) {
                yield break;
            }

            DatabaseAlbumArtistListModel albumartist_model = new DatabaseAlbumArtistListModel (src, src.DatabaseTrackModel, ServiceManager.DbConnection, src.UniqueId);
            DatabaseArtistListModel artist_model = new DatabaseArtistListModel (src, src.DatabaseTrackModel, ServiceManager.DbConnection, src.UniqueId);
            DatabaseAlbumListModel album_model = new DatabaseAlbumListModel (src, src.DatabaseTrackModel, ServiceManager.DbConnection, src.UniqueId);
            DatabaseQueryFilterModel<string> genre_model = new DatabaseQueryFilterModel<string> (src, src.DatabaseTrackModel, ServiceManager.DbConnection,
                        Catalog.GetString ("All Genres ({0})"), src.UniqueId, BansheeQuery.GenreField, "Genre");
            DatabaseYearListModel year_model = new DatabaseYearListModel (src, src.DatabaseTrackModel, ServiceManager.DbConnection, src.UniqueId);

            if (this == src) {
                this.artist_model = artist_model;
                this.album_model = album_model;
                this.genre_model = genre_model;
            }

            yield return genre_model;
            yield return albumartist_model;
            yield return artist_model;
            yield return year_model;
            yield return album_model;
        }

        protected virtual void AfterInitialized ()
        {
            DatabaseTrackModel.Initialize (TrackCache);
            OnSetupComplete ();
        }

        protected virtual void InitializeTrackModel ()
        {
        }

        protected bool NeedsReloadWhenFieldsChanged (Hyena.Query.QueryField [] fields)
        {
            if (fields == null) {
                return true;
            }

            foreach (QueryField field in fields)
                if (NeedsReloadWhenFieldChanged (field))
                    return true;

            return false;
        }

        private List<QueryField> query_fields;
        private QueryNode last_query;
        protected virtual bool NeedsReloadWhenFieldChanged (Hyena.Query.QueryField field)
        {
            if (field == null)
                return true;

            // If it's the artist or album name, then we care, since it affects the browser
            // FIXME this should be based on what filters (aka browsers) are active.  InternetRadio,
            // for example, has only a Genre filter.
            if (field == Banshee.Query.BansheeQuery.ArtistField || field == Banshee.Query.BansheeQuery.AlbumField) {
                return true;
            }

            if (DatabaseTrackModel == null) {
                Log.Error ("DatabaseTrackModel should not be null in DatabaseSource.NeedsReloadWhenFieldChanged");
                return false;
            }

            // If it's the field we're sorting by, then yes, we care
            var sort_column = DatabaseTrackModel.SortColumn;
            if (sort_column != null && sort_column.Field == field) {
                return true;
            }

            // Make sure the query isn't dependent on this field
            QueryNode query = DatabaseTrackModel.Query;
            if (query != null) {
                if (query != last_query) {
                    query_fields = new List<QueryField> (query.GetFields ());
                    last_query = query;
                }

                if (query_fields.Contains (field))
                    return true;
            }

            return false;
        }

#region Public Properties

        public override int Count {
            get { return ever_counted ? DatabaseTrackModel.UnfilteredCount : SavedCount; }
        }

        public override int FilteredCount {
            get { return DatabaseTrackModel.Count; }
        }

        public virtual TimeSpan Duration {
            get { return DatabaseTrackModel.Duration; }
        }

        public virtual long FileSize {
            get { return DatabaseTrackModel.FileSize; }
        }

        public override string FilterQuery {
            set {
                base.FilterQuery = value;
                DatabaseTrackModel.UserQuery = FilterQuery;
                ThreadAssist.SpawnFromMain (delegate {
                    Reload ();
                });
            }
        }

        public virtual bool CanAddTracks {
            get { return true; }
        }

        public virtual bool CanRemoveTracks {
            get { return true; }
        }

        public virtual bool CanDeleteTracks {
            get { return true; }
        }

        public virtual bool ConfirmRemoveTracks {
            get { return true; }
        }

        public virtual bool CanRepeat {
            get { return true; }
        }

        public virtual bool CanShuffle {
            get { return true; }
        }

        public override string TrackModelPath {
            get { return DBusServiceManager.MakeObjectPath (DatabaseTrackModel); }
        }

        public TrackListModel TrackModel {
            get { return DatabaseTrackModel; }
        }

        public virtual bool ShowBrowser {
            get { return true; }
        }

        public virtual bool Indexable {
            get { return false; }
        }

        private int saved_count;
        protected int SavedCount {
            get { return saved_count; }
            set { saved_count = value; }
        }

        public override bool AcceptsInputFromSource (Source source)
        {
            return CanAddTracks && source != this;
        }

        public bool IsTemporary { get; set; }

        public override bool AcceptsUserInputFromSource (Source source)
        {
            return base.AcceptsUserInputFromSource (source) && CanAddTracks;
        }

        public override bool HasViewableTrackProperties {
            get { return true; }
        }

#endregion

#region Filters (aka Browsers)

        private IList<IFilterListModel> available_filters;
        public IList<IFilterListModel> AvailableFilters {
            get { return available_filters ?? (available_filters = new List<IFilterListModel> ()); }
            protected set { available_filters = value; }
        }

        private IList<IFilterListModel> default_filters;
        public IList<IFilterListModel> DefaultFilters {
            get { return default_filters ?? (default_filters = new List<IFilterListModel> ()); }
            protected set { default_filters = value; }
        }

        private IList<IFilterListModel> current_filters;
        public IList<IFilterListModel> CurrentFilters {
            get {
                if (current_filters == null) {
                    current_filters = new List<IFilterListModel> ();
                    string [] current = current_filters_schema.Get ();
                    if (current != null) {
                        foreach (string filter_name in current) {
                            foreach (IFilterListModel filter in AvailableFilters) {
                                if (filter.FilterName == filter_name) {
                                    current_filters.Add (filter);
                                    break;
                                }
                            }
                        }
                    } else {
                        foreach (IFilterListModel filter in DefaultFilters) {
                            current_filters.Add (filter);
                        }
                    }
                }
                return current_filters;
            }
            protected set { current_filters = value; }
        }

        public void ReplaceFilter (IFilterListModel old_filter, IFilterListModel new_filter)
        {
            int i = current_filters.IndexOf (old_filter);
            if (i != -1) {
                current_filters[i] = new_filter;
                SaveCurrentFilters ();
            }
        }

        public void AppendFilter (IFilterListModel filter)
        {
            if (current_filters.IndexOf (filter) == -1) {
                current_filters.Add (filter);
                SaveCurrentFilters ();
            }
        }

        public void RemoveFilter (IFilterListModel filter)
        {
            if (current_filters.Remove (filter)) {
                SaveCurrentFilters ();
            }
        }

        private void SaveCurrentFilters ()
        {
            Reload ();
            if (current_filters == null) {
                current_filters_schema.Set (null);
            } else {
                string [] filters = new string [current_filters.Count];
                int i = 0;
                foreach (IFilterListModel filter in CurrentFilters) {
                    filters[i++] = filter.FilterName;
                }
                current_filters_schema.Set (filters);
            }

            EventHandler handler = FiltersChanged;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }

        private SchemaEntry<string[]> current_filters_schema;

#endregion

#region Public Methods

        public virtual void Reload ()
        {
            ever_counted = ever_reloaded = true;
            reload_limiter.Execute ();
        }

        protected void RateLimitedReload ()
        {
            lock (track_model) {
                DatabaseTrackModel.Reload ();
            }
            OnUpdated ();
            Save ();
        }

        public virtual bool HasDependencies {
            get { return false; }
        }

        public void RemoveTrack (int index)
        {
            if (index != -1) {
                first_nonremoved_track = TrackModel[index - 1];
                RemoveTrackRange (track_model, new RangeCollection.Range (index, index));
                OnTracksRemoved ();
            }
        }

        public void RemoveTrack (DatabaseTrackInfo track)
        {
            RemoveTrack (track_model.IndexOf (track));
        }

        public void RemoveTracks (Selection selection)
        {
            RemoveTracks (track_model, selection);
        }

        public void RemoveTracks (DatabaseTrackListModel model, Selection selection)
        {
            FindFirstNotRemovedTrack (model, selection);
            WithTrackSelection (model, selection, RemoveTrackRange);
            OnTracksRemoved ();
        }

        protected void FindFirstNotRemovedTrack (DatabaseTrackListModel model, Selection selection)
        {
            first_nonremoved_track = null;

            var playback_src = ServiceManager.PlaybackController.Source as DatabaseSource;
            if (playback_src != this && playback_src.Parent != this)
                return;

            int i = model.IndexOf (ServiceManager.PlayerEngine.CurrentTrack);
            if (!selection.Contains (i))
                return;

            var range = selection.Ranges.First (r => r.Start <= i && i <= r.End);
            first_nonremoved_track = model[range.Start - 1];
        }

        public void DeleteTracks (Selection selection)
        {
            DeleteTracks (track_model, selection);
        }

        public virtual void DeleteTracks (DatabaseTrackListModel model, Selection selection)
        {
            if (model == null)
                return;

            FindFirstNotRemovedTrack (model, selection);
            WithTrackSelection (model, selection, DeleteTrackRange);
            OnTracksDeleted ();
        }

        public bool AddSelectedTracks (Source source)
        {
            var track_src = source as ITrackModelSource;
            return AddSelectedTracks (source, track_src == null ? null : track_src.TrackModel.Selection);
        }

        public virtual bool AddSelectedTracks (Source source, Selection selection)
        {
            if (!AcceptsInputFromSource (source) || selection.Count == 0)
                return false;

            DatabaseTrackListModel model = (source as ITrackModelSource).TrackModel as DatabaseTrackListModel;
            if (model == null) {
                return false;
            }

            WithTrackSelection (model, selection, AddTrackRange);
            OnTracksAdded ();
            OnUserNotifyUpdated ();
            return true;
        }

        public bool AddAllTracks (Source source)
        {
            var track_src = source as ITrackModelSource;
            if (track_src != null) {
                var selection = new Selection () { MaxIndex = track_src.TrackModel.Selection.MaxIndex };
                selection.SelectAll ();
                return AddSelectedTracks (source, selection);
            }
            return false;
        }

        public virtual void RateSelectedTracks (int rating)
        {
            RateSelectedTracks (track_model, rating);
        }

        public virtual void RateSelectedTracks (DatabaseTrackListModel model, int rating)
        {
            Selection selection = model.Selection;
            if (selection.Count == 0)
                return;

            lock (model) {
                foreach (RangeCollection.Range range in selection.Ranges) {
                    RateTrackRange (model, range, rating);
                }
            }
            OnTracksChanged (BansheeQuery.RatingField);

            // In case we updated the currently playing track
            DatabaseTrackInfo track = ServiceManager.PlayerEngine.CurrentTrack as DatabaseTrackInfo;
            if (track != null) {
                track.Refresh ();
                ServiceManager.PlayerEngine.TrackInfoUpdated ();
            }
        }

        public override SourceMergeType SupportedMergeTypes {
            get { return SourceMergeType.All; }
        }

        public override void MergeSourceInput (Source source, SourceMergeType mergeType)
        {
            if (mergeType == SourceMergeType.Source || mergeType == SourceMergeType.All) {
                AddAllTracks (source);
            } else if (mergeType == SourceMergeType.ModelSelection) {
                AddSelectedTracks (source);
            }
        }

#endregion

#region Protected Methods

        protected virtual void OnTracksAdded ()
        {
            Reload ();
        }

        protected void OnTracksChanged ()
        {
            OnTracksChanged (null);
        }

        protected virtual void OnTracksChanged (params QueryField [] fields)
        {
            HandleTracksChanged (this, new TrackEventArgs (fields));
            foreach (PrimarySource psource in PrimarySources.ToArray ()) {
                psource.NotifyTracksChanged (fields);
            }
        }

        TrackInfo first_nonremoved_track = null;
        protected virtual void OnTracksDeleted ()
        {
            PruneArtistsAlbums ();
            HandleTracksDeleted (this, new TrackEventArgs ());
            foreach (PrimarySource psource in PrimarySources) {
                psource.NotifyTracksDeleted ();
            }
            SkipTrackIfRemoved ();
        }

        protected virtual void OnTracksRemoved ()
        {
            PruneArtistsAlbums ();
            Reload ();
            SkipTrackIfRemoved ();
        }

        protected void SkipTrackIfRemoved ()
        {
            var playback_src = ServiceManager.PlaybackController.Source as DatabaseSource;
            if (playback_src == null || (playback_src != this && playback_src.Parent != this))
                return;

            var track = ServiceManager.PlayerEngine.CurrentTrack as DatabaseTrackInfo;
            if (track == null || playback_src.DatabaseTrackModel.Contains (track))
                return;

            Log.DebugFormat ("Playing track was removed from {0}, so stopping it", this);
            if (ServiceManager.PlaybackController.StopWhenFinished) {
                ServiceManager.PlayerEngine.Close ();
            } else {
                // Set the PriorTrack to the track before the one we removed, so that, if we're in non-shuffle mode,
                // we'll pick the first non-removed track after the one that was playing
                if (first_nonremoved_track != null) {
                    ServiceManager.PlaybackController.PriorTrack = first_nonremoved_track;
                }
                ServiceManager.PlaybackController.Next ();
            }
        }

        // If we are a PrimarySource, return ourself and our children, otherwise if our Parent
        // is one, do so for it, otherwise do so for all PrimarySources.
        private IEnumerable<PrimarySource> PrimarySources {
            get {
                PrimarySource psource;
                if ((psource = this as PrimarySource) != null) {
                    yield return psource;
                } else {
                    if ((psource = Parent as PrimarySource) != null) {
                        yield return psource;
                    } else {
                        foreach (Source source in ServiceManager.SourceManager.Sources) {
                            if ((psource = source as PrimarySource) != null) {
                                yield return psource;
                            }
                        }
                    }
                }
            }
        }

        protected HyenaSqliteCommand rate_track_range_command;
        protected HyenaSqliteCommand RateTrackRangeCommand {
            get {
                if (rate_track_range_command == null) {
                    if (track_model.CachesJoinTableEntries) {
                        rate_track_range_command = new HyenaSqliteCommand (String.Format (@"
                            UPDATE CoreTracks SET Rating = ?, DateUpdatedStamp = ? WHERE
                                TrackID IN (SELECT TrackID FROM {0} WHERE
                                    {1} IN (SELECT ItemID FROM CoreCache WHERE ModelID = {2} LIMIT ?, ?))",
                            track_model.JoinTable, track_model.JoinPrimaryKey, track_model.CacheId
                        ));
                    } else {
                        rate_track_range_command = new HyenaSqliteCommand (String.Format (@"
                            UPDATE CoreTracks SET Rating = ?, DateUpdatedStamp = ? WHERE TrackID IN (
                                SELECT ItemID FROM CoreCache WHERE ModelID = {0} LIMIT ?, ?)",
                            track_model.CacheId
                        ));
                    }
                }
                return rate_track_range_command;
            }
        }

        private bool ever_reloaded = false, ever_counted = false;
        public override void Activate ()
        {
            if (!ever_reloaded)
                Reload ();
        }

        public override void Deactivate ()
        {
            DatabaseTrackModel.InvalidateCache (false);
            foreach (IFilterListModel filter in AvailableFilters) {
                filter.InvalidateCache (false);
            }
        }

        protected virtual void RemoveTrackRange (DatabaseTrackListModel model, RangeCollection.Range range)
        {
            Log.ErrorFormat ("RemoveTrackRange not implemented by {0}", this);
        }

        protected virtual void DeleteTrackRange (DatabaseTrackListModel model, RangeCollection.Range range)
        {
            Log.ErrorFormat ("DeleteTrackRange not implemented by {0}", this);
        }

        protected virtual void AddTrackRange (DatabaseTrackListModel model, RangeCollection.Range range)
        {
            Log.ErrorFormat ("AddTrackRange not implemented by {0}", this);
        }

        protected virtual void AddTrack (DatabaseTrackInfo track)
        {
            Log.ErrorFormat ("AddTrack not implemented by {0}", this);
        }

        protected virtual void RateTrackRange (DatabaseTrackListModel model, RangeCollection.Range range, int rating)
        {
            ServiceManager.DbConnection.Execute (RateTrackRangeCommand,
                rating, DateTime.Now, range.Start, range.End - range.Start + 1);
        }

        protected void WithTrackSelection (DatabaseTrackListModel model, TrackRangeHandler handler)
        {
            WithTrackSelection (model, model.Selection, handler);
        }

        protected void WithTrackSelection (DatabaseTrackListModel model, Selection selection, TrackRangeHandler handler)
        {
            if (selection.Count == 0)
                return;

            lock (model) {
                foreach (RangeCollection.Range range in selection.Ranges) {
                    handler (model, range);
                }
            }
        }

        protected HyenaSqliteCommand prune_command;
        protected HyenaSqliteCommand PruneCommand {
            get {
                return prune_command ?? (prune_command = new HyenaSqliteCommand (String.Format (
                        @"DELETE FROM CoreCache WHERE ModelID = ? AND ItemID NOT IN (SELECT ArtistID FROM CoreTracks WHERE TrackID IN (SELECT {0}));
                          DELETE FROM CoreCache WHERE ModelID = ? AND ItemID NOT IN (SELECT AlbumID FROM CoreTracks WHERE TrackID IN (SELECT {0}));",
                        track_model.TrackIdsSql
                    ),
                    artist_model.CacheId, artist_model.CacheId, 0, artist_model.Count,
                    album_model.CacheId, album_model.CacheId, 0, album_model.Count
                ));
            }
        }

        protected void InvalidateCaches ()
        {
            track_model.InvalidateCache (true);

            foreach (IFilterListModel filter in CurrentFilters) {
                filter.InvalidateCache (true);
            }
        }

        protected virtual void PruneArtistsAlbums ()
        {
            //Console.WriteLine ("Pruning with {0}", PruneCommand.Text);
            //ServiceManager.DbConnection.Execute (PruneCommand);
        }

        protected virtual void HandleTracksAdded (Source sender, TrackEventArgs args)
        {
        }

        protected virtual void HandleTracksChanged (Source sender, TrackEventArgs args)
        {
        }

        protected virtual void HandleTracksDeleted (Source sender, TrackEventArgs args)
        {
        }

#endregion

    }
}
