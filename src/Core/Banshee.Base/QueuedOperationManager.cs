/***************************************************************************
 *  QueuedOperationManager.cs
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@abock.org>
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */
 
using System;
using System.Collections.Generic;

using Gtk;

using Banshee.Kernel;
using Banshee.Widgets;

namespace Banshee.Base
{
    public delegate void QueuedOperationHandler(object o, QueuedOperationArgs args);
    
    public class QueuedOperationArgs : EventArgs
    {
        public object Object;
        public string ReturnMessage;
        public bool Abort;
    }

    public class QueuedOperationManager
    {
        public class OperationCanceledException : ApplicationException
        {
        }
    
        private class QueuedOperationJob : IJob
        {
            private int id = 0;
            private QueuedOperationManager manager;
            
            private object @object;
            private QueuedOperationHandler handler;
            private QueuedOperationArgs args;
            
            public QueuedOperationJob(QueuedOperationManager manager, int id)
            {
                this.manager = manager;
                this.id = id;
            }
            
            public void Run()
            {
                args = new QueuedOperationArgs();
                args.Object = @object;
                
                try {
                    handler(manager, args);
                } catch(QueuedOperationManager.OperationCanceledException) {
                    args.Abort = true;
                }
            }
            
            public int ID {
                get { return id; }
            }
            
            public QueuedOperationHandler Handler {
                get { return handler; }
                set { handler = value; }
            }
            
            public QueuedOperationArgs Args {
                get { return args; }
            }
            
            public object @Object {
                get { return @object; }
                set { @object = value; }
            }
        }
        
        private int total_count;
        private int processed_count;
        private int id;

        private ActiveUserEvent user_event;
        public ActiveUserEvent UserEvent {
            get {
                CreateUserEvent();
                return user_event;
            }
        }
        
        private static int all_ids = 0;
        private static int RequestID()
        {
            return all_ids++;
        }

        public event QueuedOperationHandler OperationRequested;
        public event EventHandler Finished;
        
        public QueuedOperationManager()
        {
            id = RequestID();
            Scheduler.JobFinished += OnJobFinished;
        }
        
        private void CreateUserEvent()
        {
            if(user_event == null) {
                user_event = new ActiveUserEvent(ActionMessage, true);
                user_event.Icon = IconThemeUtils.LoadIcon(22, "system-search", Stock.Find);
                user_event.CancelRequested += OnCancelRequested;
                lock(user_event) {
                    total_count = 0;
                    processed_count = 0;
                }
            }
        }
        
        private void DestroyUserEvent()
        {
            if(user_event != null) {
                lock(user_event) {
                    user_event.Dispose();
                    user_event = null;
                }
            }
            
            total_count = 0;
            processed_count = 0;
                    
            Scheduler.JobFinished -= OnJobFinished;
            
            EventHandler handler = Finished;
            if(handler != null) {
                handler(this, new EventArgs());
            }
        }
        
        private void OnJobFinished(IJob _job)
        {
            if(Scheduler.ScheduledJobsCount == 0) {
                FinalizeOperation();
                return;
            }
            
            QueuedOperationJob job = _job as QueuedOperationJob;
            
            if(job == null) {
                return;
            }
        
            processed_count++;
                
            if(handle_user_event) {
                UpdateCount(job.Args.ReturnMessage);
            }
            
            if(job.Args.Abort || processed_count == total_count) {
                FinalizeOperation();
            }
        }
        
        private void UpdateCount(string message)
        {
            CreateUserEvent();

            double new_progress = (double)processed_count / (double)total_count;
            double old_progress = user_event.Progress;
            
            if(new_progress >= 0.0 && new_progress <= 1.0 && Math.Abs(new_progress - old_progress) > 0.001) {
                string disp_progress = String.Format(ProgressMessage, processed_count, total_count);
                
                user_event.Header = disp_progress;
                user_event.Message = message;
                user_event.Progress = new_progress;
            }
        }
        
        private void OnCancelRequested(object o, EventArgs args)
        {
            FinalizeOperation();
        }
        
        private void FinalizeOperation()
        {
            lock(this) {
                Scheduler.Suspend();
                
                Queue<IJob> to_remove = new Queue<IJob>();
                
                foreach(IJob job in Scheduler.ScheduledJobs) {
                    if(job is QueuedOperationJob && ((QueuedOperationJob)job).ID == id) {
                        to_remove.Enqueue(job);
                    }
                }
                
                while(to_remove.Count > 0) {
                    Scheduler.Unschedule(to_remove.Dequeue());
                }
                
                Scheduler.Resume();
                
                total_count = 0;
                processed_count = 0;
                
                DestroyUserEvent();
            }
        }
        
        public void Enqueue(object obj)
        {
            lock(this) {
                CreateUserEvent();
                total_count++;
                
                QueuedOperationJob job = new QueuedOperationJob(this, id);
                job.Handler = OperationRequested;
                job.Object = obj;
                
                Scheduler.Schedule(job);
            }
        }

        private string action_message;
        public string ActionMessage {
            get { return action_message; }
            set { action_message = value; }
        }
        
        private string progress_message;
        public string ProgressMessage {
            get { return progress_message; }
            set { progress_message = value; }
        }
        
        private bool handle_user_event = true;
        public bool HandleActveUserEvent {
            get { return handle_user_event; }
            set { handle_user_event = value; }
        }
        
        public int TotalCount {
            get { return total_count; }
        }
        
        public int ProcessedCount { 
            get { return processed_count; }
        }
    }
}

