//
// RawBlockDevice.cs
//
// Author:
//   alex <${AuthorEmail}>
//
// Copyright (c) 2010 alex
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

using Banshee.Hardware;

namespace Banshee.Hardware.Gio
{
    class RawBlockDevice : RawDevice
    {
        public RawBlockDevice (GLib.Drive drive, Manager manager, GioDriveMetadetaSource gioMetadata, UdevMetadataSource udevMetadata)
            : base (manager, gioMetadata, udevMetadata)
        {
            Drive = drive;
        }

        GLib.Drive Drive { get; set; }

        public IEnumerable<Volume> Volumes {
            get {
                foreach (var maybe_volume in Drive.Volumes) {
                    var volume = maybe_volume as GLib.Volume ?? GLib.VolumeAdapter.GetObject (maybe_volume as GLib.Object);
                    if (volume == null) {
                        yield return null;
                        continue;
                    }
                    var device = Manager.GudevDeviceFromGioVolume (volume);
                    if (device == null) {
                        yield return null;
                        continue;
                    }
                    yield return new Volume (new RawVolume (volume,
                                                            Manager,
                                                            new GioVolumeMetadataSource (volume),
                                                            new UdevMetadataSource (device)));
                }
            }
        }

#region implemented abstract members of Banshee.Hardware.Gio.RawDevice
        public override string Identifier {
           get {
               return Uuid;
           }
        }

        public override string IdMediaPlayer {
           get {
               return UdevMetadata.IdMediaDevice;
           }
        }

        public override bool IsRemovable {
           get {
               return Drive.CanEject ();
           }
        }

        public override string Name {
           get {
               return Drive.Name;
           }
        }

        public override string Model {
           get {
               return UdevMetadata.Model;
           }
        }

        public override string Product {
           get {
               return "Product not implemented";
           }
        }

        public override string Serial {
           get {
               return UdevMetadata.Serial;
           }
        }

        public override string Subsystem {
           get {
               return UdevMetadata.Subsystem;
           }
        }

        public override string Uuid {
           get {
               return UdevMetadata.Uuid;
           }
        }

        public override string Vendor {
           get {
               return UdevMetadata.Vendor;
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
#endregion
    }
}

#endif
