//
// QueryValue.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2007-2008 Novell, Inc.
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
using System.Xml;
using System.Text;

using Hyena;

namespace Hyena.Data.Query
{
    public abstract class QueryValue
    {
        private static Type [] subtypes = new Type [] {typeof(StringQueryValue), typeof(IntegerQueryValue), typeof(FileSizeQueryValue), typeof(DateQueryValue)};

        public static QueryValue CreateFromUserQuery (string input, QueryField field)
        {
            QueryValue val = (field == null) ? new StringQueryValue () : Activator.CreateInstance (field.ValueType) as QueryValue;
            val.ParseUserQuery (input);
            return val;
        }

        public static QueryValue CreateFromXml (XmlElement parent, QueryField field)
        {
            if (field != null) {
                QueryValue val = Activator.CreateInstance (field.ValueType) as QueryValue;
                return CreateFromXml (val, parent) ? val : null;
            } else {
                foreach (Type subtype in subtypes) {
                    QueryValue val = Activator.CreateInstance (subtype) as QueryValue;
                    if (CreateFromXml (val, parent)) {
                        return val;
                    }
                }
            }
            return null;
        }

        private static bool CreateFromXml (QueryValue val, XmlElement parent)
        {
            XmlElement val_node = parent[val.XmlElementName];
            if (val_node != null) {
                val.ParseXml (val_node);
                return !val.IsEmpty;
            }
            return false;
        }

        private bool is_empty = true;
        public bool IsEmpty {
            get { return is_empty; }
            protected set { is_empty = value; }
        }

        public abstract object Value { get; }
        public abstract string XmlElementName { get; }

        public virtual void AppendXml (XmlElement node)
        {
            node.InnerText = Value.ToString ();
        }

        public virtual string ToUserQuery ()
        {
            return Value.ToString ();
        }

        public virtual string ToSql ()
        {
            return Value.ToString ();
        }

        public abstract void ParseUserQuery (string input);
        public abstract void ParseXml (XmlElement node);
    }
}
