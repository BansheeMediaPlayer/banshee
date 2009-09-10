//
// SchemaEntry.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2005-2008 Novell, Inc.
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

namespace Banshee.Configuration
{
    public struct SchemaEntry<T> : IEquatable<SchemaEntry<T>>
    {
        public static SchemaEntry<T> Zero;
    
        public SchemaEntry (string @namespace, string key, T defaultValue, 
            string shortDescription, string longDescription) :
            this (@namespace, key, defaultValue, default(T), default(T), shortDescription, longDescription)
        {
        }

        public SchemaEntry (string @namespace, string key, T defaultValue, T minValue, T maxValue,
            string shortDescription, string longDescription)
        {
            Namespace = @namespace;
            Key = key;
            DefaultValue = defaultValue;
            MinValue = minValue;
            MaxValue = maxValue;
            ShortDescription = shortDescription;
            LongDescription = longDescription;
        }

        public T Get ()
        {
            return ConfigurationClient.Get<T> (this);
        }

        public T Get (T fallback)
        {
            return ConfigurationClient.Get<T> (this, fallback);
        }

        public bool Set (T value)
        {
            if (!Object.Equals (MinValue, default (T)) || !Object.Equals(MaxValue, default (T))) {
                if (((IComparable<T>)MinValue).CompareTo (value) > 0 ||
                    ((IComparable<T>)MaxValue).CompareTo (value) < 0) {
                    return false;
                }
            }

            ConfigurationClient.Set<T> (this, value);
            return true;
        }

        public readonly string Namespace;
        public readonly string Key;
        public readonly T DefaultValue;
        public readonly T MinValue;
        public readonly T MaxValue;
        public readonly string ShortDescription;
        public readonly string LongDescription;
        
        public bool Equals (SchemaEntry<T> entry)
        {
            return Namespace == entry.Namespace && Key == entry.Key;
        }
        
        public override bool Equals (object o)
        {
            return (o is SchemaEntry<T>) && Equals ((SchemaEntry<T>)o);
        }
        
        public override int GetHashCode ()
        {
            return Namespace.GetHashCode () ^ Key.GetHashCode ();
        }
    }
}
