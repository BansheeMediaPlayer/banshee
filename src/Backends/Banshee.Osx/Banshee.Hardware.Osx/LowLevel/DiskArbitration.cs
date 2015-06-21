//
// DiskArbitration.cs
//
// Author:
//   Timo Dörr <timo@latecrew.de>
//
// Copyright 2012 Timo Dörr
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

using System;
using System.Runtime.InteropServices;

namespace Banshee.Hardware.Osx.LowLevel
{
    public static class DiskArbitration
    {
        private const string DiskArbitrationLibrary = "/Systems/Library/Frameworks/DiskArbitration.framework/DiskArbitration";

        [DllImport (DiskArbitrationLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr DASessionCreate (IntPtr allocator);
    
        [DllImport (DiskArbitrationLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr DARegisterDiskAppearedCallback (IntPtr session, IntPtr match, IntPtr callback, IntPtr context);
    
        [DllImport (DiskArbitrationLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr DARegisterDiskDescriptionChangedCallback (IntPtr session, IntPtr match, IntPtr watch, IntPtr callback, IntPtr context);
    
        [DllImport (DiskArbitrationLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr DARegisterDiskDisappearedCallback (IntPtr session, IntPtr match, IntPtr callback, IntPtr context);
        
        [DllImport (DiskArbitrationLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr DAUnregisterCallback (IntPtr session, IntPtr callback, IntPtr context);

        [DllImport (DiskArbitrationLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr DASessionScheduleWithRunLoop (IntPtr session , IntPtr runLoop , IntPtr runloopMode);
    
        [DllImport (DiskArbitrationLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr DASessionUnscheduleFromRunLoop (IntPtr session , IntPtr runLoop , IntPtr runloopMode);

        [DllImport (DiskArbitrationLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr DADiskCopyDescription (IntPtr disk);

        [DllImport (DiskArbitrationLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr DADiskCopyIOMedia (IntPtr disk);

        [DllImport (DiskArbitrationLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DADiskUnmount (IntPtr disk, int unmountOptions, UnmountCallback callback, IntPtr context);

        [DllImport (DiskArbitrationLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr DADiskCreateFromBSDName (IntPtr allocator, IntPtr da_session_ref, string name);

        [DllImport (DiskArbitrationLibrary, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr DADiskCreateFromVolumePath (IntPtr allocator, IntPtr da_session_ref, IntPtr urlref);

        public delegate void UnmountCallback (IntPtr disk, IntPtr dissenter, IntPtr context);
    }
}
