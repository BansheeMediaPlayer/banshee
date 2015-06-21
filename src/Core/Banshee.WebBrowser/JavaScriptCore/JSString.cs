// 
// JSString.cs
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
    public struct JSString
    {
        #pragma warning disable 0414
        private IntPtr raw;
        #pragma warning restore 0414

        public static JSString Zero {
            get { return new JSString (IntPtr.Zero); }
        }

        public JSString (IntPtr raw)
        {
            this.raw = raw;
        }

        internal static string ToStringAndRelease (JSString str)
        {
            if (str.Equals (JSString.Zero) || str.raw.Equals (IntPtr.Zero)) {
                return null;
            }

            try {
                return str.Value;
            } finally {
                str.Release ();
            }
        }

        internal static string ToStringAndRelease (IntPtr raw)
        {
            if (raw == IntPtr.Zero) {
                return null;
            }

            return ToStringAndRelease (new JSString (raw));
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSStringCreateWithCharacters (IntPtr chars, IntPtr numChars);

        public static JSString New (string str)
        {
            if (str == null) {
                return JSString.Zero;
            }

            var native = IntPtr.Zero;
            try {
                return new JSString (JSStringCreateWithCharacters (
                    native = Marshal.StringToHGlobalUni (str),
                    new IntPtr (str.Length)));
            } finally {
                Marshal.FreeHGlobal (native);
            }
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern JSString JSStringRetain (JSString str);

        public void Retain ()
        {
            JSStringRetain (this);
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern void JSStringRelease (JSString str);

        public void Release ()
        {
            if (!Equals (JSString.Zero)) {
                JSStringRelease (this);
            }
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool JSStringIsEqual (JSString a, JSString b);

        public bool IsEqual (JSString str)
        {
            return JSStringIsEqual (this, str);
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSStringGetLength (JSString str);

        public int Length {
            get { return JSStringGetLength (this).ToInt32 (); }
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSStringGetCharactersPtr (JSString str);

        public string Value {
            get { return Marshal.PtrToStringUni (JSStringGetCharactersPtr (this), Length); }
        }
    }
}