//
// HardwareManager.cs
//
// Author:
//   Timo Dörr <timo@latecrew.de>
//
// Copyright (C) 2012 Timo Dörr
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using MonoMac.AppKit;
using MonoMac.Foundation;

using Hyena;
using Banshee.Hardware;
using Banshee.Hardware.Osx;
using Banshee.Hardware.Osx.LowLevel;
using Banshee.ServiceStack;

namespace Banshee.OsxBackend
{
    public sealed class HardwareManager : IHardwareManager, IService
    {
        public event DeviceAddedHandler DeviceAdded;
        public event DeviceChangedHandler DeviceChanged;
        public event DeviceRemovedHandler DeviceRemoved;

        private List<IDevice> devices = new List<IDevice> ();

        private OsxDiskArbiter diskArbiter;

        public HardwareManager ()
        {
            OsxService.GlobalInit ();
            this.diskArbiter = new OsxDiskArbiter ();
            diskArbiter.DeviceAppeared += OnDeviceAppeared;
            diskArbiter.DeviceChanged += OnDeviceChanged;
            diskArbiter.DeviceDisappeared += OnDeviceDisappeared;
            diskArbiter.StartListening ();
        }

        private void OnDeviceAppeared (object o, DeviceArguments args)
        {
            Device device = new Device (args);

            Hyena.Log.DebugFormat ("device appeared: {0}, path: {1}", device.Uuid,
                args.DeviceProperties.GetStringValue ("DAVolumePath"));

            lock (this) {
                // only handle devices  which have a VolumePath (=MountPoint)
                if (!args.DeviceProperties.HasKey ("DAVolumePath")) {
                    return;
                }

                var protocol = args.DeviceProperties.GetStringValue ("DADeviceProtocol");

                IDevice new_device = null;
                if (!string.IsNullOrEmpty (protocol) && protocol == "USB") {
                    new_device = new UsbVolume (args);
                } else {
                   new_device = new Volume (args, null);
                }

                // avoid adding a device twice - might happen since DeviceAppeared and DeviceChanged both fire
                var old_device = devices.Where (v => { return v.Uuid == new_device.Uuid; }).FirstOrDefault ();
                if (old_device != null) {
                    return;
                }
                if (new_device != null) {
                    devices.Add (new_device);

                    var added_handler = DeviceAdded;
                    if (added_handler != null) {
                        // Notify that a device was added (i.e. to refresh device list)
                        added_handler (this, new DeviceAddedArgs ((IDevice)new_device));
                    }
                }
            }
        }

        private void OnDeviceChanged (object o, DeviceArguments args)
        {
            Device device = new Device (args);

            Hyena.Log.DebugFormat ("device changed: {0}, path: {1}", device.Uuid,
                args.DeviceProperties.GetStringValue ("DAVolumePath"));

            lock (this) {
                var old_device = devices.Where (d => d.Uuid == device.Uuid).FirstOrDefault ();
                if (old_device != null) {
                    // a device that was currently attached has changed 
                    // remove the device and immediately re-add it
                    devices.Remove (old_device);
                    var remove_handler = DeviceRemoved;
                    if (remove_handler != null) {
                        remove_handler (old_device, new DeviceRemovedArgs (old_device.Uuid));
                    }
                }

                // do not add device without a VolumePath (=MountPoint)
                if (!args.DeviceProperties.HasKey ("DAVolumePath")) {
                    return;
                }

                IDevice new_device = null;
                var protocol = args.DeviceProperties.GetStringValue ("DADeviceProtocol");
                if (!string.IsNullOrEmpty (protocol) && protocol == "USB") {
                    new_device = new UsbVolume (args);
                } else {
                    new_device = new Volume (args);
                }
                devices.Add (new_device);
                var added_handler = DeviceAdded;
                if (added_handler != null) {
                    added_handler (this, new DeviceAddedArgs ((IDevice)new_device));
                }
            }
        }

        private void OnDeviceDisappeared (object o, DeviceArguments args)
        {
            Device device = new Device (args);

            Hyena.Log.InformationFormat ("device disappeared: {0}, path: {1}", device.Uuid,
                args.DeviceProperties.GetStringValue ("DAVolumePath"));

            lock (this) {
                var old_device = devices.Where (d => d.Uuid == device.Uuid).FirstOrDefault ();
                if (old_device != null) {
                    devices.Remove (old_device);
                    var removed_handler = DeviceRemoved;
                    if (removed_handler != null) {
                        removed_handler (this, new DeviceRemovedArgs (old_device.Uuid));
                    }
                }
            }
        }

        public void Dispose ()
        {
            if (diskArbiter != null) {
                diskArbiter.Dispose ();
            }
        }

        public IEnumerable<IDevice> GetAllDevices ()
        {
            var l = devices.Where (v => { return v is Volume ; }).Select (v => v as IDevice);
            
            return l;
        }

        public IEnumerable<IBlockDevice> GetAllBlockDevices ()
        {
            var l = devices.Where (v => { return v is Volume; }).Select (v => v as IBlockDevice);

            return l;
        }

        public IEnumerable<ICdromDevice> GetAllCdromDevices ()
        {
            yield break;
        }

        public IEnumerable<IDiskDevice> GetAllDiskDevices ()
        {
            // cdrom / dvdrom currently not supported
            return null;
        }

        #region IService implementation
        public string ServiceName {
            get { return "OS X HardwareManager"; }
        }
        #endregion
    }
}
