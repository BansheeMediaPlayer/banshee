//
// PersistentColumnController.cs
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

using Hyena.Data;
using Hyena.Data.Gui;
using Banshee.Sources;
using Banshee.Configuration;

namespace Banshee.Collection.Gui
{
    public class PersistentColumnController : ColumnController
    {
        private string root_namespace;
        private bool loaded = false;
        private bool pending_changes;
        private uint timer_id = 0;

        private string source_id, unique_source_id;
        private Source source;
        public Source Source {
            get { return source; }
            set {
                if (source == value) {
                    return;
                }

                if (source != null) {
                    Save ();
                }

                source = value;
                source_id = unique_source_id = null;

                if (source != null) {
                    // If we have a parent, use their UniqueId so all children of a parent persist the same columns
                    source_id = source.ParentConfigurationId;
                    unique_source_id = source.ConfigurationId;
                    Load ();
                }
            }
        }

        public PersistentColumnController (string rootNamespace) : base ()
        {
            if (String.IsNullOrEmpty (rootNamespace)) {
                throw new ArgumentException ("Argument must not be null or empty", "rootNamespace");
            }

            root_namespace = rootNamespace;
        }

        public void Load ()
        {
            lock (this) {
                if (source == null) {
                    return;
                }

                loaded = false;

                int i = 0;
                foreach (Column column in this) {
                    if (column.Id != null) {
                        string @namespace = MakeNamespace (column.Id);
                        column.Visible = ConfigurationClient.Get<bool> (@namespace, "visible", column.Visible);
                        column.Width = ConfigurationClient.Get<double> (@namespace, "width", column.Width);
                        column.OrderHint = ConfigurationClient.Get<int> (@namespace, "order", i);
                    } else {
                        column.OrderHint = -1;
                    }
                    i++;
                }

                Columns.Sort ((a, b) => a.OrderHint.CompareTo (b.OrderHint));

                string sort_ns = String.Format ("{0}.{1}.{2}", root_namespace, unique_source_id, "sort");
                string sort_column_id = ConfigurationClient.Get<string> (sort_ns, "column", null);
                if (sort_column_id != null) {
                    ISortableColumn sort_column = null;
                    foreach (Column column in this) {
                        if (column.Id == sort_column_id) {
                            sort_column = column as ISortableColumn;
                            break;
                        }
                    }

                    if (sort_column != null) {
                        int sort_dir = ConfigurationClient.Get<int> (sort_ns, "direction", 0);
                        SortType sort_type = sort_dir == 0 ? SortType.None : sort_dir == 1 ? SortType.Ascending : SortType.Descending;
                        sort_column.SortType = sort_type;
                        base.SortColumn = sort_column;
                    }
                } else {
                    base.SortColumn = null;
                }

                loaded = true;
            }

            OnUpdated ();
        }

        public override ISortableColumn SortColumn {
            set { base.SortColumn = value; Save (); }
        }

        public void Save ()
        {
            if (timer_id == 0) {
                timer_id = GLib.Timeout.Add (500, OnTimeout);
            } else {
                pending_changes = true;
            }
        }

        private bool OnTimeout ()
        {
            if (pending_changes) {
                pending_changes = false;
                return true;
            } else {
                SaveCore ();
                timer_id = 0;
                return false;
            }
        }

        private void SaveCore ()
        {
            lock (this) {
                if (source == null) {
                    return;
                }

                for (int i = 0; i < Count; i++) {
                    if (Columns[i].Id != null) {
                        Save (Columns[i], i);
                    }
                }

                if (SortColumn != null) {
                    string ns = String.Format ("{0}.{1}.{2}", root_namespace, unique_source_id, "sort");
                    ConfigurationClient.Set<string> (ns, "column", SortColumn.Id);
                    ConfigurationClient.Set<int> (ns, "direction", (int)SortColumn.SortType);
                }
            }
        }

        private void Save (Column column, int index)
        {
            string @namespace = MakeNamespace (column.Id);
            ConfigurationClient.Set<int> (@namespace, "order", index);
            ConfigurationClient.Set<bool> (@namespace, "visible", column.Visible);
            ConfigurationClient.Set<double> (@namespace, "width", column.Width);
        }

        protected override void OnWidthsChanged ()
        {
            if (loaded) {
                Save ();
            }

            base.OnWidthsChanged ();
        }

        private string MakeNamespace (string name)
        {
            return String.Format ("{0}.{1}.{2}", root_namespace, source_id, name);
        }

        public override bool EnableColumnMenu {
            get { return true; }
        }
    }
}
