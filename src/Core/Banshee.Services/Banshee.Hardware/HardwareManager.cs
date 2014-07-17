//
// HardwareManager.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2008-2009 Novell, Inc.
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

using Mono.Addins;
using Hyena;

using Banshee.ServiceStack;

namespace Banshee.Hardware
{
    public sealed class HardwareManager : IService, IHardwareManager, IDisposable
    {
        private IHardwareManager manager;
        private Dictionary<string, ICustomDeviceProvider> custom_device_providers = new Dictionary<string, ICustomDeviceProvider> ();

        public event DeviceAddedHandler DeviceAdded;
        public event DeviceChangedHandler DeviceChanged;
        public event DeviceRemovedHandler DeviceRemoved;

        public HardwareManager ()
        {
            var extensions = Banshee.IO.Provider.GetOrderedExtensions (
                "/Banshee/Platform/HardwareManager",
                "Banshee.Hardware.Gio", "Banshee.HalBackend.HardwareManager"
            );

            foreach (var node in extensions) {
                try {
                    manager = (IHardwareManager)node.CreateInstance (typeof (IHardwareManager));
                    Log.DebugFormat ("Loaded HardwareManager backend: {0}", node.Id);
                    break;
                } catch (Exception e) {
                    Log.Error ("Hardware manager extension failed to load", e);
                }
            }

            if (manager == null) {
                throw new Exception ("No HardwareManager extensions could be loaded. Hardware support will be disabled.");
            }

            manager.DeviceAdded += OnDeviceAdded;
            manager.DeviceChanged += OnDeviceChanged;
            manager.DeviceRemoved += OnDeviceRemoved;

            ServiceManager.Get<DBusCommandService> ().ArgumentPushed += OnCommandLineArgument;

            AddinManager.AddExtensionNodeHandler ("/Banshee/Platform/HardwareDeviceProvider", OnExtensionChanged);
        }

        public void Dispose ()
        {
            lock (this) {
                if (manager != null) {
                    manager.DeviceAdded -= OnDeviceAdded;
                    manager.DeviceChanged -= OnDeviceChanged;
                    manager.DeviceRemoved -= OnDeviceRemoved;
                    manager.Dispose ();
                    manager = null;
                }

                ServiceManager.Get<DBusCommandService> ().ArgumentPushed -= OnCommandLineArgument;

                if (custom_device_providers != null) {
                    foreach (ICustomDeviceProvider provider in custom_device_providers.Values) {
                        provider.Dispose ();
                    }

                    custom_device_providers.Clear ();
                    custom_device_providers = null;
                }

                AddinManager.RemoveExtensionNodeHandler ("/Banshee/Platform/HardwareDeviceProvider", OnExtensionChanged);
            }
        }

        private void OnExtensionChanged (object o, ExtensionNodeEventArgs args)
        {
            lock (this) {
                TypeExtensionNode node = (TypeExtensionNode)args.ExtensionNode;

                if (args.Change == ExtensionChange.Add && !custom_device_providers.ContainsKey (node.Id)) {
                    custom_device_providers.Add (node.Id, (ICustomDeviceProvider)node.CreateInstance ());
                } else if (args.Change == ExtensionChange.Remove && custom_device_providers.ContainsKey (node.Id)) {
                    ICustomDeviceProvider provider = custom_device_providers[node.Id];
                    provider.Dispose ();
                    custom_device_providers.Remove (node.Id);
                }
            }
        }

#region Device Command Line Argument Handling

        private event DeviceCommandHandler device_command;
        public event DeviceCommandHandler DeviceCommand {
            add {
                try {
                    device_command += value;
                } finally {
                    if (value != null && StartupDeviceCommand != null) {
                        value (this, StartupDeviceCommand);
                    }
                }
            }

            remove { device_command -= value; }
        }

        private Banshee.Hardware.DeviceCommand startup_device_command;
        private bool startup_device_command_checked = false;

