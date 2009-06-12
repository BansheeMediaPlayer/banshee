//
// EqualizerPresetComboBox.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2006-2007 Novell, Inc.
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
using Gtk;

namespace Banshee.Equalizer.Gui
{
    public class EqualizerPresetComboBox : Gtk.ComboBoxEntry
    {
        private EqualizerManager manager;
        private ListStore store;
        private EqualizerSetting active_eq;
        
        public EqualizerPresetComboBox () : this (EqualizerManager.Instance)
        {
        }
        
        public EqualizerPresetComboBox (EqualizerManager manager) : base ()
        {
            if (manager == null) {
                throw new ArgumentNullException ("provide an EqualizerManager or use default constructor");
            }
            
            this.manager = manager;
            BuildWidget ();
        }
        
        private void BuildWidget ()
        {
            store = new ListStore (typeof (string), typeof (EqualizerSetting));
            Model = store;
            TextColumn = 0;
            
            foreach (EqualizerSetting eq in manager) {
                AddEqualizerSetting(eq);
            }
            
            manager.EqualizerAdded += delegate (object o, EqualizerSettingEventArgs args) {
                AddEqualizerSetting (args.EqualizerSetting);
            };
            
            manager.EqualizerRemoved += delegate (object o, EqualizerSettingEventArgs args) {
                RemoveEqualizerSetting (args.EqualizerSetting);
            };
        }
        
        protected override void OnChanged ()
        {
            EqualizerSetting eq = ActiveEqualizer;
            if (eq != null) {
                active_eq = eq;
            } else if (eq == null && active_eq == null) {
                base.OnChanged ();
                return;
            } else if (eq == null) {
                eq = active_eq;
            }
            
            if (Entry == null) {
                return;
            }

            eq.Name = Entry.Text;
            
            TreeIter iter;
            if (GetIterForEqualizerSetting (eq, out iter)) {
                store.SetValue (iter, 0, eq.Name);
            }
            
            if (eq != ActiveEqualizer) {
                ActiveEqualizer = eq;
                base.OnChanged ();
            }
        }
        
        public bool ActivatePreferredEqualizer (string name)
        {
            if (name == null || name == "") {
                return ActivateFirstEqualizer ();
            } else {
                foreach (EqualizerSetting eq in manager) {
                    if (eq.Name == name) {
                        ActiveEqualizer = eq;
                        return true;
                    }
                }
                
                return false;
            }
        }
        
        public bool ActivateFirstEqualizer ()
        {
            TreeIter iter;
        
            if (store.IterNthChild (out iter, 0)) {
                ActiveEqualizer = GetEqualizerSettingForIter (iter);
                return true;
            }
            
            return false;
        }
        
        private void AddEqualizerSetting (EqualizerSetting eq)
        {
            store.AppendValues (eq.Name, eq);
        }
        
        private void RemoveEqualizerSetting (EqualizerSetting eq)
        {
            TreeIter iter;
            if (GetIterForEqualizerSetting (eq, out iter)) {
                store.Remove (ref iter);
            }
            
            if (!ActivateFirstEqualizer ()) {
                active_eq = null;
                if (Entry != null) {
                    Entry.Text = String.Empty;
                }
            }
        }
        
        private EqualizerSetting GetEqualizerSettingForIter (TreeIter iter)
        {
            return store.GetValue (iter, 1) as EqualizerSetting;
        }
        
        private bool GetIterForEqualizerSetting (EqualizerSetting eq, out TreeIter iter)
        {
            for (int i = 0, n = store.IterNChildren (); i < n; i++) {
                if (store.IterNthChild (out iter, i) && store.GetValue (iter, 1) == eq) {
                    return true;
                }
            }
            
            iter = TreeIter.Zero;
            return false;
        }
        
        public EqualizerSetting ActiveEqualizer {
            get {
                TreeIter iter;
                return GetActiveIter (out iter) ? GetEqualizerSettingForIter (iter) : null;
            }
            
            set {
                active_eq = value;
                
                TreeIter iter;
                if (GetIterForEqualizerSetting (value, out iter)) {
                    SetActiveIter (iter);
                }
            }
        }
    }
}
