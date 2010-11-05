//
// HardwareManager.cs
//
// Author:
//   Alex Launi <alex.launi@gmail.com>
//
// Copyright (c) 2010 Alex Launi
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
using System.Collections.Generic;

using Banshee.Hardware;
using Hyena;

namespace Banshee.Hardware.Gio
{
    public class HardwareManager : IHardwareManager
    {
        private Manager manager;

        public HardwareManager ()
        {
            manager = new Manager ();
            manager.DeviceAdded += HandleManagerDeviceAdded;
            manager.DeviceRemoved += HandleManagerDeviceRemoved;
        }

        public void Dispose ()
        {
            manager.Dispose ();
        }

        void HandleManagerDeviceAdded (object o, MountArgs args)
        {
            HandleManagerDeviceAdded (args.Device);
        }

        void HandleManagerDeviceRemoved (object o, MountArgs args)
        {
            if (DeviceRemoved != null) {
                DeviceRemoved (this, new DeviceRemovedArgs (args.Device.Uuid));
            }
        }

        void HandleManagerDeviceAdded (IDevice device)
        {
            if (device != null && DeviceAdded != null) {
                DeviceAdded (this, new DeviceAddedArgs (device));
            }
        }

#region IHardwareManager

        public event DeviceAddedHandler DeviceAdded;
        public event DeviceRemovedHandler DeviceRemoved;

        public IEnumerable<IDevice> GetAllDevices ()
        {
            foreach (IDevice device in manager.GetAllDevices ()) {
                if (device != null) {
                    yield return device;
                }
            }
        }

        public IEnumerable<IBlockDevice> GetAllBlockDevices ()
        {
            return GetAllBlockDevices<IBlockDevice> ();
        }

        public IEnumerable<ICdromDevice> GetAllCdromDevices ()
        {
            return GetAllBlockDevices<ICdromDevice> ();
        }

        public IEnumerable<IDiskDevice> GetAllDiskDevices ()
        {
            return GetAllBlockDevices<IDiskDevice> ();
        }
#endregion

        private IEnumerable<T> GetAllBlockDevices<T> () where T : class, IBlockDevice
        {
            foreach (var lowDevice in manager.GetAllDevices ()) {
                T device = lowDevice as T;
                if (device != null) {
                    yield return device;
                }
            }
        }

        internal static IDevice Resolve (IDevice device)
        {
            IDevice dev = BlockDevice.Resolve (device);
            if (dev == null)
                dev = UsbVolume.Resolve (device);
            if (dev == null)
                dev = Volume.Resolve (device);
            if (dev == null)
                dev = UsbDevice.Resolve (device);
            if (dev == null)
                dev = Device.Resolve (device);

            return dev;
        }
    }
}
#endif
