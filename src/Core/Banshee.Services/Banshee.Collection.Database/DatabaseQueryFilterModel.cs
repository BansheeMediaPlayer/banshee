//
// DatabaseQueryFilterModel.cs
//
// Authors:
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
using System.Text;
using System.Reflection;

using Hyena.Query;
using Hyena.Data;
using Hyena.Data.Sqlite;

using Mono.Unix;

using Banshee.ServiceStack;
using Banshee.Database;

namespace Banshee.Collection.Database
{
    public class DatabaseQueryFilterModel<T> : DatabaseFilterListModel<QueryFilterInfo<T>, QueryFilterInfo<T>>
    {
        private QueryField field;
        private readonly QueryField query_filter_field = new QueryField (
            "itemid", "ItemID",
            Catalog.GetString ("Value"), "CoreCache.ItemID", false
        );

        private string select_all_fmt;

        public DatabaseQueryFilterModel (Banshee.Sources.DatabaseSource source, DatabaseTrackListModel trackModel,
            BansheeDbConnection connection, string select_all_fmt, string uuid, QueryField field, string filter_column)
            : base (field.Name, field.Label, source, trackModel, connection, QueryFilterInfo<T>.CreateProvider (filter_column, field), new QueryFilterInfo<T> (), String.Format ("{0}-{1}", uuid, field.Name))
        {
            this.field = field;
            this.select_all_fmt = select_all_fmt;

            ReloadFragmentFormat = @"
                FROM CoreTracks, CoreCache{0}
                    WHERE {4}(CoreCache.ModelID = {1}) AND CoreCache.ItemID = {2} {3}
                    ORDER BY Value";

            QueryFields = new QueryFieldSet (query_filter_field);
        }

        public override bool CachesValues { get { return true; } }

        public override string GetSqlFilter ()
        {
            if (Selection.AllSelected)
                return null;

            StringBuilder sb = new StringBuilder ("(");
            bool first = true;
            //QueryListNode or = new QueryListNode (Keyword.Or);
            foreach (object o in GetSelectedObjects ()) {
                if (o != select_all_item) {
                    string sql = null;
                    QueryValue qv = QueryValue.CreateFromStringValue (o.ToString (), field);
                    //QueryListNode and = new QueryListNode (Keyword.And, or);
                    if (qv != null) {
                        if (qv is IntegerQueryValue) {
                            /*QueryTermNode term = new QueryTermNode ();
                            term.Field = field;
                            field.ToSql (IntegerQueryValue.GreaterThanEqual, qv);
                            field.ToSql (IntegerQueryValue.GreaterThanEqual, qv);*/
                        } else if (qv is StringQueryValue) {
                            // TODO this might need fixing
                            //sql = field.ToSql (StringQueryValue.Equal, qv, true);
                            sql = field.ToSql (StringQueryValue.Equal, qv);
                        }
                    } else {
                        // TODO this might need fixing
                        //sql = field.ToSql (NullQueryValue.IsNullOrEmpty, NullQueryValue.Instance, true);
                        sql = field.ToSql (NullQueryValue.IsNullOrEmpty, NullQueryValue.Instance);
                    }

                    if (sql != null) {
                        if (first) {
                            first = false;
                        } else {
                            sb.Append (" OR ");
                        }
                        sb.Append (sql);
                    }
                }
            }
            sb.Append (")");
            return first ? null : sb.ToString ();
        }

        protected override string ItemToFilterValue (object o)
        {
            throw new NotImplementedException ();
        }

        public override string FilterColumn {
            get { return String.Empty; }
        }

        public override void UpdateSelectAllItem (long count)
        {
            select_all_item.Title = String.Format (select_all_fmt, count);
        }
    }

    /*public class DatabaseNumericQueryFilterModel<T> : DatabaseQueryFilterModel<T>
    {
        public DatabaseNumericQueryFilterModel (Banshee.Sources.DatabaseSource source, DatabaseTrackListModel trackModel,
            BansheeDbConnection connection, SqliteModelProvider<T> provider, U selectAllItem, string uuid, QueryField field)
            : base (source, trackModel
    }*/
}
