// 
// Task.cs
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

namespace Migo2.Async
{
    public abstract class Task
    {
        private bool busy;
        private bool completed;
        private TaskState state;
        
        private int progress;
        private string name;
        private object userState;

        private CancellationType cancellationType;
        private CancellationType requestedCancellationType;

        private Guid groupID;
        private CommandQueue commandQueue;

        private readonly object syncRoot = new object ();
        
        public EventHandler<TaskEventArgs> Started;
        public EventHandler<TaskCompletedEventArgs> Completed;
        
        public EventHandler<TaskEventArgs> Updated; // General update, name, other properties for re-draw.
        
        public EventHandler<TaskStateChangedEventArgs> StateChanged;
        public EventHandler<ProgressChangedEventArgs>  ProgressChanged;
        
        public bool IsBusy
        {
            get {
                lock (syncRoot) {
                    return busy;
                }
            }
        }

        public bool IsFinished
        {
            get {
                lock (syncRoot) {
                    return completed;
                }
            }
        }

        protected CancellationType RequestedCancellationType
        {
            get { return requestedCancellationType; }
        }

        public CommandQueue EventQueue
        {
            get {
                return commandQueue;
            }
            
            set {
                lock (syncRoot) {
                    commandQueue = value;
                }
            }
        }

        public string Name
        {
            get {
                return name;
            }
            
            set {
                lock (syncRoot) {
                    if (value != name) {
                        name = value;
                        OnUpdated ();
                    }
                }
            }
        }

        public int Progress
        {
            get {
                return progress;
            }

            protected set {
                SetProgress (value);
            }
        }

        public virtual TaskState State {
            get {
                lock (syncRoot) {
                    return state;
                }
            }
            
            protected set { 
                lock (syncRoot) {
                    SetState (value);
                }
            }
        }

        public object SyncRoot
        {
            get {
                return syncRoot;
            }
        }

        public object UserState {
            get {
                return userState;
            }
        }

        internal Guid GroupID {
            get {
                return groupID;
            }

            set {
                groupID = value;
            }
        }

        protected Task () : this (String.Empty, null)
        {
        }

        protected Task (string name, object userState)
        {
            progress = 0;
            GroupID  = Guid.Empty;
            state    = TaskState.Ready;
            
            this.name = name;            
            this.userState = userState;
        }

        public abstract void CancelAsync ();
        public abstract void ExecuteAsync ();
        
        public virtual void StopAsync ()
        {
            CancelAsync ();
        }
        
        public virtual void PauseAsync ()
        {
            throw new NotImplementedException ("PauseAsync");
        }
       
        public virtual void ResumeAsync ()
        {
            lock (syncRoot) {
                if (SetResumeRequested ()) {
                    SetState (TaskState.Ready);
                }
            }
        }
       
        public override string ToString ()
        {
            return !String.IsNullOrEmpty (name) ? name : GetType ().ToString ();
        }
        
        protected virtual bool SetBusy ()
        {
            lock (syncRoot) {
                if (busy) {
                    throw new InvalidOperationException ("Concurrent operations are not supported.");
                } else if (completed) {
                    throw new InvalidOperationException ("Task executed previously.");
                } else if (requestedCancellationType == CancellationType.None) {
                    busy = true;
                    SetState (TaskState.Running);
                    return true;
                }
            }
            
            return false;
        }

        protected virtual bool SetCompleted ()
        {
            return SetCompleted (false);
        }

        protected virtual bool SetCompleted (bool paused)
        {
            lock (syncRoot) {
                if (!completed) {
                    busy = false;
                    
                    if (!paused) {
                        completed = true;  
                    }
                    
                    return true;
                }
            }
            
            return false;
        }

        protected virtual void SetProgress (int progress)
        {
            lock (syncRoot) {
                if (progress < 0 || progress > 100) {
                    throw new ArgumentOutOfRangeException ("progress");
                } else if (this.progress != progress) {
                    this.progress = progress;
                    OnProgressChanged (progress);
                }
            }
        }

