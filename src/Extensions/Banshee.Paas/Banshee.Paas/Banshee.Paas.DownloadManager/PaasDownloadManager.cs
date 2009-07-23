// 
// PaasDownloadManager.cs
//  
// Author:
//       Mike Urbanski <michael.c.urbanski@gmail.com>
// 
// Copyright (c) 2009 Michael C. Urbanski
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Linq;
using System.Collections.Generic;

using Hyena.Collections;

using Migo2.Async;
using Migo2.Collections;
using Migo2.DownloadService;

using Banshee.ServiceStack;

using Banshee.Paas.Data;
using Banshee.Paas.DownloadManager.Data;

namespace Banshee.Paas.DownloadManager
{
    public class PaasDownloadManager : HttpDownloadManager
    {
        long primary_source_id;
        private Dictionary<long,HttpFileDownloadTask> downloads;

        public PaasDownloadManager (long primarySourceID, int maxDownloads, string tmpDir) : base (maxDownloads, tmpDir)
        {
            primary_source_id = primarySourceID;
            downloads = new Dictionary<long,HttpFileDownloadTask> ();
            
            TaskCompleted += (sender, e) => {
                if (e.Task.State != TaskState.Paused) {
                    lock (SyncRoot) {
                        downloads.Remove ((e.Task.UserState as PaasItem).DbId);
                    }
                }
            };
        }

        public override void CancelAsync ()
        {
            lock (SyncRoot) {
                base.CancelAsync ();
                RemoveQueuedDownloadRange (Tasks.Select (t => (int)(t.UserState as PaasItem).DbId));                
            }
        }

        public TaskState CheckActiveDownloadStatus (PaasItem item)
        {
            return CheckActiveDownloadStatus (item.DbId);
        }

        public TaskState CheckActiveDownloadStatus (long itemID)
        {
            lock (SyncRoot) {
                if (downloads.ContainsKey (itemID)) {
                    return downloads[itemID].State;
                }

                return TaskState.None;
            }
        }

        public bool Contains (PaasItem item)
        {
            return Contains (item.DbId);
        }
        
        public bool Contains (long itemID)
        {
            lock (SyncRoot) {
                return downloads.ContainsKey (itemID);
            }            
        }

        public void QueueDownload (PaasItem item)
        {
            lock (SyncRoot) {
                if (!IsDisposing) {
                    if (!downloads.ContainsKey (item.DbId)) {
                        HttpFileDownloadTask task = CreateDownloadTask (item.Url, item);
                        task.Name = String.Format ("{0} - {1}", item.Channel.Name, item.Name);                                                
                        
                        downloads.Add (item.DbId, task);
                        Add (task);
                    }
                }
            }
        }

        public void QueueDownload (IEnumerable<PaasItem> items)
        {
            lock (SyncRoot) {
                if (!IsDisposing) {
                    HttpFileDownloadTask task = null;                
                    List<HttpFileDownloadTask> tasks = new List<HttpFileDownloadTask> ();

                    foreach (PaasItem item in items.Where (i => !downloads.ContainsKey (i.DbId))) {
                        task = CreateDownloadTask (item.Url, item);
                        task.Name = String.Format ("{0} - {1}", item.Channel.Name, item.Name);
                        
                        tasks.Add (task);
                        downloads.Add (item.DbId, task);
                    }

                    if (tasks.Count > 0) {
                        Add (tasks);
                    }
                }
            }
        }

        public void CancelDownload (PaasItem item)
        {
            lock (SyncRoot) {
                if (!IsDisposing) {           
                    if (downloads.ContainsKey (item.DbId)) {
                        downloads[item.DbId].CancelAsync ();
                        RemoveQueuedDownload (item.DbId);
                    }
                }
            }
        }

        public void CancelDownload (IEnumerable<PaasItem> items)
        {
            lock (SyncRoot) {
                if (!IsDisposing) {
                    RangeCollection rc = new RangeCollection ();
                    
                    foreach (PaasItem item in items) {
                        if (downloads.ContainsKey (item.DbId)) {
                            rc.Add ((int)item.DbId);
                            downloads[item.DbId].CancelAsync ();
                        }
                    }

                    RemoveQueuedDownloadRange (rc);
                }
            }
        }

        public void PauseDownload (PaasItem item)
        {
            lock (SyncRoot) {
                if (!IsDisposing) {           
                    if (downloads.ContainsKey (item.DbId)) {
                        downloads[item.DbId].PauseAsync ();
                    }
                }
            }
        }

