/***************************************************************************
 *  NjbDap.cs
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
using System.IO;
using System.Collections;
using Mono.Unix;
using Hal;
using NJB=Njb;
using Banshee.Dap;
using Banshee.Base;

namespace Banshee.Dap.Njb
{
    [DapProperties(DapType = DapType.NonGeneric)]
    [SupportedCodec(CodecType.Mp3)]
    [SupportedCodec(CodecType.Wma)]
    public sealed class NjbDap : DapDevice
    {
        private static NJB.Discoverer discoverer;
        private NJB.Device device;
        private NJB.DeviceId device_id;
        private uint ping_timer_id;
        
        private ulong disk_free;
        private ulong disk_total;
        private short usb_bus_number;
        private short usb_device_number;
        private Hal.Device hal_device;
        
        private Queue remove_queue = new Queue();
        
        static NjbDap()
        {
            try {
                discoverer = new NJB.Discoverer();
            } catch(Exception e) {
                Console.WriteLine(e);
                Console.WriteLine("Could not initialize NJB Discoverer: " + e.Message);
            }
        }
        
        public override InitializeResult Initialize(Hal.Device halDevice)
        {
            if(discoverer == null || 
                !halDevice.PropertyExists("usb.bus_number") ||
                !halDevice.PropertyExists("usb.linux.device_number") ||
                !halDevice.PropertyExists("usb.vendor_id") ||
                !halDevice.PropertyExists("usb.product_id")) {
                return InitializeResult.Invalid;
            }
            
            usb_bus_number = (short)halDevice.GetPropertyInt("usb.bus_number");
            usb_device_number = (short)halDevice.GetPropertyInt("usb.linux.device_number");
            
            device_id = NJB.DeviceId.GetDeviceId(
                (short)halDevice.GetPropertyInt("usb.vendor_id"),
                (short)halDevice.GetPropertyInt("usb.product_id"));
            
            if(device_id == null) {
                return InitializeResult.Invalid;
            }
            
            hal_device = halDevice;
            
            base.Initialize(halDevice);
            
            CanCancelSave = false;
            return InitializeResult.Valid;
        }
        
        private int device_grabs = 1;
        private void GrabDevice()
        {
            Activate();
        
            if(device_grabs > 0) {
                return;
            }
            
            device_grabs++;
            
            if(device != null) {
                device.Open();
                device.Capture();
            }
        }
        
        private void ReleaseDevice()
        {
            if(device_grabs <= 0) {
                return;
            }
            
            if(device != null) {
                device.Release();
                device.Close();
            }
            
            device_grabs--;
        }

        public override void Activate()
        {
            if(device != null) {
                return;
            }
        
            discoverer.Rescan();
            
            foreach(NJB.Device tmp_device in discoverer) {
                if(!tmp_device.Open()) {
                    string errors = String.Empty;
                    foreach(string error in tmp_device.ErrorsPending) {
                        errors += "<big>\u2022</big> " + GLib.Markup.EscapeText(error.Replace("_", "-")) + "\n";
                    }
                    
                    LogCore.Instance.PushError(Catalog.GetString("Cannot read device"), errors);
                    continue;
                }
                
                if(tmp_device.UsbDeviceId == usb_device_number && tmp_device.UsbBusPath == usb_bus_number) {
                    device = tmp_device;
                    break;
                }
                
                tmp_device.Dispose();
            }

            if(device == null) { 
                Dispose();
                return;
            }
            
            InstallProperty("Model", device.Name);
            InstallProperty("Vendor", hal_device["usb.vendor"]);
            InstallProperty("Firmware Revision", device.FirmwareRevision.ToString());
            InstallProperty("Hardware Revision", device.HardwareRevision.ToString());
            InstallProperty("Serial Number", hal_device.PropertyExists("usb.serial") 
                ? hal_device["usb.serial"] : device.SdmiIdString);

            // ping the NJB device every 5 minutes
            ping_timer_id = GLib.Timeout.Add(300000, delegate {
                if(device == null) {
                    return false;
                }
                
                if(device_grabs > 0) {
                    return true;
                }
                
                GrabDevice();
                device.Ping();
                ReleaseDevice();
                return true;
            });
            
            ReloadDatabase();
            ReleaseDevice();
        }

        public override void Dispose()
        {
            if(ping_timer_id > 0) {
                GLib.Source.Remove(ping_timer_id);
            }
            
            ReleaseDevice();
            base.Dispose();
        }
        
        private void ReloadDatabase()
        {
            ClearTracks(false);
            GrabDevice();
            
            foreach(NJB.Song song in device.GetSongs()) {
                NjbDapTrackInfo track = new NjbDapTrackInfo(song, this);
                AddTrack(track);            
            }
            
            disk_free = device.DiskFree;
            disk_total = device.DiskTotal;
            
            ReleaseDevice();
        }
        
        protected override TrackInfo OnTrackAdded(TrackInfo track)
        {
            if(track is NjbDapTrackInfo || !TrackExistsInList(track, Tracks)) {
                return track;
            }
            
            return null;
        }
        
        protected override void OnTrackRemoved(TrackInfo track)
        {
            if(!(track is NjbDapTrackInfo)) {
                return;
            }
            
            remove_queue.Enqueue(track);
        }
        
        private int sync_total;
        private int sync_finished;
        private string sync_message;
        
        private void OnSyncProgressChanged(object o, NJB.TransferProgressArgs args)
        {
            UpdateSaveProgress(Catalog.GetString("Synchronizing Device"), sync_message,
                (sync_finished + ((double)args.Current / (double)args.Total)) / sync_total);
        }
        
        public override void Synchronize()
        {
            try {
                GrabDevice();
                int remove_total = remove_queue.Count;
                
                while(remove_queue.Count > 0) {
                    NjbDapTrackInfo track = remove_queue.Dequeue() as NjbDapTrackInfo;
                    UpdateSaveProgress(Catalog.GetString("Synchronizing Device"), Catalog.GetString("Removing Songs"),
                        (double)(remove_total - remove_queue.Count) / (double)remove_total);
                    device.DeleteSong(track.Song);
                }
                
                device.ProgressChanged += OnSyncProgressChanged;
                
                sync_total = 0;
                sync_finished = 0;
                
                foreach(TrackInfo track in Tracks) {
                    if(track is NjbDapTrackInfo) {
                        continue;
                    }
                    
                    sync_total++;
                }
                
                foreach(TrackInfo track in Tracks) {
                    if(track == null ||  track is NjbDapTrackInfo || track.Uri == null) {
                        continue;
                    }
                    
                    FileInfo file;
                    
                    try {
                        file = new FileInfo(track.Uri.LocalPath);
                        if(!file.Exists) {
                            continue;
                        }
                    } catch {
                        continue;
                    }
                    
                    try {
                        NJB.Song song = new NJB.Song(device);
                        song.Codec = NJB.Codec.Mp3;
                        song.FileSize = (uint)file.Length;
                        song.FileName = Path.GetFileName(file.FullName);
                        song.Artist = track.Artist;
                        song.Album = track.Album;
                        song.Title = track.Title;
                        song.Genre = track.Genre;
                        song.TrackNumber = (ushort)track.TrackNumber;
                        song.Year = (ushort)track.Year;
                        song.Duration = track.Duration;
                        
                        sync_message = String.Format("{0} - {1}", song.Artist, song.Title);
                        device.SendSong(song, track.Uri.LocalPath);
                        sync_finished++;
                    } catch {
                        Console.WriteLine("Could not sync song:");   
                        foreach(string error in device.ErrorsPending) {
                            Console.WriteLine("  * {0}", error);
                        }
                    }
                }
                
                device.ProgressChanged -= OnSyncProgressChanged;
            } catch(Exception e) {
                Console.WriteLine(e);
            } finally {
                ReleaseDevice();
                ReloadDatabase();
                FinishSave();
            }
        }
        
        public override Gdk.Pixbuf GetIcon(int size)
        {
            string prefix = "multimedia-player-";
            string id = "dell-dj-pocket";
            Gdk.Pixbuf icon = IconThemeUtils.LoadIcon(prefix + id, size);
            return icon == null? base.GetIcon(size) : icon;
        }
        
        public override string Name {
            get {
                return device_id.DisplayName;
            }
        }
        
        public override void SetOwner(string owner)
        {
            try {
                GrabDevice();
                device.Owner = owner;
            } catch {
                LogCore.Instance.PushError(Catalog.GetString("Device Error"),
                    Catalog.GetString("Could not set the owner of the device."));
            } finally {
                ReleaseDevice();
            }
        }
        
        public override string Owner {
            get {
                Activate();
                return device.Owner;
            }
        }
        
        public override string GenericName {
            get {
                Activate();
                return device_id.DisplayName;
            }
        }
        
        public override ulong StorageCapacity {
            get {
                Activate();
                return disk_total;
            }
        }
        
        public override ulong StorageUsed {
            get {
                Activate();
                return disk_total - disk_free;
            }
        }
        
        public override bool IsReadOnly {
            get {
                return false;
            }
        }
        
        public override bool IsPlaybackSupported {
            get {
                return false;
            }
        }
    }
}
