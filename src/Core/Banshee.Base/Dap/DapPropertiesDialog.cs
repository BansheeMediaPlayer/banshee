/***************************************************************************
 *  DapPropertiesDialog.cs
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@abock.org>
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */

using System;
using Mono.Unix;
using Gtk;

using Banshee.Base;
using Banshee.Widgets;
using Banshee.Sources;
using Banshee.AudioProfiles.Gui;

namespace Banshee.Dap
{
    public class DapPropertiesDialog : Dialog
    {
        private DapSource source;
        private DapDevice device;
        private Entry nameEntry;
        private Entry ownerEntry;
        
        private string name_update;
        private string owner_update;
        
        private bool edited;
        
        public DapPropertiesDialog(DapSource source) : base(
            // Translators: {0} is the name assigned to a Digital Audio Player by its owner
            String.Format(Catalog.GetString("{0} Properties"), source.Device.Name),
            null,
            DialogFlags.Modal | DialogFlags.NoSeparator,
            Stock.Close,
            ResponseType.Close)
        {
            this.source = source;
            this.device = source.Device;
            
            HBox iconbox = new HBox();
            iconbox.Spacing = 10;
            Image icon = new Image();
            icon.Yalign = 0.0f;
            icon.Pixbuf = device.GetIcon(48);
            iconbox.PackStart(icon, false, false, 0);
            
            VBox box = new VBox();
            box.Spacing = 10;
            
            PropertyTable table = new PropertyTable();
            table.ColumnSpacing = 10;
            table.RowSpacing = 5;
            
            if(!device.IsReadOnly && device.CanSetName) {
                nameEntry = table.AddEntry(Catalog.GetString("Device name"), device.Name);
                nameEntry.Changed += OnEntryChanged;
            } else {
                table.AddLabel(Catalog.GetString("Device name"), device.Name);
            }
                        
            if(!device.IsReadOnly && device.CanSetOwner) {
                ownerEntry = table.AddEntry(Catalog.GetString("Owner name"), device.Owner);
                ownerEntry.Changed += OnEntryChanged;
            } else {
                table.AddLabel(Catalog.GetString("Owner name"), device.Owner);
            }
    
            if(!device.IsReadOnly) {   
                try {
                    VBox profile_description_box = new VBox();
                    ProfileComboBoxConfigurable profile_box = new ProfileComboBoxConfigurable(Globals.AudioProfileManager, 
                        device.ID, profile_description_box);
                    profile_box.Combo.MimeTypeFilter = device.SupportedPlaybackMimeTypes;
                    table.AddWidget(Catalog.GetString("Encode to"), profile_box);
                    table.AddWidget(null, profile_description_box);
                    profile_description_box.Show();
                
                    table.AddSeparator();
                } catch(Exception e) {
                    Console.WriteLine(e);
                }
            }
            
            table.AddWidget(Catalog.GetString("Volume usage"), UsedProgressBar);
    
            box.PackStart(table, true, true, 0);
            
            PropertyTable extTable = new PropertyTable();
            extTable.ColumnSpacing = 10;
            extTable.RowSpacing = 5;
            
            foreach(DapDevice.Property property in device.Properties) {
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
        
        private ProgressBar UsedProgressBar {
            get {
                ProgressBar usedBar = new ProgressBar();
                usedBar.Fraction = source.DiskUsageFraction;
                usedBar.Text = source.DiskUsageString + " " + source.DiskAvailableString;
                return usedBar;
            }
        }
        
        private void OnEntryChanged(object o, EventArgs args)
        {
            if(ownerEntry != null) {
                owner_update = ownerEntry.Text;
            }
            
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
                return owner_update;
            }
        }
    }
}
