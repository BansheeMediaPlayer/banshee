//
// SourceWatcher.cs
//
// Authors:
//   Christian Martellini <christian.martellini@gmail.com>
//   Alexander Kojevnikov <alexander@kojevnikov.com>
//
// Copyright (C) 2009 Christian Martellini
// Copyright (C) 2009 Alexander Kojevnikov
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

using Hyena;
using Hyena.Data.Sqlite;

using Banshee.Base;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Library;
using Banshee.Metadata;
using Banshee.Query;
using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Streaming;

namespace Banshee.LibraryWatcher
{
    public class SourceWatcher : IDisposable
    {
        private readonly LibraryImportManager import_manager;
        private readonly LibrarySource library;
        private readonly FileSystemWatcher watcher;
        private readonly ManualResetEvent handle;
        private readonly Thread watch_thread;

        private readonly Queue<QueueItem> queue = new Queue<QueueItem> ();
        private readonly TimeSpan delay = TimeSpan.FromMilliseconds (1000);

        private bool active;
        private bool disposed;

        private class QueueItem
        {
            public DateTime When;
            public WatcherChangeTypes ChangeType;
            public string OldFullPath;
            public string FullPath;
            public string MetadataHash;
        }

        public SourceWatcher (LibrarySource library)
        {
            this.library = library;
            handle = new ManualResetEvent(false);
            string path = library.BaseDirectoryWithSeparator;
            if (String.IsNullOrEmpty (path)) {
                throw new Exception ("Will not create LibraryWatcher for the blank directory");
            }

            string home = Environment.GetFolderPath (Environment.SpecialFolder.Personal) + Path.DirectorySeparatorChar;
            if (path == home) {
                throw new Exception ("Will not create LibraryWatcher for the entire home directory");
            }

            string root = Path.GetPathRoot (Environment.CurrentDirectory);
            if (path == root || path == root + Path.DirectorySeparatorChar) {
                throw new Exception ("Will not create LibraryWatcher for the entire root directory");
            }

            if (!Banshee.IO.Directory.Exists (path)) {
                throw new Exception ("Will not create LibraryWatcher for non-existent directory");
            }

            import_manager = ServiceManager.Get<LibraryImportManager> ();

            watcher = new FileSystemWatcher (path);
            watcher.IncludeSubdirectories = true;
            watcher.Changed += OnModified;
            watcher.Created += OnModified;
            watcher.Deleted += OnModified;
            watcher.Renamed += OnModified;

            active = true;
            watch_thread = new Thread (new ThreadStart (Watch));
            watch_thread.Name = String.Format ("LibraryWatcher for {0}", library.Name);
            watch_thread.IsBackground = true;
            watch_thread.Start ();
        }

#region Public Methods

        public void Dispose ()
        {
            if (!disposed) {
                active = false;
                watcher.Changed -= OnModified;
                watcher.Created -= OnModified;
                watcher.Deleted -= OnModified;
                watcher.Renamed -= OnModified;

                lock (queue) {
                    queue.Clear ();
                }

                watcher.Dispose ();
                disposed = true;
            }
        }

#endregion

#region Private Methods

        private readonly double MAX_TIME_BETWEEN_CHANGED_EVENTS = TimeSpan.FromSeconds (10).TotalMilliseconds;

        Dictionary<string, System.Timers.Timer> created_items_bag = new Dictionary<string, System.Timers.Timer> ();

        private System.Timers.Timer CreateTimer (string fullpath)
        {
            var timer = new System.Timers.Timer (MAX_TIME_BETWEEN_CHANGED_EVENTS);
            timer.Elapsed += (sender, e) => TimeUpForChangedEvent (fullpath);
            return timer;
        }

        private void OnCreation (string fullpath)
        {
            var timer = CreateTimer (fullpath);
            lock (created_items_bag) {
                created_items_bag [fullpath] = timer;
            }
            timer.AutoReset = false;
            timer.Start ();
        }

