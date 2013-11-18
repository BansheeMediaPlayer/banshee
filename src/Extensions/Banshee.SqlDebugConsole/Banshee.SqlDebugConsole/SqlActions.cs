//
// SqlActions.cs
//
// Authors:
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
using System.Collections.Generic;
using Gtk;

using Mono.Unix;

using Hyena.Data.Sqlite;

using Banshee.Base;
using Banshee.Sources;
using Banshee.ServiceStack;
using Banshee.Gui;

using Browser = Banshee.Web.Browser;

namespace Banshee.SqlDebugConsole
{
    public class SqlActions : BansheeActionGroup, IExtensionService, IDisposable
    {
        private uint actions_id;

        public SqlActions () : base (ServiceManager.Get<InterfaceActionService> (), "SqlDebugActions")
        {
        }

        public void Initialize ()
        {
            Add (new ActionEntry [] {
                new ActionEntry (
                    "ShowConsoleAction", null,
                     Catalog.GetString ("Show SQL Console"),
                     null, String.Empty, OnShowConsole
                ),
                new ActionEntry (
                    "StartSqlMonitoringAction", "gtk-start",
                    Catalog.GetString ("Start SQL Monitoring"),
                    null, String.Empty, OnStartSqlMonitoring
                ),
                new ActionEntry (
                    "StopSqlMonitoringAction", "gtk-stop",
                    Catalog.GetString ("Stop SQL Monitoring"),
                    null, String.Empty, OnStopSqlMonitoring
                )
            });

            actions_id = Actions.UIManager.AddUiFromResource ("GlobalUI.xml");
            Actions.AddActionGroup (this);

            UpdateActions ();
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                Actions.UIManager.RemoveUi (actions_id);
                Actions.RemoveActionGroup (this);
            }
            base.Dispose (disposing);
        }

#region Action Handlers

        private void OnShowConsole (object sender, EventArgs args)
        {
            // The idea is this would open a non-modal dialog with a TreeView
            // showing the queries that were executed, their run time, etc.  And
            // there would be a text entry box where you could run queries, with
            // Run Once, Run [n] Times buttons.
            Console.WriteLine ("console start requested");
        }

        private void OnStartSqlMonitoring (object sender, EventArgs args)
        {
            LoggingSql = true;
        }

        private void OnStopSqlMonitoring (object sender, EventArgs args)
        {
            LoggingSql = false;
        }

#endregion

        private bool LoggingSql {
            get { return Hyena.Data.Sqlite.HyenaSqliteCommand.LogAll; }
            set {
                if (value == LoggingSql) {
                    return;
                }

                if (value) {
                    HyenaSqliteCommand.CommandExecuted += OnCommandExecuted;
                } else {
                    HyenaSqliteCommand.CommandExecuted -= OnCommandExecuted;
                }

                HyenaSqliteCommand.LogAll = value;
                UpdateActions ();
            }
        }

        private void OnCommandExecuted (object o, CommandExecutedArgs args)
        {
            Hyena.Log.DebugFormat ("in {0}ms executed {1}", args.Ms, args.Sql);
        }

        private void UpdateActions ()
        {
            this["StartSqlMonitoringAction"].Sensitive = !LoggingSql;
            this["StopSqlMonitoringAction"].Sensitive = LoggingSql;
        }

        string IService.ServiceName {
            get { return "SqlDebuggerActions"; }
        }
    }
}
