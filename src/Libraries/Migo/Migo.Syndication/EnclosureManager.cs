//
// EnclosureManager.cs
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
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Hyena;
using Hyena.Data.Sqlite;

using Migo.DownloadCore;
using Migo.TaskCore;
using Migo.TaskCore.Collections;

namespace Migo.Syndication
{
    public class EnclosureManager
    {
        private Dictionary<FeedEnclosure, HttpFileDownloadTask> queued_downloads;
        private DownloadManager download_manager;
        private bool disposed;
        private readonly object sync = new object ();
        private ManualResetEvent download_handle;

        public EnclosureManager (DownloadManager downloadManager)
        {
            download_manager = downloadManager;
            download_manager.Tasks.TaskAdded += OnDownloadTaskAdded;
            download_manager.Tasks.TaskRemoved += OnDownloadTaskRemoved;
            download_manager.Group.TaskStatusChanged += OnDownloadTaskStatusChangedHandler;

            queued_downloads = new Dictionary<FeedEnclosure, HttpFileDownloadTask> ();

            download_handle = new ManualResetEvent (true);
        }

        public void Dispose (AutoResetEvent disposeHandle)
        {

            disposed = true;

            List<HttpFileDownloadTask> tasks = null;

            lock (sync) {
                if (queued_downloads.Count > 0) {
                    tasks = new List<HttpFileDownloadTask> (queued_downloads.Values);
                }
            }

            if (tasks != null) {
                foreach (HttpFileDownloadTask t in tasks) {
                    t.Stop ();
                }

                download_handle.WaitOne ();
            }

            if (download_handle != null) {
                download_handle.Close ();
                download_handle = null;
            }

            if (download_manager != null) {
                download_manager.Tasks.TaskAdded -= OnDownloadTaskAdded;
                download_manager.Tasks.TaskRemoved -= OnDownloadTaskRemoved;
                download_manager = null;
            }
        }

        public HttpFileDownloadTask QueueDownload (FeedEnclosure enclosure)
        {
            return QueueDownload (enclosure, true);
        }

        public HttpFileDownloadTask QueueDownload (FeedEnclosure enclosure, bool queue)
        {
            if (enclosure == null) {
                throw new ArgumentNullException ("enc");
            }

            HttpFileDownloadTask task = null;

            lock (sync) {
                if (disposed) {
                    return null;
                }

                if (!queued_downloads.ContainsKey (enclosure)) {
                    Feed parentFeed = enclosure.Item.Feed;

                    if (parentFeed != null) {
                        try {
                            task = download_manager.CreateDownloadTask (enclosure.Url, enclosure);
                            //Console.WriteLine ("Task DL path:  {0}", task.LocalPath);
                            task.Name = String.Format ("{0} - {1}", parentFeed.Title, enclosure.Item.Title);
    
                            //task.StatusChanged
                            task.Completed += OnDownloadTaskCompletedHandler;
    
                            // Race condition...
                            // Should only be added when the task is associated or
                            // it can be canceled before it is added to the progress manager.
    
                            // Add a pre-association dict and move tasks to the
                            // queued dict once they've been offically added.
    
                            queued_downloads.Add (enclosure, task);
                        } catch (Exception e) {
                            Log.Exception ("Could not download podcast enclosure", e);
                        }
                    }
                }

                if (task != null && queue) {
                    download_manager.QueueDownload (task);
                }
            }

            return task;
        }

        public IEnumerable<HttpFileDownloadTask> QueueDownloads (IEnumerable<FeedEnclosure> encs)
        {
            if (encs == null) {
                throw new ArgumentNullException ("encs");
            }

            ICollection<HttpFileDownloadTask> encsCol = encs as ICollection<HttpFileDownloadTask>;

            List<HttpFileDownloadTask> tasks = (encsCol == null) ?
                new List<HttpFileDownloadTask> () :
                new List<HttpFileDownloadTask> (encsCol.Count);

            HttpFileDownloadTask tmpTask = null;

            lock (sync) {
                if (disposed) {
                    return tasks;
                }

                foreach (FeedEnclosure enc in encs) {
                    tmpTask = QueueDownload (enc, false);
                    if (tmpTask != null) {
                        tasks.Add (tmpTask);
                    }
                }

                if (tasks.Count > 0) {
                    download_manager.QueueDownload (tasks);
                }
            }

            return tasks;
        }

