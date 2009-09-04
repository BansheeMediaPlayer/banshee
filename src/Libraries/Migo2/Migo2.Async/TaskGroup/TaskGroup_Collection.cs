// 
// TaskGroup_Collection.cs
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
using System.Collections.Generic;

using Migo2.Collections;

namespace Migo2.Async
{
    public partial class TaskGroup<T> where T : Task
    {
        private List<T> list;

        public EventHandler<ReorderedEventArgs> Reordered;
        public EventHandler<TaskAddedEventArgs<T>> TaskAdded;

        public int Count
        {
            get {
                lock (sync) {                    
                    return list.Count;
                }                
            }
        }

        protected ICollection<T> Tasks
        {
            get { return list; }
        }

        private void InitTaskGroup_Collection ()
        {
            list = new List<T> ();
        }

        public T this [int index] 
        {
            get {
                lock (sync) {  
                    CheckDisposed ();                
                    CheckIndex (index);
                    return list[index];
                }
            }
            
            set {
                lock (sync) {      
                    CheckDisposed ();
                    CheckIndex (index);
                    list[index] = value;
                }
            }
        }        
        
        public void Add (T task)
        {
            CheckTask (task);
            
            lock (sync) {
                CheckDisposed ();
                
                if (!CanAdd ()) {
                    return;
                }
                
                int index = list.Count;
                
                list.Add (task);
                Associate (task);
                gsm.IncrementRemainingTaskCount ();

                OnTaskAdded (index, task);
            }
            
            Execute ();
        }

        public void Add (IEnumerable<T> tasks)
        {
            bool execute = false;    
            CheckTasks (tasks);
        
            lock (sync) {  
                CheckDisposed ();
                
                if (!CanAdd ()) {
                    return;
                }
            
                int index = list.Count;
                ICollection<Pair<int,T>> coll = CreatePairs (index, tasks);            
                
                if (coll.Count > 0) {
                    execute = true;                
                    list.AddRange (tasks);
                    Associate (tasks);
                    gsm.RemainingTasks += coll.Count;                
                    
                    OnTasksAdded (coll);
                }
            }
            
            if (execute) {
                Execute ();                 
            }
        }

        public bool Contains (T task)
        {
            lock (sync) {
                CheckDisposed ();
                
                if (task == null) {
                    return false;
                } else {            
                    lock (sync) {        
                        return list.Contains (task);
                    }
                }
            }
        }        
        
        public void CopyTo (T[] array, int index)
        {
            lock (sync) { 
                CheckDisposed ();
                CheckCopyArgs (array, index);        
                list.CopyTo (array, index);
            }
        }
        
        public void CopyTo (Array array, int index)
        {
            lock (sync) {   
                CheckDisposed ();
                CheckCopyArgs (array, index);            
                Array.Copy (list.ToArray (), 0, array, index, list.Count);
            }
        }
        
        // I'd like to make the TaskGroup's SyncRoot private in the future, this would be a problem for GE.
        public IEnumerator<T> GetEnumerator ()
        {
            foreach (T task in list) {
                yield return task;
            }
        }
        
        public int IndexOf (T task)
        {
            lock (sync) {
                CheckDisposed ();
                
                if (task == null) {
                    return -1;
                } else {            
                    lock (sync) {        
                        return list.IndexOf (task);
                    }
                }
            }
        }
        
        public void Insert (int index, T task)
        {
            CheckTask (task);
            
            lock (sync) {
                CheckDisposed ();
                
                if (!CanAdd ()) {
                    return;
                }
                        
                CheckDestIndex (index);

                list.Insert (index, task);
                
                OnTaskAdded (index, task);
            }
            
            Execute ();
        }
        
        public void InsertRange (int index, IEnumerable<T> tasks)
        {
            if (!CanAdd ()) {
                return;
            }
                
            CheckTasks (tasks);
            
            bool execute = false;
            
            lock (sync) {
                CheckDisposed ();
                CheckDestIndex (index);
                
                ICollection<Pair<int,T>> coll = CreatePairs (index, tasks);
                
                if (coll.Count > 0) {
                    list.InsertRange (index, tasks);                    

                    Associate (tasks);
                    gsm.RemainingTasks += coll.Count;                         
                    
                    OnTasksAdded (coll);    
                    execute = true;
                }
            }

            if (execute) {
                Execute ();                
            }
        }

