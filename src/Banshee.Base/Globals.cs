/***************************************************************************
 *  Globals.cs
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
using GConf;

namespace Banshee.Base
{
    public static class Globals
    {
        private static GConf.Client gconf_client;
        private static NetworkDetect network_detect;
        private static ActionManager action_manager;
        private static Library library;
        private static ArgumentQueue argument_queue;
        private static AudioCdCore audio_cd_core;
        private static Random random;
        private static DBusRemote dbus_remote;
        private static Banshee.Gui.UIManager ui_manager;
        
        public static void Initialize()
        {
            if(!Directory.Exists(Paths.ApplicationData)) {
                Directory.CreateDirectory(Paths.ApplicationData);
            }
            
            Mono.Unix.Catalog.Init(ConfigureDefines.GETTEXT_PACKAGE, ConfigureDefines.LOCALE_DIR);
        
            ui_manager = new Banshee.Gui.UIManager();
            
            if(!Branding.Initialize()) {
                System.Environment.Exit(1);
                return;
            }
        
            gconf_client = new GConf.Client();
            network_detect = NetworkDetect.Instance;
            action_manager = new ActionManager();
            library = new Library();
            random = new Random();
            
            Banshee.Gstreamer.Utilities.Initialize();
            PlayerEngineCore.Initialize();
            
            try {
                audio_cd_core = new AudioCdCore();
            } catch(ApplicationException e) {
                LogCore.Instance.PushWarning("Audio CD support will be disabled for this instance", e.Message, false);
            }
            
            try {
                Banshee.Dap.DapCore.Initialize();
            } catch(ApplicationException e) {
                LogCore.Instance.PushWarning("DAP support will be disabled for this instance", e.Message, false);
            }
            
            Banshee.Plugins.PluginCore.Initialize();
        }
        
        public static void Dispose()
        {
            Banshee.Plugins.PluginCore.Dispose();
            network_detect.Dispose();
            library.Db.Dispose();
            Banshee.Dap.DapCore.Dispose();
            HalCore.Dispose();
        }
        
        public static GConf.Client Configuration {
            get {
                return gconf_client;
            }
        }

        public static NetworkDetect Network {
            get {
                return network_detect;
            }
        }
        
        public static ActionManager ActionManager {
            get {
                return action_manager;
            }
        }
        
        public static Library Library {
            get {
                return library;
            }
        }
        
        public static ArgumentQueue ArgumentQueue {
            set {
                argument_queue = value;
            }
            
            get {
                return argument_queue;
            }
        }
        
        public static AudioCdCore AudioCdCore {
            get {
                return audio_cd_core;
            }
        }
        
        public static Random Random {
            get {
                return random;
            }
        }
        
        public static DBusRemote DBusRemote {
            get {
                return dbus_remote;
            }
            
            set {
                dbus_remote = value;
            }
        }
        
        public static Banshee.Gui.UIManager UIManager {
            get { return ui_manager; }
        }
    }
    
    public static class InterfaceElements
    {
        private static Gtk.Window main_window;
        public static Gtk.Window MainWindow {
            get { return main_window; }
            set {
                if(main_window == null) {
                    main_window = value;
                }
            }
        }

        private static Gtk.Container playlist_container;
        public static Gtk.Container PlaylistContainer {
            get { return playlist_container; }
            set {
                if(playlist_container == null) {
                    playlist_container = value;
                }
            }
        }

        private static Gtk.Box main_container;
        public static Gtk.Box MainContainer {
            get { return main_container; }
            set {
                if(main_container == null) {
                    main_container = value;
                }
            }
        }
	
        private static Gtk.TreeView playlist_view;
        public static Gtk.TreeView PlaylistView {
            get { return playlist_view; }
            set {
                if(playlist_view == null) {
                    playlist_view = value;
                }
            }
        }
        
        private static Banshee.Widgets.SearchEntry search_entry;
        public static Banshee.Widgets.SearchEntry SearchEntry {
            get { return search_entry; }
            set {
                if(search_entry == null) {
                    search_entry = value;
                }
            }
        }
        
        public static void DetachPlaylistContainer()
        {
            if(PlaylistContainer.Parent != null) {
                (PlaylistContainer.Parent as Gtk.Container).Remove(PlaylistContainer);
            }
        }
    }
}
