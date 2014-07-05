/***************************************************************************
 *  Karma.cs
 *
 *  Copyright (C) 2006-2008 Bob Copeland <me@bobcopeland.com>
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
using System.Collections.Generic;
using Mono.Unix;

using KarmaLib=Karma;

using Hyena;
using Banshee.Base;
using Banshee.Configuration;
using Banshee.Dap;
using Banshee.Hardware;
using Banshee.Collection.Database;

namespace Banshee.Dap.Karma
{
    public class KarmaSource : DapSource
    {
        private KarmaLib.Device device;
        private String mount_point;

        // for getting karma track given a DatabaseTrackInfo
        private Dictionary<long, KarmaTrackInfo> track_map =
            new Dictionary<long, KarmaTrackInfo>();

        public override void DeviceInitialize(IDevice dev, bool force)
        {
            base.DeviceInitialize(dev, force);

            if (!IsKarma(dev))
                throw new InvalidDeviceException();

            try
            {
                device = new KarmaLib.Device(mount_point);
                device.ProgressChanged += OnFileUploaded;
            } catch(Exception e) {
                Log.Exception("Could not load KarmaLib", e);
                throw new InvalidDeviceException();
            }

            Initialize();
        }

        protected override void LoadFromDevice()
        {
            ReloadDatabase();
            OnTracksAdded();
        }

        private bool IsKarma(IDevice dev)
        {
            IVolume volume = dev as IVolume;

            if (volume == null)
                return false;

            IUsbDevice usbdev = volume.ResolveRootUsbDevice();
            if (usbdev == null)
                return false;

            int vendor_id = usbdev.VendorId;
            int product_id = usbdev.ProductId;

            mount_point = volume.MountPoint;
            return (vendor_id == 0x045a && product_id == 0x5210);
        }

        private void ReloadDatabase()
        {
            foreach (KarmaLib.Song song in device.GetSongs()) {
                KarmaTrackInfo track = new KarmaTrackInfo(song,
                    mount_point);
                track.PrimarySource = this;
                track.Save(false);
                track_map[track.TrackId] = track;
            }
        }

        public override void Import()
        {
            Log.Information("Unimplemented");
        }

        protected override bool DeleteTrack(DatabaseTrackInfo track)
        {
            KarmaTrackInfo karma_track = track_map[track.TrackId];
            if (karma_track == null)
                return true;

            lock (device) {
                device.DeleteSong(karma_track.KarmaId);
                device.Save();
                track_map.Remove(track.TrackId);
            }

            return true;
        }

        protected override void AddTrackToDevice(DatabaseTrackInfo track, SafeUri fromUri)
        {
            if (track.PrimarySourceId == DbId)
                return;

            lock (device) {
                device.QueueAddSong(fromUri.LocalPath);
                device.Save();
            }
        }

        private void OnFileUploaded(KarmaLib.Song song)
        {
            KarmaTrackInfo karma_track = new KarmaTrackInfo(song, mount_point);
            karma_track.PrimarySource = this;
            karma_track.Save(false);
            track_map[karma_track.TrackId] = karma_track;
        }

        public override long BytesCapacity {
            get {
                return (long) device.GetStorageDetails().StorageSize;
            }
        }

        public override long BytesUsed {
            get {
                KarmaLib.StorageDetails details = device.GetStorageDetails();
                return (long) (details.StorageSize - details.FreeSpace);
            }
        }

        public override bool IsReadOnly {
            get {
                return false;
            }
        }
    }
}
