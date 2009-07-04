// 
// DownloadManagerInterface.cs
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
using System.ComponentModel;

using Gtk;

using Migo2.Async;
using Migo2.DownloadService;

using Banshee.ServiceStack;

using Banshee.Paas.Gui;

namespace Banshee.Paas
{
    public class DownloadManagerInterface : IDisposable 
    {
        private bool cancelled;
        private PaasSource source;
        private HttpDownloadManager manager;
        private DownloadUserJob downloadJob;        
        private DownloadSource downloadSource;        
        
        private readonly object sync = new object ();        
        
        public DownloadManagerInterface (PaasSource source, HttpDownloadManager manager)
        {
            if (manager == null) {
                throw new ArgumentNullException ("manager");
            }

            this.source = source;
            this.manager = manager;
            
            downloadSource = new DownloadSource ();
            manager.Started += OnManagerStartedHandler;   
            manager.Stopped += OnManagerStoppedHandler;  
            manager.ProgressChanged += OnManagerProgressChangedHandler;
            manager.StatusChanged += OnManagerStatusChangedHandler;
            
            manager.TaskAdded += OnManagerTaskAddedHandler;
            manager.TaskProgressChanged += (sender, e) => {
                lock (this) {
                    if (cancelled) {
                        return;
                    }
                }

                downloadSource.Reload ();
            };
        
            manager.TaskCompleted += (sender, e) => {
                if (e.Task.State != TaskState.Paused) {
                    lock (this) {
                        if (cancelled) {
                            return;
                        }
                    }
                    
                    downloadSource.RemoveTask (e.Task);                    
                }
            };
            //manager.Group.TaskCompleted += OnManagerTaskCompletedHandler;            
        }
        
        public void Dispose ()
        {
            lock (sync) {
                if (manager != null) {
                    manager.Started -= OnManagerStartedHandler;   
                    manager.Stopped -= OnManagerStoppedHandler;  
                    manager.ProgressChanged -= OnManagerProgressChangedHandler;
                    manager.StatusChanged -= OnManagerStatusChangedHandler; 
                    
                    manager = null;
                }
            }

            ServiceStack.Application.Invoke (delegate {                                
                lock (sync) {
                    if (downloadJob != null) {
                        downloadJob.CancelRequested -= OnCancelRequested;
                        downloadJob.Finish ();
                        downloadJob = null;   
                        source.RemoveChildSource (downloadSource);   
                        downloadSource = null;                    
                    }        
                }
            });            
        }

        private void OnManagerStartedHandler (object sender, EventArgs e)
        {
            ServiceStack.Application.Invoke (delegate {                                
                lock (sync) {
                    cancelled = false;
                    
                    if (downloadJob == null) {
                        source.AddChildSource (downloadSource);                                       
                        
                        downloadJob = new DownloadUserJob ();
                        downloadJob.CancelRequested += OnCancelRequested;  
                        downloadJob.Register ();
                    }        
                }
            });                
        }          
        
        private void OnManagerStoppedHandler (object sender, EventArgs e)
        {
            ServiceStack.Application.Invoke (delegate {            
                lock (sync) {
                    downloadSource.Clear ();

                    if (downloadJob != null) {                        
                        downloadJob.CancelRequested -= OnCancelRequested;
                        downloadJob.Finish ();
                        downloadJob = null;   
                        source.RemoveChildSource (downloadSource);                                                            
                    }
                }
            });
        } 

        private void OnManagerProgressChangedHandler (object sender, ProgressChangedEventArgs e)
        {
            Gtk.Application.Invoke (delegate {
                lock (sync) {
                    if (downloadJob != null) {
                        downloadJob.UpdateProgress (e.ProgressPercentage);
                    }
                }
            });                                
        }   

        private void OnManagerStatusChangedHandler (object sender, GroupStatusChangedEventArgs e)
        {
            HttpDownloadGroupStatusChangedEventArgs args = e as HttpDownloadGroupStatusChangedEventArgs;
            
            ServiceStack.Application.Invoke (delegate {            
                lock (sync) {
                    if (downloadJob != null) {
                        downloadJob.UpdateStatus (args.RunningTasks, args.RemainingTasks, args.CompletedTasks, args.BytesPerSecond);
                    }
                }             
            });            
        }         

        private void OnManagerTaskAddedHandler (object sender, TaskAddedEventArgs<HttpFileDownloadTask> e)
        {
            if (e.TaskPair != null) {
                downloadSource.AddTaskPair (e.TaskPair);
            } else if (e.TaskPairs != null) {
                downloadSource.AddTaskPairs (e.TaskPairs);                
            }
        }

        private void OnCancelRequested (object sender, EventArgs e)
        {
            lock (this) {
                cancelled = true;
                manager.CancelAsync ();
            }
        }          
    }   
}
