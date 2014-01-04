//
// ProfileComboBoxConfigurable.cs
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
using Gtk;

using Hyena.Widgets;
using Banshee.Preferences.Gui;

namespace Banshee.MediaProfiles.Gui
{
    public class ProfileComboBoxConfigurable : VBox
    {
        private ProfileComboBox combo;
        private ProfileConfigureButton button;
        private DescriptionLabel description;
        private string configuration_id;

        public ProfileComboBoxConfigurable(MediaProfileManager manager, string configurationId)
            : this(manager, configurationId, null)
        {
        }

        public ProfileComboBoxConfigurable(MediaProfileManager manager, string configurationId, Box parent)
        {
            HBox editor = new HBox();

            configuration_id = configurationId;
            combo = new ProfileComboBox(manager);
            combo.Show();

            button = new ProfileConfigureButton(configurationId);
            button.ComboBox = combo;
            button.Show();

            editor.Spacing = 5;
            editor.PackStart(combo, true, true, 0);
            editor.PackStart(button, false, false, 0);
            editor.Show();

            ProfileConfiguration config = manager.GetActiveProfileConfiguration (configurationId);

            if (config != null) {
                Combo.SetActiveProfile(config.Profile);
            }

            description = new DescriptionLabel (delegate {
                var profile = Combo.ActiveProfile;
                return profile != null ? profile.Description : "";
            });

            Combo.Changed += delegate {
                if(Combo.ActiveProfile != null) {
                    Hyena.Log.DebugFormat ("Setting active encoding profile: {0} (saved to {1})",
                        Combo.ActiveProfile.Name, configurationId);
                    ProfileConfiguration.SaveActiveProfile (Combo.ActiveProfile, configurationId);
                    description.Update ();
                }
            };

            Combo.StateChanged += delegate {
                if (Combo.State == StateType.Insensitive && description.Parent != null) {
                    ((Container)parent ?? this).Remove (description);
                } else if (description.Parent == null) {
                    description.PackInto (parent ?? this, parent != null);
                }
            };

            Spacing = 5;
            PackStart (editor, true, true, 0);
            description.PackInto (parent ?? this, parent != null);
        }

        public ProfileComboBox Combo {
            get { return combo; }
        }

        public string ConfigurationID {
            get { return configuration_id; }
        }
    }
}
