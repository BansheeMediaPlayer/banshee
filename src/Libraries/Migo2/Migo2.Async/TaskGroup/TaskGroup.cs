// 
// TaskGroup.cs
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
using System.Threading;
using System.ComponentModel;

using System.Collections;
using System.Collections.Generic;

using Migo2.Collections;

namespace Migo2.Async
{
    public partial class TaskGroup<T> where T : Task
    {
        private bool disposed;
        private bool is_disposing;

        private bool executing;
        private bool cancelRequested;

        private List<T> currentTasks;
        private CommandQueue commandQueue;
        
        private GroupStatusManager gsm;
        private GroupProgressManager<T> gpm;

        private ManualResetEvent mre;

        private readonly Guid id;
        private readonly object sync = new object ();

        public event EventHandler<TaskEventArgs<T>> TaskStarted;
        public event EventHandler<TaskCompletedEventArgs<T>> TaskCompleted;

        public event EventHandler<TaskEventArgs<T>> TaskUpdated;
        public event EventHandler<TaskStateChangedEventArgs<T>> TaskStateChanged;        
        public event EventHandler<TaskProgressChangedEventArgs<T>> TaskProgressChanged;

        public event EventHandler<EventArgs> Started;
        public event EventHandler<EventArgs> Stopped;
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;
        public event EventHandler<GroupStatusChangedEventArgs> StatusChanged;

        public virtual int CompletedTasks
        {
            get {
                lock (sync) {
                    CheckDisposed ();
                    return gsm.CompletedTasks;
                }
            }
        }

        protected virtual bool IsDisposed
        {
            get {
                lock (sync) { return disposed; }
            }
        }

        protected virtual bool IsDisposing
        {
            get {
                lock (sync) { return disposed | is_disposing; }
            }
        }

        public virtual bool IsBusy
        {
            get {
                lock (sync) {
                    CheckDisposed ();
                    return executing;
                }
            }
        }

        public virtual int RunningTasks
        {
            get {
                lock (sync) {
                    CheckDisposed ();
                    return gsm.RunningTasks;
                }
            }
        }

        public virtual int RemainingTasks
        {
            get {
                lock (sync) {
                    CheckDisposed ();
                    return gsm.RemainingTasks;
                }
            }
        }

        public WaitHandle Handle {
            get { return mre; }
        }

        protected CommandQueue EventQueue
        {
            get { return commandQueue; }
        }

        protected List<T> CurrentTasks {
            get { return currentTasks; }
        } 

        protected GroupProgressManager<T> ProgressManager
        {
            get { return gpm; }
            set { SetProgressManager (value); }
        }

        protected GroupStatusManager StatusManager
        {
            get {
                if (gsm == null) {
                    SetStatusManager (new GroupStatusManager ());
                }

                return gsm;
            }

            set { SetStatusManager (value); }
        }

        public int MaxRunningTasks
        {
            get {
                lock (sync) {
                    CheckDisposed ();
                    return gsm.MaxRunningTasks;
                }
            }

            set {
                lock (sync) {
                    CheckDisposed ();
                    gsm.MaxRunningTasks = value;
                }
            }
        }

        public object SyncRoot
        {
            get { return sync; }
        }

        private bool IsDone
        {
            get { return (gsm.RemainingTasks == 0); }
        }

        public TaskGroup (int maxRunningTasks)
            : this (maxRunningTasks, null, null)
        {
        }

        protected TaskGroup (int maxRunningTasks, GroupStatusManager statusManager)
            : this (maxRunningTasks, statusManager, null)
        {
        }

        protected TaskGroup (int maxRunningTasks,
                             GroupStatusManager statusManager,
                             GroupProgressManager<T> progressManager)
        {
            if (maxRunningTasks < 0) {
                throw new ArgumentException ("maxRunningTasks must be >= 0");
            }

            mre = new ManualResetEvent (true);

            InitTaskGroup_Collection ();
            currentTasks = new List<T> (maxRunningTasks);

            id = Guid.NewGuid ();
            commandQueue = new CommandQueue ();

            SetStatusManager (statusManager ?? new GroupStatusManager ());
            SetProgressManager (progressManager ?? new GroupProgressManager<T> ());

            gsm.MaxRunningTasks = maxRunningTasks;
        }

