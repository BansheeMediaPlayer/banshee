//
// PlaylistSource.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2005-2008 Novell, Inc.
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
using System.Collections;
using System.Collections.Generic;

using Mono.Unix;

using Hyena;
using Hyena.Data;
using Hyena.Data.Sqlite;
using Hyena.Collections;

using Banshee.Base;
using Banshee.ServiceStack;
using Banshee.Database;
using Banshee.Sources;
using Banshee.Collection;
using Banshee.Collection.Database;

namespace Banshee.Playlist
{
    public class PlaylistSource : AbstractPlaylistSource, IUnmapableSource
    {
        private static HyenaSqliteCommand add_track_range_command;
        private static HyenaSqliteCommand add_track_command;
        private static HyenaSqliteCommand remove_track_range_command;

        private static string add_track_range_from_joined_model_sql;

        private static string generic_name = Catalog.GetString ("Playlist");

        protected override string SourceTable {
            get { return "CorePlaylists"; }
        }

        protected override string SourcePrimaryKey {
            get { return "PlaylistID"; }
        }

        protected override string TrackJoinTable {
            get { return "CorePlaylistEntries"; }
        }

        protected long MaxViewOrder {
            get {
                return ServiceManager.DbConnection.Query<long> (
                    "SELECT MAX(ViewOrder) + 1 FROM CorePlaylistEntries WHERE PlaylistID = ?", DbId);
            }
        }

        static PlaylistSource ()
        {
            add_track_range_command = new HyenaSqliteCommand (@"
                INSERT INTO CorePlaylistEntries
                    (EntryID, PlaylistID, TrackID, ViewOrder)
                    SELECT null, ?, ItemID, OrderId + ?
                        FROM CoreCache WHERE ModelID = ?
                        LIMIT ?, ?"
            );

            add_track_command = new HyenaSqliteCommand (@"
                INSERT INTO CorePlaylistEntries
                    (EntryID, PlaylistID, TrackID, ViewOrder)
                    VALUES (null, ?, ?, ?)"
            );

            add_track_range_from_joined_model_sql = @"
                INSERT INTO CorePlaylistEntries
                    (EntryID, PlaylistID, TrackID, ViewOrder)
                    SELECT null, ?, TrackID, OrderId + ?
                        FROM CoreCache c INNER JOIN {0} e ON c.ItemID = e.{1}
                        WHERE ModelID = ?
                        LIMIT ?, ?";

            remove_track_range_command = new HyenaSqliteCommand (@"
                DELETE FROM CorePlaylistEntries WHERE PlaylistID = ? AND
                    EntryID IN (SELECT ItemID FROM CoreCache
                        WHERE ModelID = ? ORDER BY OrderID LIMIT ?, ?)"
            );
        }

#region Constructors

        public PlaylistSource (string name, PrimarySource parent) : base (generic_name, name, parent)
        {
            SetProperties ();
        }

        protected PlaylistSource (string name, int dbid, PrimarySource parent) : this (name, dbid, -1, 0, parent, 0, false)
        {
        }

        protected PlaylistSource (string name, int dbid, int sortColumn, int sortType, PrimarySource parent, int count, bool is_temp)
            : base (generic_name, name, dbid, sortColumn, sortType, parent, is_temp)
        {
            SetProperties ();
            SavedCount = count;
        }

#endregion

        private void SetProperties ()
        {
            Properties.SetString ("Icon.Name", "source-playlist");
            Properties.SetString ("RemoveTracksActionLabel", Catalog.GetString ("Remove From Playlist"));
            Properties.SetString ("UnmapSourceActionLabel", Catalog.GetString ("Delete Playlist"));
        }

#region Source Overrides

        public override bool AcceptsInputFromSource (Source source)
        {
            return base.AcceptsInputFromSource (source) && (
                source == Parent || (source.Parent == Parent || Parent == null)
                // This is commented out because we don't support (yet?) DnD from Play Queue to a playlist
                //(source.Parent == Parent || Parent == null || (source.Parent == null && !(source is PrimarySource)))
            );
        }

        public override SourceMergeType SupportedMergeTypes {
            get { return SourceMergeType.All; }
        }

#endregion

#region AbstractPlaylist overrides

        protected override void AfterInitialized ()
        {
            base.AfterInitialized ();
            if (PrimarySource != null) {
                PrimarySource.TracksChanged += HandleTracksChanged;
                PrimarySource.TracksDeleted += HandleTracksDeleted;
            }

            TrackModel.CanReorder = true;
        }

        protected override void Update ()
        {
            ServiceManager.DbConnection.Execute (new HyenaSqliteCommand (
                String.Format (
                    @"UPDATE {0}
                        SET Name = ?,
                            SortColumn = ?,
                            SortType = ?,
                            CachedCount = ?,
                            IsTemporary = ?
                        WHERE PlaylistID = ?",
                    SourceTable
                ), Name, -1, 0, Count, IsTemporary, dbid
            ));
        }

