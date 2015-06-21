// 
// JSClassDefinition.cs
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
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace JavaScriptCore
{
    public class JSClassDefinition
    {
        private struct JSClassDefinitionNative
        {
            public int version;
            public JSClassAttribute attributes;

            public IntPtr class_name;
            public IntPtr parent_class;

            public IntPtr /* JSStaticValue[] */ static_values;
            public IntPtr static_functions;

            public JSObject.InitializeCallback initialize;
            public JSObject.FinalizeCallback finalize;
            public JSObject.HasPropertyCallback has_property;
            public JSObject.GetPropertyCallback get_property;
            public JSObject.SetPropertyCallback set_property;
            public JSObject.DeletePropertyCallback delete_property;
            public JSObject.GetPropertyNamesCallback get_property_names;
            public JSObject.CallAsFunctionCallback call_as_function;
            public JSObject.CallAsConstructorCallback call_as_constructor;
            public JSObject.HasInstanceCallback has_instance;
            public JSObject.ConvertToTypeCallback convert_to_type;
        }

        private JSClassDefinitionNative raw;
        private Dictionary<string, MethodInfo> static_methods;
        private JSObject.CallAsFunctionCallback static_function_callback;

        public virtual string ClassName {
            get { return GetType ().FullName.Replace (".", "_").Replace ("+", "_"); }
        }

        public JSClassDefinition ()
        {
            raw = new JSClassDefinitionNative ();
            raw.class_name = Marshal.StringToHGlobalAnsi (ClassName);

            InstallClassOverrides ();
            InstallStaticMethods ();
        }

        private void InstallClassOverrides ()
        {
            Override ("OnInitialize", () => raw.initialize = new JSObject.InitializeCallback (JSInitialize));
            Override ("OnFinalize", () => raw.finalize = new JSObject.FinalizeCallback (JSFinalize));
            Override ("OnJSHasProperty", () => raw.has_property = new JSObject.HasPropertyCallback (JSHasProperty));
            Override ("OnJSGetProperty", () => raw.get_property = new JSObject.GetPropertyCallback (JSGetProperty));
            Override ("OnJSSetProperty", () => raw.set_property = new JSObject.SetPropertyCallback (JSSetProperty));
            Override ("OnJSDeleteProperty", () => raw.delete_property = new JSObject.DeletePropertyCallback (JSDeleteProperty));
            Override ("OnJSGetPropertyNames", () => raw.get_property_names = new JSObject.GetPropertyNamesCallback (JSGetPropertyNames));
            Override ("OnJSCallAsConstructor", () => raw.call_as_constructor = new JSObject.CallAsConstructorCallback (JSCallAsConstructor));
        }

        private void InstallStaticMethods ()
        {
            List<JSStaticFunction> methods = null;

            foreach (var method in GetType ().GetMethods (
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)) {
                foreach (var _attr in method.GetCustomAttributes (typeof (JSStaticFunctionAttribute), false)) {
                    var attr = (JSStaticFunctionAttribute)_attr;
                    var p = method.GetParameters ();

                    if (method.ReturnType != typeof (JSValue) || p.Length != 3 &&
                        p[0].ParameterType != typeof (JSObject) ||
                        p[1].ParameterType != typeof (JSObject) ||
                        p[2].ParameterType != typeof (JSValue [])) {
                        throw new Exception (String.Format ("Invalid signature for method annotated " +
                            "with JSStaticFunctionAttribute: {0}:{1} ('{2}'); signature should be " +
                            "'JSValue:JSFunction,JSObject,JSValue[]'",
                                GetType ().FullName, method.Name, attr.Name));
                    }

                    if (static_methods == null) {
                        static_methods = new Dictionary<string, MethodInfo> ();
                    } else if (static_methods.ContainsKey (attr.Name)) {
                        throw new Exception ("Class already contains static method named '" + attr.Name  + "'");
                    }

                    static_methods.Add (attr.Name, method);

                    if (methods == null) {
                        methods = new List<JSStaticFunction> ();
                    }

                    if (static_function_callback == null) {
                        static_function_callback = new JSObject.CallAsFunctionCallback (OnStaticFunctionCallback);
                    }

                    methods.Add (new JSStaticFunction () {
                        Name = attr.Name,
                        Attributes = attr.Attributes,
                        Callback = static_function_callback
                    });
                }
            }

            if (methods != null && methods.Count > 0) {
                var size = Marshal.SizeOf (typeof (JSStaticFunction));
                var ptr = Marshal.AllocHGlobal (size * (methods.Count + 1));

                for (int i = 0; i < methods.Count; i++) {
                    Marshal.StructureToPtr (methods[i],
                        new IntPtr (ptr.ToInt64 () + size * i), false);
                }

                Marshal.StructureToPtr (new JSStaticFunction (),
                    new IntPtr (ptr.ToInt64 () + size * methods.Count), false);

                raw.static_functions = ptr;
            }
        }

        private void Override (string methodName, Action handler)
        {
            var method = GetType ().GetMethod (methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method != null && (method.Attributes & MethodAttributes.VtableLayoutMask) == 0) {
                handler ();
            }
        }

        [DllImport (JSContext.NATIVE_IMPORT, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr JSClassCreate (ref JSClassDefinition.JSClassDefinitionNative definition);

        private JSClass class_handle;
        public JSClass ClassHandle {
            get { return class_handle ?? (class_handle = CreateClass ()); }
        }

        public JSClass CreateClass ()
        {
            return new JSClass (JSClassCreate (ref raw));
        }

        private IntPtr OnStaticFunctionCallback (IntPtr ctx, IntPtr function, IntPtr thisObject,
            IntPtr argumentCount, IntPtr arguments, ref IntPtr exception)
        {
            var context = new JSContext (ctx);
            var fn = new JSObject (ctx, function);
            string fn_name = null;
            if (fn.HasProperty ("name")) {
                var prop = fn.GetProperty ("name");
                if (prop != null && prop.IsString) {
                    fn_name = prop.StringValue;
                }
            }

            MethodInfo method = null;
            if (fn_name == null || !static_methods.TryGetValue (fn_name, out method)) {
                return JSValue.NewUndefined (context).Raw;
            }

            var result = method.Invoke (null, new object [] {
                fn,
                new JSObject (context, thisObject),
                JSValue.MarshalArray (ctx, arguments, argumentCount)
            });

            return result == null
                ? JSValue.NewUndefined (context).Raw
                : ((JSValue)result).Raw;
        }

        private void JSInitialize (IntPtr ctx, IntPtr obj)
        {
            OnJSInitialize (new JSObject (ctx, obj));
        }

        protected virtual void OnJSInitialize (JSObject obj)
        {
        }

        private void JSFinalize (IntPtr obj)
        {
            OnJSFinalize (new JSObject (obj));
        }

        protected virtual void OnJSFinalize (JSObject obj)
        {
        }

        private bool JSHasProperty (IntPtr ctx, IntPtr obj, JSString propertyName)
        {
            return OnJSHasProperty (new JSObject (ctx, obj), propertyName.Value);
        }

        protected virtual bool OnJSHasProperty (JSObject obj, string propertyName)
        {
            return false;
        }

        private IntPtr JSGetProperty (IntPtr ctx, IntPtr obj, JSString propertyName, ref IntPtr exception)
        {
            var context = new JSContext (ctx);
            return (OnJSGetProperty (new JSObject (context, obj),
                propertyName.Value) ?? JSValue.NewNull (context)).Raw;
        }

        protected virtual JSValue OnJSGetProperty (JSObject obj, string propertyName)
        {
            return JSValue.NewUndefined (obj.Context);
        }

        private bool JSSetProperty (IntPtr ctx, IntPtr obj, JSString propertyName,
            IntPtr value, ref IntPtr exception)
        {
            var context = new JSContext (ctx);
            try {
                return OnJSSetProperty (new JSObject (context, obj), propertyName.Value, new JSValue (context, value));
            } catch (JSErrorException e) {
                exception = e.Error.Raw;
                return false;
            }
        }

        protected virtual bool OnJSSetProperty (JSObject obj, string propertyName, JSValue value)
        {
            return false;
        }

        private bool JSDeleteProperty (IntPtr ctx, IntPtr obj, JSString propertyName, ref IntPtr exception)
        {
            return OnJSDeleteProperty (new JSObject (ctx, obj), propertyName.Value);
        }

        protected virtual bool OnJSDeleteProperty (JSObject obj, string propertyName)
        {
            return false;
        }

        private void JSGetPropertyNames (IntPtr ctx, IntPtr obj, JSPropertyNameAccumulator propertyNames)
        {
            OnJSGetPropertyNames (new JSObject (ctx, obj), propertyNames);
        }

        protected virtual void OnJSGetPropertyNames (JSObject obj, JSPropertyNameAccumulator propertyNames)
        {
        }

        private IntPtr JSCallAsConstructor (IntPtr ctx, IntPtr constructor,
            IntPtr argumentCount, IntPtr arguments, ref IntPtr exception)
        {
            var result = OnJSCallAsConstructor (new JSObject (ctx, constructor),
                JSValue.MarshalArray (ctx, arguments, argumentCount));
            return result == null
                ? JSValue.NewUndefined (new JSContext (ctx)).Raw
                : ((JSValue)result).Raw;
        }

        protected virtual JSObject OnJSCallAsConstructor (JSObject constructor, JSValue [] args)
        {
            return null;
        }
    }
}
