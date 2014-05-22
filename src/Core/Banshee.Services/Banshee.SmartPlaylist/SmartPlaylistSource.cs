//
// SmartPlaylistSource.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2006-2007 Gabriel Burt
// Copyright (C) 2007 Novell, Inc.
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
using Hyena.Query;
using Hyena.Data.Sqlite;

using Banshee.Base;
using Banshee.Query;
using Banshee.Sources;
using Banshee.Database;
using Banshee.Playlist;
using Banshee.ServiceStack;
using Banshee.Collection;
using Banshee.Collection.Database;

#pragma warning disable 0169

namespace Banshee.SmartPlaylist
{
    public class SmartPlaylistSource : AbstractPlaylistSource, IUnmapableSource
    {
        private static List<SmartPlaylistSource> playlists = new List<SmartPlaylistSource> ();
        private static uint timeout_id = 0;

        static SmartPlaylistSource () {
            Migrator.MigrateAll ();

            ServiceManager.SourceManager.SourceAdded += HandleSourceAdded;
            ServiceManager.SourceManager.SourceRemoved += HandleSourceRemoved;
        }

        private static string generic_name = Catalog.GetString ("Smart Playlist");
        private static string properties_label = Catalog.GetString ("Edit Smart Playlist");

        private QueryOrder query_order;
        private QueryLimit limit;
        private IntegerQueryValue limit_value;

        private List<SmartPlaylistSource> dependencies = new List<SmartPlaylistSource>();


#region Properties

        // Source override
        public override bool HasProperties {
            get { return true; }
        }

        public override bool CanAddTracks {
            get { return false; }
        }

        public override bool CanRemoveTracks {
            get { return false; }
        }

        // AbstractPlaylistSource overrides
        protected override string SourceTable {
            get { return "CoreSmartPlaylists"; }
        }

        protected override string SourcePrimaryKey {
            get { return "SmartPlaylistID"; }
        }

        protected override string TrackJoinTable {
            get { return "CoreSmartPlaylistEntries"; }
        }

        protected override bool CachesJoinTableEntries {
            get { return false; }
        }

        // Custom properties
        private List<QueryField> relevant_fields = new List<QueryField> ();
        private QueryNode condition;
        public QueryNode ConditionTree {
            get { return condition; }
            set {
                condition = value;
                relevant_fields.Clear ();
                if (condition != null) {
                    condition_sql = condition.ToSql (BansheeQuery.FieldSet);
                    //until bug in DB layer is fixed, escape String.Format chars
                    condition_sql = condition_sql.Replace ("{", "{{").Replace ("}", "}}");
                    condition_xml = condition.ToXml (BansheeQuery.FieldSet);

                    foreach (QueryTermNode term in condition.GetTerms ()) {
                        if (!relevant_fields.Contains (term.Field))
                            relevant_fields.Add (term.Field);
                    }
                } else {
                    condition_sql = null;
                    condition_xml = null;
                }
            }
        }

        private string additional_conditions;
        public void AddCondition (string part)
        {
            if (!String.IsNullOrEmpty (part)) {
                additional_conditions = additional_conditions == null ? part : String.Format ("{0} AND {1}", additional_conditions, part);
            }
        }

        private string condition_sql;
        public virtual string ConditionSql {
            get { return condition_sql; }
            protected set { condition_sql = value; }
        }

        private string condition_xml;
        public string ConditionXml {
            get { return condition_xml; }
            set {
                condition_xml = value;
                ConditionTree = XmlQueryParser.Parse (condition_xml, BansheeQuery.FieldSet);
            }
        }

        public QueryOrder QueryOrder {
            get { return query_order; }
            set {
                query_order = value;
                if (value != null && value.Field != null) {
                    Properties.Set<string> ("TrackListSortField", value.Field.Name);
                    Properties.Set<bool> ("TrackListSortAscending", value.Ascending);
                } else {
                    Properties.Remove ("TrackListSortField");
                    Properties.Remove ("TrackListSortAscending");
                }
            }
        }

