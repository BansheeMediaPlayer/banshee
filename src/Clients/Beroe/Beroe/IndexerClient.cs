//
// Client.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
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
using System.IO;
using System.Reflection;
using System.Diagnostics;

using DBus;
using Hyena;

using Banshee.Base;
using Banshee.Database;
using Banshee.Preferences;
using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Collection.Indexer;

namespace Beroe
{
    public class IndexerClient : Client, IIndexerClient, IDBusExportable
    {
        public static void Main ()
        {
            if (!DBusConnection.Enabled) {
                Log.Error ("All commands ignored, DBus support is disabled");
                return;
            } else if (ApplicationContext.CommandLine.Contains ("client")) {
                ActAsRemoteClient ();
                return;
            } else if (DBusConnection.ApplicationInstanceAlreadyRunning) {
                Log.Error ("Banshee is already running");
                return;
            } else if (DBusConnection.NameHasOwner ("CollectionIndexer")) {
                Log.Error ("Another indexer is already running");
                return;
            }

            Log.Information ("Indexer client started");

            Startup ();
        }

        private static void ActAsRemoteClient ()
        {
        }

        private static void Startup ()
        {
            Application.InitializePaths ();

            ThreadAssist.InitializeMainThread ();

            ServiceManager.Initialize ();
            ServiceManager.RegisterService<DBusServiceManager> ();
            ServiceManager.RegisterService<BansheeDbConnection> ();
            ServiceManager.RegisterService<SourceManager> ();
            ServiceManager.RegisterService<PreferenceService> ();
            ServiceManager.RegisterService<CollectionIndexerService> ();
            ServiceManager.RegisterService<IndexerClient> ();
            ServiceManager.Run ();

            ServiceManager.Get<IndexerClient> ().Run ();
        }

        private string [] reboot_args;

        public void Run ()
        {
            ServiceManager.Get<CollectionIndexerService> ().ShutdownHandler = DBusConnection.QuitMainLoop;

            ServiceManager.SourceManager.AddSource (new Banshee.Library.MusicLibrarySource ());
            ServiceManager.SourceManager.AddSource (new Banshee.Library.VideoLibrarySource ());

            DBusConnection.RunMainLoop ();

            ServiceManager.Shutdown ();

            if (reboot_args != null) {
                Log.Debug ("Rebooting");

                System.Text.StringBuilder builder = new System.Text.StringBuilder ();
                foreach (string arg in reboot_args) {
                    builder.AppendFormat ("\"{0}\" ", arg);
                }

                // FIXME: Using Process.Start sucks, but DBus doesn't let you specify
                // extra command line arguments
                DBusConnection.Disconnect ("CollectionIndexer");
                Process.Start ("banshee", builder.ToString ());
                // Bus.Session.StartServiceByName (DBusConnection.DefaultBusName);
                // Bus.Session.Iterate ();
            }
        }

        public void Hello ()
        {
            Log.Debug ("Received a Hello over DBus");
        }

        public void RebootWhenFinished (string [] args)
        {
            lock (this) {
                Log.Debug ("Banshee will be started when the indexer finishes. Notifying indexer that it should hurry!");
                reboot_args = args;
                ServiceManager.Get<CollectionIndexerService> ().RequestCleanupAndShutdown ();
                GLib.Timeout.Add (2500, delegate {
                    ServiceManager.Get<CollectionIndexerService> ().ForceShutdown ();
                    return false;
                });
            }
        }

        IDBusExportable IDBusExportable.Parent {
            get { return null; }
        }

        string IService.ServiceName {
            get { return "IndexerClient"; }
        }

        public override string ClientId {
            get { return "BeroeIndexerClient"; }
        }
    }
}

