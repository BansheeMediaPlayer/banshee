/***************************************************************************
 *  DownloadManager.cs
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
using System.IO;
using System.Web;

using System.Text;
using System.Threading;
using System.ComponentModel;
using System.Collections.Generic;

using System.Security.Cryptography;

using Migo.TaskCore;
using Migo.TaskCore.Collections;

namespace Migo.DownloadCore
{
	public class DownloadManager : IDisposable
	{
        private static Dictionary<string, Type> downloader_types;

        static DownloadManager () {
            downloader_types = new Dictionary<string,Type> (StringComparer.OrdinalIgnoreCase);
        }
		
        public static void Register (string fileType, Type type)
        {
            lock (downloader_types) {
                downloader_types.Add (fileType, type);
            }
        }
		
		
        private bool disposed;
        private string tmpDir;
        
        private HttpFileDownloadGroup dg;
        private TaskList<HttpFileDownloadTask> tasks;    
        
        public HttpFileDownloadGroup Group
        {
            get {
                return dg;   
            }
        }
        
        public object SyncRoot
        {
            get {
                return tasks.SyncRoot;   
            }
        }
        
        public TaskList<HttpFileDownloadTask> Tasks
        {
            get {
                return tasks;   
            }
        }
        
        public DownloadManager (int maxDownloads, string tmpDownloadDir)
        {
            if (!tmpDownloadDir.EndsWith (Convert.ToString (Path.DirectorySeparatorChar))) {
                tmpDownloadDir += Path.DirectorySeparatorChar;
            }
            
            if (!Directory.Exists (tmpDownloadDir)) {
                try {
                    Directory.CreateDirectory (tmpDownloadDir);
                } catch (Exception e) {
                    throw new ApplicationException (String.Format (
                        "Unable to create temporary download directory:  {0}",
                        e.Message
                    ));
                }
            }
            
            tmpDir = tmpDownloadDir;
            tasks = new TaskList<HttpFileDownloadTask> ();
            
            dg = new HttpFileDownloadGroup (maxDownloads, tasks); 
            dg.TaskStopped += TaskStoppedHandler;
            dg.TaskAssociated += TaskAssociatedHandler;
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
            string fileName = System.Web.HttpUtility.UrlDecode (segments[segments.Length-1].Trim ('/'));
            
            MD5 hasher = MD5.Create ();
            
            byte[] hash = hasher.ComputeHash (
                Encoding.UTF8.GetBytes (url)
            );

            string urlHash = BitConverter.ToString (hash)
                                         .Replace ("-", String.Empty)
                                         .ToLower ();
            
            string remoteUri = url;
            string localPath = tmpDir + urlHash + Path.DirectorySeparatorChar + Hyena.StringUtil.EscapeFilename (fileName);

            HttpFileDownloadTask task = null;
            string [] parts = fileName.Split ('.');
            if (parts.Length > 0 && downloader_types.ContainsKey (parts[parts.Length - 1])) {
                task = (HttpFileDownloadTask) Activator.CreateInstance (downloader_types[parts[parts.Length - 1]], remoteUri, localPath, userState);
            } else {
                task = new HttpFileDownloadTask (url , localPath, userState );
            }
			
            return task;
        }

        public void Dispose ()
        {
            if (SetDisposed ()) {
                if (dg != null) {                
                    dg.StopAsync ();
                    dg.Handle.WaitOne ();

                    dg.TaskStopped -= TaskStoppedHandler;       
                    dg.Dispose ();
                }
                
                tasks = null;
            }
        }
                
        public void QueueDownload (HttpFileDownloadTask task)
        {
            if (task == null) {
                throw new ArgumentNullException ("task");
            }

            lock (SyncRoot) {
                if (disposed) {
                    return;
                }
                
                tasks.Add (task);
            }
        }
        
        public void QueueDownload (IEnumerable<HttpFileDownloadTask> tasks)
        {
            if (tasks == null) {
                throw new ArgumentNullException ("tasks");
            }

            lock (SyncRoot) {
                if (disposed) {
                    return;
                }
                
                this.tasks.AddRange (tasks);
            }
        }        

        public void RemoveDownload (HttpFileDownloadTask task)
        {
            if (task == null) {
                throw new ArgumentNullException ("task");
            }

            lock (SyncRoot) {
                if (disposed) {
                    return;
                }
                
                tasks.Remove (task);
            }            
        }
        
        public void RemoveDownload (IEnumerable<HttpFileDownloadTask> tasks)
        {
            if (tasks == null) {
                throw new ArgumentNullException ("tasks");
            }

            lock (SyncRoot) {
                if (disposed) {
                    return;
                }
                
                this.tasks.Remove (tasks);
            }            
        } 

        private bool SetDisposed ()
        {
            bool ret = false;
                
            lock (SyncRoot) {
                if (!disposed) {
                    ret = disposed = true;   
                }
            }
                
            return ret;
        }                
        
        private void TaskStoppedHandler (object sender, 
                                         TaskEventArgs<HttpFileDownloadTask> e) 
        {
            lock (SyncRoot) {
                if (e.Task.IsCompleted) {
                    tasks.Remove (e.Task);                	
                }
            }            
        }
        
        private void TaskAssociatedHandler (object sender, 
                                            TaskEventArgs<HttpFileDownloadTask> e)
        {
            dg.Execute ();
        }
    }   
}
