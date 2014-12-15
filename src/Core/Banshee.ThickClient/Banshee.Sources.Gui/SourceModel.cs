//
// SourceModel.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
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
using System.Collections.Generic;
using Gtk;

using Hyena;

using Banshee.Sources;
using Banshee.ServiceStack;

namespace Banshee.Sources.Gui
{
    public delegate void SourceRowEventHandler (object o, SourceRowEventArgs args);

    public sealed class SourceRowEventArgs : EventArgs
    {
        public SourceRowEventArgs (Source source, TreeIter iter, TreeIter parentIter)
        {
            this.source = source;
            this.iter = iter;
            this.parent_iter = parentIter;
        }

        private Source source;
        public Source Source {
            get { return source; }
        }

        private TreeIter iter;
        public TreeIter Iter {
            get { return iter; }
        }

        private TreeIter parent_iter;
        public TreeIter ParentIter {
            get { return parent_iter; }
        }
    }

    public class SourceModel : TreeStore
    {
        private object sync = new object ();

        public event SourceRowEventHandler SourceRowInserted;
        public event SourceRowEventHandler SourceRowRemoved;

        public Predicate<Source> Filter { get; set; }

        protected SourceModel (IntPtr ptr) : base (ptr) {}

        public SourceModel () : base (typeof (Source), typeof (int), typeof (EntryType))
        {
            SetSortColumnId (1, SortType.Ascending);
            ChangeSortColumn ();

            ServiceManager.SourceManager.SourceAdded += OnSourceAdded;
            ServiceManager.SourceManager.SourceRemoved += OnSourceRemoved;
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                ServiceManager.SourceManager.SourceAdded -= OnSourceAdded;
                ServiceManager.SourceManager.SourceRemoved -= OnSourceRemoved;
            }
            base.Dispose (disposing);
        }

        private void OnSourceAdded (SourceAddedArgs args)
        {
            ThreadAssist.ProxyToMain (delegate {
                AddSource (args.Source);
            });
        }

        private void OnSourceRemoved (SourceEventArgs args)
        {
            ThreadAssist.ProxyToMain (delegate {
                RemoveSource (args.Source);
            });
        }

        protected virtual void OnSourceRowInserted (Source source, TreeIter iter, TreeIter parentIter)
        {
            SourceRowEventHandler handler = SourceRowInserted;
            if (handler != null) {
                handler (this, new SourceRowEventArgs (source, iter, parentIter));
            }
        }

        protected virtual void OnSourceRowRemoved (Source source, TreeIter iter)
        {
            SourceRowEventHandler handler = SourceRowRemoved;
            if (handler != null) {
                handler (this, new SourceRowEventArgs (source, iter, TreeIter.Zero));
            }
        }

#region Source <-> Iter Methods

        public Source GetSource (TreeIter iter)
        {
            return GetValue (iter, 0) as Source;
        }

        public Source GetSource (TreePath path)
        {
            if (path == null)
                return null;

            TreeIter iter;
            if (GetIter (out iter, path)) {
                return GetSource (iter);
            }

            return null;
        }

        public TreeIter FindSource (Source source)
        {
            foreach (TreeIter iter in FindInModel (0, source)) {
                return iter;
            }

            return TreeIter.Zero;
        }

        public IEnumerable<TreeIter> FindInModel (int column, object match)
        {
            TreeIter iter = TreeIter.Zero;
            GetIterFirst (out iter);
            return FindInModel (column, match, iter);
        }

        public IEnumerable<TreeIter> FindInModel (int column, object match, TreeIter iter)
        {
            if (!IterIsValid (iter)) {
                yield break;
            }

            do {
                object result = GetValue (iter, column);
                Type result_type = result != null ? result.GetType () : null;
                if (result_type != null && ((result_type.IsValueType && result.Equals (match)) || result == match)) {
                    yield return iter;
                }

                if (IterHasChild (iter)) {
                    TreeIter citer = TreeIter.Zero;
                    IterChildren (out citer, iter);
                    foreach (TreeIter yiter in FindInModel (column, match, citer)) {
                        if (!yiter.Equals (TreeIter.Zero)) {
                            yield return yiter;
                        }
                    }
                }
            } while (IterNext (ref iter));
        }

#endregion


#region Add/Remove Sources / SourceManager interaction

        public void AddSource (Source source)
        {
            AddSource (source, TreeIter.Zero);
        }

        public void AddSource (Source source, TreeIter parent)
        {
            ThreadAssist.AssertInMainThread ();
            lock (sync) {
                if (Filter != null && !Filter (source)) {
                    return;
                }

                // Don't add duplicates
                if (!FindSource (source).Equals (TreeIter.Zero)) {
                    return;
                }

                // Don't add a child source before its parent
                if (parent.Equals (TreeIter.Zero) && source.Parent != null) {
                    return;
                }

                int position = source.Order;

                var args = new object [] {
                    source,
                    position,
                    source is SourceManager.GroupSource ? EntryType.Group : EntryType.Source
                };

                TreeIter iter = parent.Equals (TreeIter.Zero)
                    ? InsertWithValues (position, args)
                    : InsertWithValues (parent, position, args);

                lock (source.Children) {
                    foreach (Source child in source.Children) {
                        AddSource (child, iter);
                    }
                }

                source.ChildSourceAdded += OnSourceChildSourceAdded;
                source.ChildSourceRemoved += OnSourceChildSourceRemoved;

                OnSourceRowInserted (source, iter, parent);
            }
        }

        public void RemoveSource (Source source)
        {
            ThreadAssist.AssertInMainThread ();
            lock (sync) {
                TreeIter iter = FindSource (source);
                if (!iter.Equals (TreeIter.Zero)) {
                    Remove (ref iter);
                }

                source.ChildSourceAdded -= OnSourceChildSourceAdded;
                source.ChildSourceRemoved -= OnSourceChildSourceRemoved;

                OnSourceRowRemoved (source, iter);
            }
        }

        public void Refresh ()
        {
            ThreadAssist.AssertInMainThread ();
            Clear ();
            foreach (Source source in ServiceManager.SourceManager.Sources) {
                AddSource (source);
            }
        }

        private void OnSourceChildSourceAdded (SourceEventArgs args)
        {
            ThreadAssist.ProxyToMain (delegate {
                AddSource (args.Source, FindSource (args.Source.Parent));
            });
        }

        private void OnSourceChildSourceRemoved (SourceEventArgs args)
        {
            ThreadAssist.ProxyToMain (delegate {
                RemoveSource (args.Source);
            });
        }

#endregion

        public enum Columns : int {
            Source,
            Order,
            Type
        }

        public enum EntryType {
            Source,
            Group
        }
    }
}