        protected override void Create ()
        {
            DbId = ServiceManager.DbConnection.Execute (new HyenaSqliteCommand (
                @"INSERT INTO CorePlaylists (PlaylistID, Name, SortColumn, SortType, PrimarySourceID, IsTemporary)
                    VALUES (NULL, ?, ?, ?, ?, ?)",
                Name, -1, 1, PrimarySourceId, IsTemporary //SortColumn, SortType
            ));
        }

#endregion

#region DatabaseSource overrides

        // We can add tracks only if our parent can
        public override bool CanAddTracks {
            get {
                DatabaseSource ds = Parent as DatabaseSource;
                return ds != null ? ds.CanAddTracks : base.CanAddTracks;
            }
        }

        // We can remove tracks only if our parent can
        public override bool CanRemoveTracks {
            get {
                return (Parent is PrimarySource)
                    ? !(Parent as PrimarySource).PlaylistsReadOnly
                    : true;
            }
        }

#endregion

#region IUnmapableSource Implementation

        public virtual bool Unmap ()
        {
            if (DbId != null) {
                ServiceManager.DbConnection.Execute (new HyenaSqliteCommand (@"
                    BEGIN TRANSACTION;
                        DELETE FROM CorePlaylists WHERE PlaylistID = ?;
                        DELETE FROM CorePlaylistEntries WHERE PlaylistID = ?;
                    COMMIT TRANSACTION",
                    DbId, DbId
                ));
            }

            ThreadAssist.ProxyToMain (Remove);
            return true;
        }

#endregion

        protected void AddTrack (int track_id)
        {
            ServiceManager.DbConnection.Execute (add_track_command, DbId, track_id, MaxViewOrder);
            OnTracksAdded ();
        }

        protected override void AddTrack (DatabaseTrackInfo track)
        {
            AddTrack (track.TrackId);
        }

        public override bool AddSelectedTracks (Source source, Selection selection)
        {
            if (Parent == null || source == Parent || source.Parent == Parent) {
                return base.AddSelectedTracks (source, selection);
            } else {
                // Adding from a different primary source, so add to our primary source first
                //PrimarySource primary = Parent as PrimarySource;
                //primary.AddSelectedTracks (model);
                // then add to us
                //Log.Information ("Note: Feature Not Implemented", String.Format ("In this alpha release, you can only add tracks to {0} from {1} or its playlists.", Name, Parent.Name), true);
            }
            return false;
        }

        public virtual void ReorderSelectedTracks (int drop_row)
        {
            if (TrackModel.Selection.Count == 0 || TrackModel.Selection.AllSelected) {
                return;
            }

            TrackInfo track = TrackModel[drop_row];
            long order = track == null
                ? ServiceManager.DbConnection.Query<long> ("SELECT MAX(ViewOrder) + 1 FROM CorePlaylistEntries WHERE PlaylistID = ?", DbId)
                : ServiceManager.DbConnection.Query<long> ("SELECT ViewOrder FROM CorePlaylistEntries WHERE PlaylistID = ? AND EntryID = ?", DbId, Convert.ToInt64 (track.CacheEntryId));

            // Make room for our new items
            if (track != null) {
                ServiceManager.DbConnection.Execute ("UPDATE CorePlaylistEntries SET ViewOrder = ViewOrder + ? WHERE PlaylistID = ? AND ViewOrder >= ?",
                    TrackModel.Selection.Count, DbId, order
                );
            }

            HyenaSqliteCommand update_command = new HyenaSqliteCommand (String.Format ("UPDATE CorePlaylistEntries SET ViewOrder = ? WHERE PlaylistID = {0} AND EntryID = ?", DbId));
            HyenaSqliteCommand select_command = new HyenaSqliteCommand (String.Format ("SELECT ItemID FROM CoreCache WHERE ModelID = {0} LIMIT ?, ?", DatabaseTrackModel.CacheId));

            // Reorder the selected items
            ServiceManager.DbConnection.BeginTransaction ();
            foreach (RangeCollection.Range range in TrackModel.Selection.Ranges) {
                foreach (long entry_id in ServiceManager.DbConnection.QueryEnumerable<long> (select_command, range.Start, range.Count)) {
                    ServiceManager.DbConnection.Execute (update_command, order++, entry_id);
                }
            }
            ServiceManager.DbConnection.CommitTransaction ();

            Reload ();
        }

