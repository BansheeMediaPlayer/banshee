using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Mono.Unix;
using Hal;

#if !WINDOWS

namespace IPod.PodSleuth {

    public class HalDevice : IPod.Device {
        private const string PodsleuthPrefix = "org.banshee-project.podsleuth.ipod.";

        private Volume volume;
        private UnixDriveInfo drive;
        private string iconName;

        public override UInt64 VolumeSize {
            get { return (UInt64) drive.TotalSize; }
        }
        
        public override UInt64 VolumeUsed {
            get { return (UInt64) (drive.TotalSize - drive.TotalFreeSpace); }
        }
        
        public override UInt64 VolumeAvailable {
            get { return (UInt64) drive.TotalSize - VolumeUsed; }
        }

        public string IconName {
            get { return iconName; }
        }

        internal Volume Volume {
            get { return volume; }
        }

        private static string GetVolumeSizeString(Volume volume)
        {
            string format = "GiB";
            double value = volume.GetPropertyUInt64("volume.size") / 1000.0 / 1000.0 / 1000.0;

            if(value < 1.0) {
                format = "MiB";
                value *= 1000.0;
            }

            return String.Format("{0} {1}", (int)Math.Round(value), format);
        }


        internal HalDevice (Volume volume) {
            this.volume = volume;

            MountPoint = volume.GetPropertyString ("volume.mount_point");
            ControlPath = MountPoint + volume.GetPropertyString (PodsleuthPrefix + "control_path");
            if (volume.PropertyExists (PodsleuthPrefix + "firmware_version")) {
                FirmwareVersion = volume.GetPropertyString (PodsleuthPrefix + "firmware_version");
            }
            ArtworkFormats = new ReadOnlyCollection<ArtworkFormat> (LoadArtworkFormats ());
                
            ModelClass = volume.GetPropertyString (PodsleuthPrefix + "model.device_class");
            AdvertisedCapacity = GetVolumeSizeString (volume);
            Generation = volume.GetPropertyDouble (PodsleuthPrefix + "model.generation");
            ModelColor = volume.GetPropertyString (PodsleuthPrefix + "model.shell_color");
            VolumeLabel = volume.GetPropertyString ("volume.label");
            VolumeID = volume.Udi;
            CanWrite = !volume.GetPropertyBoolean ("volume.is_mounted_read_only");
            VolumeUuid = volume.GetPropertyString ("volume.uuid");

            if (volume.PropertyExists (PodsleuthPrefix + "firewire_id")) {
                FirewireID = volume.GetPropertyString (PodsleuthPrefix + "firewire_id");
            }
            
            ManufacturerID = volume.GetPropertyString (PodsleuthPrefix + "production.factory_id");
            ProductionIndex = (uint) volume.GetPropertyInteger (PodsleuthPrefix + "production.number");
            ProductionWeek = (uint) volume.GetPropertyInteger (PodsleuthPrefix + "production.week");
            ProductionYear = (uint) volume.GetPropertyInteger (PodsleuthPrefix + "production.year");
            SerialNumber = volume.GetPropertyString (PodsleuthPrefix + "serial_number");
            
            iconName = volume.GetPropertyString ("info.icon_name");
            
            RescanDisk ();
        }

        public override void RescanDisk () {
            drive = new UnixDriveInfo (MountPoint);
        }

        public override void Eject () {
            volume.Eject ();
        }

        private List<ArtworkFormat> LoadArtworkFormats () {
            List<ArtworkFormat> formats = new List<ArtworkFormat> ();

            if (!volume.GetPropertyBoolean (PodsleuthPrefix + "images.album_art_supported"))
                return formats;

            string[] formatList = volume.GetPropertyStringList (PodsleuthPrefix + "images.formats");

            foreach (string formatStr in formatList) {
                short correlationId, width, height, rotation;
                ArtworkUsage usage;
                int size;
                PixelFormat pformat;

                correlationId = width = height = rotation = size = 0;
                usage = ArtworkUsage.Unknown;
                pformat = PixelFormat.Unknown;

                string[] pairs = formatStr.Split(',');
                foreach (string pair in pairs) {
                    string[] splitPair = pair.Split('=');
                    if (splitPair.Length != 2)
                        continue;

                    string value = splitPair[1];
                    switch (splitPair[0]) {
                    case "corr_id":
                        correlationId = Int16.Parse (value);
                        break;
                    case "width":
                        width = Int16.Parse (value);
                        break;
                    case "height":
                        height = Int16.Parse (value);
                        break;
                    case "rotation":
                        rotation = Int16.Parse (value);
                        break;
                    case "pixel_format":
                        switch (value) {
                        case "iyuv":
                            pformat = PixelFormat.IYUV;
                            break;
                        case "rgb565":
                            pformat = PixelFormat.Rgb565;
                            break;
                        case "rgb565be":
                            pformat = PixelFormat.Rgb565BE;
                            break;
                        }
                        break;
                    case "image_type":
                        switch (value) {
                        case "photo":
                            usage = ArtworkUsage.Photo;
                            break;
                        case "album":
                            usage = ArtworkUsage.Cover;
                            break;
                        case "chapter":
                            usage = ArtworkUsage.Chapter;
                            break;
                        }
                        break;
                    }
                }

                formats.Add (new ArtworkFormat (usage, width, height, correlationId,
                                                size, pformat, rotation));
            }

            return formats;
        }
     }
}

#endif


