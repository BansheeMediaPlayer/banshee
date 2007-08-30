//
// PropertyStore.cs
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
using System.Collections.Generic;

namespace Hyena.Data
{
    public delegate void PropertyChangeEventHandler(object o, PropertyChangeEventArgs args);
    
    public class PropertyChangeEventArgs : EventArgs
    {
        private string property_name;
        private bool added;
        private bool removed;
        private object old_value;
        private object new_value;
        
        public PropertyChangeEventArgs(string propertyName, bool added, bool removed, object oldValue, object newValue)
        {
            this.property_name = propertyName;
            this.added = added;
            this.removed = removed;
            this.old_value = oldValue;
            this.new_value = newValue;
        }
        
        public string PropertyName {
            get { return property_name; }
        }
        
        public bool Added {
            get { return added; }
        }
        
        public bool Removed {
            get { return removed; }
        }
        
        public object OldValue {
            get { return old_value; }
        }
        
        public object NewValue {
            get { return new_value; }
        }
    }
    
    public class PropertyStore
    {
        private Dictionary<string, object> object_store;
        
        public event PropertyChangeEventHandler PropertyChanged;
        
        public PropertyStore()
        {
        }
        
        protected virtual void OnPropertyChanged(string propertyName, bool added, bool removed, 
            object oldValue, object newValue)
        {
            PropertyChangeEventHandler handler = PropertyChanged;
            if(handler != null) {
                PropertyChangeEventArgs args = new PropertyChangeEventArgs(propertyName, 
                    added, removed, oldValue, newValue);
                handler(this, args);
            }
        }
            
        public void Remove(string name)
        {
            lock(this) {
                if(object_store.ContainsKey(name)) {
                    object old_value = object_store[name];
                    object_store.Remove(name);
                    OnPropertyChanged(name, false, true, old_value, null);
                }
            }
        }
        
        public void Set<T>(string name, T value)
        {
            lock(this) {
                bool added = false;
                object old_value = null;
                
                if(object_store == null) {
                    object_store = new Dictionary<string, object>();
                }
                
                if(object_store.ContainsKey(name)) {
                    old_value = object_store[name];
                    object_store[name] = value;
                } else {
                    added = true;
                    object_store.Add(name, value);
                }
                
                OnPropertyChanged(name, added, false, old_value, value);
            }
        }
        
        public T Get<T>(string name)
        {
            lock(this) {
                if(object_store != null && object_store.ContainsKey(name)) {
                    return (T)object_store[name];
                }
            
                return default(T);
            }
        }
        
        public int GetInteger(string name)
        {
            return Get<int>(name);
        }
        
        public void SetInteger(string name, int value)
        {
            Set<int>(name, value);
        }
        
        public string GetString(string name)
        {
            return Get<string>(name);
        }
        
        public void SetString(string name, string value)
        {
            Set<string>(name, value);
        }
        
        public bool GetBoolean(string name)
        {
            return Get<bool>(name);
        }
        
        public void SetBoolean(string name, bool value)
        {
            Set<bool>(name, value);
        }
        
        public bool Contains(string name)
        {
            lock(this) {
                return object_store != null && object_store.ContainsKey(name);
            }
        }
    }
}
