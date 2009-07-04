// 
// HttpFileDownloadTask.cs
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
using System.Net;
using System.Text;
using System.Threading;
using System.ComponentModel;
using System.Security.Cryptography;

using Migo2.Net;
using Migo2.Async;

namespace Migo2.DownloadService
{
    public class HttpFileDownloadTask : Task
    {
        private bool preexistingFile;
        
        private int modified;
        private int rangeError;

        private string mimeType;
        private Uri remoteUri;
        private string localPath;

        private long lastTotalBytes;
        private long lastTotalBytesReceived;
        
        private int timeout = (60 * 1000); // One minute
        private string userAgent;
        
        private HttpStatusCode httpStatus;
        private HttpFileDownloadErrors error;

        private AsyncWebClient wc;
        private FileStream localStream;
        private ICredentials credentials;

        public EventHandler<DownloadStatusUpdatedEventArgs> StatusUpdated;

        public long BytesReceived {
            get {
                lock (SyncRoot) {
                    if (wc != null) {
                        return wc.Status.BytesReceived;
                    }
                }

                return 0;
            }
        }
        
        public long TotalBytes {
            get {
                lock (SyncRoot) {
                    if (wc != null) {
                        if (wc.Status.TotalBytes > 0) {
                            return wc.Status.TotalBytes;                            
                        }
                    }
                }

                return lastTotalBytes;
            }
        }        
        
        public long TotalBytesReceived {
            get {
                lock (SyncRoot) {
                    if (wc != null) {
                        if (wc.Status.TotalBytesReceived > 0) {                    
                            return wc.Status.TotalBytesReceived;
                        }
                    }
                }

                return lastTotalBytesReceived;
            }
        }     
        
        public long TransferRate {
            get {
                lock (SyncRoot) {
                    if (wc != null) {
                        return wc.Status.TransferRate;
                    }
                }

                return 0;
            }
        }            
        
        public ICredentials Credentials {
            get { return credentials; }
            set { credentials = value; }
        }

        public HttpFileDownloadErrors Error
        {
            get { return error; }
        }

        public HttpStatusCode HttpStatusCode
        {
            get { return httpStatus; }
        }

        public string LocalPath
        {
            get { return localPath; }
        }

        public string MimeType
        {
            get { return mimeType; }
        }

        public Uri RemoteUri
        {
            get { return remoteUri; }
        }

        public int Timeout {
            get { return timeout; }
            set { timeout = value; }
        }

        public string UserAgent {
            get { return userAgent; }
            set { userAgent = value; }
        }

        public HttpFileDownloadTask (string name, string remoteUri, string localPath, object userState)
            : base (!String.IsNullOrEmpty (name) ? name : remoteUri, userState)
        {
            this.remoteUri = new Uri (remoteUri);
            this.localPath = localPath;
        }

        public HttpFileDownloadTask (string remoteUri, string localPath) : this (null, remoteUri, localPath, null)
        {
        }

        public override void CancelAsync ()
        {
            Cancel (CancellationType.Aborted);        
        }

        public override void ExecuteAsync ()
        {
            lock (SyncRoot) {
                if (SetBusy ()) {
                    modified = 0;
                    rangeError = 0;
                    preexistingFile = false;
                    
                    OnStarted ();
                    ExecuteImpl ();
                }
            }
        }

        public override void PauseAsync ()
        {
            Cancel (CancellationType.Paused);
        }

        public override void StopAsync ()
        {
            Cancel (CancellationType.Stopped);
        }

        private void Cancel (CancellationType type)
        {
            lock (SyncRoot) {
                if (SetRequestedCancellationType (type)) {
                    if (!IsBusy &&
                        !IsFinished && 
                        ((RequestedCancellationType == CancellationType.Paused) ? SetCompleted (true) : SetCompleted ())) {
                        OnTaskCompleted (null, true);
                    } else if (IsBusy) {  
                        wc.CancelAsync ();
                    }
                }
            }   
        }

        private void DestroyWebClient ()
        {
            if (wc != null) {
                wc.DownloadFileCompleted -= OnDownloadFileCompletedHandler;
                wc.DownloadProgressChanged -= OnDownloadProgressChangedHandler;
                wc.ResponseReceived -= OnResponseReceivedHandler;
                wc = null;
            }       
        }

        private void CloseLocalStream (bool removeFile)
        {
            try {
                if (localStream != null) {
                    localStream.Close ();
                    localStream = null;
                }
            } catch {}

            if (removeFile) {
                RemoveFile ();
            }
        }

        private void ExecuteImpl ()
        {
            Exception err = null;
            bool fileOpenError = false;

            try {
                OpenLocalStream ();
            } catch (Exception e) {
                err = e;
                fileOpenError = true;

                if (e is UnauthorizedAccessException) {
                    error = HttpFileDownloadErrors.UnauthorizedFileAccess;
                } else if (e is IOException) {
                    error = HttpFileDownloadErrors.SharingViolation; // Probably
                } else {
                    error = HttpFileDownloadErrors.Unknown;
                }
            }

            if (err == null) {
                try {
                    InitWebClient ();
                    wc.DownloadFileAsync (remoteUri, localStream);
                } catch (Exception e) {
                    err = e;
                }
            }

            if (err != null) {
                if (!fileOpenError) {
                    CloseLocalStream (true);
                }
                
                if (SetCompleted ()) {
                    DestroyWebClient ();
                    OnTaskCompleted (err, false);                    
                }
            }
        }