        public IntegerQueryValue LimitValue {
            get { return limit_value; }
            set { limit_value = value; }
        }

        public QueryLimit Limit {
            get { return limit; }
            set { limit = value; }
        }

        protected string OrderSql {
            get { return !IsLimited || QueryOrder == null ? null : QueryOrder.ToSql (); }
        }

        protected string LimitSql {
            get { return IsLimited ? Limit.ToSql (LimitValue) : null; }
        }

        public bool IsLimited {
            get {
                return (Limit != null && LimitValue != null && !LimitValue.IsEmpty && QueryOrder != null);
            }
        }

        public bool IsHiddenWhenEmpty { get; private set; }

        public override bool HasDependencies {
            get { return dependencies.Count > 0; }
        }

        // FIXME scan ConditionTree for date fields
        // FIXME even better, scan for date fields and see how fine-grained they are;
        //       eg if set to Last Added < 2 weeks ago, don't need a timer going off
        //       every 1 minute - every hour or two would suffice.  Just change this
        //       property to a TimeSpan, rename it TimeDependentResolution or something
        public bool TimeDependent {
            get { return false; }
        }

#endregion

#region Constructors

        public SmartPlaylistSource (string name, PrimarySource parent) : base (generic_name, name, parent)
        {
            SetProperties ();
        }

        public SmartPlaylistSource (string name, QueryNode condition, QueryOrder order, QueryLimit limit, IntegerQueryValue limit_value, PrimarySource parent)
            : this (name, condition, order, limit, limit_value, false, parent)
        {
        }

        public SmartPlaylistSource (string name, QueryNode condition, QueryOrder order, QueryLimit limit, IntegerQueryValue limit_value, bool hiddenWhenEmpty, PrimarySource parent)
            : this (name, parent)
        {
            ConditionTree = condition;
            QueryOrder = order;
            Limit = limit;
            LimitValue = limit_value;
            IsHiddenWhenEmpty = hiddenWhenEmpty;

            UpdateDependencies ();
        }

        // For existing smart playlists that we're loading from the database
        protected SmartPlaylistSource (long dbid, string name, string condition_xml, string order_by, string limit_number, string limit_criterion, PrimarySource parent, int count, bool is_temp, bool hiddenWhenEmpty) :
            base (generic_name, name, dbid, -1, 0, parent, is_temp)
        {
            ConditionXml = condition_xml;
            QueryOrder = BansheeQuery.FindOrder (order_by);
            Limit = BansheeQuery.FindLimit (limit_criterion);
            LimitValue = new IntegerQueryValue ();
            LimitValue.ParseUserQuery (limit_number);
            SavedCount = count;
            IsHiddenWhenEmpty = hiddenWhenEmpty;

            SetProperties ();
            UpdateDependencies ();
        }

        private void SetProperties ()
        {
            Properties.SetString ("Icon.Name", "source-smart-playlist");
            Properties.SetString ("SourcePropertiesActionLabel", properties_label);
            Properties.SetString ("UnmapSourceActionLabel", Catalog.GetString ("Delete Smart Playlist"));
            Properties.SetString ("RemovePlayingTrackActionLabel", Catalog.GetString ("Remove From Library"));
        }

#endregion

#region Public Methods

        public void ListenToPlaylists ()
        {
        }

        public bool DependsOn (SmartPlaylistSource source)
        {
            dependencies.Contains (source);
            return false;
        }

#endregion

#region Private Methods

        private void UpdateDependencies ()
        {
            foreach (SmartPlaylistSource s in dependencies) {
                s.Updated -= OnDependencyUpdated;
            }

            dependencies.Clear ();

            if (ConditionTree != null) {
                foreach (SmartPlaylistQueryValue value in ConditionTree.SearchForValues<SmartPlaylistQueryValue> ()) {
                    SmartPlaylistSource playlist = value.ObjectValue;
                    if (playlist != null) {
                        playlist.Updated += OnDependencyUpdated;
                        dependencies.Add (playlist);
                    }
                }
            }
        }

