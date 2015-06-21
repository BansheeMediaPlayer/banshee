//
// JSContext.cs
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
    public class JSContext
    {
        public const string NATIVE_IMPORT = "libwebkitgtk-3.0.so.0";

        public IntPtr Raw { get; private set; }

        public JSContext (IntPtr raw)
        {
            Raw = raw;
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSGlobalContextCreate (IntPtr globalObjectClass);

        public JSContext ()
        {
            Raw = JSGlobalContextCreate (IntPtr.Zero);
        }

        public JSContext (JSClass globalObjectClass)
        {
            Raw = JSGlobalContextCreate (globalObjectClass.Raw);
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSEvaluateScript (IntPtr ctx, JSString script,
            IntPtr thisObject, JSString sourceURL, int startingLineNumber, ref IntPtr exception);

        public JSValue EvaluateScript (JSString script, JSObject thisObject,
            JSString sourceUrl, int startingLineNumber)
        {
            var exception = IntPtr.Zero;
            var result = JSEvaluateScript (Raw, script,
                thisObject == null ? IntPtr.Zero : thisObject.Raw,
                sourceUrl, startingLineNumber, ref exception);
            JSException.Proxy (this, exception);
            return new JSValue (this, result);
        }

        public JSValue EvaluateScript (string script, JSObject thisObject, string sourceUrl, int startingLineNumber)
        {
            var js_script = JSString.New (script);
            var js_source_url = JSString.New (sourceUrl);

            try {
                return EvaluateScript (js_script, thisObject, js_source_url, startingLineNumber);
            } finally {
                js_script.Release ();
                js_source_url.Release ();
            }
        }

        public JSValue EvaluateScript (string script, JSObject thisObject)
        {
            return EvaluateScript (script, thisObject, null, 0);
        }

        public JSValue EvaluateScript (string script)
        {
            return EvaluateScript (script, null, null, 0);
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern void JSGarbageCollect (IntPtr ctx);

        public void GarbageCollect ()
        {
            JSGarbageCollect (Raw);
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSContextGetGlobalObject (IntPtr ctx);

        public JSObject GlobalObject {
            get { return new JSObject (Raw, JSContextGetGlobalObject (Raw)); }
        }
    }
}
