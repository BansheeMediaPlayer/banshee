//
// DapPropertiesDialog.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//   Gabriel Burt <gburt@novell.com>
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
using Mono.Unix;
using Gtk;

using Banshee.Base;
using Banshee.Widgets;
using Banshee.Sources;
using Banshee.ServiceStack;
using Banshee.MediaProfiles.Gui;

namespace Banshee.Dap
{
    public class DapPropertiesDialog : Dialog
    {
        private DapSource source;
        private Entry nameEntry;
        //private Entry ownerEntry;

        private string name_update;
        //private string owner_update;

        private bool edited;

        public DapPropertiesDialog(DapSource source) : base(
            // Translators: {0} is the name assigned to a Digital Audio Player by its owner
            String.Format(Catalog.GetString("{0} Properties"), source.Name),
            null,
            DialogFlags.Modal | DialogFlags.NoSeparator,
            Stock.Close,
            ResponseType.Close)
        {
            this.source = source;

            HBox iconbox = new HBox();
            iconbox.Spacing = 10;
            //Image icon = new Image (source.Properties.Get<string> ("Icon.Names")[0], IconSize.Dialog);
            //icon.Yalign = 0.0f;
            //icon.Pixbuf = device.GetIcon(48);
            //iconbox.PackStart(icon, false, false, 0);

            VBox box = new VBox();
            box.Spacing = 10;

            PropertyTable table = new PropertyTable();
            table.ColumnSpacing = 10;
            table.RowSpacing = 5;

            if(!source.IsReadOnly && source.CanRename) {
                nameEntry = table.AddEntry(Catalog.GetString("Device name"), source.Name);
                nameEntry.Changed += OnEntryChanged;
            } else {
                table.AddLabel(Catalog.GetString("Device name"), source.Name);
            }

            /*if(device.ShowOwner) {
                if(!device.IsReadOnly && device.CanSetOwner) {
                    ownerEntry = table.AddEntry(Catalog.GetString("Owner name"), device.Owner);
                    ownerEntry.Changed += OnEntryChanged;
                } else {
                    table.AddLabel(Catalog.GetString("Owner name"), device.Owner);
                }
            }*/

            if(!source.IsReadOnly) {
                try {
                    VBox profile_description_box = new VBox();
                    ProfileComboBoxConfigurable profile_box = new ProfileComboBoxConfigurable(ServiceManager.MediaProfileManager,
                        source.UniqueId, profile_description_box);
                    profile_box.Combo.MimeTypeFilter = source.AcceptableMimeTypes;
                    table.AddWidget(Catalog.GetString("Encode to"), profile_box);
                    table.AddWidget(null, profile_description_box);
                    profile_description_box.Show();

                    table.AddSeparator();
                } catch(Exception e) {
                    Console.WriteLine(e);
                }
            }

            table.AddWidget(Catalog.GetString("Capacity used"), UsedProgressBar);

            box.PackStart(table, true, true, 0);

            PropertyTable extTable = new PropertyTable();
            extTable.ColumnSpacing = 10;
            extTable.RowSpacing = 5;

            foreach(DapSource.DapProperty property in source.DapProperties) {
                extTable.AddLabel(property.Name, property.Value);
            }

            Expander expander = new Expander(Catalog.GetString("Advanced details"));
            expander.Add(extTable);
            box.PackStart(expander, false, false, 0);

            BorderWidth = 10;
            Resizable = false;
            iconbox.PackStart(box, true, true, 0);
            iconbox.ShowAll();
            VBox.Add(iconbox);
        }

        public void RunDialog ()
        {
            Run ();

            if (!String.IsNullOrEmpty (name_update)) {
                source.Rename (name_update);
            }

            Destroy ();
        }

        private ProgressBar UsedProgressBar {
            get {
                ProgressBar usedBar = new ProgressBar();
                double progress = (double)source.BytesUsed / (double)source.BytesCapacity;
                usedBar.Fraction = Math.Max (0.0, Math.Min (1.0, progress));
                usedBar.Text = String.Format (
                    Catalog.GetString("{0} of {1}"),
                    new Hyena.Query.FileSizeQueryValue (source.BytesUsed).ToUserQuery (),
                    new Hyena.Query.FileSizeQueryValue (source.BytesCapacity).ToUserQuery ()
                );
                return usedBar;
            }
        }

        private void OnEntryChanged(object o, EventArgs args)
        {
            /*if(ownerEntry != null) {
                owner_update = ownerEntry.Text;
            }*/

            if(nameEntry != null) {
                name_update = nameEntry.Text;
            }

            edited = true;
        }

        public bool Edited {
            get {
                return edited;
            }
        }

        public string UpdatedName {
            get {
                return name_update;
            }
        }

        public string UpdatedOwner {
            get {
                return null; //owner_update;
            }
        }
    }
}