        public void Move (int destIndex, int sourceIndex)
        {
            if (sourceIndex == destIndex) {
                return;
            }
            
            lock (sync) {
                CheckDisposed ();

                if (sourceIndex >= list.Count || sourceIndex < 0) {
                    throw new ArgumentOutOfRangeException ("sourceIndex");    
                } else if (destIndex > list.Count || destIndex < 0) {
                    throw new ArgumentOutOfRangeException ("destIndex");    
                }                

                Dictionary<Task,int> oldOrder = SaveOrder ();
                
                T tmpTask;
                
                tmpTask = list[sourceIndex];
                list.RemoveAt (sourceIndex);
                    
                list.Insert (Math.Min (list.Count, destIndex), tmpTask);                 
                
                OnReordered (NewOrder (oldOrder));
            }
        }
        
        public void Move (int destIndex, int[] sourceIndices)
        {
            if (sourceIndices == null) {
                throw new ArgumentNullException ("sourceIndices");                       
            }        

            lock (sync) {
                CheckDisposed ();
                int maxIndex = list.Count;
                
                if (destIndex > maxIndex || destIndex < 0) {
                    throw new ArgumentOutOfRangeException ("destIndex");    
                }
                
                List<T> tmpList = new List<T> (sourceIndices.Length);
                            
                foreach (int i in sourceIndices) {
                    if (i < 0 || i > maxIndex) {
                        throw new ArgumentOutOfRangeException ("sourceIndices");
                    }
                    
                    // A possible performance enhancement is to check for 
                    // contiguous regions in the source and remove those regions 
                    // at once.
                    tmpList.Add (list[i]);
                }

                Dictionary<Task,int> oldOrder = SaveOrder ();

                if (tmpList.Count > 0) {            
                    int offset = 0;
                    Array.Sort (sourceIndices);
                    
                    foreach (int i in sourceIndices)
                    {
                        try {
                            list.RemoveAt (i-offset);
                        } catch { continue; }
                        
                        ++offset;
                    }

                    list.InsertRange (Math.Min (destIndex, list.Count), tmpList);
                    OnReordered (NewOrder (oldOrder));
                }
            }
        }
        
        public void Move (int destIndex, T task)
        {
            if (task == null) {
                throw new ArgumentNullException ("task");
            }
        
            int index;
                    
            lock (sync) {
                CheckDisposed ();            
                index = list.IndexOf (task);
                
                if (index != -1) {
                    Move (destIndex, index);
                }
            }
        }        
        
        public void Move (int destIndex, IEnumerable<T> tasks)
        {
            if (tasks == null) {
                throw new ArgumentNullException ("tasks");
            }
        
            int index;
            List<int> indices = new List<int> ();
                    
            lock (sync) {
                CheckDisposed ();
                
                foreach (T task in tasks) {
                    index = list.IndexOf (task);
                    if (index != -1) {
                        indices.Add (index);
                    }
                }
                
                if (indices.Count > 0) {
                    Move (destIndex, indices.ToArray ());
                }
            }
        }
        
        public void Reverse ()
        {
            lock (sync) {
                CheckDisposed ();            
                Dictionary<Task,int> oldOrder = SaveOrder ();
                list.Reverse ();
                OnReordered (NewOrder (oldOrder));
            }
        }

        protected virtual IEnumerable<Pair<int,int>> DetectContinuity (int[] indices)
        {                        
            if (indices == null) {
                throw new ArgumentNullException ("indices");
            } else if (indices.Length == 0) {
                return null;
            }
            
            int cnt;
            int len = indices.Length;
            List<Pair<int,int>> ret = new List<Pair<int,int>>();            
            
            int i = len-1;
            
            while (i > 0) {
                cnt = 1;
                while (indices[i] == indices[i-1]+1)
                {
                    ++cnt;
                    if (--i == 0) {
                        break;
                    }
                }

                ret.Add (new Pair<int,int>(indices[i--], cnt));
            }
            
            return ret;
        }
        