        private void TimeUpForChangedEvent (string fullpath)
        {
            lock (created_items_bag) {
                created_items_bag [fullpath].Stop ();
                created_items_bag [fullpath].Dispose ();
                created_items_bag.Remove (fullpath);
            }
            var fake_args = new FileSystemEventArgs (WatcherChangeTypes.Created,
                                                     System.IO.Path.GetDirectoryName (fullpath),
                                                     System.IO.Path.GetFileName (fullpath));
            EnqueueAffectedElement (fake_args);
        }

        private void OnModified (object source, FileSystemEventArgs args)
        {
            if (args.ChangeType == WatcherChangeTypes.Created) {
                OnCreation (args.FullPath);
                return;
            } else if (args.ChangeType == WatcherChangeTypes.Changed) {
                lock (created_items_bag) {
                    System.Timers.Timer timer;
                    if (created_items_bag.TryGetValue (args.FullPath, out timer)) {
                        // A file we saw being created was modified, restart the timer
                        timer.Stop ();
                        timer.Start ();
                        return;
                    }
                }
            }

            EnqueueAffectedElement (args);
        }

        private void EnqueueAffectedElement (FileSystemEventArgs args)
        {
            var item = new QueueItem {
                When = DateTime.Now,
                ChangeType = args.ChangeType,
                FullPath = args.FullPath,
                OldFullPath = args is RenamedEventArgs ? ((RenamedEventArgs)args).OldFullPath : args.FullPath
            };

            lock (queue) {
                queue.Enqueue (item);
            }
            handle.Set ();

            Hyena.Log.DebugFormat ("Watcher: {0} {1}{2}",
                item.ChangeType, args is RenamedEventArgs ? item.OldFullPath + " => " : "", item.FullPath);
        }

        private void Watch ()
        {
            watcher.EnableRaisingEvents = true;

            while (active) {
                WatcherChangeTypes change_types = 0;
                while (queue.Count > 0) {
                    QueueItem item;
                    lock (queue) {
                        item = queue.Dequeue ();
                    }

                    int sleep =  (int) (item.When + delay - DateTime.Now).TotalMilliseconds;
                    if (sleep > 0) {
                        Hyena.Log.DebugFormat ("Watcher: sleeping {0}ms", sleep);
                        Thread.Sleep (sleep);
                    }

                    try {
                        if (item.ChangeType == WatcherChangeTypes.Changed) {
                            UpdateTrack (item.FullPath);
                        } else if (item.ChangeType == WatcherChangeTypes.Created) {
                            AddTrack (item.FullPath);
                        } else if (item.ChangeType == WatcherChangeTypes.Deleted) {
                            RemoveTrack (item.FullPath);
                        } else if (item.ChangeType == WatcherChangeTypes.Renamed) {
                            RenameTrack (item.OldFullPath, item.FullPath);
                        }
    
                        change_types |= item.ChangeType;
                    } catch (Exception e) {
                        Log.Error (String.Format ("Watcher: Error processing {0}", item.FullPath), e.Message, false);
                    }
                }

                if ((change_types & WatcherChangeTypes.Deleted) > 0) {
                    library.NotifyTracksDeleted ();
                }
                if ((change_types & (WatcherChangeTypes.Renamed |
                    WatcherChangeTypes.Created | WatcherChangeTypes.Changed)) > 0) {
                    library.NotifyTracksChanged ();
                }

                handle.WaitOne ();
                handle.Reset ();
            }
        }

        private void UpdateTrack (string track)
        {
            using (var reader = ServiceManager.DbConnection.Query (
                DatabaseTrackInfo.Provider.CreateFetchCommand (String.Format (
                "CoreTracks.PrimarySourceID = ? AND {0} = ? LIMIT 1",
                BansheeQuery.UriField.Column)),
                library.DbId, new SafeUri (track).AbsoluteUri)) {
                if (reader.Read ()) {
                    var track_info = DatabaseTrackInfo.Provider.Load (reader);
                    if (Banshee.IO.File.GetModifiedTime (track_info.Uri) > track_info.FileModifiedStamp) {
                        using (var file = StreamTagger.ProcessUri (track_info.Uri)) {
                            StreamTagger.TrackInfoMerge (track_info, file, false,
                                SaveTrackMetadataService.WriteRatingsEnabled.Value,
                                SaveTrackMetadataService.WritePlayCountsEnabled.Value);
                        }
                        track_info.LastSyncedStamp = DateTime.Now;
                        track_info.Save (false);
                    }
                }
            }
        }

