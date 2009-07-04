// 
// GroupStatusManager.cs
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
using System.Collections.Generic;

namespace Migo2.Async
{
    public class GroupStatusManager : IDisposable
    {
        private bool disposed;
        private bool suspendUpdate;

        private int runningTasks;
        private int completedTasks;
        private int remainingTasks;
        private int maxRunningTasks = 0;

        private ManualResetEvent mre;
        private List<Task> pendingTasks; // Should be a Dictionary
        
        public event EventHandler<GroupStatusChangedEventArgs> StatusChanged;

        public virtual int CompletedTasks
        {
            get {
                CheckDisposed ();
                return completedTasks;
            }
        }

        public virtual int RunningTasks
        {
            get {
                CheckDisposed ();
                return runningTasks;
            }
        }

        public virtual bool SuspendUpdate
        {
            get {
                CheckDisposed ();
                return suspendUpdate;
            }

            set {
                CheckDisposed ();
                suspendUpdate = value;
            }
        }

        public virtual int RemainingTasks
        {
            get {
                CheckDisposed ();
                return remainingTasks;
            }

            set {
                CheckDisposed ();
                SetRemainingTasks (value);
            }
        }

        public virtual int MaxRunningTasks
        {
            get {
                CheckDisposed ();
                return maxRunningTasks;
            }

            set {
                CheckDisposed ();
                SetMaxRunningTasks (value);
            }
        }

        public virtual WaitHandle Handle {
            get {
                CheckDisposed ();
                return mre;
            }
        }

        public GroupStatusManager () : this (0, 0) 
        {
        }
        
        public GroupStatusManager (int totalTasks) : this (totalTasks, 0) 
        {
        }
        
        public GroupStatusManager (int maxRunningTasks, int totalTasks)
        {
            pendingTasks = new List<Task> (maxRunningTasks);
            mre = new ManualResetEvent (false);

            SetRemainingTasks (totalTasks);
            SetMaxRunningTasks (maxRunningTasks);
        }

        public virtual void Dispose ()
        {
            if (!disposed) {
                disposed = true;

                if (mre != null) {
                    mre.Close ();
                    mre = null;
                }
            }
        }

        public virtual bool DropPendingTask (Task task)
        {
            if (task == null) {
                throw new ArgumentNullException ("task");
            }
            
            return pendingTasks.Remove (task);
        }

        public virtual bool RegisterPendingTask (Task task)
        {
            if (task == null) {
                throw new ArgumentNullException ("task");
            }
            
            if (!pendingTasks.Contains (task)) {
                pendingTasks.Add (task);
                return true;
            }
            
            return false;
        }

        public virtual int IncrementCompletedTaskCount ()
        {
            CheckDisposed ();

            ++completedTasks;
            OnStatusChanged ();

            return completedTasks;
        }

        public virtual int DecrementCompletedTaskCount ()
        {
            CheckDisposed ();

            if (completedTasks == 0) {
                throw new InvalidOperationException ("Completed task count cannot be less than 0");
            }

            --completedTasks;
            OnStatusChanged ();

            return completedTasks;
        }

        public virtual int IncrementRunningTaskCount ()
        {
            CheckDisposed ();

            if (runningTasks >= remainingTasks) {
                throw new InvalidOperationException ("Running task count cannot be > remaining task count");
            }

            ++runningTasks;
            OnStatusChanged ();

            Evaluate ();

            return runningTasks;
        }

        public virtual int DecrementRunningTaskCount ()
        {
            CheckDisposed ();

            if (runningTasks == 0) {
                throw new InvalidOperationException ("Runing task count cannot be less than 0");
            }

            --runningTasks;
            OnStatusChanged ();
            Evaluate ();

            return runningTasks;
        }

        public virtual int IncrementRemainingTaskCount ()
        {
            CheckDisposed ();

            SetRemainingTasks (remainingTasks + 1);
            Evaluate ();

            return remainingTasks;
        }

        public virtual int DecrementRemainingTaskCount ()
        {
            CheckDisposed ();

            if (remainingTasks == 0) {
                throw new InvalidOperationException ("Remaining task count cannot be less than 0");
            }

            SetRemainingTasks (remainingTasks - 1);
            Evaluate ();

            return remainingTasks;
        }

        public virtual void ResetWait ()
        {
            CheckDisposed ();
            mre.Reset ();
        }

        public virtual void Reset ()
        {
            completedTasks = 0;
            suspendUpdate = false;
        }

        public virtual bool SetRemainingTasks (int newRemainingTasks)
        {
            CheckDisposed ();
            if (newRemainingTasks < 0) {
                throw new ArgumentException ("newRemainingTasks must be >= 0");
            }

            bool ret = false;

            if (remainingTasks != newRemainingTasks) {
                ret = true;
                remainingTasks = newRemainingTasks;
                Evaluate ();
                OnStatusChanged ();                
            }

            return ret;
        }

        public virtual void Update ()
        {
            CheckDisposed ();
            OnStatusChanged ();
            Evaluate ();
        }

        public virtual void Wait ()
        {
            CheckDisposed ();
            mre.WaitOne ();
        }

        protected virtual void CheckDisposed ()
        {
            if (disposed) {
                throw new ObjectDisposedException (GetType ().FullName);
            }
        }

        public virtual void Evaluate ()
        {
            if (suspendUpdate) {
                return;
            }
/*
            if ((remainingTasks == 0 && maxRunningTasks > 0) ||
                (runningTasks < maxRunningTasks && (remainingTasks - runningTasks) > 0)) {
                mre.Set ();
            }
*/            
            int pendingAndRunningTasks = pendingTasks.Count + runningTasks;
            
            if ((remainingTasks == 0 && maxRunningTasks > 0) ||
                (pendingAndRunningTasks < maxRunningTasks && (remainingTasks - pendingAndRunningTasks) > 0)) {
                mre.Set ();
            }
        }

        protected virtual void SetMaxRunningTasks (int newMaxTasks)
        {
            CheckDisposed ();

            if (newMaxTasks < 0) {
                throw new ArgumentException ("newMaxTasks must be >= 0");
            }

            if (maxRunningTasks != newMaxTasks) {
                maxRunningTasks = newMaxTasks;
                Evaluate ();
            }
        }

        protected virtual void OnStatusChanged (GroupStatusChangedEventArgs e)
        {
            if (suspendUpdate) {
                return;
            }

            EventHandler<GroupStatusChangedEventArgs> handler = StatusChanged;

            if (handler != null) {
                handler (this, e);
            }
        }

        protected virtual void OnStatusChanged ()
        {
            if (suspendUpdate) {
                return;
            }

            OnStatusChanged (
                new GroupStatusChangedEventArgs (remainingTasks, runningTasks, completedTasks)
            );
        }
    }
}
