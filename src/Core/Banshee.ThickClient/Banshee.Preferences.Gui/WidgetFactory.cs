//
// WidgetFactory.cs
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
using System.Reflection;
using Gtk;

using Banshee.Preferences;

namespace Banshee.Preferences.Gui
{
    public static class WidgetFactory
    {
        public static Widget GetWidget (PreferenceBase preference)
        {
            if (preference == null) {
                return null;
            }

            Widget widget = preference.DisplayWidget as Widget;
            //OnPreferenceChanged (preference);
            
            return widget ?? GetWidget (preference, preference.GetType ().GetProperty ("Value").PropertyType);
        }
        
        private static Widget GetWidget (PreferenceBase preference, Type type)
        {
            Widget pref_widget = null;
            Widget widget = null;
            if (type == typeof (bool)) {
                pref_widget = new PreferenceCheckButton (preference);
            } else if (type == typeof (string)) {
                pref_widget = new PreferenceEntry (preference);
            } else if (type == typeof (int)) {
                var schema_preference = preference as SchemaPreference<int>;
                if (schema_preference == null) {
                    pref_widget = new PreferenceSpinButton (preference);
                } else {
                    pref_widget = new PreferenceSpinButton (preference, schema_preference.MinValue, schema_preference.MaxValue);
                }
            }

            if (pref_widget != null) {
                pref_widget.Sensitive = preference.Sensitive;
                pref_widget.Visible = preference.Visible;

                DescriptionLabel label = null;
                if (preference.ShowDescription) {
                    VBox box = new VBox ();
                    box.PackStart (pref_widget, false, false, 0);
                    label = new DescriptionLabel (preference.Description);
                    label.Visible = !String.IsNullOrEmpty (preference.Description);
                    label.PackInto (box, false);
                    widget = box;
                }

                preference.Changed += delegate (Root pref) {
                    Banshee.Base.ThreadAssist.ProxyToMain (delegate {
                        pref_widget.Sensitive = pref.Sensitive;
                        pref_widget.Visible = pref.Visible;
                        /*if (label != null) {
                            label.Text = pref.Description;
                            label.Visible = !String.IsNullOrEmpty (preference.Description);
                        }*/

                        if (pref_widget is PreferenceCheckButton) {
                            (pref_widget as PreferenceCheckButton).Label = pref.Name;
                        }
                    });
                };
            }
            
            return widget ?? pref_widget;
        }

        public static Widget GetMnemonicWidget (PreferenceBase preference)
        {
            if (preference == null) {
                return null;
            }
            
            return preference.MnemonicWidget as Widget;
        }
        
        private class PreferenceCheckButton : CheckButton
        {
            private bool sync;
            private PreferenceBase preference;
            
            public PreferenceCheckButton (PreferenceBase preference)
            {
                this.preference = preference;
                Label = preference.Name;
                UseUnderline = true;
                Active = (bool)preference.BoxedValue;
                sync = true;
            }
            
            protected override void OnToggled ()
            {
                base.OnToggled ();
                
                if (sync) {
                    preference.BoxedValue = Active;
                }
            }
        }
        
        private class PreferenceEntry : Entry
        {
            private bool sync;
            private PreferenceBase preference;
            
            public PreferenceEntry (PreferenceBase preference)
            {
                this.preference = preference;
                string value = (string)preference.BoxedValue;
                Text = value ?? String.Empty;
                sync = true;
            }
            
            protected override void OnChanged ()
            {
                base.OnChanged ();
                
                if (sync) {
                    preference.BoxedValue = Text;
                }
            }
        }

        private class PreferenceSpinButton : HBox
        {
            private bool sync;
            private PreferenceBase preference;

            public PreferenceSpinButton (PreferenceBase preference) : this (preference, 0, 100)
            {
            }

            public PreferenceSpinButton (PreferenceBase preference, int min_value, int max_value)
            {
                var spin_button = new SpinButton (min_value, max_value, 1);
                spin_button.Changed += delegate {
                    if (sync) {
                        this.preference.BoxedValue = (int)spin_button.Value;
                    }
                };
                spin_button.Show ();
                PackEnd (spin_button, false, false, 0);
                this.preference = preference;
                spin_button.Value = (int)preference.BoxedValue;
                sync = true;
            }
        }
    }
}
