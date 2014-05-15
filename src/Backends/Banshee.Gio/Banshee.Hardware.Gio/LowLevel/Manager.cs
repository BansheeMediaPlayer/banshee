//
// Manager.cs
//
// Author:
//   Alex Launi <alex.launi@gmail.com>
//   Nicholas Little <arealityfarbetween@googlemail.com>
//
// Copyright (c) 2010 Alex Launi
// Copyright (c) 2014 Nicholas Little
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

#if ENABLE_GIO_HARDWARE
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using GLib;
using GUdev;
using System.Runtime.InteropServices;
using Banshee.Hardware;

namespace Banshee.Hardware.Gio
{
    public class Manager : IEnumerable<IDevice>, IDisposable
    {
        private readonly string[] subsystems = new string[] {"block", "usb"};

        private Client client;
        private VolumeMonitor monitor;
        // When a device is unplugged we need to be able to map the Gio.Volume to the
        // GUDev.Device as the device will already be gone from udev. We use the native
        // handle for the Gio volume as the key to link it to the correct gudev device.
        private Dictionary<IntPtr, GUdev.Device> volume_device_map;

        public event EventHandler<MountArgs> DeviceAdded;
        public event EventHandler<MountArgs> DeviceRemoved;

        public Manager ()
        {
            client = new Client (subsystems);
            monitor = VolumeMonitor.Default;
            monitor.MountAdded += HandleMonitorMountAdded;
            monitor.MountRemoved += HandleMonitorMountRemoved;
            monitor.VolumeAdded += HandleMonitorVolumeAdded;
            monitor.VolumeRemoved += HandleMonitorVolumeRemoved;
            volume_device_map= new Dictionary<IntPtr, GUdev.Device> ();
        }

#region IDisposable
        public void Dispose ()
        {
            client.Dispose ();
            monitor.Dispose ();
        }
#endregion

        void HandleMonitorMountAdded (object o, MountAddedArgs args)
        {
            Hyena.Log.Debug ("(Ignored) MountAdded event received");
        }

        void HandleMonitorMountRemoved (object o, MountRemovedArgs args)
        {
            Hyena.Log.Debug ("(Ignored) MountRemoved event received");
        }

        private void HandleMonitorVolumeAdded (object o, VolumeAddedArgs args)
        {
            var volume = GLib.VolumeAdapter.GetObject ((GLib.Object) args.Args [0]);
            if (volume == null) {
                return;
            }

            VolumeAdded (volume);
        }

        void HandleMonitorVolumeRemoved (object o, VolumeRemovedArgs args)
        {
            var volume = GLib.VolumeAdapter.GetObject ((GLib.Object) args.Args [0]);
            if (volume == null) {
                return;
            }

            VolumeRemoved (volume);
        }

        private void VolumeAdded (GLib.IVolume volume)
        {
            var device = GudevDeviceFromGioVolume (volume);
            volume_device_map [volume.Handle] = device;
            var h = DeviceAdded;
            if (h != null) {
                if (device == null) {
                    Hyena.Log.Debug (string.Format ("Tried to mount {0}/{1} with no matching udev device", volume.Name, volume.Uuid));
                    return;
                }
                var v = new RawVolume (volume,
                    this,
                    new GioVolumeMetadataSource (volume),
                    new UdevMetadataSource (device));
                h (this, new MountArgs (HardwareManager.Resolve (new Device (v))));
            }
        }

        void VolumeRemoved (GLib.IVolume volume)
        {
            var h = DeviceRemoved;
            if (h != null) {
                GUdev.Device device;
                if (!volume_device_map.TryGetValue (volume.Handle, out device)) {
                    Hyena.Log.Debug (string.Format ("Tried to unmount {0}/{1} with no matching udev device", volume.Name, volume.Uuid));
                    return;
                }
                var v = new RawVolume (volume,
                                          this,
                                          new GioVolumeMetadataSource (volume),
                                          new UdevMetadataSource (device));

                h (this, new MountArgs (new Device (v)));
            }
        }

        public IEnumerable<IDevice> GetAllDevices ()
        {
            foreach (GLib.IVolume vol in monitor.Volumes) {
                var device = GudevDeviceFromGioVolume (vol);
                if (device == null) {
                    continue;
                }

                volume_device_map [vol.Handle] = device;
                var raw = new RawVolume (vol,
                                         this,
                                         new GioVolumeMetadataSource (vol),
                                         new UdevMetadataSource (device));
                yield return HardwareManager.Resolve (new Device (raw));
            }
        }

        public GUdev.Device GudevDeviceFromSubsystemPropertyValue (string sub, string prop, string val)
        {
            foreach (GUdev.Device dev in client.QueryBySubsystem (sub)) {
                if (dev.HasProperty (prop) && dev.GetProperty (prop) == val)
                    return dev;
            }

            return null;
        }


        public GUdev.Device GudevDeviceFromGioDrive (GLib.IDrive drive)
        {
            GUdev.Device device = null;

            if (drive == null) {
                return null;
            }

            string devFile = drive.GetIdentifier ("unix-device");
            if (!String.IsNullOrEmpty (devFile)) {
                device = client.QueryByDeviceFile (devFile);
            }

            return device;
        }

        public GUdev.Device GudevDeviceFromGioVolume (GLib.IVolume volume)
        {
            GUdev.Device device = null;

            if (volume == null) {
                return null;
            }

            var s = volume.GetIdentifier ("unix-device");
            if (!String.IsNullOrEmpty (s)) {
                device = client.QueryByDeviceFile (s);
            }

            if (device == null) {
                s = volume.Uuid;
                foreach (GUdev.Device d in client.QueryBySubsystem ("usb")) {
                    if (s == d.GetSysfsAttr ("serial")) {
                        device = d;
                        break;
                    }
                }
            }

            return device;
        }

        public GUdev.Device GudevDeviceFromGioMount (GLib.IMount mount)
        {
            if (mount == null) {
                return null;
            }

            return GudevDeviceFromGioVolume (mount.Volume);
        }

#region IEnumerable
        public IEnumerator<IDevice> GetEnumerator ()
        {
            foreach (var device in GetAllDevices ())
                yield return device;
        }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return GetEnumerator ();
        }
#endregion
    }

    public class MountArgs : EventArgs
    {
        public IDevice Device {
            get; private set;
        }

        public MountArgs (IDevice device)
        {
            Device = device;
        }
    }
}
#endif
