//
// Booter.cs
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

//
// Crazy Banshee Boot Procedure
//
//    [Exec or DBus Activation] <---------------------------------------------------------.
//                |                                                                       |
//                v                                                                       |
//    [Bootloader (Banshee.exe)]                                                          |
//                |                                                                       |
//                v                   yes                                                 |
//    <org.bansheeproject.Banshee?> -------> [Load DBus Proxy Client (Halie.exe)] -----.  |
//                |no                                                                  |  |
//                v                             yes                                    |  |
//    <org.bansheeproject.CollectionIndexer?> -------> [Tell Indexer to Reboot] -----/IPC/'
//                |no                                                                  |
//                v                        yes                                         |
//    <command line contains --indexer?> -------> [Load Indexer Client (Beroe.exe)]    |
//                |no                                                                  |
//                v                          yes                                       |
//    <command line contains --client=XYZ> -------> [Load XYZ Client]                  |
//                |no                                                                  |
//                v                                                                    |
//    [Load Primary Interface Client (Nereid.exe)] <-----------------------------------'
//

using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Mono.Unix;

using DBus;

using Hyena;
using Hyena.CommandLine;

using Banshee.Base;
using Banshee.ServiceStack;
using Banshee.Collection.Indexer;

namespace Booter
{
    public static class Booter
    {
        public static void Main ()
        {
            Application.InitializePaths ();

            if (CheckHelpVersion ()) {
                return;
            }

            if (!DBusConnection.GrabDefaultName ()) {
                // DBus Command/Query/File Proxy Client
                BootClient ("Halie");
                NotifyStartupComplete ();
            } else if (DBusConnection.NameHasOwner ("CollectionIndexer")) {
                // Tell the existing indexer to start Banshee when it's done
                IIndexerClient indexer = DBusServiceManager.FindInstance<IIndexerClient> ("CollectionIndexer", "/IndexerClient");
                try {
                    indexer.Hello ();
                    indexer.RebootWhenFinished (Environment.GetCommandLineArgs ());
                    Log.Warning ("The Banshee indexer is currently running. Banshee will be started when the indexer finishes.");
                } catch (Exception e) {
                    Log.Error ("CollectionIndexer found on the Bus, but doesn't say Hello", e);
                }
            } else if (ApplicationContext.CommandLine.Contains ("indexer")) {
                // Indexer Client
                BootClient ("Beroe");
            } else if (ApplicationContext.CommandLine.Contains ("client")) {
                BootClient (Path.GetFileNameWithoutExtension (ApplicationContext.CommandLine["client"]));
            } else {
                BootClient ("Nereid");
            }
        }

        private static void BootClient (string clientName)
        {
            AppDomain.CurrentDomain.ExecuteAssembly (Path.Combine (Path.GetDirectoryName (
                Assembly.GetEntryAssembly ().Location), String.Format ("{0}.exe", clientName)));
        }

        private const string LibGdkLibrary = "libgdk-win32-2.0-0.dll";

