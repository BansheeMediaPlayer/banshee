//
// Catalog.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2006-2008 Novell, Inc.
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
//

using System;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Mono.Unix;

namespace Banshee.I18n
{
    public static class Catalog
    {
        private static Dictionary<Assembly, string> domain_assembly_map = new Dictionary<Assembly, string> ();
        private static List<Assembly> default_domain_assemblies = new List<Assembly> ();

        public static void Init (string domain, string localeDir)
        {
            if (String.IsNullOrEmpty (domain)) {
                throw new ArgumentException ("No text domain specified");
            }

            IntPtr domain_ptr = UnixMarshal.StringToHeap (domain);
            IntPtr localedir_ptr = IntPtr.Zero;

            try {
                BindTextDomainCodeset (domain_ptr);

                if (localeDir != null && localeDir.Length > 0) {
                    localedir_ptr = UnixMarshal.StringToHeap (localeDir);
                    BindTextDomain (domain_ptr, localedir_ptr);
                }
            } finally {
                UnixMarshal.FreeHeap (domain_ptr);
                if (localedir_ptr != IntPtr.Zero) {
                    UnixMarshal.FreeHeap (localedir_ptr);
                }
            }
        }

        public static string GetString (string msgid)
        {
            return GetString (GetDomainForAssembly (Assembly.GetCallingAssembly ()), msgid);
        }

        public static string GetString (string domain, string msgid)
        {
            IntPtr msgid_ptr = UnixMarshal.StringToHeap (msgid);
            IntPtr domain_ptr = domain == null ? IntPtr.Zero : UnixMarshal.StringToHeap (domain);

            if (domain == null) {
                // FIXME banshee-1?
                IntPtr ptr = UnixMarshal.StringToHeap ("banshee");
                UnixMarshal.FreeHeap (ptr);
            }

            try {
                IntPtr ret_ptr = domain_ptr == IntPtr.Zero ?
                    gettext (msgid_ptr) :
                    dgettext (domain_ptr, msgid_ptr);

                if (msgid_ptr != ret_ptr) {
                    return UnixMarshal.PtrToStringUnix (ret_ptr);
                }

                return msgid;
            } finally {
                UnixMarshal.FreeHeap (msgid_ptr);
                if (domain_ptr != IntPtr.Zero) {
                    UnixMarshal.FreeHeap (domain_ptr);
                }
            }
        }

        public static string GetString (string msgid, string msgidPlural, int n)
        {
            return GetString (GetDomainForAssembly (Assembly.GetCallingAssembly ()), msgid, msgidPlural, n);
        }

        public static string GetPluralString (string msgid, string msgidPlural, int n)
        {
            return GetString (msgid, msgidPlural, n);
        }

        public static string GetString (string domain, string msgid, string msgidPlural, int n)
        {
            IntPtr msgid_ptr = UnixMarshal.StringToHeap (msgid);
            IntPtr msgid_plural_ptr = UnixMarshal.StringToHeap (msgidPlural);
            IntPtr domain_ptr = domain == null ? IntPtr.Zero : UnixMarshal.StringToHeap (domain);

            try {
                IntPtr ret_ptr = domain_ptr == IntPtr.Zero ?
                    ngettext (msgid_ptr, msgid_plural_ptr, n) :
                    dngettext (domain_ptr, msgid_ptr, msgid_plural_ptr, n);

                if (ret_ptr == msgid_ptr) {
                    return msgid;
                } else if (ret_ptr == msgid_plural_ptr) {
                    return msgidPlural;
                }

                return UnixMarshal.PtrToStringUnix (ret_ptr);
            } finally {
                UnixMarshal.FreeHeap (msgid_ptr);
                UnixMarshal.FreeHeap (msgid_plural_ptr);
                if (domain_ptr != IntPtr.Zero) {
                    UnixMarshal.FreeHeap (domain_ptr);
                }
            }
        }

        private static string GetDomainForAssembly (Assembly assembly)
        {
            if (default_domain_assemblies.Contains (assembly)) {
                return null;
            } else if (domain_assembly_map.ContainsKey (assembly)) {
                return domain_assembly_map[assembly];
            }

            AssemblyCatalogAttribute [] attributes = assembly.GetCustomAttributes (
                typeof(AssemblyCatalogAttribute), true) as AssemblyCatalogAttribute [];

            if (attributes == null || attributes.Length == 0) {
                default_domain_assemblies.Add (assembly);
                return null;
            }

            string domain = attributes[0].Domain;

            Init (domain, attributes[0].LocaleDir);
            domain_assembly_map.Add (assembly, domain);

            return domain;
        }

        private static void BindTextDomainCodeset (IntPtr domain)
        {
            IntPtr codeset = UnixMarshal.StringToHeap("UTF-8");

            try {
                if (bind_textdomain_codeset (domain, codeset) == IntPtr.Zero) {
                    throw new UnixIOException (Mono.Unix.Native.Errno.ENOMEM);
                }
            } finally {
                UnixMarshal.FreeHeap (codeset);
            }
        }

        private static void BindTextDomain (IntPtr domain, IntPtr localedir)
        {
            if (bindtextdomain (domain, localedir) == IntPtr.Zero) {
                throw new UnixIOException (Mono.Unix.Native.Errno.ENOMEM);
            }
        }

        private const string LibIntlibrary = "intl";

        [DllImport (LibIntlibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr bind_textdomain_codeset (IntPtr domain, IntPtr codeset);

        [DllImport (LibIntlibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr bindtextdomain (IntPtr domain, IntPtr locale_dir);

        [DllImport (LibIntlibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dgettext (IntPtr domain, IntPtr msgid);

        [DllImport (LibIntlibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dngettext (IntPtr domain, IntPtr msgid_singular, IntPtr msgid_plural, Int32 n);

        [DllImport (LibIntlibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr gettext (IntPtr msgid);

        [DllImport (LibIntlibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr ngettext (IntPtr msgid_singular, IntPtr msgid_plural, Int32 n);
    }
}
