//
// CollectionIndexer.cs
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

using DBus;

using Banshee.Library;
using Banshee.Sources;
using Banshee.ServiceStack;
using Banshee.Collection.Database;

namespace Banshee.Collection.Indexer
{
    public class CollectionIndexerService : ICollectionIndexerService, IDBusExportable, IDisposable
    {
        private List<LibrarySource> libraries = new List<LibrarySource> ();
        private string [] available_export_fields;
        private int open_indexers;

        public event ActionHandler CollectionChanged;
        public event ActionHandler CleanupAndShutdown;

        private ActionHandler shutdown_handler;
        public ActionHandler ShutdownHandler {
            get { return shutdown_handler; }
            set { shutdown_handler = value; }
        }

        public CollectionIndexerService ()
        {
            DBusConnection.Connect ("CollectionIndexer");

            ServiceManager.SourceManager.SourceAdded += OnSourceAdded;
            ServiceManager.SourceManager.SourceRemoved += OnSourceRemoved;

            foreach (Source source in ServiceManager.SourceManager.Sources) {
                MonitorLibrary (source as LibrarySource);
            }
        }

        public void Dispose ()
        {
            while (libraries.Count > 0) {
                UnmonitorLibrary (libraries[0]);
            }
        }

        void ICollectionIndexerService.Hello ()
        {
            Hyena.Log.DebugFormat ("Hello called on {0}", GetType ());
        }

        public void Shutdown ()
        {
            lock (this) {
                if (open_indexers == 0 && shutdown_handler != null) {
                    shutdown_handler ();
                }
            }
        }

        public void ForceShutdown ()
        {
            Dispose ();
            if (shutdown_handler != null) {
                shutdown_handler ();
            }
        }

        public ICollectionIndexer CreateIndexer ()
        {
            lock (this) {
                return new CollectionIndexer (null);
            }
        }

        internal void DisposeIndexer (CollectionIndexer indexer)
        {
            lock (this) {
                ServiceManager.DBusServiceManager.UnregisterObject (indexer);
                open_indexers--;
            }
        }

        ObjectPath ICollectionIndexerService.CreateIndexer ()
        {
            lock (this) {
                ObjectPath path = ServiceManager.DBusServiceManager.RegisterObject (new CollectionIndexer (this));
                open_indexers++;
                return path;
            }
        }


        public bool HasCollectionCountChanged (int count)
        {
            lock (this) {
                int total_count = 0;

                foreach (LibrarySource library in libraries) {
                    total_count += library.Count;
                }

                return count != total_count;
            }
        }

        public bool HasCollectionLastModifiedChanged (long time)
        {
            lock (this) {
                long last_updated = 0;

                foreach (LibrarySource library in libraries) {
                    last_updated = Math.Max (last_updated, ServiceManager.DbConnection.Query<long> (
                        String.Format ("SELECT MAX(CoreTracks.DateUpdatedStamp) {0}",
                            library.DatabaseTrackModel.UnfilteredQuery)));
                }

                return last_updated > time;
            }
        }

        public string [] GetAvailableExportFields ()
        {
            lock (this) {
                if (available_export_fields != null) {
                    return available_export_fields;
                }

                List<string> fields = new List<string> ();

                foreach (KeyValuePair<string, System.Reflection.PropertyInfo> field in TrackInfo.GetExportableProperties (
                    typeof (DatabaseTrackInfo))) {
                    fields.Add (field.Key);
                }

                available_export_fields = fields.ToArray ();
                return available_export_fields;
            }
        }

        private void MonitorLibrary (LibrarySource library)
        {
            if (library == null || !library.Indexable || libraries.Contains (library)) {
                return;
            }

            libraries.Add (library);

            library.TracksAdded += OnLibraryChanged;
            library.TracksDeleted += OnLibraryChanged;
            library.TracksChanged += OnLibraryChanged;
        }

        private void UnmonitorLibrary (LibrarySource library)
        {
            if (library == null || !libraries.Contains (library)) {
                return;
            }

            library.TracksAdded -= OnLibraryChanged;
            library.TracksDeleted -= OnLibraryChanged;
            library.TracksChanged -= OnLibraryChanged;

            libraries.Remove (library);
        }

        private void OnSourceAdded (SourceAddedArgs args)
        {
            MonitorLibrary (args.Source as LibrarySource);
        }

        private void OnSourceRemoved (SourceEventArgs args)
        {
            UnmonitorLibrary (args.Source as LibrarySource);
        }

        private void OnLibraryChanged (object o, TrackEventArgs args)
        {
            if (args.ChangedFields == null) {
                OnCollectionChanged ();
                return;
            }

            foreach (Hyena.Query.QueryField field in args.ChangedFields) {
                if (field != Banshee.Query.BansheeQuery.LastPlayedField &&
                    field != Banshee.Query.BansheeQuery.LastSkippedField &&
                    field != Banshee.Query.BansheeQuery.PlayCountField &&
                    field != Banshee.Query.BansheeQuery.SkipCountField) {
                    OnCollectionChanged ();
                    return;
                }
            }
        }

        public void RequestCleanupAndShutdown ()
        {
            ActionHandler handler = CleanupAndShutdown;
            if (handler != null) {
                handler ();
            }
        }

        private void OnCollectionChanged ()
        {
            ActionHandler handler = CollectionChanged;
            if (handler != null) {
                handler ();
            }
        }

        IDBusExportable IDBusExportable.Parent {
            get { return null; }
        }

        string IService.ServiceName {
            get { return "CollectionIndexerService"; }
        }
    }
}