        private void OnDependencyUpdated (object sender, EventArgs args)
        {
            Reload ();
        }

#endregion

#region AbstractPlaylist overrides

        protected override void AfterInitialized ()
        {
            base.AfterInitialized ();

            if (PrimarySource != null) {
                PrimarySource.TracksAdded += HandleTracksAdded;
                PrimarySource.TracksChanged += HandleTracksChanged;
                PrimarySource.TracksDeleted += HandleTracksDeleted;
            }

            if (IsHiddenWhenEmpty) {
                RefreshAndReload ();
            }
        }

        protected override void Create ()
        {
            DbId = ServiceManager.DbConnection.Execute (new HyenaSqliteCommand (@"
                INSERT INTO CoreSmartPlaylists
                    (Name, Condition, OrderBy, LimitNumber, LimitCriterion, PrimarySourceID, IsTemporary, IsHiddenWhenEmpty)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
                Name, ConditionXml,
                QueryOrder != null ? QueryOrder.Name : null,
                IsLimited ? LimitValue.ToSql () : null,
                IsLimited ? Limit.Name : null,
                PrimarySourceId, IsTemporary, IsHiddenWhenEmpty
            ));
            UpdateDependencies ();
        }

        protected override void Update ()
        {
            ServiceManager.DbConnection.Execute (new HyenaSqliteCommand (@"
                UPDATE CoreSmartPlaylists
                    SET Name = ?,
                        Condition = ?,
                        OrderBy = ?,
                        LimitNumber = ?,
                        LimitCriterion = ?,
                        CachedCount = ?,
                        IsTemporary = ?,
                        IsHiddenWhenEmpty = ?
                    WHERE SmartPlaylistID = ?",
                Name, ConditionXml,
                IsLimited ? QueryOrder.Name : null,
                IsLimited ? LimitValue.ToSql () : null,
                IsLimited ? Limit.Name : null,
                Count, IsTemporary, IsHiddenWhenEmpty, DbId
            ));
            UpdateDependencies ();
        }

        protected override bool NeedsReloadWhenFieldChanged (Hyena.Query.QueryField field)
        {
            if (base.NeedsReloadWhenFieldChanged (field))
                return true;

            if (QueryOrder != null && QueryOrder.Field == field)
                return true;

            if (relevant_fields.Contains (field))
                return true;

            return false;
        }

#endregion

#region DatabaseSource overrides

        public void RefreshAndReload ()
        {
            Refresh ();
            Reload ();
        }

        private bool refreshed = false;
        public override void Reload ()
        {
            if (!refreshed) {
                Refresh ();
            } else {
                // Don't set this on the first refresh
                Properties.Set<bool> ("NotifyWhenAdded", IsHiddenWhenEmpty);
            }

            base.Reload ();

            if (IsHiddenWhenEmpty && Parent != null) {
                bool contains_me = Parent.ContainsChildSource (this);
                int count = Count;

                if (count == 0 && contains_me) {
                    Parent.RemoveChildSource (this);
                } else if (count > 0 && !contains_me) {
                    Parent.AddChildSource (this);
                }
            }
        }

