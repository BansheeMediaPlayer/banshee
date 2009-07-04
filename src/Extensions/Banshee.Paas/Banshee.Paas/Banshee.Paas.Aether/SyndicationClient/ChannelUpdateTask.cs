// 
// ChannelUpdateTask.cs
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

using Migo2.Net;
using Migo2.Async;

using Banshee.Paas.Data;

namespace Banshee.Paas.Aether
{
    public class ChannelUpdateTask : Task
    {
        private AsyncWebClient wc;
        private PaasChannel channel;
        private AsyncStateManager state_manager;

        private string result;

        public PaasChannel Channel {
            get { return channel; }
        }

        public string Result {
            get { return result; }
        }

        public ChannelUpdateTask (PaasChannel channel)
        {
            this.channel = channel;
            state_manager = new AsyncStateManager ();
        }

        public override void CancelAsync ()
        {
            lock (SyncRoot) {
                if (state_manager.SetCancelled ()) {
                    if (wc == null) {
                        EmitCompletionEvent (null);
                    } else {
                       wc.CancelAsync ();
                    }
                }                
            }
        }

        public override void ExecuteAsync ()
        {
            lock (SyncRoot) {
                if (state_manager.SetBusy ()) {
                    OnStarted ();
                    
                    try {
                        wc = new AsyncWebClient ();                  
                        wc.Timeout = (30 * 1000); // 30 Seconds  
    
                        if (channel.LastDownloadTime != DateTime.MinValue) {
                            wc.IfModifiedSince = channel.LastDownloadTime.ToUniversalTime ();
                        }
                        
                        wc.DownloadStringCompleted += OnDownloadDataReceived;
                        wc.DownloadStringAsync (new Uri (channel.Url));
                    } catch (Exception e) {
                        EmitCompletionEvent (e);
                    }                        
                }
            }
        }

        private void OnDownloadDataReceived (object sender, DownloadStringCompletedEventArgs args) 
        {
            Exception error = null;
            
            lock (SyncRoot) {
                if (!state_manager.Cancelled) {
                    state_manager.SetCompleted ();
                    
                    if (args.Error != null) {
                        error = args.Error;  
                    } else {
                        result = args.Result;
                    }                              
                }
                
                EmitCompletionEvent (error);
            }
        }
        
        private void EmitCompletionEvent (Exception e)
        {
            if (wc != null) {
                wc.DownloadStringCompleted -= OnDownloadDataReceived;
            }
//
//            if (e != null) {
//                Hyena.Log.Exception (e);            
//            }
//            
            OnTaskCompleted (e, state_manager.Cancelled);            
        }
    }
}
