//
// SchemaPreference.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
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

using Banshee.Configuration;

namespace Banshee.Preferences
{
    public delegate void SchemaPreferenceUpdatedHandler ();

    public class SchemaPreference<T> : Preference<T>
    {
        private SchemaEntry<T> schema;
        private SchemaPreferenceUpdatedHandler handler;
        
        public SchemaPreference (SchemaEntry<T> schema, string name) : this (schema, name, null)
        {
        }
        
        public SchemaPreference (SchemaEntry<T> schema, string name, string description) 
            : this (schema, name, description, null)
        {
        }
        
        public SchemaPreference (SchemaEntry<T> schema, string name, string description, SchemaPreferenceUpdatedHandler handler) 
            : base (schema.Key, name, description)
        {
            this.schema = schema;
            this.handler = handler;
        }
        
        public override T Value {
            get { return schema.Get (); }
            set { 
                if (!schema.Set (value)) {
                    return;
                }
                if (handler != null) {
                    handler ();
                }
                OnValueChanged ();
            }
        }

        public T MinValue {
            get { return schema.MinValue; }
        }

        public T MaxValue {
            get { return schema.MaxValue; }
        }
    }
}
