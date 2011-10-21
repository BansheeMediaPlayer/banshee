//
// TaskStatusIcon.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
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
using System.Text;
using System.Collections.Generic;

using Mono.Unix;

using Gtk;

using Hyena;
using Hyena.Jobs;
using Hyena.Widgets;

using Banshee.Base;
using Banshee.ServiceStack;

namespace Banshee.Gui.Widgets
{
    public class TaskStatusIcon : AnimatedImage
    {
        private List<Job> jobs = new List<Job> ();

        public bool ShowOnlyBackgroundTasks { get; set; }
        public bool IntermittentVisibility { get; set; }
        public uint IntermittentVisibleTime { get; set; }
        public uint IntermittentHiddenTime { get; set; }

        private uint turn_off_id;
        private uint turn_on_id;

        public TaskStatusIcon ()
        {
            ShowOnlyBackgroundTasks = true;
            IntermittentVisibility = false;
            IntermittentVisibleTime = 2500;
            IntermittentHiddenTime = 2 * IntermittentVisibleTime;

            // Setup widgetry
            try {
                Pixbuf = Gtk.IconTheme.Default.LoadIcon ("process-working", 22, IconLookupFlags.NoSvg);
                FrameHeight = 22;
                FrameWidth = 22;
                Load ();
                TaskActive = false;
            } catch (Exception e) {
                Hyena.Log.Exception (e);
            }

            // Listen for jobs
            JobScheduler job_manager = ServiceManager.Get<JobScheduler> ();
            job_manager.JobAdded += OnJobAdded;
            job_manager.JobRemoved += OnJobRemoved;

            Update ();
        }

        protected TaskStatusIcon (IntPtr raw) : base (raw)
        {
        }

        private void Update ()
        {
            lock (jobs) {
                if (jobs.Count > 0) {
                    var sb = new StringBuilder ();

                    sb.Append ("<b>");
                    sb.Append (GLib.Markup.EscapeText (Catalog.GetPluralString (
                        "Active Task Running", "Active Tasks Running", jobs.Count)));
                    sb.Append ("</b>");

                    foreach (Job job in jobs) {
                        sb.AppendLine ();
                        sb.AppendFormat ("<small> \u2022 {0}</small>",
                            GLib.Markup.EscapeText (job.Title));
                    }

                    TooltipMarkup = sb.ToString ();
                    TaskActive = true;
                } else {
                    TooltipText = null;
                    TaskActive = false;
                }
            }
        }

        private bool first = true;
        private bool task_active = false;
        private bool TaskActive {
            set {
                if (!first && task_active == value) {
                    return;
                }

                first = false;
                task_active = value;

                if (task_active) {
                    if (IntermittentVisibility) {
                        TurnOn ();
                    } else {
                        Active = true;
                        Sensitive = true;
                    }
                } else {
                    if (IntermittentVisibility) {
                        TurnOff ();
                    } else {
                        Active = false;
                        Sensitive = false;
                    }
                }
            }
        }

        private bool TurnOn ()
        {
            if (task_active) {
                Active = true;
                Sensitive = true;
                if (turn_off_id == 0) {
                    turn_off_id = Banshee.ServiceStack.Application.RunTimeout (IntermittentHiddenTime, TurnOff);
                }
            }

            turn_on_id = 0;
            return false;
        }

        private bool TurnOff ()
        {
            Active = false;
            Sensitive = task_active;

            if (task_active && turn_on_id == 0) {
                turn_on_id = Banshee.ServiceStack.Application.RunTimeout (IntermittentVisibleTime, TurnOn);
            }

            turn_off_id = 0;
            return false;
        }

        private void OnJobUpdated (object o, EventArgs args)
        {
            ThreadAssist.ProxyToMain (Update);
        }

        private void AddJob (Job job)
        {
            lock (jobs) {
                if (job == null || (ShowOnlyBackgroundTasks && !job.IsBackground) || job.IsFinished) {
                    return;
                }

                jobs.Add (job);
                job.Updated += OnJobUpdated;
            }

            ThreadAssist.ProxyToMain (Update);
        }

        private void OnJobAdded (Job job)
        {
            AddJob (job);
        }

        private void RemoveJob (Job job)
        {
            lock (jobs) {
                if (jobs.Contains (job)) {
                    job.Updated -= OnJobUpdated;
                    jobs.Remove (job);
                }
            }

            ThreadAssist.ProxyToMain (Update);
        }

        private void OnJobRemoved (Job job)
        {
            RemoveJob (job);
        }
    }
}