        public virtual void CancelAsync ()
        {
            if (SetCancelled ()) {
                lock (sync) {
                    foreach (T task in list) {
                        task.CancelAsync ();
                    }
                }
            }
        }

        public virtual void StopAsync ()
        {
            if (SetCancelled ()) {
                lock (sync) {
                    foreach (T task in list) {
                        task.StopAsync ();
                    }
                }
            }        
        }

        public virtual void Dispose ()
        {
            if (SetDisposing ()) {
                commandQueue.Dispose ();
    
                if (SetDisposed ()) {
                    DisposeImpl ();
                }                
            }
        }
        
        // Most subclasses will only need to override DisposeImpl
        protected virtual void DisposeImpl ()
        {
            gsm.StatusChanged -= OnStatusChangedHandler;
            gpm.ProgressChanged -= OnProgressChangedHandler;
            
            if (gsm is IDisposable) {
                gsm.Dispose ();
            }

            mre.Close ();

            gpm = null;
            gsm = null;
            mre = null;
        }

        public void Execute ()
        {
            if (SetExecuting (true)) {
                OnStarted ();
                SpawnExecutionThread ();
            }
        }

        protected virtual bool SetCancelled ()
        {
            lock (sync) {
                CheckDisposed ();

                if (executing && !cancelRequested) {
                    cancelRequested = true;
                    return true;
                }
            }

            return false;
        }

        protected bool SetDisposed ()
        {
            lock (sync) {
                if (executing) {
                    throw new InvalidOperationException ("Unable to dispose while executing");
                }
            
                if (!disposed) {
                    disposed = true;
                    return true;
                }
            }

            return false;
        }

        protected bool SetDisposing ()
        {
            lock (SyncRoot) {
                // It's ok for sub-classes to call this in their dispose methods too.
                if (!disposed) {
                    is_disposing = true;
                    return true;
                }
            }
                
            return false;
        } 

        protected virtual bool SetExecuting (bool exec)
        {
            lock (sync) {
                CheckDisposed ();

                if (exec) {
                    if (!executing && !cancelRequested) {
                        executing = true;
                        return true;
                    }
                } else {
                    executing = false;
                    cancelRequested = false;
                    return true;
                }
            }

            return false;
        }

        protected virtual void SetProgressManager (GroupProgressManager<T> progressManager)
        {
            CheckDisposed ();

            if (progressManager == null) {
                throw new ArgumentNullException ("progressManager");
            } else if (gpm != null) {
                throw new InvalidOperationException ("ProgressManager already set");
            }

            gpm = progressManager;
            gpm.ProgressChanged += OnProgressChangedHandler;
        }

        protected virtual void SetStatusManager (GroupStatusManager statusManager)
        {
            CheckDisposed ();

            if (statusManager == null) {
                throw new ArgumentNullException ("statusManager");
            } else if (gsm != null) {
                throw new InvalidOperationException ("StatusManager already set");
            }

            gsm = statusManager;
            gsm.StatusChanged += OnStatusChangedHandler;
        }

        protected virtual void CheckDisposed ()
        {
            if (disposed) {
                throw new ObjectDisposedException (GetType ().FullName);
            }
        }

        protected virtual void Associate (T task)
        {
            Associate (task, true);
        }

        protected virtual void Associate (IEnumerable<T> tasks)
        {
            foreach (T task in tasks) {
                if (task != null) {
                    Associate (task, false);
                }
            }

            gpm.Add (tasks);
        }