        public void CancelDownload (FeedEnclosure enc)
        {
            lock (sync) {
                HttpFileDownloadTask task = FindDownloadTask (enc);

                if (task != null) {
                    // Look into multi-cancel later
                    task.CancelAsync ();
                }
            }
        }

        public void StopDownload (FeedEnclosure enc)
        {
            lock (sync) {
                HttpFileDownloadTask task = FindDownloadTask (enc);

                if (task != null) {
                    task.Stop ();
                }
            }
        }

        /*private void OnFeedItemRemoved (Feed feed, FeedItem item)
        {
            if (feed == null) {
                throw new ArgumentNullException ("feed");
            } else if (item == null) {
                throw new ArgumentNullException ("item");
            }

            EventHandler<FeedItemEventArgs> handler = FeedItemRemoved;

            if (item.Enclosure != null) {
                lock (sync) {
                    HttpFileDownloadTask task;

                    if (queued_downloads.TryGetValue ((FeedEnclosure)item.Enclosure, out task)) {
                        task.CancelAsync ();
                    }
                }
            }

            if (handler != null) {
                OnFeedItemEvent (handler, new FeedItemEventArgs (feed, item));
            }
        }

        private void OnFeedItemsRemoved (Feed feed, IEnumerable<FeedItem> items)
        {
            if (feed == null) {
                throw new ArgumentNullException ("feed");
            } else if (items == null) {
                throw new ArgumentNullException ("items");
            }

            EventHandler<FeedItemEventArgs> handler = FeedItemRemoved;

            lock (sync) {
                HttpFileDownloadTask task;

                foreach (FeedItem item in items) {
                    if (item.Enclosure != null) {
                        if (queued_downloads.TryGetValue ((FeedEnclosure)item.Enclosure, out task)) {
                            task.CancelAsync ();
                        }
                    }
                }
            }

            if (handler != null) {
                OnFeedItemEvent (handler, new FeedItemEventArgs (feed, items));
            }
        } */

        private HttpFileDownloadTask FindDownloadTask (FeedEnclosure enc)
        {
            if (enc == null) {
                throw new ArgumentNullException ("enc");
            }

            return FindDownloadTaskImpl ((FeedEnclosure)enc);
        }

        private HttpFileDownloadTask FindDownloadTaskImpl (FeedEnclosure enc)
        {
            HttpFileDownloadTask task = null;
            Feed parentFeed = enc.Item.Feed as Feed;

            if (parentFeed != null && queued_downloads.ContainsKey (enc)) {
                task = queued_downloads[enc];
            }

            return task;
        }

        private void TaskAddedAction (HttpFileDownloadTask task)
        {
            Feed parentFeed = null;
            FeedEnclosure enc = task.UserState as FeedEnclosure;

            if (enc != null) {
                lock (sync) {
                    parentFeed = enc.Item.Feed;

                    if (parentFeed != null && queued_downloads.ContainsKey (enc)) {
                        if (queued_downloads.Count == 0) {
                            download_handle.Reset ();
                        }

                        enc.DownloadStatus = FeedDownloadStatus.Pending;
                        //parentFeed.IncrementQueuedDownloadCount ();
                    }
                }
            }
        }

        private void OnDownloadTaskAdded (object sender, TaskAddedEventArgs<HttpFileDownloadTask> e)
        {
            if (e.Task != null) {
                TaskAddedAction (e.Task);
            } else if (e.Tasks != null) {
                foreach (HttpFileDownloadTask task in e.Tasks) {
                    TaskAddedAction (task);
                }
            }
        }

        private void OnDownloadTaskCompletedHandler (object sender, TaskCompletedEventArgs args)
        {
            HttpFileDownloadTask task = sender as HttpFileDownloadTask;
            FeedEnclosure enc = task.UserState as FeedEnclosure;

            if (enc != null) {
                if (args.Error != null || task.Status == TaskStatus.Failed) {
                    enc.DownloadStatus = FeedDownloadStatus.DownloadFailed;
                } else if (!args.Cancelled) {
                    if (task.Status == TaskStatus.Succeeded) {
                        try {
                            enc.SetFileImpl (
                                task.RemoteUri.ToString (),
                                Path.GetDirectoryName (task.LocalPath),
                                task.MimeType,
                                Path.GetFileName (task.LocalPath)
                            );
                        } catch (Exception e) {
                            Log.Exception (e);
                            enc.LastDownloadError = FeedDownloadError.DownloadFailed;
                            enc.DownloadStatus = FeedDownloadStatus.DownloadFailed;
                            enc.Save ();
                        }
                    }
                }
            }
        }

