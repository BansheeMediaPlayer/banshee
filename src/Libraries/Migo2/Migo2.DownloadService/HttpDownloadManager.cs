// 
// HttpDownloadManager.cs
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
using System.Web;

using System.Text;
using System.Threading;
using System.ComponentModel;
using System.Collections.Generic;

using System.Security.Cryptography;

using Migo2.Async;
using Migo2.Collections;

namespace Migo2.DownloadService
{
    public class HttpDownloadManager : HttpDownloadGroup
    {
        //private bool disposing;
        private string tmp_dir;

        public string TempDownloadDirectory {
            get { 
                lock (SyncRoot) {
                    return tmp_dir; 
                }
            }
            
            set {
                if (String.IsNullOrEmpty (value)) {
                    return;
                }

                lock (SyncRoot) {
                    if (IsDisposing) {
                        return;
                    }
                    
                    if (!value.EndsWith (Convert.ToString (Path.DirectorySeparatorChar))) {
                        value += Path.DirectorySeparatorChar;
                    }
                    
                    if (!Directory.Exists (value)) {
                        try {
                            Directory.CreateDirectory (value);
                        } catch (Exception e) {
                            throw new ApplicationException (String.Format (
                                "Unable to create temporary download directory:  {0}",
                                e.Message
                            ));
                        }
                    }
                    
                    tmp_dir = value;
                }
            }      
        }

        public HttpDownloadManager (int maxDownloads, string tmpDownloadDir) : base (maxDownloads)
        {
            TempDownloadDirectory = tmpDownloadDir;
        }

        public HttpFileDownloadTask CreateDownloadTask (string url) 
        {
            return CreateDownloadTask (url, null);
        }

        public HttpFileDownloadTask CreateDownloadTask (string url, object userState)
        {
            Uri uri;

            if (String.IsNullOrEmpty (url)) {
                throw new ArgumentException ("Cannot be null or empty", "url");
            } else if (!Uri.TryCreate (url, UriKind.Absolute, out uri)) {
                throw new UriFormatException ("url:  Is not a well formed Uri.");
            }

            string[] segments = uri.Segments;
            string fileName = segments[segments.Length-1].Trim ('/');

            MD5 hasher = MD5.Create ();
            byte[] hash = hasher.ComputeHash (Encoding.UTF8.GetBytes (url));

            string urlHash = BitConverter.ToString (hash)
                                         .Replace ("-", String.Empty)
                                         .ToLower ();

            string localPath = String.Concat (
                Path.Combine (TempDownloadDirectory, urlHash), Path.DirectorySeparatorChar, 
                Hyena.StringUtil.EscapeFilename (fileName)
            );  

            return new HttpFileDownloadTask (
                System.Web.HttpUtility.UrlDecode (fileName), url, localPath, userState
            );         
        }


        public override void Dispose ()
        {
            if (SetDisposing ()) {
                StopAsync ();
                Handle.WaitOne ();
                base.Dispose ();
            }
        }
                
        public void QueueDownload (HttpFileDownloadTask task)
        {
            if (task == null) {
                throw new ArgumentNullException ("task");
            }

            lock (SyncRoot) {
                if (IsDisposing) {
                    return;
                }
                
                Add (task);
            }
        }
        
        public void QueueDownload (IEnumerable<HttpFileDownloadTask> tasks)
        {
            if (tasks == null) {
                throw new ArgumentNullException ("tasks");
            }

            lock (SyncRoot) {
                if (IsDisposing) {
                    return;
                }

                Add (tasks);
            }
        }                       
    }   
}