        protected virtual void Associate (T task, bool addToProgressGroup)
        {
            CheckDisposed ();

            if (task.GroupID != Guid.Empty) {
                throw new ApplicationException ("task:  Already associated with a group");
            }

            task.GroupID = id;
            task.EventQueue = commandQueue;
            
            task.Started += OnTaskStartedHandler;
            task.Completed += OnTaskCompletedHandler;

            task.Updated += OnTaskUpdatedHandler;
            task.StateChanged += OnTaskStateChangedHandler;            
            task.ProgressChanged += OnTaskProgressChangedHandler;
                
            if (addToProgressGroup) {
                gpm.Add (task);
            }
        }

        protected virtual bool CheckID (T task)
        {
            return (task.GroupID == id);
        }

        protected virtual void Disassociate (IEnumerable<T> tasks)
        {
            foreach (T task in tasks) {
                Disassociate (task, false);
            }

            gpm.Remove (tasks);
        }

        protected virtual void Disassociate (T task)
        {
            Disassociate (task, true);
        }

        protected virtual void Disassociate (T task, bool removeFromGroup)
        {                        
            if (CheckID (task)) {
                task.EventQueue = null;            
                task.GroupID = Guid.Empty;
                
                task.Started -= OnTaskStartedHandler;
                task.Completed -= OnTaskCompletedHandler;
    
                task.Updated -= OnTaskUpdatedHandler;
                task.StateChanged -= OnTaskStateChangedHandler;            
                task.ProgressChanged -= OnTaskProgressChangedHandler;
                
                if (removeFromGroup) {
                    list.Remove (task);                                        
                    gpm.Remove (task);
                }
            }
        }

        protected virtual void Reset ()
        {
            lock (sync) {
                gsm.Reset ();
                gpm.Reset ();
            }
        }

        protected virtual void OnStarted ()
        {
            OnTaskGroupStarted ();
            mre.Reset ();            
        }

        protected virtual void OnStopped ()
        {
            OnTaskGroupStopped ();
            mre.Set ();            
        }

        protected virtual void OnTaskStarted (T task)
        {
            OnTaskEvent (task, TaskStarted);
        }

        protected virtual void OnTaskCompleted (T task, TaskCompletedEventArgs e)
        {
            EventHandler<TaskCompletedEventArgs<T>> handler = TaskCompleted;

            if (handler != null) {
                commandQueue.Register (
                    new EventWrapper<TaskCompletedEventArgs<T>> (
                        handler, this, new TaskCompletedEventArgs<T> (task, e)
                    )
                );
            }
        }

        protected virtual void OnTaskStateChanged (T task, TaskStateChangedEventArgs e)
        {
            EventHandler<TaskStateChangedEventArgs<T>> handler = TaskStateChanged;

            if (handler != null) {
                commandQueue.Register (
                    new EventWrapper<TaskStateChangedEventArgs<T>> (
                        handler, this, new TaskStateChangedEventArgs<T> (task, e)
                    )
                );
            }
        }

        protected virtual void OnTaskStateChangedHandler (object sender, TaskStateChangedEventArgs e)
        {
            lock (sync) {
                if (e.OldState == TaskState.Paused && e.NewState == TaskState.Ready) {
                    gsm.Evaluate ();                                
                }

                OnTaskStateChanged (sender as T, e); 
            }
        }  

        protected virtual void OnTaskUpdatedHandler (object sender, TaskEventArgs e)
        {
            OnTaskEvent (sender as T, TaskUpdated);
        }

        protected virtual void OnStatusChangedHandler (object sender, GroupStatusChangedEventArgs e)
        {
            lock (sync) {
                EventHandler<GroupStatusChangedEventArgs> handler = StatusChanged;

                if (handler != null) {
                    commandQueue.Register (new EventWrapper<GroupStatusChangedEventArgs> (handler, this, e));
                }
            }
        }

