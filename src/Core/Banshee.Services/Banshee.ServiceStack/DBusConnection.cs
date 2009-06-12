//
// DBusConnection.cs
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
using System.Collections.Generic;

using NDesk.DBus;
using org.freedesktop.DBus;

using Hyena;
using Banshee.Base;

namespace Banshee.ServiceStack
{
    public static class DBusConnection
    {
        private const string BusPrefix = "org.bansheeproject";
        public const string DefaultServiceName = "Banshee";
        public const string DefaultBusName=  "org.bansheeproject.Banshee";
    
        static DBusConnection ()
        {
            enabled = !ApplicationContext.CommandLine.Contains ("disable-dbus");
        }
                    
        private static List<string> active_connections = new List<string> (); 
    
        public static string MakeBusName (string serviceName)
        {
            return String.Format ("{0}.{1}", BusPrefix, serviceName);
        }
    
        private static bool enabled;
        public static bool Enabled {
            get { return enabled; }
        }
        
        private static bool connect_tried;
        public static bool ConnectTried {
            get { return connect_tried; }
        }
        
        public static bool ApplicationInstanceAlreadyRunning {
            get {
                try {
                    return Bus.Session.NameHasOwner (DefaultBusName);
                } catch {
                    return false;
                }
            }
        }
        
        public static bool ServiceIsConnected (string service)
        {
            return active_connections.Contains (service);   
        }
        
        public static void Disconnect (string serviceName)
        {
            if (active_connections.Contains (serviceName)) {
                active_connections.Remove (serviceName);
            }
            
            Bus.Session.ReleaseName (MakeBusName (serviceName));
        }
        
        public static bool Connect ()
        {
            return Connect (DefaultServiceName);
        }
        
        public static bool Connect (string serviceName)
        {
            connect_tried = true;
            
            if (!enabled) {
                return false;
            }
            
            try {
                if (Connect (serviceName, true) == RequestNameReply.PrimaryOwner) {
                    active_connections.Add (serviceName);
                    return true;
                }
            } catch {
                Log.Warning ("DBus support could not be started. Disabling for this session.");
                enabled = false;
            }
            
            return false;
        }
        
        public static bool NameHasOwner (string serviceName)
        {
            try {
                return Bus.Session.NameHasOwner (MakeBusName (serviceName));
            } catch {
                return false;
            }
        }
        
        private static RequestNameReply Connect (string serviceName, bool init)
        {
            connect_tried = true;
            
            if (init) {
                BusG.Init ();
            }
            
            string bus_name = MakeBusName (serviceName);
            RequestNameReply name_reply = Bus.Session.RequestName (bus_name);
            Log.DebugFormat ("Bus.Session.RequestName ('{0}') replied with {1}", bus_name, name_reply);
            return name_reply;
        }
        
        private static GLib.MainLoop mainloop;
        
        public static void RunMainLoop ()
        {
            if (mainloop == null) {
                mainloop = new GLib.MainLoop ();
            }
            
            if (!mainloop.IsRunning) {
                mainloop.Run ();
            }
        }
        
        public static void QuitMainLoop ()
        {
            if (mainloop != null && mainloop.IsRunning) {
                mainloop.Quit ();
            }
        }
    }
}