        private void InitWebClient ()
        {
            wc = new AsyncWebClient ();

            if (localStream.Length > 0) {
                wc.Range = Convert.ToInt32 (localStream.Length);
            }

            wc.Timeout = timeout;
            
            if (credentials != null) {
                wc.Credentials = credentials;
            }
            
            if (!String.IsNullOrEmpty (userAgent)) {
                wc.UserAgent = userAgent;                
            }
            
            wc.DownloadFileCompleted += OnDownloadFileCompletedHandler;
            wc.DownloadProgressChanged += OnDownloadProgressChangedHandler;
            wc.ResponseReceived += OnResponseReceivedHandler;   
            wc.StatusUpdated += OnWebClientStatusUpdatedHandler;
        }

        private void OpenLocalStream ()
        {
            if (File.Exists (localPath)) {
                localStream = File.Open (localPath, FileMode.Append, FileAccess.Write, FileShare.None);

                preexistingFile = true;
            } else {
                preexistingFile = false;

                if (!Directory.Exists (Path.GetDirectoryName (localPath))) {
                    Directory.CreateDirectory (Path.GetDirectoryName (localPath));
                }

                localStream = File.Open (localPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            }
        }

        private void RemoveFile ()
        {
            if (File.Exists (localPath)) {
                try {
                    preexistingFile = false;
                    File.Delete (localPath);
                    Directory.Delete (Path.GetDirectoryName (localPath));
                } catch {}
            }
        }

        private void OnDownloadFileCompletedHandler (object sender, AsyncCompletedEventArgs e)
        {
            bool retry = false;

            lock (SyncRoot) {
                try {                        
                    if (e.Error != null) {
                        WebException we = e.Error as WebException;
                        
                        if (we != null) {
                            if(we.Status == WebExceptionStatus.ProtocolError) {
                                HttpWebResponse resp = we.Response as HttpWebResponse;                                                          
                               
                                if (resp != null) {
                                    httpStatus = resp.StatusCode;
                                    // This is going to get triggered if the file on disk is complete.
                                    // Maybe request range-1 and see if a content length of 0 is returned.   
                                    if (resp.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable) { 
                                        if (rangeError++ == 0) {
                                            retry = true;                                               
                                        }
                                    }
                                }
                            }
                        }
                        
                        if (!retry) {
                            error = HttpFileDownloadErrors.HttpError;                            
                        }
                    } else if (modified++ == 1) {
                        retry = true;
                    }
                } catch (Exception ex) {
                    Console.WriteLine (ex.Message);
                    Console.WriteLine (ex.StackTrace);
                } finally {
                    if (retry) {
                        CloseLocalStream (true);
                        DestroyWebClient ();
                        ExecuteImpl ();                    
                    } else if (
                        ((RequestedCancellationType == CancellationType.Paused) ? SetCompleted (true) : SetCompleted ())
                    ) {
                        if (e.Cancelled && RequestedCancellationType == CancellationType.Aborted) {
                            CloseLocalStream (true);
                        } else {
                            CloseLocalStream (false);
                        }

                        DestroyWebClient ();
                        OnTaskCompleted (e.Error, e.Cancelled);
                    }
                }             
            }
        }

        private void OnDownloadProgressChangedHandler (object sender, Migo2.Net.DownloadProgressChangedEventArgs e)
        {
            lock (SyncRoot) {
                if (e.ProgressPercentage != 0) {
                    SetProgress (e.ProgressPercentage);
                }
            }
        }

        private void OnResponseReceivedHandler (object sender, EventArgs e)
        {
            lock (SyncRoot) {
                if (wc != null && wc.ResponseHeaders != null) {
                    mimeType = wc.ResponseHeaders.Get ("Content-Type");

                    httpStatus = wc.Response.StatusCode;

                    if (preexistingFile &&
                        wc.Response.LastModified.ToUniversalTime () > File.GetLastWriteTimeUtc (localPath)) {
                        ++modified;
                        wc.CancelAsync ();
                    }
                }
            }
        }
        
        protected virtual void OnWebClientStatusUpdatedHandler (object sender, DownloadStatusUpdatedEventArgs e)
        {
            lock (SyncRoot) {
                lastTotalBytes = e.Status.TotalBytes;
                lastTotalBytesReceived = e.Status.TotalBytesReceived;
            }

            OnDownloadStatusUpdated (e.Status);
        }
        
        protected virtual void OnDownloadStatusUpdated (DownloadStatus status)
        {
            CommandQueue queue = EventQueue;
            EventHandler<DownloadStatusUpdatedEventArgs> handler = StatusUpdated;
            
            if (queue != null && handler != null) {
                queue.Register (delegate { handler (this, new DownloadStatusUpdatedEventArgs (status, UserState)); });
            } else if (handler != null) {
                ThreadPool.QueueUserWorkItem (
                    delegate { handler (this, new DownloadStatusUpdatedEventArgs (status, UserState)); }
                );
            }            
        }
    }
}
