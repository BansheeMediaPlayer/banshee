//
// ConfirmShutdownDialog.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2006-2007 Novell, Inc.
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
using Gtk;
using Mono.Unix;

using Hyena.Jobs;

using Banshee.ServiceStack;

namespace Banshee.Gui.Dialogs
{
    public class ConfirmShutdownDialog : ErrorListDialog
    {
        private Scheduler scheduler;

        public ConfirmShutdownDialog() : base()
        {
            ListView.Model = new ListStore(typeof(string), typeof(Job));
            ListView.AppendColumn("Error", new CellRendererText(), "text", 0);
            ListView.HeadersVisible = false;

            Header = Catalog.GetString("Important tasks are running");
            Message = Catalog.GetString(
                "Closing Banshee now will cancel any currently running tasks. They cannot " +
                "be resumed automatically the next time Banshee is run.");
                
            IconNameStock = Stock.DialogQuestion;
            
            Dialog.DefaultResponse = ResponseType.Cancel;
            
            AddButton(Catalog.GetString("Quit anyway"), ResponseType.Ok, false);
            AddButton(Catalog.GetString("Continue running"), ResponseType.Cancel, true);

            scheduler = ServiceManager.JobScheduler;
            foreach (Job job in scheduler.Jobs) {
                AddJob (job);
            }
            
            scheduler.JobAdded += AddJob;
            scheduler.JobRemoved += RemoveJob;
        }
        
        public void AddString(string message)
        {
            (ListView.Model as ListStore).AppendValues(message, null);
        }

        private void AddJob(Job job)
        {
            if (job.Has (PriorityHints.DataLossIfStopped)) {
                Banshee.Base.ThreadAssist.ProxyToMain(delegate {
                    TreeIter iter = (ListView.Model as ListStore).Prepend();
                    (ListView.Model as ListStore).SetValue(iter, 0, job.Title);
                    (ListView.Model as ListStore).SetValue(iter, 1, job);
                });
            }
        }

        private void RemoveJob(Job job)
        {
            if(!scheduler.HasAnyDataLossJobs) {
                Dialog.Respond(Gtk.ResponseType.Ok);
                return;
            }

            if (ListView == null || ListView.Model == null) {
                return;
            }
            
            for(int i = 0, n = ListView.Model.IterNChildren(); i < n; i++) {
                TreeIter iter;
                if(!ListView.Model.IterNthChild(out iter, i)) {
                    break;
                }
                    
                if(ListView.Model.GetValue(iter, 1) == job) {
                    Banshee.Base.ThreadAssist.ProxyToMain(delegate {
                        (ListView.Model as ListStore).Remove(ref iter);
                    });
                    break;
                }
            }
        }
    }
}
