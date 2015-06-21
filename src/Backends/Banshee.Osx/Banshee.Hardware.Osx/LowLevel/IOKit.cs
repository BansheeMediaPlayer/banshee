//
// IOKit.cs
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
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

using MonoMac;
using MonoMac.CoreFoundation;
using MonoMac.Foundation;
using MonoMac.AppKit;
using MonoMac.ObjCRuntime;

namespace Banshee.Hardware.Osx.LowLevel
{
    /// <summary>
    /// Wrapper against the OS X IOKit framework.
    /// Especially helpful for is the "IORegistryExplorer" program that ships with Xcode to browse
    /// connected devices and review their properties.
    /// </summary>
    internal class IOKit
    {
        private const string IOKitLibrary = "/SystemS/Library/Frameworks/IOKit.framework/IOKit";

        public static IntPtr FindInParent (IntPtr entry , CFString field)
        {
            // the field we search for, i.e. idVendor as the usb vendor id
            IntPtr key = field.Handle;

            IntPtr ptr = IORegistryEntryCreateCFProperty (entry, key, IntPtr.Zero, 0);
            if (ptr == IntPtr.Zero) {
                // key does not exist, go up one level
                IntPtr parent;

                // we search in the IOService plane - other planes might be IOUSB or IODeviceTree etc.
                // see IORegistryExplorer program that ships with OS X Xcode.
                IORegistryEntryGetParentEntry (entry , "IOService", out parent);
                if (parent != IntPtr.Zero) {
                    return FindInParent (parent, field);
                } else {
                    return IntPtr.Zero;
                }
            } else {
                return entry;
            }
        }

        public static IntPtr GetUsbProperty (IntPtr registry_entry, CFString key)
        {
            if (registry_entry == IntPtr.Zero || key.Handle == IntPtr.Zero) {
                return IntPtr.Zero;
            }

            IntPtr parent_entry = IOKit.FindInParent (registry_entry, key);
            if (parent_entry == IntPtr.Zero) {
                return IntPtr.Zero;
            }

            IntPtr ptr = IORegistryEntryCreateCFProperty (parent_entry, key.Handle, IntPtr.Zero, 0);
            //CFShow (ptr);
            return ptr;
        }

        [DllImport (IOKitLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern void IOObjectRelease (IntPtr obj);

        [DllImport (IOKitLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr IORegistryEntryCreateCFProperty (IntPtr entry, IntPtr key, IntPtr allocator, uint options);

        [DllImport (IOKitLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern void IORegistryEntryGetParentIterator (IntPtr iterator, IntPtr plane, out IntPtr parent);

        [DllImport (IOKitLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern void IORegistryEntryGetParentEntry (IntPtr entry, string plane, out IntPtr parent);

        [DllImport (IOKitLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr IORegistryEntryFromPath (IntPtr master_port, string path);

        [DllImport (IOKitLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr IORegistryEntryGetPath (IntPtr entry, string plane, string path);
    }
}
