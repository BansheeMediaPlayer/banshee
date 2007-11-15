/***************************************************************************
 *  PowerManagement.cs
 *
 *  Copyright (C) 2006 Novell, Inc.
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
using System.Collections;
using System.Collections.Generic;
using Mono.Unix;
using NDesk.DBus;
using org.freedesktop.DBus;

using Banshee.MediaEngine;
 
namespace Banshee.Base
{
    public delegate void DpmsModeChangedHandler(string mode);
    public delegate void OnAcChangedHandler(bool state);

    // http://cvs.gnome.org/viewcvs/*checkout*/gnome-power-manager/docs/dbus-interface.html
    [Interface("org.gnome.PowerManager")]
    public interface IPowerManager
    {
        bool Suspend();
        bool Hibernate();
        bool Shutdown();
        bool Reboot();
        bool AllowedSuspend();
        bool AllowedHibernate();
        bool AllowedShutdown();
        bool AllowedReboot();
        //string DpmsMode{get; set;}
        void SetDpmsMode(string mode);
        string GetDpmsMode();
        uint Inhibit(string application, string reason);
        void UnInhibit(uint cookie);
        //bool OnAc{get;}
        bool GetOnAc();
        //bool LowPowerMode{get;}
        bool GetLowPowerMode();
        event DpmsModeChangedHandler DpmsModeChanged;
        event OnAcChangedHandler OnAcChanged;
    }

    [Interface("org.freedesktop.PowerManagement.Inhibit")]
    public interface IPowerManagementInhibit
    {
        uint Inhibit(string application, string reason);
        void UnInhibit(uint cookie);
        bool HasInhibit();
    }

    public static class PowerManagement
    {
        private const string oldBusName = "org.gnome.PowerManager";
        private const string oldObjectPath = "/org/gnome/PowerManager";

        private const string BusName = "org.freedesktop.PowerManagement";
        private const string ObjectPath = "/org/freedesktop/PowerManagement/Inhibit";

        private static object FindInstance()
        {
            if (!Bus.Session.NameHasOwner(BusName)) {
                if (!Bus.Session.NameHasOwner(oldBusName)) {
                    throw new ApplicationException(String.Format("Name {0} or {1} has no owner", BusName, oldBusName));
                }

                old_gpm = true;

                IPowerManager old_inhibit = Bus.Session.GetObject<IPowerManager>(
                    oldBusName, new ObjectPath(oldObjectPath));
                
                try {
                    //GetOnAc() was called getOnAc() in gpm version <= 2.14.x
                    old_inhibit.GetOnAc();
                } catch(Exception e) {
                    throw new ApplicationException(String.Format(("Unsupported version of GNOME Power Manager: " + e.Message)));
                }

                return old_inhibit;
            }
            
            old_gpm = false;
            
            IPowerManagementInhibit inhibit = Bus.Session.GetObject<IPowerManagementInhibit>(
                BusName, new ObjectPath(ObjectPath));

            // Check if calling HasInhibit() works, this was not there before
            try {
                inhibit.HasInhibit ();
            } catch(Exception e) {
                throw new ApplicationException(String.Format(("Unsupported version of GNOME Power Manager: " + e.Message)));
            }
            
            return inhibit;
        }

        private static readonly string INHIBIT_PLAY_REASON = Catalog.GetString("Playing Music");
        
        private static Dictionary<string, uint> gpm_inhibit_cookie_map = null;
        private static object gpm = null;
        private static bool old_gpm = false;
        
        public static void Initialize()
        {
            try {
                gpm = FindInstance();
            } catch(Exception e) {
                LogError("Cannot find GNOME Power Manager: " + e.Message);
                gpm = null;
                return;
            }
            
            gpm_inhibit_cookie_map = new Dictionary<string, uint>();
            PlayerEngineCore.StateChanged += OnPlayerEngineCoreStateChanged;
        }
        
        public static void Dispose()
        {
            if(gpm != null) {
                UnInhibitAll();
                gpm_inhibit_cookie_map = null;
                gpm = null;
            }
        }
        
        private static void OnPlayerEngineCoreStateChanged(object o, PlayerEngineStateArgs args)
        {
            if(args.State == PlayerEngineState.Playing) {
                Inhibit(INHIBIT_PLAY_REASON);
            } else {
                UnInhibit(INHIBIT_PLAY_REASON);
            }
        }
        
        private static void LogError(string message)
        {
            LogCore.Instance.PushWarning("Power Management Call Failed", message, false);
        }
        
        public static void Inhibit(string reason)
        {
            if(gpm == null || gpm_inhibit_cookie_map.ContainsKey(reason)) {
                return;
            }
            
            try {
                uint cookie;
                if (old_gpm)
                    cookie = ((IPowerManager) gpm).Inhibit("Banshee", reason);
                else
                    cookie = ((IPowerManagementInhibit) gpm).Inhibit("Banshee", reason);
                gpm_inhibit_cookie_map[reason] = cookie;
            } catch(Exception e) {
                LogError("Inhibit: " + e.Message);
            }
        }
        
        public static void UnInhibit(string reason)
        {
            UnInhibit(reason, true);
        }

        private static void UnInhibit(string reason, bool removeCookie)
        {
            if(gpm == null || !gpm_inhibit_cookie_map.ContainsKey(reason)) {
                return;
            }
            
            try {
                uint cookie = gpm_inhibit_cookie_map[reason];
                if (old_gpm)
                    ((IPowerManager) gpm).UnInhibit(cookie);
                else
                    ((IPowerManagementInhibit) gpm).UnInhibit(cookie);                    
            } catch(Exception e) {
                LogError("UnInhibit: " + e.Message);
            }
            
            if (removeCookie) {
                gpm_inhibit_cookie_map.Remove(reason);
            }
        }

        public static void UnInhibitAll()
        {
            if(gpm == null || gpm_inhibit_cookie_map == null) {
                return;
            }

            foreach(string reason in gpm_inhibit_cookie_map.Keys) {
                UnInhibit(reason, false);
        }

            gpm_inhibit_cookie_map.Clear();
        }

    }
}
 