        private void AddTrack (string track)
        {
            import_manager.ImportTrack (track);

            // Trigger file rename.
            string uri = new SafeUri(track).AbsoluteUri;
            var command = new HyenaSqliteCommand (String.Format (@"
                UPDATE CoreTracks
                SET DateUpdatedStamp = LastSyncedStamp + 1
                WHERE {0} = ?",
                BansheeQuery.UriField.Column), uri);
            ServiceManager.DbConnection.Execute (command);
        }

        private void RemoveTrack (string track)
        {
            string uri = new SafeUri(track).AbsoluteUri;
            string hash_sql = String.Format (
                @"SELECT TrackID, MetadataHash FROM CoreTracks WHERE {0} = ? LIMIT 1",
                BansheeQuery.UriField.Column
            );
            long track_id = 0;
            string hash = null;
            using (var reader = new HyenaDataReader (ServiceManager.DbConnection.Query (hash_sql, uri))) {
                if (reader.Read ()) {
                    track_id = reader.Get<long> (0);
                    hash = reader.Get<string> (1);
                }
            }

            if (hash != null) {
                lock (queue) {
                    var item = queue.FirstOrDefault (
                        i => i.ChangeType == WatcherChangeTypes.Created && GetMetadataHash (i) == hash);
                    if (item != null) {
                        item.ChangeType = WatcherChangeTypes.Renamed;
                        item.OldFullPath = track;
                        return;
                    }
                }
            }

            string delete_sql = @"
                INSERT INTO CoreRemovedTracks (DateRemovedStamp, TrackID, Uri)
                    SELECT ?, TrackID, " + BansheeQuery.UriField.Column + @"
                    FROM CoreTracks WHERE TrackID IN ({0})
                ;
                DELETE FROM CoreTracks WHERE TrackID IN ({0})";

            // If track_id is 0, it's a directory.
            HyenaSqliteCommand delete_command;
            if (track_id > 0) {
                delete_command = new HyenaSqliteCommand (String.Format (delete_sql,
                    "?"), DateTime.Now, track_id, track_id);
            } else {
                string pattern = StringUtil.EscapeLike (uri) + "/_%";
                string select_sql = String.Format (@"SELECT TrackID FROM CoreTracks WHERE {0} LIKE ? ESCAPE '\'",
                                                   BansheeQuery.UriField.Column);
                delete_command = new HyenaSqliteCommand (String.Format (delete_sql, select_sql),
                    DateTime.Now, pattern, pattern);
            }

            ServiceManager.DbConnection.Execute (delete_command);
        }

        private void RenameTrack(string oldFullPath, string fullPath)
        {
            if (oldFullPath == fullPath) {
                // FIXME: bug in Mono, see bnc#322330
                return;
            }
            string old_uri = new SafeUri (oldFullPath).AbsoluteUri;
            string new_uri = new SafeUri (fullPath).AbsoluteUri;
            string pattern = StringUtil.EscapeLike (old_uri) + "%";
            var rename_command = new HyenaSqliteCommand (String.Format (@"
                UPDATE CoreTracks
                SET Uri = REPLACE ({0}, ?, ?),
                    DateUpdatedStamp = ?
                WHERE {0} LIKE ? ESCAPE '\'",
                BansheeQuery.UriField.Column),
                old_uri, new_uri, DateTime.Now, pattern);
            ServiceManager.DbConnection.Execute (rename_command);
        }

        private string GetMetadataHash (QueueItem item)
        {
            if (item.ChangeType == WatcherChangeTypes.Created && item.MetadataHash == null) {
                var uri = new SafeUri (item.FullPath);
                if (DatabaseImportManager.IsWhiteListedFile (item.FullPath) && Banshee.IO.File.Exists (uri)) {
                    var track = new TrackInfo ();
                    using (var file = StreamTagger.ProcessUri (uri)) {
                        StreamTagger.TrackInfoMerge (track, file);
                    }
                    item.MetadataHash = track.MetadataHash;
                }
            }
            return item.MetadataHash;
        }

#endregion
    }
}
