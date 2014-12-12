//
// DatabaseBrowsableListModel.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//   Gabriel Burt <gburt@novell.com>
//
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
using System.Text;
using System.Collections.Generic;

using Hyena;
using Hyena.Data;
using Hyena.Data.Sqlite;
using Hyena.Query;

using Banshee.Collection;
using Banshee.Database;

namespace Banshee.Collection.Database
{
    public abstract class DatabaseFilterListModel<T, U> : FilterListModel<U>, ICacheableDatabaseModel, ISearchable
        where T : U, new () where U : ICacheableItem, new()
    {
        private readonly BansheeModelCache<T> cache;
        private readonly Banshee.Sources.DatabaseSource source;

        private long count;
        private string reload_fragment;

        private string reload_fragment_format;
        protected string ReloadFragmentFormat {
            get { return reload_fragment_format; }
            set { reload_fragment_format = value; }
        }

        protected readonly U select_all_item;
        private BansheeDbConnection connection;

        public DatabaseFilterListModel (string name, string label, Banshee.Sources.DatabaseSource source,
                                        DatabaseTrackListModel trackModel, BansheeDbConnection connection, SqliteModelProvider<T> provider, U selectAllItem, string uuid)
            : base (trackModel)
        {
            this.source = source;
            FilterName = name;
            FilterLabel = label;
            select_all_item = selectAllItem;

            this.connection = connection;
            cache = new BansheeModelCache <T> (connection, uuid, this, provider);
            HasSelectAllItem = true;
        }

        private int select_all_offset;
        protected bool HasSelectAllItem {
            get { return cache.HasSelectAllItem; }
            set {
                cache.HasSelectAllItem = value;
                select_all_offset = value ? 1 : 0;
            }
        }

        public override void Clear ()
        {
            UnfilteredCount = 0;
            count = 0;
        }

        protected bool CalculateUnfilteredCount { get; set; }

        private HyenaSqliteCommand unfiltered_count_command;
        private HyenaSqliteCommand UnfilteredCountCommand {
            get {
                return unfiltered_count_command ?? (unfiltered_count_command =
                    new HyenaSqliteCommand (String.Format (
                        "SELECT COUNT(*) {0}", GenerateReloadFragment (false)
                    ))
                );
            }
        }

        private void UpdateUnfilteredAggregates ()
        {
            using (var reader = new HyenaDataReader (connection.Query (UnfilteredCountCommand))) {
                UnfilteredCount = reader.Get<int> (0);
            }
        }

        protected virtual void GenerateReloadFragment ()
        {
            ReloadFragment = GenerateReloadFragment (true);
        }

        private string GenerateReloadFragment (bool filtered)
        {
            return String.Format (
                ReloadFragmentFormat,
                FilteredModel.CachesJoinTableEntries ? FilteredModel.JoinFragment : null,
                FilteredModel.CacheId,
                FilteredModel.CachesJoinTableEntries
                    ? String.Format (
                        "{0}.{1} AND CoreTracks.TrackID = {0}.{2}",
                        FilteredModel.JoinTable, FilteredModel.JoinPrimaryKey, FilteredModel.JoinColumn)
                    : "CoreTracks.TrackID",
                filtered ? GetFilterFragment () : "",
                connection.LikelihoodSupport ? "UNLIKELY" : ""
            );
        }

        private string GetFilterFragment ()
        {
            StringBuilder qb = new StringBuilder ();
            foreach (IFilterListModel model in UpstreamFilters) {
                string filter = model.GetSqlFilter ();
                if (filter != null) {
                    qb.Append (" AND ");
                    qb.Append (filter);
                }
            }
            return qb.ToString ();
        }

        private IEnumerable<IFilterListModel> UpstreamFilters {
            get {
                foreach (IFilterListModel model in source.CurrentFilters) {
                    if (this == model) {
                        break;
                    } else {
                        yield return model;
                    }
                }
            }
        }

        public virtual bool CachesValues { get { return false; } }

        protected abstract string ItemToFilterValue (object o);

        // Ick, duplicated from DatabaseTrackListModel
        public override string GetSqlFilter ()
        {
            string filter = null;

            // If the only item is the "All" item, then we shouldn't allow any matches, so insert an always-false condition
            if (HasSelectAllItem && Count == 1) {
                return "0=1";
            } else {
                ModelHelper.BuildIdFilter<object> (GetSelectedObjects (), FilterColumn, null,
                    delegate (object item) {
                        if (!select_all_item.Equals (item)) {
                            return ItemToFilterValue (item);
                        }
                        return null;
                    },
                    delegate (string new_filter) { filter = new_filter; }
                );
            }

            return filter;
        }

        public abstract void UpdateSelectAllItem (long count);

        public override void Reload (bool notify)
        {
            GenerateReloadFragment ();

            lock (cache) {
                if (CalculateUnfilteredCount) {
                    UpdateUnfilteredAggregates ();
                }

                connection.BeginTransaction ();
                cache.SaveSelection ();
                cache.Reload ();
                cache.UpdateAggregates ();
                cache.RestoreSelection ();
                connection.CommitTransaction ();

                count = cache.Count + select_all_offset;
            }

            UpdateSelectAllItem (count - select_all_offset);

            if (notify)
                OnReloaded ();
        }

        public override U this[int index] {
            get {
                if (HasSelectAllItem && index == 0)
                    return select_all_item;

                lock (cache) {
                    return cache.GetValue (index - select_all_offset);
                }
            }
        }

        public override int Count {
            get { return (int) count; }
        }

        public int UnfilteredCount { get; private set; }

        // Implement ICacheableModel
        public virtual int FetchCount {
            get { return 40; }
        }

        public virtual string SelectAggregates { get { return null; } }

        public string ReloadFragment {
            get { return reload_fragment; }
            protected set { reload_fragment = value; }
        }

        public int CacheId {
            get {
                lock (cache) {
                    return (int) cache.CacheId;
                }
            }
        }

        public IEnumerable<object> GetSelectedObjects ()
        {
            foreach (object o in SelectedItems) {
                yield return o;
            }
        }

        public override void InvalidateCache (bool notify)
        {
            lock (cache) {
                cache.Clear ();
            }
            if (notify) {
                OnReloaded ();
            }
        }

        private QueryFieldSet query_fields;
        public QueryFieldSet QueryFields {
            get { return query_fields; }
            protected set { query_fields = value; }
        }

        public int IndexOf (QueryNode query, long offset)
        {
            lock (cache) {
                if (query == null) {
                    return -1;
                }

                int index = (int) cache.IndexOf (query.ToSql (QueryFields), offset);
                return index >= 0 ? index + select_all_offset : index;
            }
        }

        public abstract string FilterColumn { get; }

        public virtual string JoinTable { get { return null; } }
        public virtual string JoinFragment { get { return null; } }
        public virtual string JoinPrimaryKey { get { return null; } }
        public virtual string JoinColumn { get { return null; } }
        public virtual bool CachesJoinTableEntries { get { return false; } }
    }
}
