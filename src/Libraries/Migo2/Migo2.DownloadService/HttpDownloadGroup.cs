// 
// HttpDownloadGroup.cs
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
using System.IO;
using System.Timers;  // migrate to System.Threading.Timer
using System.Threading;

using System.ComponentModel;
using System.Collections.Generic;

using Hyena;

using Migo2.Net;
using Migo2.Async;

namespace Migo2.DownloadService
{
    public class HttpDownloadGroup : TaskGroup<HttpFileDownloadTask>
    {
        private long transferRate;
        private long transferRatePreviously;
        private long bytesThisInterval = 0;

        private DateTime lastTick;
        private DownloadStatusManager dsm;
        private System.Timers.Timer transferTimer;

        private Dictionary<HttpFileDownloadTask,long> transferRateDict;

        public EventHandler<DownloadTaskStatusUpdatedEventArgs> TaskStatusUpdated;

        public HttpDownloadGroup (int maxDownloads) : base (maxDownloads, new DownloadStatusManager ())
        {
            dsm = StatusManager as DownloadStatusManager;
            transferRateDict = new Dictionary<HttpFileDownloadTask,long> (dsm.MaxRunningTasks);

            InitTransferTimer ();
        }
        
        // already locked at this point
        protected override void Associate (HttpFileDownloadTask task, bool addToProgressGroup)
        {
            base.Associate (task, addToProgressGroup);
            task.StatusUpdated += OnDownloadTaskStatusUpdatedHandler;
        }
        
        // already locked at this point
        protected override void Disassociate (HttpFileDownloadTask task, bool removeFromGroup)
        {                        
            base.Disassociate (task, removeFromGroup);
            task.StatusUpdated -= OnDownloadTaskStatusUpdatedHandler;
        }

        protected override void DisposeImpl ()
        {
            lock (SyncRoot) {
                if (transferTimer != null) {
                    transferTimer.Enabled = false;
                    transferTimer.Elapsed -= OnTransmissionTimerElapsedHandler;
                    transferTimer.Dispose ();
                    transferTimer = null;
                }

                base.DisposeImpl ();
            }
        }

        protected override void OnStarted ()
        {
            lock (SyncRoot) {
                transferTimer.Enabled = true;
                lastTick = DateTime.Now;
                base.OnStarted ();
            }
        }

        protected override void OnStopped ()
        {
            lock (SyncRoot) {
                transferTimer.Enabled = false;
                base.OnStopped ();
            }
        }

        protected override void OnTaskStarted (HttpFileDownloadTask task)
        {
            lock (SyncRoot) {
                transferRateDict.Add (task, task.BytesReceived);
                base.OnTaskStarted (task);
            }
        }

        protected override void OnTaskCompleted (HttpFileDownloadTask task, TaskCompletedEventArgs e)
        {
            lock (SyncRoot) {
                if (transferRateDict.ContainsKey (task)) {
                    long bytesLastCheck = transferRateDict[task];

                    if (task.BytesReceived > bytesLastCheck) {
                        bytesThisInterval += (task.BytesReceived - bytesLastCheck);
                    }

                    transferRateDict.Remove (task);
                }

                base.OnTaskCompleted (task, e);
            }
        }

        protected virtual void SetTransferRate (long bytesPerSecond)
        {
            lock (SyncRoot) {
                dsm.SetTransferRate (bytesPerSecond);
            }
        }
        
        protected override void Reset ()
        {
            lastTick = DateTime.Now;

            transferRate = -1;
            transferRatePreviously = -1;

            base.Reset ();
        }

        protected virtual void OnTransmissionTimerElapsedHandler (object source, ElapsedEventArgs e)
        {
            lock (SyncRoot) {
                UpdateTransferRate ();
            }
        }

        protected virtual void UpdateTransferRate ()
        {
            transferRate = CalculateTransferRate ();

            if (transferRatePreviously == 0) {
                transferRatePreviously = transferRate;
            }

            transferRate = ((transferRate + transferRatePreviously) / 2);

            SetTransferRate (transferRate);
            transferRatePreviously = transferRate;
        }        

        private void InitTransferTimer ()
        {
            transferTimer = new System.Timers.Timer ();

            transferTimer.Elapsed += OnTransmissionTimerElapsedHandler;
            transferTimer.Interval = (1.5 * 1000); // 1.5 seconds
            transferTimer.Enabled = false;
        }

        private long CalculateTransferRate ()
        {
            long bytesPerSecond;

            TimeSpan duration = (DateTime.Now - lastTick);
            double secondsElapsed = duration.TotalSeconds;

            if ((int)secondsElapsed == 0) {
                return 0;
            }

            long tmpCur;
            long tmpPrev;

            foreach (HttpFileDownloadTask dt in CurrentTasks) {
                tmpCur = dt.BytesReceived;
                tmpPrev = transferRateDict[dt];
                transferRateDict[dt] = tmpCur;

                bytesThisInterval += (tmpCur - tmpPrev);
            }

            bytesPerSecond = (long) ((bytesThisInterval / secondsElapsed));

            lastTick = DateTime.Now;
            bytesThisInterval = 0;

            return bytesPerSecond;
        }
        
        protected virtual void OnDownloadTaskStatusUpdatedHandler (object sender, DownloadStatusUpdatedEventArgs e)
        {
            HttpFileDownloadTask task = sender as HttpFileDownloadTask;
            OnDownloadTaskStatusUpdated (task, e.Status);
        }

        protected virtual void OnDownloadTaskStatusUpdated (HttpFileDownloadTask task, DownloadStatus status)
        {
            CommandQueue queue = EventQueue;
            EventHandler<DownloadTaskStatusUpdatedEventArgs> handler = TaskStatusUpdated;
            
            if (queue != null && handler != null) {
                queue.Register (delegate { handler (this, new DownloadTaskStatusUpdatedEventArgs (task, status)); });
            } else if (handler != null) {
                ThreadPool.QueueUserWorkItem (
                    delegate { 
                        handler (this, new DownloadTaskStatusUpdatedEventArgs (task, status)); 
                    }
                );
            }            
        }        
    }
}
