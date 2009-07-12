// 
// DownloadListModel.cs
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
using System.Collections;
using System.Collections.Generic;

using Mono.Unix;

using Hyena.Data;
using Hyena.Collections;

using Migo2.Async;
using Migo2.Collections;
using Migo2.DownloadService;

namespace Banshee.Paas.DownloadManager.Gui
{
    public class DownloadListModel : IListModel<HttpFileDownloadTask>
    {
        public event EventHandler Cleared;
        public event EventHandler Reloaded;
        
        private List<HttpFileDownloadTask> tasks;
        private Selection selection = new Selection ();

        public bool CanReorder { 
            get { return true; } 
        }
        
        public int Count {
            get { 
                lock (SyncRoot) {
                    return tasks.Count;
                }
            }
        }

        public Selection Selection {
            get { return selection; }
        }

        public HttpFileDownloadTask this[int index] {
            get {
                lock (SyncRoot) {            
                    return GetIndex (index);
                }
            }
        }

        private object SyncRoot {
            get { return ((ICollection)tasks).SyncRoot; }
        }

        public DownloadListModel ()
        {
            tasks = new List<HttpFileDownloadTask> ();
        }

        public void AddTask (HttpFileDownloadTask task)
        {
            if (task != null) {
                lock (SyncRoot) {
                    tasks.Add (task);
                }                
            }
            
            OnReloaded ();            
        }

        public void AddTasks (IEnumerable<HttpFileDownloadTask> tasks)
        {
            if (tasks != null) {
                lock (SyncRoot) {
                    foreach (HttpFileDownloadTask task in tasks) {
                        if (task != null) {
                            this.tasks.Remove (task);                            
                        }
                    }
                }                
            }
            
            OnReloaded ();            
        }

        public void AddTaskPair (Pair<int,HttpFileDownloadTask> pair)
        {
            if (pair != null && pair.Second != null) {
                lock (SyncRoot) {
                    tasks.Insert (pair.First, pair.Second);
                }                
            }
            
            OnReloaded ();
        }

        public void AddTaskPairs (IEnumerable<Pair<int,HttpFileDownloadTask>> pairs)
        {
            if (pairs != null) {
                lock (SyncRoot) {
                    foreach (Pair<int,HttpFileDownloadTask> pair in pairs) {
                        if (pair != null && pair.Second != null) {
                            tasks.Insert (pair.First, pair.Second);
                        }
                    }
                }
            }
            
            OnReloaded ();
        }

        public IEnumerable<HttpFileDownloadTask> GetSelected ()
        {
            lock (SyncRoot) {
                HttpFileDownloadTask task = null;
                List<HttpFileDownloadTask> selected = new List<HttpFileDownloadTask> ();

                foreach (int i in selection) {
                    task = GetIndex (i);
                    
                    if (task != null) {
                        selected.Add (task);
                    }
                }

                return selected;
            }
        }

        public void RemoveTask (HttpFileDownloadTask task)
        {
            if (task != null) {
                lock (SyncRoot) {
                    tasks.Remove (task);
                }                
            }
            
            OnReloaded ();            
        }

        public void RemoveTasks (IEnumerable<HttpFileDownloadTask> tasks)
        {
            if (tasks != null) {
                lock (SyncRoot) {
                    foreach (HttpFileDownloadTask task in tasks) {
                        if (task != null) {
                            this.tasks.Remove (task);                            
                        }
                    }
                }
            }
            
            OnReloaded ();            
        }

        public void Clear ()
        {
            lock (SyncRoot) {
                tasks.Clear ();
            }
            
            OnCleared ();
        }
        
        public void Reload ()
        {
            OnReloaded ();
        }

        public void Reorder (int[] newWorldOrder)
        {
            lock (SyncRoot) {
                int len = newWorldOrder.Length;
                int[] order = new int[len];
                Dictionary<HttpFileDownloadTask, int> positions = new Dictionary<HttpFileDownloadTask, int> (len);

                int i = 0;
                for (; i < order.Length; ++i) {
                    order[newWorldOrder[i]] = i;
                }

                i = 0;
                foreach (var t in tasks) {
                    positions.Add (t, order[i++]);
                }

                tasks.Sort (new OrderComparer<HttpFileDownloadTask> (positions));
                selection.Clear ();
            }

            OnReloaded ();
        }

        private HttpFileDownloadTask GetIndex (int index)
        {         
            if (index >= 0 && index < tasks.Count) {
                return tasks[index];
            }
            
            return null;
        }

        private void OnReloaded ()
        {
            Banshee.Base.ThreadAssist.ProxyToMain (delegate {
                EventHandler handler = Reloaded;
                if (handler != null) {
                    handler (this, EventArgs.Empty);
                }
            });
        }
        
        private void OnCleared ()
        {
            Banshee.Base.ThreadAssist.ProxyToMain (delegate {
                EventHandler handler = Cleared;
                if (handler != null) {
                    handler (this, EventArgs.Empty);
                }
            });
        }
    }
}
