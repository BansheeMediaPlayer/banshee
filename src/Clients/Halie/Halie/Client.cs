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
using System.Collections.Generic;

using DBus;

using Hyena;
using Banshee.Base;
using Banshee.ServiceStack;
using Banshee.MediaEngine;
using Banshee.PlaybackController;
using Banshee.Collection.Indexer;

namespace Halie
{
    public static class Client
    {
        // NOTE: Interfaces are copied from Banshee.ThickClient/Banshee.Gui
        // since we don't want to link against any GUI assemblies for this
        // client. It's a simple interface
        [Interface ("org.bansheeproject.Banshee.ClientWindow")]
        public interface IClientWindow
        {
            void Present ();
            void Hide ();
            void Fullscreen ();
        }

        [Interface ("org.bansheeproject.Banshee.GlobalUIActions")]
        public interface IGlobalUIActions
        {
            void ShowImportDialog ();
            void ShowAboutDialog ();
            void ShowOpenLocationDialog ();
            void ShowPreferencesDialog ();
        }

        private static bool hide_field;
        private static DBusCommandService command;

        public static void Main ()
        {
            DBusConnection.Init ();

            if (!DBusConnection.Enabled) {
                Error ("All commands ignored, DBus support is disabled");
                return;
            } else if (!DBusConnection.ApplicationInstanceAlreadyRunning) {
                Error ("Banshee does not seem to be running");
                return;
            }

            command = DBusServiceManager.FindInstance<DBusCommandService> ("/DBusCommandService");
            hide_field = ApplicationContext.CommandLine.Contains ("hide-field");

            bool present =
                HandlePlayerCommands () &&
                HandleGlobalUIActions () &&
                !ApplicationContext.CommandLine.Contains ("indexer");
            HandleWindowCommands (present);
            HandleFiles ();
        }

        private static void HandleWindowCommands (bool present)
        {
            IClientWindow window = DBusServiceManager.FindInstance<IClientWindow> ("/ClientWindow");
            if (window == null) {
                return;
            }

            foreach (KeyValuePair<string, string> arg in ApplicationContext.CommandLine.Arguments) {
                switch (arg.Key) {
                    case "show":
                    case "present": present = true; break;
                    case "fullscreen": window.Fullscreen (); break;
                    case "hide":
                        present = false;
                        window.Hide ();
                        break;
                }
            }

            if (present && !ApplicationContext.CommandLine.Contains ("no-present")) {
                window.Present ();
            }
        }

        private static void HandleFiles ()
        {
            foreach (string file in ApplicationContext.CommandLine.Files) {
                // If it looks like a URI with a protocol, leave it as is
                if (System.Text.RegularExpressions.Regex.IsMatch (file, "^\\w+\\:\\/")) {
                    command.PushFile (file);
                } else {
                    command.PushFile (Path.GetFullPath (file));
                }
            }
        }

        private static bool HandleGlobalUIActions ()
        {
            var global_ui_actions = DBusServiceManager.FindInstance<IGlobalUIActions> ("/GlobalUIActions");
            var handled = false;

            if (ApplicationContext.CommandLine.Contains ("show-import-media")) {
                global_ui_actions.ShowImportDialog ();
                handled |= true;
            }

            if (ApplicationContext.CommandLine.Contains ("show-about")) {
                global_ui_actions.ShowAboutDialog ();
                handled |= true;
            }

            if (ApplicationContext.CommandLine.Contains ("show-preferences")) {
                global_ui_actions.ShowPreferencesDialog ();
                handled |= true;
            }

            if (ApplicationContext.CommandLine.Contains ("show-open-location")) {
                global_ui_actions.ShowOpenLocationDialog ();
                handled |= true;
            }

            return !handled;
        }

