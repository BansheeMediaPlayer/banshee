
/***************************************************************************
 *  Main.cs
 *
 *  Copyright (C) 2005 Novell
 *  Written by Aaron Bockover (aaron@aaronbock.net)
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
using System.Diagnostics;
using Gnome;
using Mono.Unix;

using Banshee.Base;

namespace Banshee
{    
    public static class BansheeEntry
    {    
        public static void Main(string [] args)
        {
#if EXCEPTION_DIALOG        
            try {
                Startup(args);
            } catch(Exception e) {
                Console.Error.WriteLine(e);
                Gtk.Application.Init();
                ExceptionDialog dlg = new ExceptionDialog(e);
                dlg.Run();
                dlg.Destroy();
                System.Environment.Exit(1);
            }
#else
            Startup(args);
#endif
        }
        
        [System.Runtime.InteropServices.DllImport("libdbus-glib-1")]
        private static extern void dbus_g_thread_init();
        
        private static void Startup(string [] args)
        {
            try {
                Utilities.SetProcessName("banshee");
            } catch {}
            
            Gtk.Application.Init();
            dbus_g_thread_init();
        
            Globals.ArgumentQueue = new ArgumentQueue(new ArgumentLayout [] {
                new ArgumentLayout("enqueue <files>","Files to enqueue, must be last argument specified"),
                new ArgumentLayout("show",           "Show window"),
                new ArgumentLayout("hide",           "Hide window"),
                new ArgumentLayout("next",           "Play next song"),
                new ArgumentLayout("previous",       "Play previous song"),
                new ArgumentLayout("toggle-playing", "Toggle playing of current song"),
                new ArgumentLayout("play",           "Play current song"),
                new ArgumentLayout("pause",          "Pause current song"),
                new ArgumentLayout("query-artist",   "Get artist name for current playing song"),
                new ArgumentLayout("query-album",    "Get album name for current playing song"),
                new ArgumentLayout("query-title",    "Get track title for current playing song"),
                new ArgumentLayout("query-genre",    "Get track genre for current playing song"),
                new ArgumentLayout("query-duration", "Get duration of current playing song in seconds"),
                new ArgumentLayout("query-position", "Get position of current playing song in seconds"),
                new ArgumentLayout("query-uri",      "Get URI of current playing song"),
                new ArgumentLayout("query-cover-uri","Get URI of the album cover of current playing song"),
                new ArgumentLayout("query-status",   "Get player status (-1: Not loaded, 0: Paused, 1: Playing)"),
                new ArgumentLayout("hide-field",     "Do not display field name for --query-* results"),
                new ArgumentLayout("help",           "List available command line arguments"),
                new ArgumentLayout("audio-cd <dev>", "Start Banshee and/or select source mapped to <device>"),
                new ArgumentLayout("dap <dev>",      "Start Banshee and/or select source mapped to <device>"),
                new ArgumentLayout("version",        "Show Banshee Version")
            }, args, "enqueue");
            
            HandleShallowCommands();
            RemotePlayer dbus_core = DetectInstanceAndDbus();
            HandleDbusCommands(dbus_core);
            
            Globals.Initialize();
            new Program(StringUtil.UcFirst(ConfigureDefines.PACKAGE), ConfigureDefines.VERSION, Modules.UI, args);
            PlayerCore.UserInterface = new Banshee.PlayerUI();
            
            Gtk.Application.Run();
        }
    
        private static RemotePlayer DetectInstanceAndDbus()
        {
            try {
                return RemotePlayer.FindInstance();
            } catch {
                /*Process current_process = Process.GetCurrentProcess();
                foreach(Process process in Process.GetProcesses()) {
                    if(process.ProcessName == current_process.ProcessName && process.Id != current_process.Id) {
                        Console.WriteLine(Catalog.GetString(""));
                        Console.WriteLine(Catalog.GetString("Banshee is already running. If you were trying " + 
                            "to control the already running instance of Banshee, D-Bus must be enabled. " +
                            "Banshee could not connect to your D-Bus Session Bus."));
                        System.Environment.Exit(1);
                    }
                }*/

                return null;
            }
        }
        
        private static void HandleDbusCommands(RemotePlayer remote_player)
        {
            if(remote_player == null) {
                return;
            }
            
            bool present = true;
        
            foreach(string arg in Globals.ArgumentQueue.Arguments) {
                bool dequeue = true;
                switch(arg) {
                case "toggle-playing":
                    remote_player.TogglePlaying();
                    present = false;
                    break;
                case "play":
                    remote_player.Play();
                    present = false;
                    break;
                case "pause":
                    remote_player.Pause();
                    present = false;
                    break;
                case "show":
                    remote_player.ShowWindow();
                    present = false;
                    break;
                case "hide":
                    remote_player.HideWindow();
                    present = false;
                    break;
                case "next":
                    remote_player.Next();
                    present = false;
                    break;
                case "previous":
                    remote_player.Previous();
                    present = false;
                    break;
                case "query-artist":
                    PrintQueryResult("Artist", remote_player.GetPlayingArtist());
                    present = false;
                    break;
                case "query-album":
                    PrintQueryResult("Album", remote_player.GetPlayingAlbum());
                    present = false;
                    break;
                case "query-title":
                    PrintQueryResult("Title", remote_player.GetPlayingTitle());
                    present = false;
                    break;
                case "query-genre":
                    PrintQueryResult("Genre", remote_player.GetPlayingGenre());
                    present = false;
                    break;
                case "query-duration":
                    PrintQueryResult("Duration", remote_player.GetPlayingDuration());
                    present = false;
                    break;
                case "query-position":
                    PrintQueryResult("Position", remote_player.GetPlayingPosition());
                    present = false;
                    break;
                case "query-uri":
                    PrintQueryResult("Uri", remote_player.GetPlayingUri());
                    present = false;
                    break;
                case "query-cover-uri":
                    PrintQueryResult("CoverUri", remote_player.GetPlayingCoverUri());
                    present = false;
                    break;
                case "query-status":
                    PrintQueryResult("Status", remote_player.GetPlayingStatus());
                    present = false;
                    break;
                case "set-position":
                    string string_position = Globals.ArgumentQueue[arg];
                    int position = 0;
                    try {
                        position = Convert.ToInt32(string_position);
                        remote_player.SetPlayingPosition(position);
                    } catch(Exception) {
                        Console.WriteLine("Invalid position `{0}'. Integer value expected.", string_position);
                    }
                    present = false;
                    break;
                case "audio-cd":
                    remote_player.SelectAudioCd(Globals.ArgumentQueue[arg]);
                    dequeue = false;
                    Present(remote_player);
                    break;
                case "dap":
                    remote_player.SelectDap(Globals.ArgumentQueue[arg]);
                    dequeue = false;
                    Present(remote_player);
                    break;
                case "hide-field":
                    dequeue = false;
                    break;
                }
                
                if(dequeue) {
                    Globals.ArgumentQueue.Dequeue(arg);
                }
            }
            
            if(present) {
                try {
                    Present(remote_player);
                } catch(DBus.DBusException) {
                    return;
                }
            }
            
            // Major nasty hack to work around dbus-sharp bug: bad IL in object Finalizer
            System.GC.SuppressFinalize(remote_player);
            System.Environment.Exit(0);
        }
        
        private static void PrintQueryResult(string field, object value)
        {
            if(Globals.ArgumentQueue.Contains("hide-field")) {
                Console.WriteLine(value);
            } else {
                Console.WriteLine("{0}: {1}", field, value);
            }
        }
        
        private static void Present(RemotePlayer remote_player)
        {
            remote_player.PresentWindow();
        }
        
        private static void HandleShallowCommands()
        {
            if(Globals.ArgumentQueue.Contains("print-todo")) {
                BansheeTodo.PrintReport();
                Console.WriteLine("");
                System.Environment.Exit(0);
            }
            
            if(Globals.ArgumentQueue.Contains("version")) {
                Console.WriteLine("Banshee " + ConfigureDefines.VERSION);
                System.Environment.Exit(0);
            }
            
            if(!Globals.ArgumentQueue.Contains("help")) {
                return;
            }
            
            int max_name_len = 0;
            int max_var_len = 0;

            foreach(ArgumentLayout layout in Globals.ArgumentQueue.AvailableArguments) {
                if(layout.Name.Length > max_name_len) {
                    max_name_len = layout.Name.Length;
                }

                if(layout.ValueKind != null && layout.ValueKind.Length > max_var_len) {
                    max_var_len = layout.ValueKind.Length;
                }
            }

            max_var_len = max_var_len > 0 ? max_var_len + 3 : 0;
            max_name_len++;

            Console.WriteLine("Usage: banshee [ options ... ]\n       where options include:\n");

            foreach(ArgumentLayout layout in Globals.ArgumentQueue.AvailableArguments) {
                Console.WriteLine("  --{0,-" + max_name_len + "} {1,-" + max_var_len + "} {2}", 
                    layout.Name, layout.ValueKind == null 
                        ? String.Empty 
                        : "<" + layout.ValueKind + ">", layout.Description);
            }

            Console.WriteLine("");
            
            System.Environment.Exit(0);
        }
    }
}