        [DllImport (LibGdkLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool gdk_init_check (IntPtr argc, IntPtr argv);

        [DllImport (LibGdkLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void gdk_notify_startup_complete ();

        private static void NotifyStartupComplete ()
        {
            try {
                if (gdk_init_check (IntPtr.Zero, IntPtr.Zero)) {
                    gdk_notify_startup_complete ();
                }
            } catch (Exception e) {
                Log.Error ("Problem with NotifyStartupComplete", e);
            }
        }

        private static bool CheckHelpVersion ()
        {
            if (ApplicationContext.CommandLine.ContainsStart ("help")) {
                ShowHelp ();
                return true;
            } else if (ApplicationContext.CommandLine.Contains ("version")) {
                ShowVersion ();
                return true;
            }

            return false;
        }

        private static void ShowHelp ()
        {
            Console.WriteLine ("Usage: {0} [options...] [files|URIs...]", "banshee");
            Console.WriteLine ();

            string css_file = Path.Combine (Paths.ApplicationData, "gtk.css").Replace (
                            Environment.GetFolderPath (Environment.SpecialFolder.Personal), "~");

            Layout commands = new Layout (
                new LayoutGroup ("help", Catalog.GetString ("Help Options"),
                    new LayoutOption ("help", Catalog.GetString ("Show this help")),
                    new LayoutOption ("help-playback", Catalog.GetString ("Show options for controlling playback")),
                    new LayoutOption ("help-query-track", Catalog.GetString ("Show options for querying the playing track")),
                    new LayoutOption ("help-query-player", Catalog.GetString ("Show options for querying the playing engine")),
                    new LayoutOption ("help-ui", Catalog.GetString ("Show options for the user interface")),
                    new LayoutOption ("help-debug", Catalog.GetString ("Show options for developers and debugging")),
                    new LayoutOption ("help-all", Catalog.GetString ("Show all option groups")),
                    new LayoutOption ("version", Catalog.GetString ("Show version information"))
                ),

                new LayoutGroup ("playback", Catalog.GetString ("Playback Control Options"),
                    new LayoutOption ("next", Catalog.GetString ("Play the next track, optionally restarting if the 'restart' value is set")),
                    new LayoutOption ("previous", Catalog.GetString ("Play the previous track, optionally restarting if the 'restart' value is set")),
                    new LayoutOption ("restart-or-previous", Catalog.GetString ("If the current song has been played longer than 4 seconds then restart it, otherwise the same as --previous")),
                    new LayoutOption ("play-enqueued", Catalog.GetString ("Automatically start playing any tracks enqueued on the command line")),
                    new LayoutOption ("play", Catalog.GetString ("Start playback")),
                    new LayoutOption ("pause", Catalog.GetString ("Pause playback")),
                    new LayoutOption ("toggle-playing", Catalog.GetString ("Toggle playback")),
                    new LayoutOption ("stop", Catalog.GetString ("Completely stop playback")),
                    new LayoutOption ("stop-when-finished", Catalog.GetString (
                        "Enable or disable playback stopping after the currently playing track (value should be either 'true' or 'false')")),
                    new LayoutOption ("set-volume=LEVEL", Catalog.GetString ("Set the playback volume (0-100), prefix with +/- for relative values")),
                    new LayoutOption ("set-position=POS", Catalog.GetString ("Seek to a specific point (seconds, float)")),
                    new LayoutOption ("set-rating=RATING", Catalog.GetString ("Set the currently played track's rating (0 to 5)"))
                ),

                new LayoutGroup ("query-player", Catalog.GetString ("Player Engine Query Options"),
                    new LayoutOption ("query-current-state", Catalog.GetString ("Current player state")),
                    new LayoutOption ("query-last-state", Catalog.GetString ("Last player state")),
                    new LayoutOption ("query-can-pause", Catalog.GetString ("Query whether the player can be paused")),
                    new LayoutOption ("query-can-seek", Catalog.GetString ("Query whether the player can seek")),
                    new LayoutOption ("query-volume", Catalog.GetString ("Player volume")),
                    new LayoutOption ("query-position", Catalog.GetString ("Player position in currently playing track"))
                ),

                new LayoutGroup ("query-track", Catalog.GetString ("Playing Track Metadata Query Options"),
                    new LayoutOption ("query-uri", Catalog.GetString ("URI")),
                    new LayoutOption ("query-artist", Catalog.GetString ("Artist Name")),
                    new LayoutOption ("query-album", Catalog.GetString ("Album Title")),
                    new LayoutOption ("query-title", Catalog.GetString ("Track Title")),
                    new LayoutOption ("query-duration", Catalog.GetString ("Duration")),
                    new LayoutOption ("query-track-number", Catalog.GetString ("Track Number")),
                    new LayoutOption ("query-track-count", Catalog.GetString ("Track Count")),
                    new LayoutOption ("query-disc", Catalog.GetString ("Disc Number")),
                    new LayoutOption ("query-year", Catalog.GetString ("Year")),
                    new LayoutOption ("query-rating", Catalog.GetString ("Rating")),
                    new LayoutOption ("query-score", Catalog.GetString ("Score")),
                    new LayoutOption ("query-bit-rate", Catalog.GetString ("Bit Rate"))
                ),

                new LayoutGroup ("ui", Catalog.GetString ("User Interface Options"),
                    new LayoutOption ("show|--present", Catalog.GetString ("Present the user interface on the active workspace")),
                    new LayoutOption ("fullscreen", Catalog.GetString ("Enter the full-screen mode")),
                    new LayoutOption ("hide", Catalog.GetString ("Hide the user interface")),
                    new LayoutOption ("no-present", Catalog.GetString ("Do not present the user interface, regardless of any other options")),
                    new LayoutOption ("show-import-media", Catalog.GetString ("Present the import media dialog box")),
                    new LayoutOption ("show-about", Catalog.GetString ("Present the about dialog")),
                    new LayoutOption ("show-open-location", Catalog.GetString ("Present the open location dialog")),
                    new LayoutOption ("show-preferences", Catalog.GetString ("Present the preferences dialog"))
                ),

                new LayoutGroup ("debugging", Catalog.GetString ("Debugging and Development Options"),
                    new LayoutOption ("debug", Catalog.GetString ("Enable general debugging features")),
                    new LayoutOption ("debug-sql", Catalog.GetString ("Enable debugging output of SQL queries")),
                    new LayoutOption ("debug-addins", Catalog.GetString ("Enable debugging output of Mono.Addins")),
                    new LayoutOption ("db=FILE", Catalog.GetString ("Specify an alternate database to use")),
                    new LayoutOption ("fetch-artwork", Catalog.GetString ("Force fetching of missing cover artwork")),
                    new LayoutOption ("gconf-base-key=KEY", Catalog.GetString ("Specify an alternate key, default is /apps/banshee-1/")),
                    new LayoutOption ("uninstalled", Catalog.GetString ("Optimize instance for running uninstalled; " +
                        "most notably, this will create an alternate Mono.Addins database in the working directory")),
                    new LayoutOption ("disable-dbus", Catalog.GetString ("Disable DBus support completely")),
                    new LayoutOption ("no-gtkcss", String.Format (Catalog.GetString (
                        "Skip loading a custom gtk.css file ({0}) if it exists"), css_file)),
                    new LayoutOption ("debug-gtkcss", String.Format (Catalog.GetString (
                        "Reload the custom gtk.css file ({0}) every 5 seconds"), css_file))
                )
            );

            if (ApplicationContext.CommandLine.Contains ("help-all")) {
                Console.WriteLine (commands);
                return;
            }

            List<string> errors = null;

            foreach (KeyValuePair<string, string> argument in ApplicationContext.CommandLine.Arguments) {
                switch (argument.Key) {
                    case "help": Console.WriteLine (commands.ToString ("help")); break;
                    case "help-debug": Console.WriteLine (commands.ToString ("debugging")); break;
                    case "help-query-track": Console.WriteLine (commands.ToString ("query-track")); break;
                    case "help-query-player": Console.WriteLine (commands.ToString ("query-player")); break;
                    case "help-ui": Console.WriteLine (commands.ToString ("ui")); break;
                    case "help-playback": Console.WriteLine (commands.ToString ("playback")); break;
                    default:
                        if (argument.Key.StartsWith ("help")) {
                            (errors ?? (errors = new List<string> ())).Add (argument.Key);
                        }
                        break;
                }
            }

            if (errors != null) {
                Console.WriteLine (commands.LayoutLine (String.Format (Catalog.GetString (
                    "The following help arguments are invalid: {0}"),
                    Hyena.Collections.CollectionExtensions.Join (errors, "--", null, ", "))));
            }
        }

        private static void ShowVersion ()
        {
            Console.WriteLine ("Banshee {0} ({1}) http://banshee.fm", Application.DisplayVersion, Application.Version);
            Console.WriteLine ("Copyright 2005-{0} Novell, Inc. and Contributors.", DateTime.Now.Year);
        }
    }
}