        private static bool HandlePlayerCommands ()
        {
            IPlayerEngineService player = DBusServiceManager.FindInstance<IPlayerEngineService> ("/PlayerEngine");
            IPlaybackControllerService controller = DBusServiceManager.FindInstance<IPlaybackControllerService> ("/PlaybackController");
            IDictionary<string, object> track = null;
            int handled_count = 0;

            foreach (KeyValuePair<string, string> arg in ApplicationContext.CommandLine.Arguments) {
                handled_count++;
                switch (arg.Key) {
                    // For the player engine
                    case "play":           player.Play ();          break;
                    case "pause":          player.Pause ();         break;
                    case "stop":           player.Close ();         break;
                    case "toggle-playing": player.TogglePlaying (); break;

                    // For the playback controller
                    case "first":    controller.First ();                                    break;
                    case "next":     controller.Next (ParseBool (arg.Value, "restart"));     break;
                    case "previous": controller.Previous (ParseBool (arg.Value, "restart")); break;
                    case "restart-or-previous":
                        controller.RestartOrPrevious (ParseBool (arg.Value, "restart"));
                        break;
                    case "stop-when-finished":
                        controller.StopWhenFinished = !ParseBool (arg.Value);
                        break;
                    case "set-position":
                        player.Position = (uint)Math.Round (Double.Parse (arg.Value) * 1000);
                        break;
                    case "set-volume":
                        if (arg.Value.Length > 1) {
                            if (arg.Value[0] == '+') {
                                player.Volume += UInt16.Parse (arg.Value.Substring (1));
                                break;
                            }
                            if (arg.Value[0] == '-') {
                                var dec = UInt16.Parse (arg.Value.Substring (1));
                                player.Volume = (ushort)(player.Volume > dec ? player.Volume - dec : 0);
                                break;
                            }
                        }
                        player.Volume = UInt16.Parse (arg.Value);
                        break;
                    case "set-rating":
                        player.Rating = Byte.Parse (arg.Value);
                        break;
                    default:
                        if (arg.Key.StartsWith ("query-")) {
                            if (track == null) {
                                try {
                                    track = player.CurrentTrack;
                                } catch {
                                }
                            }
                            HandleQuery (player, track, arg.Key.Substring (6));
                        } else {
                            command.PushArgument (arg.Key, arg.Value ?? String.Empty);
                            handled_count--;
                        }
                        break;
                }
            }

            return handled_count <= 0;
        }

        private static void HandleQuery (IPlayerEngineService player, IDictionary<string, object> track, string query)
        {
            // Translate legacy query arguments into new ones
            switch (query) {
                case "title":    query = "name";   break;
                case "duration": query = "length"; break;
                case "uri":      query = "URI";    break;
            }

            switch (query) {
                case "all":
                    if (track != null) {
                        foreach (KeyValuePair<string, object> field in track) {
                            DisplayTrackField (field.Key, field.Value);
                        }
                    }

                    HandleQuery (player, track, "position");
                    HandleQuery (player, track, "volume");
                    HandleQuery (player, track, "current-state");
                    HandleQuery (player, track, "last-state");
                    HandleQuery (player, track, "can-pause");
                    HandleQuery (player, track, "can-seek");
                    break;
                case "position":
                    DisplayTrackField ("position", TimeSpan.FromMilliseconds (player.Position).TotalSeconds);
                    break;
                case "volume":
                    DisplayTrackField ("volume", player.Volume);
                    break;
                case "current-state":
                    DisplayTrackField ("current-state", player.CurrentState);
                    break;
                case "last-state":
                    DisplayTrackField ("last-state", player.LastState);
                    break;
                case "can-pause":
                    DisplayTrackField ("can-pause", player.CanPause);
                    break;
                case "can-seek":
                    DisplayTrackField ("can-seek", player.CanSeek);
                    break;
                case "URI":
                case "artist":
                case "album":
                case "name":
                case "length":
                case "track-number":
                case "track-count":
                case "disc":
                case "year":
                case "rating":
                case "score":
                case "bit-rate":
                    if (track == null) {
                        Error ("not playing");
                        break;
                    }
                    DisplayTrackField (query, track.ContainsKey (query) ? track[query] : "");
                    break;
                default:
                    Error ("'{0}' field unknown", query);
                    break;
            }
        }

        private static void DisplayTrackField (string field, object value)
        {
            if (field == String.Empty) {
                return;
            } else if (field == "name") {
                field = "title";
            } else if (field == "length") {
                field = "duration";
            }

            string result = null;
            if (value is bool) {
                result = (bool)value ? "true" : "false";
            } else {
                result = value.ToString ();
            }

            if (hide_field) {
                Console.WriteLine (result);
            } else {
                Console.WriteLine ("{0}: {1}", field.ToLower (), result);
            }
        }

        private static bool ParseBool (string value)
        {
            return ParseBool (value, "true", "yes");
        }

        private static bool ParseBool (string value, params string [] trueValues)
        {
            if (String.IsNullOrEmpty (value)) {
                return false;
            }

            value = value.ToLower ();

            foreach (string trueValue in trueValues) {
                if (value == trueValue) {
                    return true;
                }
            }

            return false;
        }

        private static void Error (string error, params object [] args)
        {
            Console.WriteLine ("Error: {0}", String.Format (error, args));
        }
    }
}

