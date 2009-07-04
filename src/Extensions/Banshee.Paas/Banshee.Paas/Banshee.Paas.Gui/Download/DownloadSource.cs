// 
// DownloadSource.cs
//  
// Authors:
//   Mike Urbanski <michael.c.urbanski@gmail.com>
//   Aaron Bockover <abockover@novell.com>
// 
// Copyright (c) 2009 Michael C. Urbanski
// Copyright (C) 2007 Novell, Inc. (ErrorSource.cs)
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
using System.Collections.Generic;

using Mono.Unix;

using Hyena.Data;
using Hyena.Collections;

using Banshee.Sources;

using Migo2.Async;
using Migo2.Collections;
using Migo2.DownloadService;

namespace Banshee.Paas
{
    public class DownloadSource : Source, IObjectListModel, IUnmapableSource
    {
        private List<HttpFileDownloadTask> tasks = new List<HttpFileDownloadTask> ();
        private Selection selection = new Selection ();
        
        public event EventHandler Cleared;
        public event EventHandler Reloaded;
        
        public DownloadSource () : base (Catalog.GetString ("Downloads"), Catalog.GetString ("Downloads"), 0)
        {
            Properties.SetStringList ("Icon.Name", "gtk-network", "network");
            
            //Properties.SetString ("UnmapSourceActionLabel", Catalog.GetString ("Close Error Report"));
            //Properties.SetString ("GtkActionPath", "/ErrorSourceContextMenu");
        }

        public bool Unmap ()
        {
            Clear ();
            Parent.RemoveChildSource (this);
            return true;
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
        
        private ColumnDescription [] columns = new ColumnDescription [] {
            new ColumnDescription ("Name", Catalog.GetString ("Name"), .35),
            new ColumnDescription ("Progress", Catalog.GetString ("Progress"), .65)
        };
        
        public ColumnDescription [] ColumnDescriptions {
            get { return columns; }
        }
        
        public override void Activate ()
        {
            Reload ();
        }

        public void AddTaskPair (Pair<int,HttpFileDownloadTask> pair)
        {
            lock (this) {
                tasks.Insert (pair.First, pair.Second);
            }
            
            OnUpdated ();
            OnReloaded ();
        }

        public void AddTaskPairs (IEnumerable<Pair<int,HttpFileDownloadTask>> pairs)
        {
            lock (this) {
                foreach (Pair<int,HttpFileDownloadTask> pair in pairs) {
                    tasks.Insert (pair.First, pair.Second);
                }
            }
            
            OnUpdated ();
            OnReloaded ();
        }

        public void RemoveTask (HttpFileDownloadTask task)
        {
            lock (this) {
                tasks.Remove (task);
            }
            
            OnUpdated ();
            OnReloaded ();            
        }

        public void RemoveTasks (IEnumerable<HttpFileDownloadTask> tasks)
        {
            lock (this) {
                foreach (HttpFileDownloadTask task in tasks) {
                    this.tasks.Remove (task);
                }
            }
            
            OnUpdated ();
            OnReloaded ();            
        }

        public void Clear ()
        {
            lock (this) {
                tasks.Clear ();
            }
            
            OnUpdated ();
            OnCleared ();
        }
        
        public void Reload ()
        {
            OnReloaded ();
        }

        public override int Count {
            get { 
                lock (this) {
                    return tasks.Count;
                }
            }
        }

        public bool CanReorder { 
            get { return true; } 
        }
        
        public virtual bool CanUnmap {
            get { return false; }
        }

        public bool ConfirmBeforeUnmap {
            get { return false; }
        }
        
        public object this[int index] {
            get {
                lock (this) {            
                    if (index >= 0 && index < tasks.Count) {
                        return tasks[index];
                    } 
                }
                
                return null;
            }
        }

        public Selection Selection {
            get { return selection; }
        }
    }
}