        public void Refresh ()
        {
            // Wipe the member list clean and repopulate it
            string reload_str = String.Format (
                @"DELETE FROM CoreSmartPlaylistEntries WHERE SmartPlaylistID = {0};
                  INSERT INTO CoreSmartPlaylistEntries
                    (EntryID, SmartPlaylistID, TrackID)
                    SELECT NULL, {0} as SmartPlaylistID, TrackId FROM {1}
                        WHERE {2} AND CoreTracks.PrimarySourceID = {3}
                        {4} {5} {6}",
                DbId, DatabaseTrackInfo.Provider.From, DatabaseTrackInfo.Provider.Where,
                PrimarySourceId, PrependCondition ("AND"), OrderSql, LimitSql
            );
            ServiceManager.DbConnection.Execute (reload_str);

            // If the smart playlist is limited by file size or media duration, limit it here
            if (IsLimited && !Limit.RowBased) {
                // Identify where the cut off mark is
                HyenaSqliteCommand limit_command = new HyenaSqliteCommand (String.Format (
                    @"SELECT EntryID, {0}
                      FROM CoreTracks, CoreSmartPlaylistEntries
                      WHERE SmartPlaylistID = {1} AND CoreSmartPlaylistEntries.TrackID = CoreTracks.TrackID
                      ORDER BY EntryID",
                    Limit.Column, DbId
                ));

                long limit = LimitValue.IntValue *  Limit.Factor;
                long sum = 0;
                long? cut_off_id = null;
                using (IDataReader reader = ServiceManager.DbConnection.Query (limit_command)) {
                    while (reader.Read ()) {
                        sum += Convert.ToInt64 (reader[1]);
                        if (sum > limit) {
                            cut_off_id = Convert.ToInt64 (reader[0]);
                            break;
                        }
                    }
                }

                // Remove the playlist entries after the cut off
                if (cut_off_id != null) {
                    ServiceManager.DbConnection.Execute (new HyenaSqliteCommand (
                        "DELETE FROM CoreSmartPlaylistEntries WHERE SmartPlaylistID = ? AND EntryID >= ?",
                        DbId, cut_off_id
                    ));
                }
            }

            refreshed = true;
        }

#endregion

#region IUnmapableSource Implementation

        public bool Unmap ()
        {
            if (DbId != null) {
                ServiceManager.DbConnection.Execute (new HyenaSqliteCommand (@"
                    BEGIN TRANSACTION;
                        DELETE FROM CoreSmartPlaylists WHERE SmartPlaylistID = ?;
                        DELETE FROM CoreSmartPlaylistEntries WHERE SmartPlaylistID = ?;
                    COMMIT TRANSACTION",
                    DbId, DbId
                ));
            }

            ThreadAssist.ProxyToMain (Remove);
            return true;
        }

        public bool CanRefresh {
            get { return QueryOrder == BansheeQuery.RandomOrder; }
        }

        protected override void HandleTracksAdded (Source sender, TrackEventArgs args)
        {
            if (args.When > last_added) {
                last_added = args.When;
                RefreshAndReload ();
            }
        }

        protected override void HandleTracksChanged (Source sender, TrackEventArgs args)
        {
            if (args.When > last_updated) {
                if (NeedsReloadWhenFieldsChanged (args.ChangedFields)) {
                    last_updated = args.When;
                    RefreshAndReload ();
                } else {
                    InvalidateCaches ();
                }
            }
        }

        protected override void HandleTracksDeleted (Source sender, TrackEventArgs args)
        {
            if (args.When > last_removed) {
                last_removed = args.When;
                RefreshAndReload ();
                /*if (ServiceManager.DbConnection.Query<int> (count_removed_command, last_removed) > 0) {
                    if (Limit == null) {
                        //track_model.UpdateAggregates ();
                        //OnUpdated ();
                        Reload ();
                    } else {
                        Reload ();
                    }
                }*/
            }
        }

        public override bool SetParentSource (Source parent)
        {
            base.SetParentSource (parent);
            if (IsHiddenWhenEmpty && Count == 0) {
                return false;
            }
            return true;
        }

#endregion

        private string PrependCondition (string with)
        {
            string sql = String.IsNullOrEmpty (ConditionSql) ? " " : String.Format ("{0} ({1})", with, ConditionSql);
            if (!String.IsNullOrEmpty (additional_conditions)) {
                sql = String.IsNullOrEmpty (sql) ? additional_conditions : String.Format ("{0} AND ({1})", sql, additional_conditions);
            }
            return sql;
        }

