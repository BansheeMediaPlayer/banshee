// 
// JSPropertyNameArray.cs
// 
// Author:
//   Aaron Bockover <abockover@novell.com>
// 
// Copyright 2010 Novell, Inc.
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

namespace JavaScriptCore
{
    public struct JSPropertyNameArray
    {
        #pragma warning disable 0414
        private IntPtr raw;
        #pragma warning restore 0414

        public static JSPropertyNameArray Zero {
            get { return new JSPropertyNameArray (IntPtr.Zero); }
        }

        public JSPropertyNameArray (IntPtr raw)
        {
            this.raw = raw;
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSPropertyNameArrayRetain (JSPropertyNameArray array);

        public void Retain ()
        {
            JSPropertyNameArrayRetain (this);
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern void JSPropertyNameArrayRelease (JSPropertyNameArray array);

        public void Release ()
        {
            JSPropertyNameArrayRelease (this);
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSPropertyNameArrayGetCount (JSPropertyNameArray array);

        public int Count {
            get { return JSPropertyNameArrayGetCount (this).ToInt32 (); }
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSPropertyNameArrayGetNameAtIndex (JSPropertyNameArray array, IntPtr index);

        public string GetNameAtIndex (int index)
        {
            return new JSString (JSPropertyNameArrayGetNameAtIndex (this, new IntPtr (index))).Value;
        }

        public string this[int index] {
            get { return GetNameAtIndex (index); }
        }
    }
}

