//
// Volume.cs
//
// Author:
//   Alex Launi <alex.launi@gmail.com>
//
// Copyright (c) 2010 Alex Launi
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

#if ENABLE_GIO_HARDWARE
using System;
using System.Collections.Generic;

using Mono.Unix;

using Banshee.Hardware;
using GLib;
using System.Threading;

namespace Banshee.Hardware.Gio
{
    class RawVolume : RawDevice
    {
        const string FileSystemFree = "filesystem::free";
        const string FileSystemSize = "filesystem::size";
        const string FileSystemReadOnly = "filesystem::readonly";

        public GLib.Volume Volume {
            get; set;
        }

        public long Available {
            get {
                try {
                    using (var file_info = Volume.MountInstance.Root.QueryFilesystemInfo (FileSystemFree, null))
                        return (long) file_info.GetAttributeULong (FileSystemFree);
                } catch {
                    return 0;
                }
            }
        }

        public ulong Capacity {
            get {
                try {
                    using (var file_info = Volume.MountInstance.Root.QueryFilesystemInfo (FileSystemSize, null))
                        return file_info.GetAttributeULong (FileSystemSize);
                } catch {
                    return 0;
                }
            }
        }

        public bool CanEject{
            get { return Volume.CanEject (); }
        }

        public bool CanMount {
            get {
                return Volume.CanMount ();
            }
        }

        public bool CanUnmount {
            get {
                return IsMounted && Volume.MountInstance.CanUnmount;
            }
        }

        public override string IdMediaPlayer {
            get { return UdevMetadata.IdMediaDevice; }
        }

        public bool IsMounted {
            get {
                return Volume.MountInstance != null;
            }
        }

        public bool IsReadOnly {
            get {
                try {
                    using (var file_info = Volume.MountInstance.Root.QueryFilesystemInfo (FileSystemReadOnly, null))
                        return file_info.GetAttributeBoolean (FileSystemReadOnly);
                } catch {
                    return true;
                }
            }
        }

        public override bool IsRemovable {
            get {
                return Volume.CanEject ();
            }
        }

        // FIXME: iPhones have an empty UUID so we should return their serial instead.
        public override string Identifier {
            get { return Uuid; }
        }

        // FIXME
        public override string Model {
            get { return UdevMetadata.Model; }
        }

        public string MountPoint {
            get {
                return Volume.MountInstance == null ? null : Volume.MountInstance.Root.Path;
            }
        }

        public override string Name {
            get { return Volume.Name; }
        }

        // FIXME
        public override string Serial {
            get { return UdevMetadata.Serial; }
        }

        // FIXME
        public override string Subsystem {
            get { return UdevMetadata.Subsystem; }
        }

        //FIXME
        public override string Vendor {
            get { return UdevMetadata.Vendor; }
        }


        public RawVolume (GLib.Volume volume, Manager manager, GioVolumeMetadataSource gioMetadata, UdevMetadataSource udevMetadata)
            : base (manager, gioMetadata, udevMetadata)
        {
            Volume = volume;
        }

        public void Eject ()
        {
            if (CanEject) {
                Volume.Eject (MountUnmountFlags.Force, null, (s, result) =>
                {
                    try {
                        if (!Volume.EjectWithOperationFinish (result)) {
                            Hyena.Log.ErrorFormat ("Failed to eject {0}", Volume.Name);
                        }
                    } catch (Exception e) {
                        Hyena.Log.Exception (e);
                    }
                });
            }
        }

        public override string GetPropertyString (string key)
        {
            return UdevMetadata.GetPropertyString (key);
        }

        public override double GetPropertyDouble (string key)
        {
            return UdevMetadata.GetPropertyDouble (key);
        }

        public override bool GetPropertyBoolean (string key)
        {
            return UdevMetadata.GetPropertyBoolean (key);
        }

        public override int GetPropertyInteger (string key)
        {
            return UdevMetadata.GetPropertyInteger (key);
        }

        public override ulong GetPropertyUInt64 (string key)
        {
            return UdevMetadata.GetPropertyUInt64 (key);
        }

        public override string[] GetPropertyStringList (string key)
        {
            return UdevMetadata.GetPropertyStringList (key);
        }

        public override bool PropertyExists (string key)
        {
            return UdevMetadata.PropertyExists (key);
        }

        public void Mount ()
        {
            if (CanMount) {
                ManualResetEvent handle = new ManualResetEvent (false);
                Volume.Mount (MountMountFlags.None, null, null, delegate { handle.Set (); });
                if (!handle.WaitOne (TimeSpan.FromSeconds (5)))
                    Hyena.Log.Information ("Timed out trying to mount {0}", Name);
            }
        }

        public void Unmount ()
        {
            if (CanUnmount) {
                ManualResetEvent handle = new ManualResetEvent (false);
                Volume.MountInstance.UnmountWithOperation (MountUnmountFlags.Force, null, null, delegate { handle.Set (); });
                if (!handle.WaitOne (TimeSpan.FromSeconds (5)))
                    Hyena.Log.Information ("Timed out trying to unmount {0}", Name);
            }
        }

        public override string Product {
            get {
                return UdevMetadata.Model;
            }
        }

        public override string Uuid {
            get { return Volume.Uuid ?? UdevMetadata.Uuid; }
        }

        public RawBlockDevice Parent {
            get {
                if (Volume.Drive == null) {
                    return null;
                }

                var device = Manager.GudevDeviceFromGioDrive (Volume.Drive);
                if (device == null) {
                    return null;
                }
                return new RawBlockDevice (Volume.Drive,
                                           Manager,
                                           new GioDriveMetadetaSource (Volume.Drive),
                                           new UdevMetadataSource (device));
            }
        }

        public bool ShouldIgnore {
            get { return false; }
        }

        public string FileSystem {
            get { return Volume.MountInstance.Root.UriScheme; }
        }
    }
}
#endif
