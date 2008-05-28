
//
// Application.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2007 Novell, Inc.
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
using System.Reflection;
using System.Collections.Generic;
using Mono.Unix;

using Banshee.Library;
using Banshee.Playlist;
using Banshee.SmartPlaylist;
using Banshee.Sources;

namespace Banshee.ServiceStack
{    
    public delegate bool ShutdownRequestHandler ();
    public delegate bool TimeoutHandler ();
    public delegate bool IdleHandler ();
    public delegate void InvokeHandler ();
    public delegate bool IdleTimeoutRemoveHandler (uint id);
    public delegate uint TimeoutImplementationHandler (uint milliseconds, TimeoutHandler handler); 
    public delegate uint IdleImplementationHandler (IdleHandler handler);
    public delegate bool IdleTimeoutRemoveImplementationHandler (uint id);
    
    public static class Application
    {   
        public static event ShutdownRequestHandler ShutdownRequested;
        public static event Action<Client> ClientAdded;

        private static event Action<Client> client_started;
        public static event Action<Client> ClientStarted {
            add {
                lock (running_clients) {
                    foreach (Client client in running_clients) {
                        if (client.IsStarted) {
                            OnClientStarted (client);
                        }
                    }
                }
                client_started += value;
            }
            remove { client_started -= value; }
        }

        private static Stack<Client> running_clients = new Stack<Client> ();
        private static bool shutting_down;

        public static void Run ()
        {
            Banshee.Base.PlatformHacks.TrapMonoJitSegv ();
            
            Catalog.Init (Application.InternalName, System.IO.Path.Combine (
                Banshee.Base.Paths.InstalledApplicationDataRoot, "locale"));

            ServiceManager.Run ();

            ServiceManager.SourceManager.AddSource (new MusicLibrarySource (), true);
            ServiceManager.SourceManager.AddSource (new VideoLibrarySource (), false);
            ServiceManager.SourceManager.LoadExtensionSources ();
            
            Banshee.Base.PlatformHacks.RestoreMonoJitSegv ();
        }

        public static bool ShuttingDown {
            get { return shutting_down; }
        }
     
        public static void Shutdown ()
        {
            shutting_down = true;
            if (Banshee.Kernel.Scheduler.IsScheduled (typeof (Banshee.Kernel.IInstanceCriticalJob)) ||
                Banshee.Kernel.Scheduler.CurrentJob is Banshee.Kernel.IInstanceCriticalJob) {
                if (shutdown_prompt_handler != null && !shutdown_prompt_handler ()) {
                    shutting_down = false;
                    return;
                }
            }
            
            if (OnShutdownRequested ()) {
                Dispose ();
            }
            shutting_down = false;
        }
        
        public static void PushClient (Client client)
        {
            lock (running_clients) {
                running_clients.Push (client);
                client.Started += OnClientStarted;
            }

            Action<Client> handler = ClientAdded;
            if (handler != null) {
                handler (client);
            }
        }
        
        public static Client PopClient ()
        {
            lock (running_clients) {
                return running_clients.Pop ();
            }
        }
        
        public static Client ActiveClient {
            get { lock (running_clients) { return running_clients.Peek (); } } 
        }

        private static void OnClientStarted (Client client)
        {
            client.Started -= OnClientStarted;
            Action<Client> handler = client_started;
            if (handler != null) {
                handler (client);
            }
        }
        
        private static bool OnShutdownRequested ()
        {
            ShutdownRequestHandler handler = ShutdownRequested;
            if (handler != null) {
                foreach (ShutdownRequestHandler del in handler.GetInvocationList ()) {
                    if (!del ()) {
                        return false;
                    }
                }
            }
            
            return true;
        }
        
        public static void Invoke (InvokeHandler handler)
        {
            RunIdle (delegate { handler (); return false; });
        }
        
        public static void Invoke (EventHandler handler)
        {
            RunIdle (delegate { handler (null, EventArgs.Empty); return false; });
        }
        
        public static uint RunIdle (IdleHandler handler)
        {
            if (idle_handler == null) {
                throw new NotImplementedException ("The application client must provide an IdleImplementationHandler");
            }
            
            return idle_handler (handler);
        }
        
        public static uint RunTimeout (uint milliseconds, TimeoutHandler handler)
        {
            if (timeout_handler == null) {
                throw new NotImplementedException ("The application client must provide a TimeoutImplementationHandler");
            }
            
            return timeout_handler (milliseconds, handler);
        }
        
        public static bool IdleTimeoutRemove (uint id)
        {
            if (idle_timeout_remove_handler == null) {
                throw new NotImplementedException ("The application client must provide a IdleTimeoutRemoveImplementationHandler");
            }
            
            return idle_timeout_remove_handler (id);
        }
        
        private static void Dispose ()
        {
            ServiceManager.Shutdown ();
            
            lock (running_clients) {
                while (running_clients.Count > 0) {
                    running_clients.Pop ().Dispose ();
                }
            }
        }
        
        private static ShutdownRequestHandler shutdown_prompt_handler = null;
        public static ShutdownRequestHandler ShutdownPromptHandler {
            get { return shutdown_prompt_handler; }
            set { shutdown_prompt_handler = value; }
        }
        
        private static TimeoutImplementationHandler timeout_handler = null;
        public static TimeoutImplementationHandler TimeoutHandler {
            get { return timeout_handler; }
            set { timeout_handler = value; }
        }
        
        private static IdleImplementationHandler idle_handler = null;
        public static IdleImplementationHandler IdleHandler {
            get { return idle_handler; }
            set { idle_handler = value; }
        }
        
        private static IdleTimeoutRemoveImplementationHandler idle_timeout_remove_handler = null;
        public static IdleTimeoutRemoveImplementationHandler IdleTimeoutRemoveHandler {
            get { return idle_timeout_remove_handler; }
            set { idle_timeout_remove_handler = value; }
        }
        
        public static string InternalName {
            get { return "banshee-1"; }
        }
        
        public static string IconName {
            get { return "media-player-banshee"; }
        }
        
        private static string version;
        public static string Version {
            get { 
                if (version != null) {
                    return version;
                }
                
                try {
                    AssemblyName name = Assembly.GetEntryAssembly ().GetName ();
                    version = String.Format ("{0}.{1}.{2}", name.Version.Major, 
                        name.Version.Minor, name.Version.Build);
                } catch {
                    version = Catalog.GetString ("Unknown");
                }
                
                return version;
            }
        }
        
        private static string display_version;
        public static string DisplayVersion {
            get { 
                if (display_version != null) {
                    return display_version;
                }
                
                foreach (Attribute attribute in Assembly.GetEntryAssembly ().GetCustomAttributes (false)) {
                    Type type = attribute.GetType ();
                    PropertyInfo property = type.GetProperty ("Version");
                    if (type.Name == "AssemblyDisplayVersionAttribute" && property != null && 
                        property.PropertyType == typeof (string)) {
                        display_version = (string)property.GetValue (attribute, null); 
                    }
                }
                
                return display_version;
            }
        }
    }
}
