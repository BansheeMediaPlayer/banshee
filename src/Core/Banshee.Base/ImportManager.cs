/***************************************************************************
 *  ImportManager.cs
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
using System.IO;
using System.Collections;
using System.Threading;
using Mono.Unix;

using Gtk;

using Banshee.Widgets;
using Banshee.IO;

namespace Banshee.Base
{
    public delegate void ImportEventHandler(object o, ImportEventArgs args);
    
    public class ImportEventArgs : EventArgs
    {
        public string FileName;
        public string ReturnMessage;
    }

    public class ImportManager
    {
        private class ImportCanceledException : ApplicationException
        {
        }
        
        private static ImportManager instance;
        public static ImportManager Instance {
            get {
                if(instance == null) {
                    instance = new ImportManager();
                }
                
                return instance;
            }
        }
        
        private static Gdk.Pixbuf user_event_icon = IconThemeUtils.LoadIcon(22, "system-search", Stock.Find);
        private static readonly object user_event_mutex = new object();
        
        private Queue path_queue;
        private ActiveUserEvent user_event;
        private int total_count;
        private int processed_count;
        private int scan_ref_count = 0;
        private bool processing_queue = false;
        
        public event ImportEventHandler ImportRequested;
        public event EventHandler ImportFinished;
        
        public ImportManager()
        {
            path_queue = new Queue();
        }
        
        public bool IsImportingInProgress() 
        {
            return this.processing_queue;
        }
        
        private void CreateUserEvent()
        {
            lock(user_event_mutex) {
                if(user_event == null) {
                    user_event = new ActiveUserEvent(Title);
                    user_event.CancelMessage = CancelMessage;
                    user_event.Icon = user_event_icon;
                    user_event.Message = Catalog.GetString("Scanning for songs");
                    total_count = 0;
                    processed_count = 0;
                }
            }
        }
        
        private void DestroyUserEvent()
        {
            lock(user_event_mutex) {
                if(user_event != null) {
                    user_event.Dispose();
                    user_event = null;
                    total_count = 0;
                    processed_count = 0;
                    scan_ref_count = 0;
                }
            }
        }
        
        private void UpdateCount(string message)
        {
            CreateUserEvent();
            processed_count++;
            
            double new_progress = (double)processed_count / (double)total_count;
            double old_progress = user_event.Progress;
            
            if(new_progress >= 0.0 && new_progress <= 1.0 && Math.Abs(new_progress - old_progress) > 0.001) {
                string disp_progress = String.Format(ProgressMessage,
                    processed_count, total_count);
                
                user_event.Header = disp_progress;
                user_event.Message = message;
                user_event.Progress = new_progress;
            }
        }
        
        private void CheckForCanceled()
        {
            lock(user_event_mutex) {
                if(user_event != null && user_event.IsCancelRequested) {
                    throw new ImportCanceledException();  
                }
            }
        }
        
        private void FinalizeImport()
        {
            path_queue.Clear();
            processing_queue = false;
            DestroyUserEvent();
        }
        
        private void Enqueue(string path)
        {
            if(path_queue.Contains(path)) {
                return;
            }
            
            total_count++;
            lock(path_queue.SyncRoot) {
                path_queue.Enqueue(path);
            }
        }
        
        public void QueueSource(UriList uris)
        {
            CreateUserEvent();
            ThreadAssist.Spawn(delegate {
                try {
                    foreach(string path in uris.LocalPaths) {
                        scan_ref_count++;
                        ScanForFiles(path);
                        scan_ref_count--;
                    }
                    
                    if(scan_ref_count == 0) {
                        ProcessQueue();
                    }
                } catch(ImportCanceledException) {
                    FinalizeImport();
                }
            });
        }
        
        public void QueueSource(Gtk.SelectionData selection)
        {
            QueueSource(new UriList(selection));
        }
        
        public void QueueSource(string source)
        {
            QueueSource(new UriList(source));
        }
        
        public void QueueSource(string [] paths)
        {
            QueueSource(new UriList(paths));
        }
        
        private void ScanForFiles(string source)
        {
            CheckForCanceled();
            scan_ref_count++;
            
            bool is_regular_file = false;
            bool is_directory = false;

            SafeUri source_uri = new SafeUri(source);
            
            try {
                is_regular_file = IOProxy.File.Exists(source_uri);
                is_directory = !is_regular_file && IOProxy.Directory.Exists(source);
            } catch {
                scan_ref_count--;
                return;
            }

            try {
                if(!Path.GetFileName(source).StartsWith(".")) {
                    if(is_regular_file) {
                        Enqueue(source);
                    } else {
                        try {
                            foreach(string file in IOProxy.Directory.GetFiles(source)) {
                                ScanForFiles(file);
                            }

                            foreach(string directory in IOProxy.Directory.GetDirectories(source)) {
                                ScanForFiles(directory);
                            }
                        } catch {
                        }
                    }
                }
            } catch(System.ArgumentException) {
                // If there are illegal characters in path
            }
            
            scan_ref_count--;
        }
        
        private void ProcessQueue()
        {
            if(processing_queue) {
                return;
            }
            
            processing_queue = true;
            
            using(new Banshee.Base.Timer("Importing")) { 
            
            while(path_queue.Count > 0) {
                CheckForCanceled();
                
                string filename = path_queue.Dequeue() as string;
                    
                ImportEventHandler handler = ImportRequested;
                if(handler != null && filename != null) {
                    ImportEventArgs args = new ImportEventArgs();
                    args.FileName = filename;
                    handler(this, args);
                    UpdateCount(args.ReturnMessage);
                } else {
                    UpdateCount(null);
                }
            }
            
            }
            
            path_queue.Clear();
            processing_queue = false;
            
            if(scan_ref_count == 0) {
                DestroyUserEvent();
                if(ImportFinished != null) {
                    ImportFinished(this, new EventArgs());
                }
            }
        }

        private string title = Catalog.GetString("Importing Songs");
        public string Title {
            get { return title; }
            set { title = value; }
        }

        private string cancel_message = Catalog.GetString("The import process is currently running. Would you like to stop it?");
        public string CancelMessage {
            get { return cancel_message; }
            set { cancel_message = value; }
        }

        private string progress_message = Catalog.GetString("Importing {0} of {1}");
        public string ProgressMessage {
            get { return progress_message; }
            set { progress_message = value; }
        }
    }
}

