//
// ImportManager.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2006-2008 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Globalization;
using Mono.Unix;

using Hyena;
using Hyena.Jobs;
using Hyena.Collections;

using Banshee.IO;
using Banshee.Base;
using Banshee.ServiceStack;

namespace Banshee.Collection
{
    public class ImportManager : QueuePipeline<string>
    {

#region Importing Pipeline Element

        private class ImportElement : QueuePipelineElement<string>
        {
            private ImportManager manager;

            public ImportElement (ImportManager manager)
            {
                this.manager = manager;
            }

            public override void Enqueue (string item)
            {
                base.Enqueue (item);
                manager.UpdateScannerProgress ();
            }

            protected override string ProcessItem (string item)
            {
                try {
                    if (manager.Debug) Log.InformationFormat ("ImportElement processing {0}", item);
                    manager.OnImportRequested (item);
                } catch (Exception e) {
                    Hyena.Log.Error (e);
                }
                return null;
            }
        }

#endregion

        public bool Debug {
            get { return scanner_element.Debug; }
            set { scanner_element.Debug = value; }
        }

        private static NumberFormatInfo number_format = new NumberFormatInfo ();

        private DirectoryScannerPipelineElement scanner_element;
        private ImportElement import_element;

        private readonly object user_job_mutex = new object ();
        private UserJob user_job;
        private uint timer_id;

        public event ImportEventHandler ImportRequested;

        public ImportManager ()
        {
            AddElement (scanner_element = new DirectoryScannerPipelineElement ());
            AddElement (import_element = new ImportElement (this));
        }

#region Public API

        public override void Enqueue (string path)
        {
            CreateUserJob ();
            base.Enqueue (path);
        }

        public void Enqueue (string [] paths)
        {
            CreateUserJob ();
            foreach (string path in paths) {
                base.Enqueue (path);
            }
        }

        public bool IsImportInProgress {
            get { return import_element.Processing; }
        }

        private bool keep_user_job_hidden = false;
        public bool KeepUserJobHidden {
            get { return keep_user_job_hidden; }
            set { keep_user_job_hidden = value; }
        }

#endregion

#region User Job / Interaction

        private void CreateUserJob ()
        {
            lock (user_job_mutex) {
                if (user_job != null || KeepUserJobHidden) {
                    return;
                }

                timer_id = Log.DebugTimerStart ();

                user_job = new UserJob (Title, Catalog.GetString ("Scanning for media"));
                user_job.SetResources (Resource.Cpu, Resource.Disk, Resource.Database);
                user_job.PriorityHints = PriorityHints.SpeedSensitive | PriorityHints.DataLossIfStopped;
                user_job.IconNames = new string [] { "system-search", "gtk-find" };
                user_job.CancelMessage = CancelMessage;
                user_job.CanCancel = true;
                user_job.CancelRequested += OnCancelRequested;

                if (!KeepUserJobHidden) {
                    user_job.Register ();
                }
            }
        }

        private void DestroyUserJob ()
        {
            lock (user_job_mutex) {
                if (user_job == null) {
                    return;
                }

                if (!KeepUserJobHidden) {
                    Log.DebugTimerPrint (timer_id, Title + " duration: {0}");
                }

                user_job.CancelRequested -= OnCancelRequested;
                user_job.Finish ();
                user_job = null;
            }
        }

        private void OnCancelRequested (object o, EventArgs args)
        {
            scanner_element.Cancel ();
        }

        protected void UpdateProgress (string message)
        {
            CreateUserJob ();
            if (user_job != null) {
                double new_progress = (double)import_element.ProcessedCount / (double)import_element.TotalCount;
                double old_progress = user_job.Progress;

                if (new_progress >= 0.0 && new_progress <= 1.0 && Math.Abs (new_progress - old_progress) > 0.001) {
                    lock (number_format) {
                        string disp_progress = String.Format (ProgressMessage,
                            import_element.ProcessedCount,
                            import_element.TotalCount);

                        user_job.Title = disp_progress;
                        user_job.Status = String.IsNullOrEmpty (message) ? Catalog.GetString ("Scanning...") : message;
                        user_job.Progress = new_progress;
                    }
                }
            }
        }

        private DateTime last_enqueue_display = DateTime.Now;

        private void UpdateScannerProgress ()
        {
            CreateUserJob ();
            if (user_job != null) {
                if (DateTime.Now - last_enqueue_display > TimeSpan.FromMilliseconds (400)) {
                    lock (number_format) {
                        number_format.NumberDecimalDigits = 0;
                        user_job.Status = String.Format (Catalog.GetString ("Scanning ({0} files)..."),
                            import_element.TotalCount.ToString ("N", number_format));
                        last_enqueue_display = DateTime.Now;
                    }
                }
            }
        }

#endregion

#region Protected Import Hooks

        protected virtual void OnImportRequested (string path)
        {
            ImportEventHandler handler = ImportRequested;
            if (handler != null && path != null) {
                ImportEventArgs args = new ImportEventArgs (path);
                handler (this, args);
                UpdateProgress (args.ReturnMessage);
            } else {
                UpdateProgress (null);
            }
        }

        protected override void OnFinished ()
        {
            DestroyUserJob ();
            base.OnFinished ();
        }

#endregion

#region Properties

        private string title = Catalog.GetString ("Importing Media");
        public string Title {
            get { return title; }
            set { title = value; }
        }

        private string cancel_message = Catalog.GetString (
            "The import process is currently running. Would you like to stop it?");
        public string CancelMessage {
            get { return cancel_message; }
            set { cancel_message = value; }
        }

        private string progress_message = Catalog.GetString ("Importing {0} of {1}");
        public string ProgressMessage {
            get { return progress_message; }
            set { progress_message = value; }
        }

        public bool Threaded {
            set { import_element.Threaded = scanner_element.Threaded = value; }
        }

        public bool SkipHiddenChildren {
            set { scanner_element.SkipHiddenChildren = value; }
        }

        protected int TotalCount {
            get { return import_element == null ? 0 : import_element.TotalCount; }
        }

#endregion

    }
}
