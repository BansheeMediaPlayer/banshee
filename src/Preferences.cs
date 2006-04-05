/***************************************************************************
 *  Preferences.cs
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
using System.Reflection;
using System.Collections;
using System.Threading;
using Mono.Unix;
using Gtk;
using Glade;

using Nautilus;
using Banshee.Widgets;
using Banshee.MediaEngine;
using Banshee.Base;

namespace Banshee
{
    public class PreferencesWindow
    {
        [Widget] private Window WindowPreferences;
        [Widget] private CheckButton CopyOnImport;
        [Widget] private RadioButton RadioImport;
        [Widget] private RadioButton RadioAppend;
        [Widget] private RadioButton RadioAsk;
        [Widget] private TreeView TreeMimeSynonyms;
        [Widget] private TreeView TreeDecoders;
        [Widget] private Notebook Notebook;
        
        private string oldLibraryLocation;
        
        private string selectedBurnerId;
        private string burnKeyParent;
        private BurnDrive selectedDrive;

        private Glade.XML glade;
        
        private Label driveLoadingLabel;
        private ComboBox burnerDrivesCombo;
        private ComboBox writeSpeedCombo;
        private HBox driveContainer;
        private HBox speedContainer;
        
        private PlayerEngine SelectedEngine;
        
        private FileChooserButton libraryLocationChooser;
        
        private BurnDrive [] burnDevices;
        
        private PipelineProfileSelector rippingProfile;
        private PipelineProfileSelector ipodProfile;

        public PreferencesWindow()
        {
            glade = new Glade.XML(null, "banshee.glade", "WindowPreferences", null);
            glade.Autoconnect(this);
            
            ((Image)glade["ImageLibraryTab"]).Pixbuf = 
                Gdk.Pixbuf.LoadFromResource("library-icon-32.png");

            ((Image)glade["ImageEncodingTab"]).Pixbuf = 
                Gdk.Pixbuf.LoadFromResource("encoding-icon-32.png");
                
            ((Image)glade["ImageBurningTab"]).Pixbuf = 
                Gdk.Pixbuf.LoadFromResource("cd-action-burn-32.png");
                
            ((Image)glade["ImageAdvancedTab"]).Pixbuf = 
                Gdk.Pixbuf.LoadFromResource("advanced-icon-32.png");
                    
            IconThemeUtils.SetWindowIcon(WindowPreferences);
                    
            libraryLocationChooser = new FileChooserButton(
                Catalog.GetString("Select Library Location"), 
                FileChooserAction.SelectFolder);
            libraryLocationChooser.SelectionChanged += OnLibraryLocationChooserCurrentFolderChanged;
            (glade["LibraryLocationChooserContainer"] as Container).Add(libraryLocationChooser);
            libraryLocationChooser.Show();
          
            LoadPreferences();
            LoadPlayerEngines();
            
            driveContainer = glade["DriveComboContainer"] as HBox;
            speedContainer = glade["SpeedComboContainer"] as HBox;
                
            writeSpeedCombo = ComboBox.NewText();
            speedContainer.PackStart(writeSpeedCombo, false, false, 0);
            speedContainer.ShowAll();
            LoadBurnerDrives();

            WindowPreferences.Show();
            
            rippingProfile = new PipelineProfileSelector();
            (glade["RippingProfileContainer"] as HBox).PackStart(rippingProfile, true, true, 0);
                
            rippingProfile.ProfileKey = GetStringPref(GConfKeys.RippingProfile, "default");
            rippingProfile.Bitrate = GetIntPref(GConfKeys.RippingBitrate, -1);
                
            rippingProfile.Show();
            
            ipodProfile = new PipelineProfileSelector("mp3,aac,mp4,m4a,m4p");
            (glade["IpodProfileContainer"] as HBox).PackStart(ipodProfile, true, true, 0);
                
            ipodProfile.ProfileKey = GetStringPref(GConfKeys.IpodProfile, "default");
            ipodProfile.Bitrate = GetIntPref(GConfKeys.IpodBitrate, -1);
                
            ipodProfile.Show();
        }
        
        private bool GetBoolPref(string key, bool def)
        {
            try {
                return (bool)Globals.Configuration.Get(key);
            } catch(Exception) {
                return def;
            }
        }
        
        private int GetIntPref(string key, int def)
        {
            try {
                return (int)Globals.Configuration.Get(key);
            } catch(Exception) {
                return def;
            }
        }
        
        private string GetStringPref(string key, string def)
        {
            try {
                return (string)Globals.Configuration.Get(key);
            } catch(Exception) {
                return def;
            }
        }
        
        private void OnButtonCancelClicked(object o, EventArgs args)
        {
            WindowPreferences.Destroy();
        }
        
        private void OnButtonOkClicked(object o, EventArgs args)
        {
            SavePreferences();
            WindowPreferences.Destroy();
        }
        
        private void OnLibraryLocationChooserCurrentFolderChanged(object o, EventArgs args)
        {
        }
        
        private void OnButtonLibraryResetClicked(object o, EventArgs args)
        {
            libraryLocationChooser.SetFilename(Paths.DefaultLibraryPath);
        }
        
        private void LoadPreferences()
        {
            oldLibraryLocation = Paths.DefaultLibraryPath;
            
            try {
                oldLibraryLocation = (string)Globals.Configuration.Get(
                        GConfKeys.LibraryLocation);
            } catch(Exception) { }
            
            libraryLocationChooser.SetFilename(oldLibraryLocation);
            
            try {
                CopyOnImport.Active = (bool)Globals.Configuration.Get(
                    GConfKeys.CopyOnImport);
            } catch(Exception) {
                CopyOnImport.Active = false;
            }    
            
            try {
                selectedBurnerId = (string)Globals.Configuration.Get(
                    GConfKeys.CDBurnerId);
            } catch(Exception) {}
        }
        
        private void SavePreferences()
        {
            //string newLibraryLocation = LibraryLocationEntry.Buffer.Text;
            string newLibraryLocation = libraryLocationChooser.Filename;
        
            if(!oldLibraryLocation.Trim().Equals(newLibraryLocation.Trim())) {
                Globals.Configuration.Set(GConfKeys.LibraryLocation,
                    newLibraryLocation);
                // TODO: Move Library Directory?
            }
            
            Globals.Configuration.Set(GConfKeys.CopyOnImport,
                CopyOnImport.Active);
                
              
            if(rippingProfile != null) {
              try { 
              Globals.Configuration.Set(GConfKeys.RippingProfile,
                rippingProfile.ProfileKey);
                
              Globals.Configuration.Set(GConfKeys.RippingBitrate,
                rippingProfile.Bitrate);
              } catch(Exception) {}
            }
            
            if(ipodProfile != null) {
               try {
                Globals.Configuration.Set(GConfKeys.IpodProfile,
                ipodProfile.ProfileKey);
                
              Globals.Configuration.Set(GConfKeys.IpodBitrate,
                ipodProfile.Bitrate);
                } catch(Exception) {}
            }
            
            SaveBurnSettings();
            SaveEngineSettings();
        }
        
        private bool loading_drives = true;
        
        private void ThreadLoadBurnerDrives()
        {
            burnDevices = BurnUtil.GetDrives();
            loading_drives = false;
        }
        
        private void LoadBurnerDrives()
        {    
            if(burnerDrivesCombo != null) {
                driveContainer.Remove(burnerDrivesCombo);
                burnerDrivesCombo = null;
            }

            while(writeSpeedCombo.Model.IterNChildren() > 0)
                writeSpeedCombo.RemoveText(0);
    
            writeSpeedCombo.AppendText(Catalog.GetString("Unavailable"));
            writeSpeedCombo.Active = 0;
            writeSpeedCombo.Sensitive = false;

            driveLoadingLabel = new Label();
            driveLoadingLabel.Ypad = 7;
            driveLoadingLabel.Markup = "<i>" + Catalog.GetString("Loading Drive List...") + "</i>";
            driveLoadingLabel.Xalign = 0.0f;
            driveContainer.PackStart(driveLoadingLabel, false, false, 0);
            driveContainer.ShowAll();
            
            SensitizeBurnerWidgets(false);
            
            loading_drives = true;
            Thread th = new Thread(new ThreadStart(ThreadLoadBurnerDrives));
            th.Start();
            
            GLib.Idle.Add(OnWaitForBurnDrives);
        }
        
        private bool OnWaitForBurnDrives()
        {
            if(loading_drives) {
                return true;
            }
            
            if(burnDevices == null || burnDevices.Length == 0) {
                ShowBurnerWidgets(false);
                driveLoadingLabel.Markup = "<i>" + 
                    Catalog.GetString("No CD Burners Detected") + "</i>";
                return false;
            } 
            
            ShowBurnerWidgets(true);
            
            burnerDrivesCombo = new ComboBox();
            burnerDrivesCombo.Changed += OnBurnerDriveComboChanged;
            ListStore burnerDrivesModel = new ListStore(typeof(string), 
                typeof(string), typeof(string));
            CellRendererText rendererName = new CellRendererText();
            CellRendererText rendererDevice = new CellRendererText();
            burnerDrivesCombo.Model = burnerDrivesModel;
            burnerDrivesCombo.PackStart(rendererName, true);
            burnerDrivesCombo.PackEnd(rendererDevice, false);
            burnerDrivesCombo.SetAttributes(rendererName, "text", 0);
            burnerDrivesCombo.SetAttributes(rendererDevice, "text", 1);
            driveContainer.Remove(driveLoadingLabel);
            driveLoadingLabel = null;
            driveContainer.PackStart(burnerDrivesCombo, true, true, 0);
            driveContainer.ShowAll();
            
            TreeIter activeIter = TreeIter.Zero;
            
            for(int i = 0; i < burnDevices.Length; i++) {
                BurnDrive drive = burnDevices[i];
                string uid = BurnUtil.GetDriveUniqueId(drive);
                burnerDrivesModel.AppendValues(drive.DisplayName, 
                    drive.Device + " ", uid);
                
                if(selectedBurnerId != null && 
                    uid.Equals(selectedBurnerId)) {
                    burnerDrivesCombo.Model.IterNthChild(
                        out activeIter, i);    
                }
            }
            
            if(activeIter.Equals(TreeIter.Zero)) 
                burnerDrivesCombo.Model.GetIterFirst(out activeIter);
                
            SensitizeBurnerWidgets(true);
            burnerDrivesCombo.SetActiveIter(activeIter);
            
            return false;
        }
        
        private void SensitizeBurnerWidgets(bool sensitive)
        {
            glade["AdvancedFrame"].Sensitive = sensitive;
            glade["AudioRadio"].Sensitive = sensitive;
            glade["Mp3Radio"].Sensitive = sensitive;
            glade["DataRadio"].Sensitive = sensitive;
            glade["DiskFormatLabel"].Sensitive = sensitive;
            glade["WriteSpeedLabel"].Sensitive = sensitive;
        }
        
        private void ShowBurnerWidgets(bool visible)
        {
            glade["AdvancedFrame"].Visible = visible;
            glade["AudioRadio"].Visible = visible;
            glade["Mp3Radio"].Visible = visible;
            glade["DataRadio"].Visible = visible;
            glade["DiskFormatLabel"].Visible = visible;
            glade["WriteSpeedLabel"].Visible = visible;
            writeSpeedCombo.Visible = visible;
        }
        
        private void ChangeBurnerDrive(TreeIter iter)
        {
            ListStore drivesModel = burnerDrivesCombo.Model as ListStore;
            string driveId = drivesModel.GetValue(iter, 2) as string;
            BurnDrive drive = null;    
        
            if(burnDevices == null || burnDevices.Length == 0)
                return;
                
            foreach(BurnDrive cdr in burnDevices) {
                if(BurnUtil.GetDriveUniqueId(cdr).Equals(driveId)) {
                    drive = cdr.Copy();
                    break;
                }
            }
                    
            if(drive == null) {
                selectedDrive = null;
                burnKeyParent = null;
                selectedBurnerId = null;
                return;
            }
                
            selectedBurnerId = BurnUtil.GetDriveUniqueId(drive);
            burnKeyParent = GConfKeys.CDBurnerRoot + selectedBurnerId + "/";
            selectedDrive = drive;
            
            writeSpeedCombo.Sensitive = true;
            while(writeSpeedCombo.Model.IterNChildren() > 0)
                writeSpeedCombo.RemoveText(0);
            
            writeSpeedCombo.AppendText(Catalog.GetString("Fastest Possible"));
            
            for(int speed = drive.MaxWriteSpeed; speed >= 2; speed -= 2) {
                // Translators: this represents a CD write speed, eg "32x"
                writeSpeedCombo.AppendText(String.Format(Catalog.GetString("{0}x"), speed));
            }
            writeSpeedCombo.Active = 0;
            
            Globals.Configuration.Set(GConfKeys.CDBurnerId, selectedBurnerId);

            int disk_type = 0;
            try {
                disk_type = (int)Globals.Configuration.Get(burnKeyParent + "DiskFormat");
            } catch {
            }

            disk_type = (disk_type < 0 || disk_type > 2) ? 0 : disk_type;

            (glade["AudioRadio"] as RadioButton).Active = disk_type == 0;
            (glade["Mp3Radio"] as RadioButton).Active = disk_type == 1;
            (glade["DataRadio"] as RadioButton).Active = disk_type == 2;
            
            (glade["EjectCheck"] as CheckButton).Active = GetBoolPref(
                burnKeyParent + "Eject", true);
            (glade["DAOCheck"] as CheckButton).Active = GetBoolPref(
                burnKeyParent + "DAO", false);
            (glade["OverburnCheck"] as CheckButton).Active = GetBoolPref(
                burnKeyParent + "Overburn", false);
            (glade["SimulateCheck"] as CheckButton).Active = GetBoolPref(
                burnKeyParent + "Simulate", false);
            (glade["BurnproofCheck"] as CheckButton).Active = GetBoolPref(
                burnKeyParent + "Burnproof", true);
                
            try {
                SetComboFromSpeed((int)Globals.Configuration.Get(
                    burnKeyParent + "Speed"));
            } catch(Exception) {}
        }
        
        private void OnBurnerDriveComboChanged(object o, EventArgs args)
        {
            TreeIter iter;
            
            SaveBurnSettings();
            
            if(!burnerDrivesCombo.GetActiveIter(out iter))
                return;
            
            ChangeBurnerDrive(iter);
        }
        
        private int GetSpeedFromCombo()
        {
            if(selectedDrive == null)
                return 0;
                
            int max = selectedDrive.MaxWriteSpeed;
            int index = writeSpeedCombo.Active;
            
            if(index-- == 0)
                return max;
                
            return max - (index * 2);
        }
        
        private void SetComboFromSpeed(int speed)
        {
            if(selectedDrive == null)
                return;
        
            int max = selectedDrive.MaxWriteSpeed;
            
            if(speed % 2 != 0)
                speed--;
                
            if(speed <= 0)
                speed = 2;
            else if(speed > max)
                speed = max;
                
            writeSpeedCombo.Active = speed == max ? 0 : 
                -((speed - max) / 2) + 1;
        }
        
        private void SaveBurnSettings()
        {
            if(selectedBurnerId != null && burnKeyParent != null) {
                int disk_type = 0;
                if((glade["Mp3Radio"] as CheckButton).Active) {
                    disk_type = 1;
                } else if((glade["DataRadio"] as CheckButton).Active) {
                    disk_type = 2;
                }
                
                Globals.Configuration.Set(burnKeyParent + "DiskFormat", disk_type);
                    
                Globals.Configuration.Set(burnKeyParent + "Eject",
                    (glade["EjectCheck"] as CheckButton).Active);
                Globals.Configuration.Set(burnKeyParent + "DAO",
                    (glade["DAOCheck"] as CheckButton).Active);
                Globals.Configuration.Set(burnKeyParent + "Overburn",
                    (glade["OverburnCheck"] as CheckButton).Active);
                Globals.Configuration.Set(burnKeyParent + "Simulate",
                    (glade["SimulateCheck"] as CheckButton).Active);
                Globals.Configuration.Set(burnKeyParent + "Burnproof",
                    (glade["BurnproofCheck"] as CheckButton).Active);
                    
                Globals.Configuration.Set(burnKeyParent + "Speed",
                    GetSpeedFromCombo());
            }
        }
        
        private void LoadPlayerEngines()
        {
            ListStore store = new ListStore(typeof(string), typeof(string), typeof(PlayerEngine));
            
            ComboBox enginesCombo = new ComboBox();
            enginesCombo.Changed += OnEngineChanged;
            CellRendererText rendererName = new CellRendererText();
            enginesCombo.Model = store;
            enginesCombo.PackStart(rendererName, true);
            enginesCombo.SetAttributes(rendererName, "text", 0);
            
            (glade["EngineComboContainer"] as Box).PackStart(enginesCombo, true, true, 0);
            glade["EngineComboContainer"].ShowAll();
            
            TreeIter activeIter = TreeIter.Zero;
            
            foreach(PlayerEngine engine in PlayerEngineCore.Engines) {
                TreeIter iter = store.AppendValues(engine.Name, engine.Id, engine);
                if(PlayerEngineCore.ActiveEngine.Equals(engine)) {
                    activeIter = iter;
                }
            }
            
            if(!activeIter.Equals(TreeIter.Zero)) {
                enginesCombo.SetActiveIter(activeIter); 
            }
        }
        
        private void OnEngineChanged(object o, EventArgs args)
        {
            ComboBox box = o as ComboBox;
            ListStore store = box.Model as ListStore;
            TreeIter iter;
            
            if(!box.GetActiveIter(out iter)) {
                return;
            }
            
            PlayerEngine engine = store.GetValue(iter, 2) as PlayerEngine;
            SelectedEngine = engine;
        }
        
        private void SaveEngineSettings()
        {
            PlayerEngineCore.DefaultEngine = SelectedEngine;
            PlayerEngineCore.ActiveEngine = SelectedEngine;
        }
    }
}
