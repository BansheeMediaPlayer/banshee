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

using Migo2.Async;
using Migo2.DownloadService;

using Banshee.Paas.Data;

namespace Banshee.Paas.DownloadManager
{
    public class PaasDownloadManager : HttpDownloadManager
    {
        private Dictionary<long,HttpFileDownloadTask> downloads;

        public PaasDownloadManager (int maxDownloads, string tmpDir) : base (maxDownloads, tmpDir)
        {
            downloads = new Dictionary<long,HttpFileDownloadTask> ();
            
            TaskCompleted += (sender, e) => {
                if (e.Task.State != TaskState.Paused) {
                    lock (SyncRoot) {
                        downloads.Remove ((e.Task.UserState as PaasItem).DbId);
                    }
                }
            };
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
                    }
                }
            }
        }

        public void CancelDownload (IEnumerable<PaasItem> items)
        {
            lock (SyncRoot) {
                if (!IsDisposing) {
                    foreach (PaasItem item in items) {
                        if (downloads.ContainsKey (item.DbId)) {
                            downloads[item.DbId].CancelAsync ();
                        }
                    }
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
    }
}
