
/***************************************************************************
 *  DapCore.cs
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
using System.IO;
using System.Collections;
using System.Reflection;
using Mono.Unix;
using Hal;

using Banshee.Base;

namespace Banshee.Dap
{
    public delegate void DapEventHandler(object o, DapEventArgs args);
    
    public class DapEventArgs : EventArgs
    {
        public DapDevice Dap;
        
        public DapEventArgs(DapDevice dap)
        {
            Dap = dap;
        }
    }

    public static class DapCore 
    {
        private static Hashtable device_table = new Hashtable();
        private static Hashtable device_waiting_table = new Hashtable();
        private static ArrayList supported_dap_types = new ArrayList();
    
        public static event DapEventHandler DapAdded;
        public static event DapEventHandler DapRemoved;
    
        static DapCore()
        {
            if(!HalCore.Initialized) {
                return;
            }
            
            DirectoryInfo di = null;
            FileInfo [] files = null;
            string dap_dir = ConfigureDefines.InstallDir + "Banshee.Dap" + Path.DirectorySeparatorChar;
            
            try {
                di = new DirectoryInfo(dap_dir);
                files = di.GetFiles();
                
                if(files == null || files.Length == 0) {
                    return;
                }
            } catch(Exception) {
                return;
            }

            foreach(FileInfo file in files) {
                Assembly asm;
                try {
                    asm = Assembly.LoadFrom(file.FullName);
                } catch(Exception) {
                    continue;
                }
                
                foreach(Type type in asm.GetTypes()) {
                    if(!type.IsSubclassOf(typeof(DapDevice)) || type.IsAbstract) {
                        continue;
                    }
                    
                    try {
                        Attribute [] dap_attrs = Attribute.GetCustomAttributes(type, typeof(DapProperties));
                        DapProperties dap_props = null;
                        if(dap_attrs != null && dap_attrs.Length >= 1) {
                            dap_props = dap_attrs[0] as DapProperties;
                        }
                        
                        if(dap_props != null && dap_props.DapType == DapType.NonGeneric) {
                            supported_dap_types.Insert(0, type);
                        } else {
                            supported_dap_types.Add(type);
                        }
                    } catch(Exception) {
                        continue;
                    }
                }
            }
        }
        
        public static void Initialize()
        {
            if(!HalCore.Initialized) {
                throw new ApplicationException(Catalog.GetString(
                    "Cannot initialize DapCore because HalCore is not initialized"));
            }
            
            if(supported_dap_types.Count <= 0) {
                return;
            }
            
            BuildDeviceTable();
            
            HalCore.DeviceAdded += delegate(object o, DeviceAddedArgs args) {
                AddDevice(args.Device);
            };
            
            HalCore.DeviceRemoved += delegate(object o, DeviceRemovedArgs args) {
                RemoveDevice(args.Device);
            };
            
            HalCore.DevicePropertyModified += delegate(object o, DevicePropertyModifiedArgs args) {
                if(device_waiting_table[args.Device.Udi] != null) {
                    AddDevice(args.Device, device_waiting_table[args.Device.Udi] as Type);
                }
            };
        }
        
        public static void Dispose()
        {
            foreach(DapDevice device in Devices) {
                device.Dispose();
            }
        }
        
        private static void BuildDeviceTable()
        {
            foreach(Device device in Device.FindByStringMatch(HalCore.Context, 
                "info.category", "portable_audio_player")) {
                // Find the actual storage device that is mountable;
                // this should probably just be possible by accessing
                // portable_audio_player.storage_device, but for me
                // as of HAL 0.5.6, this property just points to its own UDI
                if(device["portable_audio_player.access_method"] == "storage" &&
                    !device.GetPropertyBool("block.is_volume")) {
                    foreach(Device storage_device in Hal.Device.FindByStringMatch(device.Context, 
                        "info.parent", device.Udi)) {
                        if(AddDevice(storage_device) && device_waiting_table[storage_device.Udi] == null) {
                            break;
                        }
                    }
                } else {
                    AddDevice(device);
                }
            }
        }
        
        private static bool AddDevice(Device device)
        {
            foreach(Type type in supported_dap_types) {
                if(AddDevice(device, type)) {
                    return true;
                }
            }
            
            return false;
        }
        
        private static bool AddDevice(Device device, Type type)
        {
            try {
                DapDevice dap = Activator.CreateInstance(type) as DapDevice;
                switch(dap.Initialize(device)) {
                    case InitializeResult.Valid:
                        AddDevice(device, dap);
                        device_waiting_table.Remove(device.Udi);
                        return true;
                    case InitializeResult.WaitForPropertyChange:
                        device.WatchProperties = true;
                        device_waiting_table[device.Udi] = type;
                        return true;
                }
            } catch(Exception e) {
                Console.WriteLine(e);
            }
            
            return false;
        }
        
        private static bool AddDevice(Device device, DapDevice dap)
        {
            if(device == null || dap == null) {
                return false;
            }
            
            dap.HalDevice = device;
            dap.Ejected += OnDapEjected;
            device_table[device.Udi] = dap;
            
            if(DapAdded != null) {
                DapAdded(dap, new DapEventArgs(dap));
            }
            
            return true;
        }
        
        private static void RemoveDevice(Device device)
        {
            DapDevice dap = device_table[device.Udi] as DapDevice;
            
            device_table.Remove(device.Udi);
            device_waiting_table.Remove(device.Udi);
            
            if(dap == null) {
                return;
            }
            
            dap.Dispose();
            
            if(DapRemoved != null) {
                DapRemoved(dap, new DapEventArgs(dap));
            }
        }
        
        private static void OnDapEjected(object o, EventArgs args)
        {
            DapDevice dap = o as DapDevice;
            if(dap == null) {
                return;
            }
            
            RemoveDevice(dap.HalDevice);
        }
        
        public static DapDevice [] Devices {
            get {
                ArrayList devices = new ArrayList();
                foreach(DapDevice dap in device_table.Values) {
                    devices.Add(dap);
                }
                return devices.ToArray(typeof(DapDevice)) as DapDevice [];
            }
        }
    }
}