        protected virtual void OnTaskCompletedHandler (object sender, TaskCompletedEventArgs e)
        {
            lock (sync) {
                T t = sender as T;
                gsm.SuspendUpdate = true;                

                try {                   
                    gsm.DropPendingTask (t);                
                    
                    if (currentTasks.Contains (t)) {
                        currentTasks.Remove (t);                        

                        if (e.State != TaskState.Paused && !e.Cancelled) {
                            gsm.IncrementCompletedTaskCount ();                            
                        }
                        
                        gsm.DecrementRunningTaskCount ();                        
                    }
                } finally {
                    if (e.State != TaskState.Paused) {
                        gsm.DecrementRemainingTaskCount ();
                        Disassociate (t);                        
                    }
                    
                    OnTaskCompleted (t, e);
                    
                    gsm.SuspendUpdate = false;
                    gsm.Update ();
                }
            }
        }

        protected virtual void OnTaskProgressChangedHandler (object sender, ProgressChangedEventArgs e)
        {
            EventHandler<TaskProgressChangedEventArgs<T>> handler = TaskProgressChanged;

            lock (sync) {
                gpm.Update (sender as T, e.ProgressPercentage);
            }

            if (handler != null) {
                handler (this, new TaskProgressChangedEventArgs<T> (sender as T, e.ProgressPercentage, e.UserState));
            }
        }

        protected virtual void OnTaskStartedHandler (object sender, EventArgs e)
        {
            lock (sync) {
                T task = sender as T;
                
                currentTasks.Add (task);

                gsm.DropPendingTask (task);
                gsm.IncrementRunningTaskCount ();
                
                OnTaskStarted (task);
            }
        }

        protected virtual void OnProgressChangedHandler (object sender, ProgressChangedEventArgs e)
        {
            lock (sync) {
                EventHandler<ProgressChangedEventArgs> handler = ProgressChanged;

                if (handler != null) {
                    commandQueue.Register (
                        new EventWrapper<ProgressChangedEventArgs> (handler, this, e)
                    );
                }
            }
        }

        private void OnTaskGroupStarted ()
        {
            EventHandler<EventArgs> handler = Started;

            if (handler != null) {
                commandQueue.Register (
                    new EventWrapper<EventArgs> (handler, this, EventArgs.Empty)
                );
            }        
        }
        
        private void OnTaskGroupStopped ()
        {
            EventHandler<EventArgs> handler = Stopped;

            if (handler != null) {
                commandQueue.Register (
                    new EventWrapper<EventArgs> (handler, this, EventArgs.Empty)
                );
            }  
        }

        private void OnTaskEvent (T task, EventHandler<TaskEventArgs<T>> eventHandler)
        {
            if (eventHandler != null) {
                commandQueue.Register (
                    new EventWrapper<TaskEventArgs<T>> (eventHandler, this, new TaskEventArgs<T> (task))
                );
            }
        }

        private void PumpQueue ()
        {
            try {                
                PumpQueueImpl ();
            } catch (Exception e) {
                Console.WriteLine (e.Message);
                Console.WriteLine (e.StackTrace);
                throw;
            }
        }

        private void PumpQueueImpl ()
        {
            T task;

            while (true) {
                gsm.Wait ();              
                
                lock (sync) {
                    gsm.ResetWait ();

                    if (IsDone) {
                        if (SetExecuting (false)) {
                            Reset ();
                            OnStopped ();    
                            return;
                        }
                    } else if (cancelRequested) {
                        continue;
                    }    
                    
                    task = list.Find (
                        delegate (T t) {
                            return (!t.IsBusy && !t.IsFinished && t.State == TaskState.Ready);
                        }
                    );

                    if (task != null) {
                        try {
                            if (gsm.RegisterPendingTask (task)) {
                                task.ExecuteAsync ();
                            }
                        } catch /*(Exception e)*/ {
                            // Add an exception logging system that allows Hyena.Logging to hook in.
                            // Implementations of 'ExecuteAsync' should never throw an exception.
                            gsm.DropPendingTask (task);
                        }
                    }
                }
            }
        }
        
        private void SpawnExecutionThread ()
        {
            Thread t = new Thread (new ThreadStart (PumpQueue));
            t.Priority = ThreadPriority.Normal;
            t.IsBackground = true;
            t.Start ();
        }
    }
}