        public void PauseDownload (IEnumerable<PaasItem> items)
        {
            lock (SyncRoot) {
                if (!IsDisposing) {
                    foreach (PaasItem item in items) {
                        if (downloads.ContainsKey (item.DbId)) {
                            downloads[item.DbId].PauseAsync ();
                        }
                    }
                }
            }
        }

        public void ResumeDownload (PaasItem item)
        {
            lock (SyncRoot) {
                if (!IsDisposing) {           
                    if (downloads.ContainsKey (item.DbId)) {
                        downloads[item.DbId].ResumeAsync ();
                    }
                }
            }
        }

        public void ResumeDownload (IEnumerable<PaasItem> items)
        {
            lock (SyncRoot) {
                if (!IsDisposing) {
                    foreach (PaasItem item in items) {
                        if (downloads.ContainsKey (item.DbId)) {
                            downloads[item.DbId].ResumeAsync ();
                        }
                    }
                }
            }
        }

        public void RestoreQueuedDownloads ()
        {
            lock (SyncRoot) {
                if (IsDisposing) {
                    return;
                }
                
                RangeCollection rc = new RangeCollection ();
                List<PaasItem> items = new List<PaasItem> ();
                
                var queued_ids = QueuedDownloadTask.Provider.FetchAllMatching (
                    "PrimarySourceID = ?", primary_source_id).Select (t => (int)t.ExternalID
                );
                
                foreach (int i in queued_ids) {
                    rc.Add (i);
                }
                    
                foreach (RangeCollection.Range range in rc.Ranges) {
                    items.AddRange (PaasItem.Provider.FetchRange (range.Start-1, range.Count));
                }

                if (items.Count > 0) {
                    QueueDownload (items);
                }
            }
        }

        private void AddQueuedDownload (long item_id, long position)
        {
            QueuedDownloadTask.Provider.Save (new QueuedDownloadTask () {
                PrimarySourceID = primary_source_id,
                ExternalID = item_id,
                Position = position
            });   
        }

        private void AddQueuedDownloads (IEnumerable<Pair<int, HttpFileDownloadTask>> pairs)
        {
            PaasItem item = null;
            ServiceManager.DbConnection.BeginTransaction ();
            
            try { 
                foreach (Pair<int,HttpFileDownloadTask> pair in pairs) {
                    item = pair.Second.UserState as PaasItem;
                    
                    if (item != null) {
                        AddQueuedDownload (item.DbId, (int)pair.First);
                    }                                    
                }

                ServiceManager.DbConnection.CommitTransaction ();
            } catch {
                ServiceManager.DbConnection.RollbackTransaction ();
                throw;                
            } 
        }

        private void RemoveQueuedDownload (long item_id)
        {
            ServiceManager.DbConnection.Execute (
                String.Format (
                    "DELETE FROM {0} WHERE PrimarySourceID = ? AND ExternalID = ?", 
                    QueuedDownloadTask.Provider.From
                ), primary_source_id, item_id
            );  
        }

        private void RemoveQueuedDownloadRange (IEnumerable<int> ids)
        {
            RangeCollection rc = new RangeCollection ();
            
            foreach (int i in ids) {
                rc.Add (i);
            }

            RemoveQueuedDownloadRange (rc);
        }

        private void RemoveQueuedDownloadRange (RangeCollection collection)
        {
            ServiceManager.DbConnection.BeginTransaction ();
            
            try {
                foreach (RangeCollection.Range range in collection.Ranges) {
                    ServiceManager.DbConnection.Execute (
                        String.Format (
                            "DELETE FROM {0} WHERE PrimarySourceID = ? AND ExternalID >= ? AND ExternalID <= ?", 
                            QueuedDownloadTask.Provider.From
                        ), primary_source_id, range.Start, range.End
                    );                
                }
                
                ServiceManager.DbConnection.CommitTransaction ();
            } catch {
                ServiceManager.DbConnection.RollbackTransaction ();
                throw;                
            }                 
        }

        protected override void OnTaskAdded (int pos, HttpFileDownloadTask task)
        {
            AddQueuedDownload ((task.UserState as PaasItem).DbId, pos);
            base.OnTaskAdded (pos, task);
        }

        protected override void OnTasksAdded (ICollection<Migo2.Collections.Pair<int, HttpFileDownloadTask>> pairs)
        {
            AddQueuedDownloads (pairs);
            base.OnTasksAdded (pairs);
        }

        protected override void OnTaskCompleted (HttpFileDownloadTask task, TaskCompletedEventArgs e)
        {
            if (e.State == TaskState.Succeeded) {
                RemoveQueuedDownload ((task.UserState as PaasItem).DbId);
            }
            
            base.OnTaskCompleted (task, e);
        }

    }
}