        public static IEnumerable<SmartPlaylistSource> LoadAll (PrimarySource parent)
        {
            ClearTemporary ();
            using (HyenaDataReader reader = new HyenaDataReader (ServiceManager.DbConnection.Query (
                @"SELECT SmartPlaylistID, Name, Condition, OrderBy, LimitNumber, LimitCriterion, PrimarySourceID, CachedCount, IsTemporary, IsHiddenWhenEmpty
                    FROM CoreSmartPlaylists WHERE PrimarySourceID = ?", parent.DbId))) {
                while (reader.Read ()) {
                    SmartPlaylistSource playlist = null;
                    try {
                        playlist = new SmartPlaylistSource (
                            reader.Get<long> (0), reader.Get<string> (1),
                            reader.Get<string> (2), reader.Get<string> (3),
                            reader.Get<string> (4), reader.Get<string> (5),
                            parent, reader.Get<int> (7), reader.Get<bool> (8),
                            reader.Get<bool> (9)
                        );
                    } catch (Exception e) {
                        Log.Warning ("Ignoring Smart Playlist", String.Format ("Caught error: {0}", e), false);
                    }

                    if (playlist != null) {
                        yield return playlist;
                    }
                }
            }
        }

        private static void ClearTemporary ()
        {
            ServiceManager.DbConnection.Execute (@"
                BEGIN TRANSACTION;
                    DELETE FROM CoreSmartPlaylistEntries WHERE SmartPlaylistID IN (SELECT SmartPlaylistID FROM CoreSmartPlaylists WHERE IsTemporary = 1);
                    DELETE FROM CoreSmartPlaylists WHERE IsTemporary = 1;
                COMMIT TRANSACTION"
            );
        }

        private static void HandleSourceAdded (SourceEventArgs args)
        {
            SmartPlaylistSource playlist = args.Source as SmartPlaylistSource;
            if (playlist == null)
                return;

            StartTimer (playlist);
            playlists.Add (playlist);
            SortPlaylists();
        }

        private static void HandleSourceRemoved (SourceEventArgs args)
        {
            SmartPlaylistSource playlist = args.Source as SmartPlaylistSource;
            if (playlist == null)
                return;

            playlists.Remove (playlist);

            StopTimer();
        }

        private static void StartTimer (SmartPlaylistSource playlist)
        {
            // Check if the playlist is time-dependent, and if it is,
            // start the auto-refresh timer.
            if (timeout_id == 0 && playlist.TimeDependent) {
                Log.Information (
                    "Starting Smart Playlist Auto-Refresh",
                    "Time-dependent smart playlist added, so starting one-minute auto-refresh timer.",
                    false
                );
                timeout_id = GLib.Timeout.Add(1000*60, OnTimerBeep);
            }
        }

        private static void StopTimer ()
        {
            // If the timer is going and there are no more time-dependent playlists,
            // stop the timer.
            if (timeout_id != 0) {
                foreach (SmartPlaylistSource p in playlists) {
                    if (p.TimeDependent) {
                        return;
                    }
                }

                // No more time-dependent playlists, so remove the timer
                Log.Information (
                    "Stopping timer",
                    "There are no time-dependent smart playlists, so stopping auto-refresh timer.",
                    false
                );

                GLib.Source.Remove (timeout_id);
                timeout_id = 0;
            }
        }

        private static bool OnTimerBeep ()
        {
            foreach (SmartPlaylistSource p in playlists) {
                if (p.TimeDependent) {
                    p.Reload();
                }
            }

            // Keep the timer going
            return true;
        }

        private static void SortPlaylists () {
            playlists.Sort (new DependencyComparer ());
        }

        public static SmartPlaylistSource GetById (long dbId)
        {
            // TODO use a dictionary
            foreach (SmartPlaylistSource sp in playlists) {
                if (sp.DbId == dbId) {
                    return sp;
                }
            }
            return null;
        }
    }

    public class DependencyComparer : IComparer<SmartPlaylistSource> {
        public int Compare(SmartPlaylistSource a, SmartPlaylistSource b)
        {
            if (b.DependsOn (a)) {
                return -1;
            } else if (a.DependsOn (b)) {
                return 1;
            } else {
                return 0;
            }
        }
    }
}

#pragma warning restore 0169