        protected bool SetRequestedCancellationType (CancellationType type)
        {
            lock (syncRoot) {
                if (requestedCancellationType == CancellationType.None || 
                    requestedCancellationType == CancellationType.Paused) {
                    requestedCancellationType = type;
                    return true;
                }
            }

            return false;
        }
        
        protected bool SetResumeRequested ()
        {
            lock (syncRoot) {
                if (!completed && !busy && cancellationType == CancellationType.Paused) {
                    cancellationType = CancellationType.None;   
                    requestedCancellationType = CancellationType.None;
                    return true;
                }
            }
            
            return false;
        }        

        private void SetState (TaskState state)
        {
            if (this.state != state) {
                TaskState oldState = this.state;
                this.state = state;
                OnStateChanged (oldState, state);
            }                
        }

        protected virtual void OnProgressChanged (int progress)
        {
            OnProgressChanged (
                new ProgressChangedEventArgs (progress, userState)
            );
        }

        protected virtual void OnProgressChanged (ProgressChangedEventArgs e)
        {
            EventHandler<ProgressChangedEventArgs> handler = ProgressChanged;

            if (handler != null) {
                CommandQueue queue = commandQueue;            
                
                if (queue != null) {
                    queue.Register (delegate { handler (this, e); });
                } else {
                    ThreadPool.QueueUserWorkItem (delegate { handler (this, e); });
                }
            }
        }

        protected virtual void OnUpdated ()
        {
            EventHandler<TaskEventArgs> handler = Updated;
            
            if (handler != null) {
                CommandQueue queue = commandQueue;            
                
                if (queue != null) {
                    queue.Register (delegate { handler (this, new TaskEventArgs ()); });
                } else {
                    ThreadPool.QueueUserWorkItem (delegate { handler (this, new TaskEventArgs ()); });
                }                
            }
        }

        protected virtual void OnStarted ()
        {
            EventHandler<TaskEventArgs> handler = Started;
            
            if (handler != null) {
                CommandQueue queue = commandQueue;            
                
                if (queue != null) {
                    queue.Register (delegate { handler (this, new TaskEventArgs ()); });
                } else {
                    ThreadPool.QueueUserWorkItem (delegate { handler (this, new TaskEventArgs ()); });
                }
            }
        }

        protected virtual void OnStateChanged (TaskState oldState, TaskState newState)
        {
            CommandQueue queue = commandQueue;
            EventHandler<TaskStateChangedEventArgs> handler = StateChanged;
            
            if (handler != null) {                
                if (queue != null) {
                    queue.Register (
                        delegate { handler (this, new TaskStateChangedEventArgs (oldState, newState)); }
                    );
                } else {
                    ThreadPool.QueueUserWorkItem (
                        delegate { handler (this, new TaskStateChangedEventArgs (oldState, newState)); }
                    );
                }
            }
        }

        protected virtual void OnTaskCompleted (Exception error, bool cancelled)
        {
            TaskCompletedEventArgs e = null;
            EventHandler<TaskCompletedEventArgs> handler = Completed;
            
            lock (syncRoot) {
                cancellationType = (cancelled) ? requestedCancellationType : CancellationType.None;
                
                if (error != null) {
                    SetState (TaskState.Failed);
                } else if (!cancelled) {
                    SetState (TaskState.Succeeded);
                } else {
                    switch (cancellationType) {
                    case CancellationType.Aborted:
                        SetState (TaskState.Cancelled); break;
                    case CancellationType.Paused:
                        SetState (TaskState.Paused);    break;                    
                    case CancellationType.Stopped:
                        SetState (TaskState.Stopped);   break;                                        
                    }
                }

                e = new TaskCompletedEventArgs (error, state, userState);
            }

            if (handler != null) {
                CommandQueue queue = commandQueue;         
                
                if (queue != null) {
                    queue.Register (delegate { handler (this, e); });
                } else {
                    ThreadPool.QueueUserWorkItem (delegate { handler (this, e); });
                }
            }
        }
    }
}
