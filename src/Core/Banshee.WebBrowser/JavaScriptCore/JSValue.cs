// 
// JSValue.cs
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
    public class JSValue
    {
        public IntPtr Raw { get; protected set; }
        public JSContext Context { get; private set; }

        public JSValue (IntPtr raw)
        {
            Raw = raw;
            Context = null;
        }

        public JSValue (IntPtr context, IntPtr raw)
        {
            Raw = raw;
            Context = new JSContext (context);
        }

        public JSValue (JSContext context, IntPtr raw)
        {
            Raw = raw;
            Context = context;
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSValueMakeBoolean (IntPtr ctx, bool value);

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSValueMakeNumber (IntPtr ctx, double value);

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSValueMakeString (IntPtr ctx, JSString value);

        public JSValue (JSContext ctx, bool value) : this (ctx, JSValueMakeBoolean (ctx.Raw, value)) { }
        public JSValue (JSContext ctx, byte value) : this (ctx, JSValueMakeNumber (ctx.Raw, (double)value)) { }
        public JSValue (JSContext ctx, sbyte value) : this (ctx, JSValueMakeNumber (ctx.Raw, (double)value)) { }
        public JSValue (JSContext ctx, short value) : this (ctx, JSValueMakeNumber (ctx.Raw, (double)value)) { }
        public JSValue (JSContext ctx, ushort value) : this (ctx, JSValueMakeNumber (ctx.Raw, (double)value)) { }
        public JSValue (JSContext ctx, int value) : this (ctx, JSValueMakeNumber (ctx.Raw, (double)value)) { }
        public JSValue (JSContext ctx, uint value) : this (ctx, JSValueMakeNumber (ctx.Raw, (double)value)) { }
        public JSValue (JSContext ctx, long value) : this (ctx, JSValueMakeNumber (ctx.Raw, (double)value)) { }
        public JSValue (JSContext ctx, ulong value) : this (ctx, JSValueMakeNumber (ctx.Raw, (double)value)) { }
        public JSValue (JSContext ctx, float value) : this (ctx, JSValueMakeNumber (ctx.Raw, (double)value)) { }
        public JSValue (JSContext ctx, double value) : this (ctx, JSValueMakeNumber (ctx.Raw, value)) { }
        public JSValue (JSContext ctx, JSString value) : this (ctx, JSValueMakeString (ctx.Raw, value)) { }
        public JSValue (JSContext ctx, string value) : this (ctx, JSValueMakeString (ctx.Raw, JSString.New (value))) { }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern JSType JSValueGetType (IntPtr ctx, IntPtr value);

        public JSType JSType {
            get { return JSValueGetType (Context.Raw, Raw); }
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool JSValueIsUndefined (IntPtr ctx, IntPtr value);

        public bool IsUndefined {
            get { return JSValueIsUndefined (Context.Raw, Raw); }
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool JSValueIsNull (IntPtr ctx, IntPtr value);

        public bool IsNull {
            get { return JSValueIsNull (Context.Raw, Raw); }
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool JSValueIsBoolean (IntPtr ctx, IntPtr value);

        public bool IsBoolean {
            get { return JSValueIsBoolean (Context.Raw, Raw); }
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool JSValueIsNumber (IntPtr ctx, IntPtr value);

        public bool IsNumber {
            get { return JSValueIsNumber (Context.Raw, Raw); }
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool JSValueIsString (IntPtr ctx, IntPtr value);

        public bool IsString {
            get { return JSValueIsString (Context.Raw, Raw); }
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool JSValueIsObject (IntPtr ctx, IntPtr value);

        public bool IsObject {
            get { return JSValueIsObject (Context.Raw, Raw); }
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool JSValueIsObjectOfClass (IntPtr ctx, IntPtr value, IntPtr jsClass);

        public bool IsObjectOfClass (JSClass jsClass)
        {
            return JSValueIsObjectOfClass (Context.Raw, Raw, jsClass.Raw);
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool JSValueIsEqual (IntPtr ctx, IntPtr a, IntPtr b, ref IntPtr exception);

        public bool IsEqual (JSValue value)
        {
            var exception = IntPtr.Zero;
            var result = JSValueIsEqual (Context.Raw, Raw, value.Raw, ref exception);
            JSException.Proxy (Context, exception);
            return result;
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool JSValueIsStrictEqual (IntPtr ctx, IntPtr a, IntPtr b);

        public bool IsStrictEqual (JSValue value)
        {
            return JSValueIsStrictEqual (Context.Raw, Raw, value.Raw);
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool JSValueIsInstanceOfConstructor (IntPtr ctx, IntPtr value,
            IntPtr constructor, ref IntPtr exception);

        public bool IsInstanceOfConstructor (JSObject constructor)
        {
            var exception = IntPtr.Zero;
            var result = JSValueIsInstanceOfConstructor (Context.Raw, Raw, constructor.Raw, ref exception);
            JSException.Proxy (Context, exception);
            return result;
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSValueMakeUndefined (IntPtr ctx);

        public static JSValue NewUndefined (JSContext ctx)
        {
            return new JSValue (ctx, JSValueMakeUndefined (ctx.Raw));
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSValueMakeNull (IntPtr ctx);

        public static JSValue NewNull (JSContext ctx)
        {
            return new JSValue (ctx, JSValueMakeNull (ctx.Raw));
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSValueMakeFromJSONString (IntPtr ctx, JSString str);

        public static JSValue FromJson (JSContext ctx, JSString json)
        {
            var obj = JSValueMakeFromJSONString (ctx.Raw, json);
            if (obj.Equals (IntPtr.Zero)) {
                throw new JSException (ctx, "Invalid JSON");
            }

            return new JSValue (ctx, obj);
        }

        public static JSValue FromJson (JSContext ctx, string json)
        {
            var json_native = JSString.New (json);
            try {
                return FromJson (ctx, json_native);
            } finally {
                json_native.Release ();
            }
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool JSValueToBoolean (IntPtr ctx, IntPtr value);

        public bool BooleanValue {
            get { return JSValueToBoolean (Context.Raw, Raw); }
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern double JSValueToNumber (IntPtr ctx, IntPtr value);

        public double NumberValue {
            get { return JSValueToNumber (Context.Raw, Raw); }
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSValueToStringCopy (IntPtr ctx, IntPtr value, ref IntPtr exception);

        public string StringValue {
            get {
                var exception = IntPtr.Zero;
                var result = JSString.ToStringAndRelease (JSValueToStringCopy (Context.Raw, Raw, ref exception));
                JSException.Proxy (Context, exception);
                return result;
            }
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSValueToObject (IntPtr ctx, IntPtr value, ref IntPtr exception);

        public JSObject ObjectValue {
            get {
                var exception = IntPtr.Zero;
                var result = JSValueToObject (Context.Raw, Raw, ref exception);
                JSException.Proxy (Context, exception);
                return new JSObject (Context, result);
            }
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSValueCreateJSONString (IntPtr ctx, IntPtr value, uint indent, ref IntPtr exception);

        public string ToJsonString (uint indent)
        {
            var exception = IntPtr.Zero;
            var result = JSString.ToStringAndRelease (JSValueCreateJSONString (Context.Raw,
                Raw, indent, ref exception));
            JSException.Proxy (Context, exception);
            return result;
        }

        public string ToJsonString ()
        {
            return ToJsonString (2);
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern void JSValueProtect (IntPtr ctx, IntPtr value);

        public void Protect ()
        {
            JSValueProtect (Context.Raw, Raw);
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern void JSValueUnprotect (IntPtr ctx, IntPtr value);

        public void Unprotect ()
        {
            JSValueUnprotect (Context.Raw, Raw);
        }

        public override string ToString ()
        {
            return IsObject ? ToJsonString (0) ?? StringValue : StringValue;
        }

        public static JSValue [] MarshalArray (IntPtr context, IntPtr items, IntPtr itemCount)
        {
            var array = new JSValue[itemCount.ToInt32 ()];
            for (int i = 0; i < array.Length; i++) {
                array[i] = new JSValue (context, Marshal.ReadIntPtr (items, i * IntPtr.Size));
            }
            return array;
        }
    }
}
