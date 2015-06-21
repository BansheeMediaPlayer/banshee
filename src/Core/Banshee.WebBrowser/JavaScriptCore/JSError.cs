// 
// JSError.cs
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
    public class JSError : JSObject
    {
        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSObjectMakeError (IntPtr ctx,
            IntPtr argumentCount, IntPtr args, ref IntPtr exception);

        public JSError (JSContext context, string name, string message) : base (context, IntPtr.Zero)
        {
            var exception = IntPtr.Zero;
            Raw = JSObjectMakeError (context.Raw, IntPtr.Zero, IntPtr.Zero, ref exception);
            JSException.Proxy (context, exception);

            if (name != null) {
                SetProperty ("name", new JSValue (context, name));
            }

            if (message != null) {
                SetProperty ("message", new JSValue (context, message));
            }
        }
    }
}