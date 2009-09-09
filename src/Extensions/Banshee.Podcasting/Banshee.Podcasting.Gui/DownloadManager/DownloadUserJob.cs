/***************************************************************************
 *  DownloadUserJob.cs
 *
 *  Copyright (C) 2007 Michael C. Urbanski
 *  Written by Mike Urbanski <michael.c.urbanski@gmail.com>
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

using Gtk;
using Mono.Unix;

using Banshee.ServiceStack;

namespace Banshee.Podcasting.Gui
{
    public class DownloadUserJob : UserJob 
    {
        private bool disposed = false;
        private bool canceled = false;
        private bool cancelRequested = false;
        
        private readonly object sync = new object ();

        public DownloadUserJob () : base (Catalog.GetString ("Downloads"), String.Empty, String.Empty)
        {
            CancelRequested += OnCancelRequested;
            
            Title = Catalog.GetString ("Downloading Podcast(s)");
            Status = Catalog.GetString ("Initializing...");           
            CancelMessage = Catalog.GetString ("Cancel all podcast downloads?");

            this.IconNames = new string[1] {
                Stock.Network
            };
            
            CanCancel = true;
        }

        public void Dispose ()
        {
            lock (sync) {
                if (disposed) {
                    throw new ObjectDisposedException (GetType ().FullName);
                } else if (cancelRequested) {
                    throw new InvalidOperationException ("Cannot dispose object while canceling.");
                } else {
                    disposed = true;   
                }
                
                CancelRequested -= OnCancelRequested;                
            }
        }
        
        private bool SetCanceled ()
        {
            bool ret = false;
            
            lock (sync) {
                if (!cancelRequested && !canceled && !disposed) {
                    CanCancel = false;
                    ret = cancelRequested = true;
                }
            }
            
            return ret;
        }

        public void UpdateProgress (int progress)
        {
            if (progress < 0 || progress > 100) {
                throw new ArgumentException ("progress:  Must be between 0 and 100.");
            }
            
            lock (sync) {
                if (canceled || cancelRequested || disposed) {
                    return;
                }
                
                Progress = (double) progress / 100;                            
            }
        }
        
        public void UpdateStatus (int downloading, int remaining, int completed, long bytesPerSecond)
        {
            if (downloading < 0) {
                throw new ArgumentException ("downloading:  Must be positive.");                
            } else if (bytesPerSecond < 0) {
                bytesPerSecond = 0;
            }
            
            lock (sync) {
                if (canceled || cancelRequested || disposed) {
                    return;
                }

                int total = remaining + completed;
                string fmt = Catalog.GetPluralString (
                        "Transferring {0} file at {1} KB/s",
                        "Transferring {0} of {2} files at {1} KB/s", total
                );

                Status = String.Format (fmt, downloading, (bytesPerSecond / 1024), total);
            }
        }   
        
        private void OnCancelRequested (object sender, EventArgs e)
        {
            if (SetCanceled ()) {
                lock (sync)  {            
                    Progress = 0.0;
                    Title = Catalog.GetString ("Canceling Downloads");
                    Status = Catalog.GetString (
                        "Waiting for downloads to terminate..."
                    );

                    cancelRequested = false;
                    canceled = true;
                }
            }           
        }
    }
}
