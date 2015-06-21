//
// CdromDevice.cs
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
using System.Runtime.InteropServices;
using Mono.Unix;

using Banshee.Hardware;

namespace Banshee.Hardware.Gio
{
    class CdromDevice : BlockDevice, ICdromDevice
    {
        // This tells us the actual type of the block device
        // i.e. 'disk' == HD. 'cd' == dvd/cd drive
        const string DeviceType = "ID_TYPE";

        public static new ICdromDevice Resolve (IDevice device)
        {
            var raw = device as IRawDevice;
            if (raw != null) {
                if (raw.Device.UdevMetadata.GetPropertyString (DeviceType) == "cd") {
                    return new CdromDevice (raw.Device);
                }
            }

            return null;
        }

        // FIXME: This is incredibly lame, there must be a way to query the
        // device itself rather than hackisly attempting to keep track of it
        public bool IsDoorLocked {
            get; private set;
        }

        // This was literally copied and pasted from Hal's CdromDevice class.
        [DllImport ("libc", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ioctl (int device, IoctlOperation request, bool lockdoor);

        private enum IoctlOperation {
            LockDoor = 0x5329
        }

        CdromDevice (RawDevice device)
            : base (device)
        {

        }

        bool LockDeviceNode (string device, bool lockdoor)
        {
            try {
                using (UnixStream stream = (new UnixFileInfo (device)).Open (
                    Mono.Unix.Native.OpenFlags.O_RDONLY |
                    Mono.Unix.Native.OpenFlags.O_NONBLOCK)) {
                    bool success = ioctl (stream.Handle, IoctlOperation.LockDoor, lockdoor) == 0;
                    IsDoorLocked = success && lockdoor;
                    return success;
                }
            } catch {
                return false;
            }
        }

        public bool LockDoor ()
        {
            lock (this) {
                return LockDeviceNode (DeviceNode, true);
            }
        }

        public bool UnlockDoor ()
        {
            lock (this) {
                return LockDeviceNode (DeviceNode, false);
            }
        }
    }
}
#endif
