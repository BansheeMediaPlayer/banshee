//
// ColumnCell.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2007 Novell, Inc.
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
using Gtk;
using Cairo;

namespace Hyena.Data.Gui
{
    public abstract class ColumnCell
    {
        private bool expand;
        private bool sensitive;
        private string property, sub_property;
        private PropertyInfo property_info, sub_property_info;
        private object bound_object;
        private object bound_object_parent;
            
        public ColumnCell (string property, bool expand)
        {
            Property = property;
            Expand = expand;
            Sensitive = true;
        }

        public void BindListItem (object item)
        {
            if (item == null) {
                bound_object_parent = null;
                bound_object = null;
                return;
            }
            
            bound_object_parent = item;
            
            if (property != null) {
                EnsurePropertyInfo ();
                bound_object = property_info.GetValue (bound_object_parent, null);

                if (sub_property != null) {
                    EnsurePropertyInfo (sub_property, ref sub_property_info, bound_object);
                    bound_object = sub_property_info.GetValue (bound_object, null);
                }
            } else {
                bound_object = bound_object_parent;
            }
        }

        private void EnsurePropertyInfo ()
        {
            EnsurePropertyInfo (property, ref property_info, bound_object_parent);
        }
        
        private void EnsurePropertyInfo (string name, ref PropertyInfo prop, object obj)
        {
            if (prop == null || prop.ReflectedType != obj.GetType ()) {
                prop = obj.GetType ().GetProperty (name);
                if (prop == null) {
                    throw new Exception (String.Format (
                        "In {0}, type {1} does not have property {2}",
                        this, obj.GetType (), name
                    ));
                }
            }
        }
        
        public virtual void NotifyThemeChange ()
        {
        }
        
        protected Type BoundType {
            get { return bound_object.GetType (); }
        }
        
        protected object BoundObject {
            get { return bound_object; }
            set {
                if (property != null) {
                    EnsurePropertyInfo ();
                    property_info.SetValue (bound_object_parent, value, null);
                }
            }
        }
        
        protected object BoundObjectParent {
            get { return bound_object_parent; }
        }
        
        public abstract void Render (CellContext context, StateType state, double cellWidth, double cellHeight);
        
        public bool Expand {
            get { return expand; }
            set { expand = value; }
        }
        
        public string Property {
            get { return property; }
            set {
                property = value;
                if (value != null) {
                    int i = value.IndexOf (".");
                    if (i != -1) {
                        property = value.Substring (0, i);
                        SubProperty = value.Substring (i + 1, value.Length - i - 1);
                    }
                }
            }
        }

        public bool Sensitive { 
            get { return sensitive; } 
            set { sensitive = value; }
        }

        public string SubProperty {
            get { return sub_property; }
            set { sub_property = value; }
        }
    }
}