        DatabaseTrackListModel last_add_range_from_model;
        HyenaSqliteCommand last_add_range_command = null;
        protected override void AddTrackRange (DatabaseTrackListModel from, RangeCollection.Range range)
        {
            last_add_range_command = (!from.CachesJoinTableEntries)
                ? add_track_range_command
                : from == last_add_range_from_model
                    ? last_add_range_command
                    : new HyenaSqliteCommand (String.Format (add_track_range_from_joined_model_sql, from.JoinTable, from.JoinPrimaryKey));

            long first_order_id = ServiceManager.DbConnection.Query<long> ("SELECT OrderID FROM CoreCache WHERE ModelID = ? LIMIT 1 OFFSET ?", from.CacheId, range.Start);
            ServiceManager.DbConnection.Execute (last_add_range_command, DbId, MaxViewOrder - first_order_id, from.CacheId, range.Start, range.Count);

            last_add_range_from_model = from;
        }

        protected override void RemoveTrackRange (DatabaseTrackListModel from, RangeCollection.Range range)
        {
            ServiceManager.DbConnection.Execute (remove_track_range_command,
                DbId, from.CacheId, range.Start, range.Count);
        }

        protected override void HandleTracksChanged (Source sender, TrackEventArgs args)
        {
            if (args.When > last_updated) {
                last_updated = args.When;
                // Playlists do not need to reload if only certain columns are changed
                if (NeedsReloadWhenFieldsChanged (args.ChangedFields)) {
                    // TODO Optimization: playlists only need to reload if one of their tracks was updated
                    //if (ServiceManager.DbConnection.Query<int> (count_updated_command, last_updated) > 0) {
                        Reload ();
                    //}
                } else {
                    InvalidateCaches ();
                }
            }
        }

        protected override void HandleTracksDeleted (Source sender, TrackEventArgs args)
        {
            if (args.When > last_removed) {
                last_removed = args.When;
                Reload ();
                /*if (ServiceManager.DbConnection.Query<int> (count_removed_command, last_removed) > 0) {
                    //ServiceManager.DbConnection.Execute ("DELETE FROM CoreCache WHERE ModelID = ? AND ItemID IN (SELECT EntryID FROM CorePlaylistEntries WHERE PlaylistID = ? AND TrackID IN (TrackID FROM CoreRemovedTracks))");
                    ServiceManager.DbConnection.Execute ("DELETE FROM CorePlaylistEntries WHERE TrackID IN (SELECT TrackID FROM CoreRemovedTracks)");
                    //track_model.UpdateAggregates ();
                    //OnUpdated ();
                }*/
            }
        }

        public static IEnumerable<PlaylistSource> LoadAll (PrimarySource parent)
        {
            ClearTemporary ();
            using (HyenaDataReader reader = new HyenaDataReader (ServiceManager.DbConnection.Query (
                @"SELECT PlaylistID, Name, SortColumn, SortType, PrimarySourceID, CachedCount, IsTemporary FROM CorePlaylists
                    WHERE Special = 0 AND PrimarySourceID = ?", parent.DbId))) {
                while (reader.Read ()) {
                    yield return new PlaylistSource (
                        reader.Get<string> (1), reader.Get<int> (0),
                        reader.Get<int> (2), reader.Get<int> (3), parent,
                        reader.Get<int> (5), reader.Get<bool> (6)
                    );
                }
            }
        }

        private static void ClearTemporary ()
        {
            ServiceManager.DbConnection.BeginTransaction ();
            ServiceManager.DbConnection.Execute (@"
                DELETE FROM CorePlaylistEntries WHERE PlaylistID IN (SELECT PlaylistID FROM CorePlaylists WHERE IsTemporary = 1);
                DELETE FROM CorePlaylists WHERE IsTemporary = 1;"
            );
            ServiceManager.DbConnection.CommitTransaction ();
        }

        public static void ClearTemporary (PrimarySource parent)
        {
            if (parent != null) {
                ServiceManager.DbConnection.BeginTransaction ();
                ServiceManager.DbConnection.Execute (@"
                    DELETE FROM CorePlaylistEntries WHERE PlaylistID IN (SELECT PlaylistID FROM CorePlaylists WHERE PrimarySourceID = ? AND IsTemporary = 1);
                    DELETE FROM CorePlaylists WHERE PrimarySourceID = ? AND IsTemporary = 1;", parent.DbId, parent.DbId
                );
                ServiceManager.DbConnection.CommitTransaction ();
            }
        }

        private static int GetPlaylistId (string name)
        {
            return ServiceManager.DbConnection.Query<int> (
                "SELECT PlaylistID FROM Playlists WHERE Name = ? LIMIT 1", name
            );
        }

        private static bool PlaylistExists (string name)
        {
            return GetPlaylistId (name) > 0;
        }

        public static string CreateUniqueName ()
        {
            return NamingUtil.PostfixDuplicate (Catalog.GetString ("New Playlist"), PlaylistExists);
        }

        public static string CreateUniqueName (IEnumerable tracks)
        {
            return NamingUtil.PostfixDuplicate (NamingUtil.GenerateTrackCollectionName (
                tracks, Catalog.GetString ("New Playlist")), PlaylistExists);
        }
    }
}