        private void DownloadTaskRemoved (FeedEnclosure enc, HttpFileDownloadTask task, bool decQueuedCount)
        {
            if (queued_downloads.ContainsKey (enc)) {
                queued_downloads.Remove (enc);
                task.Completed -= OnDownloadTaskCompletedHandler;

                if (decQueuedCount) {
                    //enc.Item.Feed.DecrementQueuedDownloadCount ();
                }

                if (queued_downloads.Count == 0) {
                	if (download_handle != null) {
                	    download_handle.Set ();
                	}
                }
            }
        }

        private void OnDownloadTaskRemoved (object sender, TaskRemovedEventArgs<HttpFileDownloadTask> e)
        {
            if (e.Task != null) {
                FeedEnclosure enc = e.Task.UserState as FeedEnclosure;

                if (enc != null) {
                    lock (sync) {
                        DownloadTaskRemoved (enc, e.Task, true);
                    }
                }
            } else if (e.Tasks != null) {
                Feed tmpParent = null;
                FeedEnclosure tmpEnclosure = null;
                List<FeedEnclosure> tmpList = null;

                Dictionary<Feed, List<FeedEnclosure>> feedDict =
                    new Dictionary<Feed,List<FeedEnclosure>> ();

                lock (sync) {
                    foreach (HttpFileDownloadTask t in e.Tasks) {
                        tmpEnclosure = t.UserState as FeedEnclosure;

                        if (tmpEnclosure != null) {
                            tmpParent = tmpEnclosure.Item.Feed;

                            if (!feedDict.TryGetValue (tmpParent, out tmpList)) {
                                tmpList = new List<FeedEnclosure> ();
                                feedDict.Add (tmpParent, tmpList);
                            }

                            tmpList.Add (tmpEnclosure);
                            DownloadTaskRemoved (tmpEnclosure, t, false);
                        }
                    }

                    //foreach (KeyValuePair<Feed,List<FeedEnclosure>> kvp in feedDict) {
                        //kvp.Key.DecrementQueuedDownloadCount (kvp.Value.Count);
                    //}
                }
            }
        }

        private void TaskStatusChanged (TaskStatusChangedInfo statusInfo)
        {
            HttpFileDownloadTask task = statusInfo.Task as HttpFileDownloadTask;

            if (task == null) {
                return;
            }

            FeedEnclosure enc = task.UserState as FeedEnclosure;

            if (enc == null) {
                return;
            }

            switch (statusInfo.NewStatus) {
            case TaskStatus.Stopped:
            case TaskStatus.Cancelled:
                enc.DownloadStatus = FeedDownloadStatus.None;
                break;
            case TaskStatus.Failed:
                enc.DownloadStatus = FeedDownloadStatus.DownloadFailed;
                break;
            case TaskStatus.Paused:
                enc.DownloadStatus = FeedDownloadStatus.Paused;
                break;
            case TaskStatus.Ready:
                enc.DownloadStatus = FeedDownloadStatus.Pending;
                break;
            case TaskStatus.Running:
                enc.DownloadStatus = FeedDownloadStatus.Downloading;
                //enc.Item.Feed.IncrementActiveDownloadCount ();
                break;
            case TaskStatus.Succeeded:
                break;
            }

            FeedsManager.Instance.FeedManager.OnItemChanged (enc.Item);

            if (statusInfo.OldStatus == TaskStatus.Running) {
                //enc.Item.Feed.DecrementActiveDownloadCount ();
            }
        }

        private void OnDownloadTaskStatusChangedHandler (object sender, TaskStatusChangedEventArgs e)
        {
            if (e.StatusChanged != null) {
                lock (sync) {
                    TaskStatusChanged (e.StatusChanged);
                }
            } else {
                lock (sync) {
                    foreach (TaskStatusChangedInfo statusInfo in e.StatusesChanged) {
                        TaskStatusChanged (statusInfo);
                    }
                }
            }
        }
    }
}
