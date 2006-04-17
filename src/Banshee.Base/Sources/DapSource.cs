/***************************************************************************
 *  DapSource.cs
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
using System.Collections;
using Mono.Unix;
using Gtk;
using Gdk;

using Banshee.Base;
using Banshee.Dap;

namespace Banshee.Sources
{
    public class DapSource : Source
    {
        private Banshee.Dap.DapDevice device;
        private EventBox syncing_container;
        private Gtk.Image dap_syncing_image = new Gtk.Image();
        
        public bool NeedSync;
        
        public DapSource(Banshee.Dap.DapDevice device) : base(device.Name, 400)
        {
            this.device = device;
            device.SaveStarted += OnDapSaveStarted;
            device.SaveFinished += OnDapSaveFinished;
            device.Reactivate += OnDapReactivate;
            CanRename = device.CanSetName;
        }
        
        protected override bool UpdateName(string oldName, string newName)
        {
            if(!device.CanSetName) {
                return true;
            }
        
            if(oldName == null || !oldName.Equals(newName)) {
                device.SetName(newName);
                Name = newName;
            }
            
            return true;
        }
        
        public override void AddTrack(TrackInfo track)
        {
            device.Activate();
            
            if(track is LibraryTrackInfo) {
                device.AddTrack(track);
            }
        }
        
        public override void RemoveTrack(TrackInfo track)
        {
            device.RemoveTrack(track);
        }
        
        public void SetSourceName(string name)
        {
            Name = name;
        }
        
        private void OnDapSaveStarted(object o, EventArgs args)
        {
            if(IsSyncing) {
                if(syncing_container == null) {
                    syncing_container = new EventBox();
                    HBox syncing_box = new HBox();
                    syncing_container.Add(syncing_box);
                    syncing_box.Spacing = 20;
                    syncing_box.PackStart(dap_syncing_image, false, false, 0);
                    Label syncing_label = new Label();
                                                    
                    syncing_container.ModifyBg(StateType.Normal, new Color(0, 0, 0));
                    syncing_label.ModifyFg(StateType.Normal, new Color(160, 160, 160));
                
                    syncing_label.Markup = "<big><b>" + GLib.Markup.EscapeText(
                        Catalog.GetString("Synchronizing your Device, Please Wait...")) + "</b></big>";
                    syncing_box.PackStart(syncing_label, false, false, 0);
                    syncing_container.ShowAll ();
                }
                
                dap_syncing_image.Pixbuf = device.GetIcon(128);
                Globals.ActionManager.DapActions.Sensitive = false;
            }
            
            OnViewChanged();
        }
        
        private void OnDapSaveFinished(object o, EventArgs args)
        {
            if(!IsSyncing) {
                Globals.ActionManager.DapActions.Sensitive = true;
            }
            
            OnViewChanged();
        }
        
        private void OnDapReactivate(object o, EventArgs args)
        {
            OnViewChanged();
            OnUpdated();
        }
        
        public override void Activate()
        {
            device.Activate();
            base.Activate();
        }

        public override int Count {
            get {
                return device.TrackCount;
            }
        }        
        
        public Banshee.Dap.DapDevice Device {
            get {
                return device;
            }
        }
        
        public override IEnumerable Tracks {
            get {
                return device.Tracks;
            }
        }
        
        public override bool Eject()
        {
            device.Eject();
            return true;
        }

        public double DiskUsageFraction {
            get {
                return (double)device.StorageUsed / (double)device.StorageCapacity;
            }
        }

        public string DiskUsageString {
            get {
                // Translators: iPod disk usage. Each {N} is something like "100 MB"
                return String.Format(
                    Catalog.GetString("{0} of {1}"),
                    Utilities.BytesToString(device.StorageUsed),
                    Utilities.BytesToString(device.StorageCapacity));
            }
        }

        public string DiskAvailableString {
            get {
                // Translators: iPod disk usage. {0} is something like "100 MB"
                return String.Format(
                    Catalog.GetString("({0} Remaining)"),
                    Utilities.BytesToString(device.StorageCapacity - device.StorageUsed));
            }
        }
        
        public bool IsSyncing {
            get {
                return device.IsSyncing;
            }
        }

        public override void ShowPropertiesDialog()
        {
            DapPropertiesDialog properties_dialog = new DapPropertiesDialog(this);
            IconThemeUtils.SetWindowIcon(properties_dialog);
            properties_dialog.Run();
            properties_dialog.Destroy();
              
            if(properties_dialog.Edited && !Device.IsReadOnly) {
                if(properties_dialog.UpdatedName != null) {
                    Device.SetName(properties_dialog.UpdatedName);
                    SetSourceName(Device.Name);
                }
                  
                if(properties_dialog.UpdatedOwner != null) {
                    Device.SetOwner(properties_dialog.UpdatedOwner);
                }
            }
        }
        
        public override Gdk.Pixbuf Icon {
            get {
                return device.GetIcon(22);
            }
        }
        
        public override Gtk.Widget ViewWidget {
            get {
                return IsSyncing ? syncing_container : Device.ViewWidget;
            }
        }
        
        public override bool ShowPlaylistHeader {
            get {
                return !IsSyncing;
            }
        }
        
        public override bool SearchEnabled {
            get {
                return !IsSyncing && Device.ViewWidget == null;
            }
        }
    }
}
