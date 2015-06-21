// 
// JSFunction.cs
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
    public delegate JSValue JSFunctionHandler (JSObject function, JSObject thisObject, JSValue [] args);

    public class JSFunction : JSObject
    {
        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSObjectMakeFunctionWithCallback (IntPtr ctx, JSString name,
            CallAsFunctionCallback callAsFunction);

        #pragma warning disable 0414
        private JSObject.CallAsFunctionCallback native_callback;
        #pragma warning restore 0414

        private JSFunctionHandler handler;

        public JSFunction (JSContext context, string name, JSFunctionHandler handler) : base (context, IntPtr.Zero)
        {
            this.handler = handler;
            var native_name = JSString.New (name);
            Raw = JSObjectMakeFunctionWithCallback (context.Raw, native_name,
                native_callback = new JSObject.CallAsFunctionCallback (JSCallback));
            native_name.Release ();
        }

        private IntPtr JSCallback (IntPtr ctx, IntPtr function, IntPtr thisObject,
            IntPtr argumentCount, IntPtr arguments, ref IntPtr exception)
        {
            var context = new JSContext (ctx);
            return handler == null
                ? JSValue.NewUndefined (context).Raw
                : handler (this, new JSObject (context, thisObject),
                    JSValue.MarshalArray (ctx, arguments, argumentCount)).Raw;
        }
    }
}