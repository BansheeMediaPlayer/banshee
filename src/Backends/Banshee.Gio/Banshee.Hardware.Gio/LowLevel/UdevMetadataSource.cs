//
// UdevMetadataSource.cs
//
// Author:
//   Alan McGovern <amcgovern@novell.com>
//
// Copyright (c) 2010 Moonlight Team
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
namespace Banshee.Hardware.Gio
{
    public class UdevMetadataSource : IMetadataSource
    {
        GUdev.Device Device {
            get; set;
        }

        public string IdMediaDevice {
            get { return Device.GetProperty ("ID_MEDIA_PLAYER"); }
        }

        public string Model {
            get { return Device.GetProperty ("ID_MODEL"); }
        }

        public string Name {
            get { return Device.Name; }
        }

        public UdevMetadataSource Parent {
            get { return Device.Parent == null ? null : new UdevMetadataSource (Device.Parent); }
        }

        public string Product {
            get { return Device.GetProperty ("PRODUCT"); }
        }

        public string Serial {
            get { return GetPropertyString ("ID_SERIAL_SHORT"); }
        }

        public string Uuid {
            get { return GetPropertyString ("DEVPATH"); }
        }

        public string Vendor {
            get { return GetPropertyString ("ID_VENDOR"); }
        }

        public string Subsystem {
            get { return GetPropertyString ("SUBSYSTEM"); }
        }

        public UdevMetadataSource (GUdev.Device device)
        {
            if (device == null) {
                throw new ArgumentNullException ("device");
            }
            Device = device;
        }

        public string GetPropertyString (string key)
        {
            return Device.GetProperty (key);
        }

        public double GetPropertyDouble (string key)
        {
            return Device.GetPropertyAsDouble (key);
        }

        public bool GetPropertyBoolean (string key)
        {
            return Device.GetPropertyAsBoolean (key);
        }

        public int GetPropertyInteger (string key)
        {
            return Device.GetPropertyAsInt (key);
        }

        public ulong GetPropertyUInt64 (string key)
        {
            return Device.GetPropertyAsUint64 (key);
        }

        public string[] GetPropertyStringList (string key)
        {
            return Device.GetPropertyAsStrv (key);
        }

        public bool PropertyExists (string key)
        {
            return Device.HasProperty (key);
        }
    }
}

#endif