        protected virtual void OnReordered (int[] newOrder)
        {
            EventHandler<ReorderedEventArgs> handler = Reordered;

            if (handler != null) {
                ReorderedEventArgs e = new ReorderedEventArgs (newOrder);
                commandQueue.Register (new EventWrapper<ReorderedEventArgs> (handler, this, e));
            }
        }
        
        protected virtual void OnTaskAdded (int pos, T task)
        {
            EventHandler<TaskAddedEventArgs<T>> handler = TaskAdded;
            
            if (handler != null) {
                TaskAddedEventArgs<T> e = new TaskAddedEventArgs<T> (pos, task);            
                commandQueue.Register (new EventWrapper<TaskAddedEventArgs<T>> (handler, this, e));
            }
        }

        protected virtual void OnTasksAdded (ICollection<Pair<int,T>> pairs)
        {
            EventHandler<TaskAddedEventArgs<T>> handler = TaskAdded;

            if (handler != null) {
                TaskAddedEventArgs<T> e = new TaskAddedEventArgs<T> (pairs);        
                commandQueue.Register (new EventWrapper<TaskAddedEventArgs<T>> (handler, this, e));
            }
        }

        private void CheckCopyArgs (Array array, int index)
        {
            if (array == null) {
                throw new ArgumentNullException ("array");
            } else if (index < 0) {
                throw new ArgumentOutOfRangeException (
                    "Value of index must be greater than or equal to 0"
                );
            } else if (array.Rank > 1) {
                throw new ArgumentException (
                    "array must not be multidimensional"
                );
            } else if (index >= array.Length) {
                throw new ArgumentException (
                    "index exceeds array length"
                );            
            } else if (list.Count > (array.Length-index)) {
                throw new ArgumentException (
                    "index and count exceed length of array"
                );            
            }
        }
        
        private bool CanAdd ()
        {
            if (disposed) {
                throw new ObjectDisposedException (GetType ().ToString ());            
            } else if (cancelRequested) {
                throw new OperationCanceledException ();
            }
            
            return true;
        }
        
        private void CheckDestIndex (int index)
        {
            if (index < 0 || index > list.Count) {
                throw new ArgumentOutOfRangeException ("index");
            }
        }

        private void CheckIndex (int index) 
        {
            if (index < 0 || index >= list.Count) {
                throw new ArgumentOutOfRangeException ("index");
            }        
        }
        
        private void CheckTask (T task)
        {
            if (task == null) {
                throw new ArgumentNullException ("task");
            }
        }

        private void CheckTasks (IEnumerable<T> tasks)
        {
            if (tasks == null) {
                throw new ArgumentNullException ("tasks");
            }
            
            foreach (Task t in tasks) {
                if (t == null) {
                    throw new ArgumentException (
                        "No task in tasks may be null"
                    );
                }
            }
        }

        private ICollection<Pair<int,T>> CreatePairs (int index, IEnumerable<T> tasks)
        {
            List<Pair<int,T>> pairs = new List<Pair<int,T>> ();
            
            foreach (T task in tasks) {
                pairs.Add (new Pair<int,T> (index++, task));
            }        
            
            return pairs;
        }
        
        private int[] NewOrder (Dictionary<Task,int> oldOrder)
        {
            int i = -1;
            int[] newOrder = new int[list.Count];
            
            foreach (Task t in list) {
                newOrder[++i] = oldOrder[t];
            }
            
            return newOrder;
        }        
        
        private Dictionary<Task,int> SaveOrder ()
        {
            Dictionary<Task,int> ret = new Dictionary<Task,int> (list.Count);
            
            int i = -1;
            
            foreach (Task t in list) {
                ret[t] = ++i;
            }
            
            return ret;
        }        
    }
}