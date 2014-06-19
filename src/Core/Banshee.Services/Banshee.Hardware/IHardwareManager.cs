//
// HardwareManager.cs
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

namespace Banshee.Hardware
{
    public interface IHardwareManager : IDisposable
    {
        event DeviceAddedHandler DeviceAdded;
        event DeviceChangedHandler DeviceChanged;
        event DeviceRemovedHandler DeviceRemoved;

        IEnumerable<IDevice> GetAllDevices ();
        IEnumerable<IBlockDevice> GetAllBlockDevices ();
        IEnumerable<ICdromDevice> GetAllCdromDevices ();
        IEnumerable<IDiskDevice> GetAllDiskDevices ();
    }

    public delegate void DeviceAddedHandler (object o, DeviceAddedArgs args);
    public delegate void DeviceChangedHandler (object o, DeviceChangedEventArgs args);
    public delegate void DeviceRemovedHandler (object o, DeviceRemovedArgs args);

    public sealed class DeviceAddedArgs : EventArgs
    {
        private IDevice device;

        public DeviceAddedArgs (IDevice device)
        {
            this.device = device;
        }

        public IDevice Device {
            get { return device; }
        }
    }

    public sealed class DeviceChangedEventArgs : EventArgs
    {
        private readonly IDevice device;

        public DeviceChangedEventArgs (IDevice device)
        {
            this.device = device;
        }

        public IDevice Device {
            get { return device; }
        }
    }

    public sealed class DeviceRemovedArgs : EventArgs
    {
        private string device_uuid;

        public DeviceRemovedArgs (string deviceUuid)
        {
            this.device_uuid = deviceUuid;
        }

        public string DeviceUuid {
            get { return device_uuid; }
        }
    }
}
