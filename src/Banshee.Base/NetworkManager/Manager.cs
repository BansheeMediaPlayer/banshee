
/***************************************************************************
 *  Manager.cs
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
using System.Reflection;
using System.Collections;
using System.Threading;
using DBus;

namespace NetworkManager
{
    public enum State {
        Unknown = 0,
        Asleep,
        Connecting,
        Connected,
        Disconnected
    }
    
    [Interface("org.freedesktop.NetworkManager")]
    internal abstract class ManagerProxy 
    {
        /* Unsupported methods: 
            
            getDialup
            activateDialup
            setActiveDevice
            createWirelessNetwork
            createTestDevice
            removeTestDevice
        */
    
        [Method] public abstract DeviceProxy [] getDevices();
        [Method] public abstract uint state();
        [Method] public abstract void sleep();
        [Method] public abstract void wake();
        [Method] public abstract bool getWirelessEnabled();
        [Method] public abstract void setWirelessEnabled(bool enabled); 
        [Method] public abstract DeviceProxy getActiveDevice();
    }

    public class Manager : IEnumerable, IDisposable
    {
        private static readonly string PATH_NAME = "/org/freedesktop/NetworkManager";
        private static readonly string INTERFACE_NAME = "org.freedesktop.NetworkManager";
        private static readonly string DBUS_LOCAL_PATH_NAME = "/org/freedesktop/DBus/Local";
        private static readonly string DBUS_LOCAL_INTERFACE_NAME = "org.freedesktop.DBus.Local";

        private Service dbus_service;
        private Connection dbus_connection;
        private ManagerProxy manager;
        
#pragma warning disable 0067
        public event EventHandler DeviceNoLongerActive;
        public event EventHandler DeviceNowActive;
        public event EventHandler DeviceActivating;
        public event EventHandler DevicesChanged;
        public event EventHandler DeviceActivationStage;
        public event EventHandler DeviceIP4AddressChange;
        public event EventHandler StateChange;
        public event EventHandler WirelessNetworkDisappeared;
        public event EventHandler WirelessNetworkAppeared;
        public event EventHandler WirelessNetworkStrengthChanged;
#pragma warning restore 0067
        
        public Manager()
        {
            Connect();
        }
        
        public void Dispose()
        {
            // Major nasty hack to work around dbus-sharp bug: bad IL in object Finalizer
            System.GC.SuppressFinalize(manager);
        }
        
        private void Connect()
        {
            dbus_connection = Bus.GetSystemBus();
#if !MISSING_DBUS_SHARP_EOD
            dbus_connection.ExitOnDisconnect = false;
#endif
            dbus_service = Service.Get(dbus_connection, INTERFACE_NAME);
            manager = (ManagerProxy)dbus_service.GetObject(typeof(ManagerProxy), PATH_NAME);
                
            dbus_service.SignalCalled += OnSignalCalled;
        }
       
        private void Reconnect()
        {
            while(true) {
                Thread.Sleep(3000);
                
                try {
                    Connect();
                    return;
                } catch(DBusException) {
                } catch(ApplicationException) {
                }
            }
        }
       
        private void OnSignalCalled(Signal signal)
        {
            if(signal.PathName == DBUS_LOCAL_PATH_NAME 
               && signal.InterfaceName == DBUS_LOCAL_INTERFACE_NAME) {
                switch(signal.Name) {
                    case "Disconnected":
                        dbus_connection.Dispose();
                        dbus_service.SignalCalled -= OnSignalCalled;
                        Dispose();
                        
                        Thread reconnect_thread = new Thread(Reconnect);
                        reconnect_thread.IsBackground = true;
                        reconnect_thread.Start();
                        break;
                }
            }                
        
            if(signal.PathName != PATH_NAME || signal.InterfaceName != INTERFACE_NAME) {
                return;
            }
            
            InvokeEvent(signal.Name);
        }
        
        private void InvokeEvent(string nmSignalName)
        {
            /*
               Ughh, this would be much nicer if it actually worked using reflection.
               But noooo, EventInfo.GetRaiseMethod *always* returns null, so 
               I can't invoke the registered raise method through reflection, which
               leaves me to do this lame switch/case hard coded event raising...
            */
            
            switch(nmSignalName) {
                case "DeviceNoLongerActive": InvokeEvent(DeviceNoLongerActive); break;
                case "DeviceNowActive": InvokeEvent(DeviceNowActive); break;
                case "DeviceActivating": InvokeEvent(DeviceActivating); break;
                case "DeviceActivationStage": InvokeEvent(DeviceActivationStage); break;
                case "DevicesChanged": InvokeEvent(DevicesChanged); break;
                case "DeviceIP4AddressChange": InvokeEvent(DeviceIP4AddressChange); break;
                case "WirelessNetworkDisappeared": InvokeEvent(WirelessNetworkDisappeared); break;
                case "WirelessNetworkAppeared": InvokeEvent(WirelessNetworkAppeared); break;
                case "WirelessNetworkStrengthChanged": InvokeEvent(WirelessNetworkStrengthChanged); break;
                case "StateChange": InvokeEvent(StateChange); break;
            }
            
            /*EventInfo event_info = GetType().GetEvent(nmSignalName);
            if(event_info == null) {
               return;
            }
            
            MethodInfo method = event_info.GetRaiseMethod(true);
            if(method != null) {
                method.Invoke(this, new object [] { this, new EventArgs() });
            }*/
        }
        
        private void InvokeEvent(EventHandler eventHandler)
        {
            EventHandler handler = eventHandler;
            if(handler != null) {
                handler(this, new EventArgs());
            }
        }
        
        public IEnumerator GetEnumerator()
        {
            foreach(DeviceProxy device in manager.getDevices()) {
                yield return new Device(device);
            }
        }
        
        public State State {
            get {
                return (State)manager.state();
            }
        }
        
        public Device [] Devices {
            get {
                ArrayList list = new ArrayList();
                
                foreach(DeviceProxy device in manager.getDevices()) {
                    list.Add(new Device(device));
                }
                
                return list.ToArray(typeof(Device)) as Device [];
            }
        }
        
        public void Wake()
        {
            manager.wake();
        }
        
        public void Sleep()
        {
            manager.sleep();
        }
        
        public bool WirelessEnabled {
            get {
                return manager.getWirelessEnabled();
            }
            
            set {
                manager.setWirelessEnabled(value);
            }
        }
        
        public Device ActiveDevice {
            get {
                foreach(Device device in this) {
                    if(device.IsLinkActive) {
                        return device;
                    }
                }
                
                return null;
            }
        }
    }
}
