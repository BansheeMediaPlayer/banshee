//
// GConfConfigurationClient.cs
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
using System.Collections;
using System.Collections.Generic;
using GConf;

using Hyena;
using Banshee.Base;
using Banshee.Configuration;

namespace Banshee.GnomeBackend
{
    public class GConfConfigurationClient : IConfigurationClient
    {
        private GConf.Client client;
        private Dictionary<string, string> key_table = new Dictionary<string, string> ();

        private static bool disable_gconf_checked = false;
        private static bool disable_gconf = false;

        private static bool DisableGConf {
            get {
                if (!disable_gconf_checked) {
                    disable_gconf = ApplicationContext.EnvironmentIsSet ("BANSHEE_DISABLE_GCONF");
                    disable_gconf_checked = true;
                }

                return disable_gconf;
            }
        }

        private static string base_key;
        private static string BaseKey {
            get {
                if (base_key == null) {
                    base_key = ApplicationContext.CommandLine["gconf-base-key"];
                    if (!base_key.StartsWith ("/apps/") || !base_key.EndsWith ("/")) {
                        Log.Debug ("Using default gconf-base-key");
                        base_key = "/apps/banshee-1/";
                    }
                }
                return base_key;
            }
        }

        public GConfConfigurationClient ()
        {
            if (Hyena.PlatformDetection.IsMeeGo) {
                throw new InvalidOperationException ("GConf is not supported on MeeGo");
            }
        }

        public void AddCallback (string @namespace, NotifyEventHandler callback)
        {
            if (DisableGConf || @namespace == null) {
                return;
            }

            client.AddNotify(@namespace, callback);
        }

        private string CreateKey (string @namespace, string part)
        {
            string hash_key = String.Concat (@namespace, part);
            lock (((ICollection)key_table).SyncRoot) {
                if (!key_table.ContainsKey (hash_key)) {
                    part = part.Replace ('/', '_');
                    if (@namespace == null) {
                        key_table.Add (hash_key, String.Concat (BaseKey, StringUtil.CamelCaseToUnderCase (part)));
                    } else if (@namespace.StartsWith ("/")) {
                        key_table.Add (hash_key, String.Concat (@namespace,
                            @namespace.EndsWith ("/") ? String.Empty : "/", StringUtil.CamelCaseToUnderCase (part)));
                    } else {
                        @namespace = @namespace.Replace ('/', '_');
                        key_table.Add (hash_key, String.Concat (BaseKey,
                            StringUtil.CamelCaseToUnderCase (String.Concat (@namespace.Replace ('.', '/'), "/", part))
                        ));
                    }

                    key_table[hash_key] = key_table[hash_key].Replace (' ', '_');
                }

                return key_table[hash_key];
            }
        }

        public T Get<T> (SchemaEntry<T> entry)
        {
            return Get<T> (entry.Namespace, entry.Key, entry.DefaultValue);
        }

        public T Get<T> (SchemaEntry<T> entry, T fallback)
        {
            return Get<T> (entry.Namespace, entry.Key, fallback);
        }

        public T Get<T> (string key, T fallback)
        {
            return Get<T> (null, key, fallback);
        }

        public T Get<T> (string @namespace, string key, T fallback)
        {
            if (DisableGConf || key == null) {
                return fallback;
            }

            if (client == null) {
                client = new GConf.Client ();
            }

            try {
                return (T)client.Get (CreateKey (@namespace, key));
            } catch (GConf.NoSuchKeyException) {
                return fallback;
            } catch (Exception e) {
                Log.Exception (String.Format ("Could no read GConf key {0}.{1}", @namespace, key), e);
                return fallback;
            }
        }

        public void Set<T> (SchemaEntry<T> entry, T value)
        {
            Set<T> (entry.Namespace, entry.Key, value);
        }

        public void Set<T> (string key, T value)
        {
            Set<T> (null, key, value);
        }

        public void Set<T> (string @namespace, string key, T value)
        {
            if (DisableGConf || key == null) {
                return;
            }

            if (client == null) {
                client = new GConf.Client ();
            }

            client.Set (CreateKey (@namespace, key), value);
        }
    }
}