        public Banshee.Hardware.DeviceCommand StartupDeviceCommand {
            get {
                lock (this) {
                    if (startup_device_command_checked) {
                        return startup_device_command;
                    }

                    startup_device_command_checked = true;

                    foreach (KeyValuePair<string, string> argument in ApplicationContext.CommandLine.Arguments) {
                        startup_device_command = Banshee.Hardware.DeviceCommand.ParseCommandLine (argument.Key, argument.Value);
                        if (startup_device_command != null) {
                            break;
                        }
                    }

                    return startup_device_command;
                }
            }
        }

        private void OnCommandLineArgument (string argument, object value, bool isFile)
        {
            Banshee.Hardware.DeviceCommand command = Banshee.Hardware.DeviceCommand.ParseCommandLine (argument, value.ToString ());
            if (command == null) {
                return;
            }

            DeviceCommandHandler handler = device_command;
            if (handler != null) {
                handler (this, command);
            }
        }

#endregion

        private void OnDeviceAdded (object o, DeviceAddedArgs args)
        {
            lock (this) {
                DeviceAddedHandler handler = DeviceAdded;
                if (handler != null) {
                    DeviceAddedArgs raise_args = args;
                    IDevice cast_device = CastToCustomDevice<IDevice> (args.Device);

                    if (cast_device != args.Device) {
                        raise_args = new DeviceAddedArgs (cast_device);
                    }

                    handler (this, raise_args);
                }
            }
        }

        private void OnDeviceChanged (object o, DeviceChangedEventArgs args)
        {
            lock (this) {
                var handler = DeviceChanged;
                if (handler != null) {
                    handler (this, args);
                }
            }
        }

        private void OnDeviceRemoved (object o, DeviceRemovedArgs args)
        {
            lock (this) {
                DeviceRemovedHandler handler = DeviceRemoved;
                if (handler != null) {
                    handler (this, args);
                }
            }
        }

        private T CastToCustomDevice<T> (T device) where T : class, IDevice
        {
            foreach (ICustomDeviceProvider provider in custom_device_providers.Values) {
                try {
                    T new_device = provider.GetCustomDevice (device);
                    if (new_device != device) {
                        return new_device;
                    }
                } catch (Exception e) {
                    Log.Error ("Exception casting device", e);
                }
            }

            return device;
        }

        private IEnumerable<T> CastToCustomDevice<T> (IEnumerable<T> devices) where T : class, IDevice
        {
            foreach (T device in devices) {
                yield return CastToCustomDevice<T> (device);
            }
        }

        public IEnumerable<IDevice> GetAllDevices ()
        {
            return CastToCustomDevice<IDevice> (manager.GetAllDevices ());
        }

        public IEnumerable<IBlockDevice> GetAllBlockDevices ()
        {
            return CastToCustomDevice<IBlockDevice> (manager.GetAllBlockDevices ());
        }

        public IEnumerable<ICdromDevice> GetAllCdromDevices ()
        {
            return CastToCustomDevice<ICdromDevice> (manager.GetAllCdromDevices ());
        }

        public IEnumerable<IDiskDevice> GetAllDiskDevices ()
        {
            return CastToCustomDevice<IDiskDevice> (manager.GetAllDiskDevices ());
        }

        public void Test ()
        {
            Console.WriteLine ("All Block Devices:");
            PrintBlockDeviceList (GetAllBlockDevices ());

            Console.WriteLine ("All CD-ROM Devices:");
            PrintBlockDeviceList (GetAllCdromDevices ());

            Console.WriteLine ("All Disk Devices:");
            PrintBlockDeviceList (GetAllDiskDevices ());
        }

        private void PrintBlockDeviceList (System.Collections.IEnumerable devices)
        {
            foreach (IBlockDevice device in devices) {
                Console.WriteLine ("{0}, {1}", device.GetType ().FullName, device);
                foreach (IVolume volume in device.Volumes) {
                    Console.WriteLine ("  {0}, {1}", volume.GetType ().FullName, volume);
                }
            }
        }

        string IService.ServiceName {
            get { return "HardwareManager"; }
        }
    }
}
