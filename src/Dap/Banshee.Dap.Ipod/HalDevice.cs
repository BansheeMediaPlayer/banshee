using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

#if !WINDOWS

using Mono.Unix;
using Hal;

namespace IPod.HalClient 
{
    internal class HalDevice : IPod.Device 
    {
        internal const string PodsleuthPrefix = "org.banshee-project.podsleuth.ipod.";

        public class HalProductionInfo : IPod.ProductionInfo
        {
            public HalProductionInfo (Volume volume)
            {
                SerialNumber = volume.GetPropertyString (PodsleuthPrefix + "serial_number");
                FactoryId = volume.GetPropertyString (PodsleuthPrefix + "production.factory_id");
                Number = volume.GetPropertyInteger (PodsleuthPrefix + "production.number");
                Week = volume.GetPropertyInteger (PodsleuthPrefix + "production.week");
                Year = volume.GetPropertyInteger (PodsleuthPrefix + "production.year");
            }
        }
        
        public class HalVolumeInfo : IPod.VolumeInfo
        {
            private UnixDriveInfo drive;
        
            public HalVolumeInfo (Volume volume)
            {
                MountPoint = volume.GetPropertyString ("volume.mount_point");
                Label = volume.GetPropertyString ("volume.label");
                IsMountedReadOnly = volume.GetPropertyBoolean ("volume.is_mounted_read_only");
                Uuid = volume.GetPropertyString ("volume.uuid");
            }
            
            public void Rescan()
            {
                drive = new UnixDriveInfo (MountPoint);
            }
            
            public override ulong Size {
                get { return (ulong) drive.TotalSize; }
            }
        
            public override ulong SpaceUsed {
                get { return (ulong) (drive.TotalSize - drive.TotalFreeSpace); }
            }
        }
        
        public class HalModelInfo : IPod.ModelInfo
        {
            public HalModelInfo (Volume volume)
            {
                AdvertisedCapacity = GetVolumeSizeString (volume);
                
                IsUnknown = true;
                if (volume.PropertyExists (PodsleuthPrefix + "is_unknown")) {
                    IsUnknown = volume.GetPropertyBoolean (PodsleuthPrefix + "is_unknown");
                }
                
                if (volume.PropertyExists (PodsleuthPrefix + "images.album_art_supported")) {
                    AlbumArtSupported = volume.GetPropertyBoolean (PodsleuthPrefix + "images.album_art_supported");
                }
                
                if (volume.PropertyExists (PodsleuthPrefix + "images.photos_supported")) {
                    PhotosSupported = volume.GetPropertyBoolean (PodsleuthPrefix + "images.photos_supported");
                }
                
                if (volume.PropertyExists (PodsleuthPrefix + "model.device_class")) {
                    DeviceClass = volume.GetPropertyString (PodsleuthPrefix + "model.device_class");
                }
                
                if (volume.PropertyExists (PodsleuthPrefix + "model.generation")) {
                    Generation = volume.GetPropertyDouble (PodsleuthPrefix + "model.generation");
                }
                
                if (volume.PropertyExists (PodsleuthPrefix + "model.shell_color")) {
                    ShellColor = volume.GetPropertyString (PodsleuthPrefix + "model.shell_color");
                }
                
                if (volume.PropertyExists ("info.icon_name")) {
                    IconName = volume.GetPropertyString ("info.icon_name");
                }
                
                if (volume.PropertyExists (PodsleuthPrefix + "capabilities")) {
                    foreach (string capability in volume.GetPropertyStringList (PodsleuthPrefix + "capabilities")) {
                        AddCapability (capability);
                    }
                }
            }
            
            private static string GetVolumeSizeString (Volume volume)
            {
                string format = "GiB";
                double value = volume.GetPropertyUInt64 ("volume.size") / 1000.0 / 1000.0 / 1000.0;

                if(value < 1.0) {
                    format = "MiB";
                    value *= 1000.0;
                }

                return String.Format ("{0} {1}", (int)Math.Round (value), format);
            }
        }

        private Volume volume;
        
        private ProductionInfo production_info;
        private HalVolumeInfo volume_info;
        private ModelInfo model_info;
        
        internal Volume Volume {
            get { return volume; }
        }
        
        public override ProductionInfo ProductionInfo {
            get { return production_info; }
        }
        
        public override VolumeInfo VolumeInfo {
            get { return volume_info; }
        }
        
        public override ModelInfo ModelInfo {
            get { return model_info; }
        }

        internal HalDevice (Volume volume) 
        {
            this.volume = volume;

            volume_info = new HalVolumeInfo (volume);
            production_info = new HalProductionInfo (volume);
            model_info = new HalModelInfo (volume);
            
            if (volume.PropertyExists (PodsleuthPrefix + "control_path")) {
                string relative_control = volume.GetPropertyString (PodsleuthPrefix + "control_path");
                if (relative_control[0] == Path.DirectorySeparatorChar) {
                    relative_control = relative_control.Substring (1);
                }
                
                ControlPath = Path.Combine(VolumeInfo.MountPoint, relative_control);
            }
            
            ArtworkFormats = new ReadOnlyCollection<ArtworkFormat> (LoadArtworkFormats ());

            if (volume.PropertyExists (PodsleuthPrefix + "firmware_version")) {
                FirmwareVersion = volume.GetPropertyString (PodsleuthPrefix + "firmware_version");
            }

            if (volume.PropertyExists (PodsleuthPrefix + "firewire_id")) {
                FirewireId = volume.GetPropertyString (PodsleuthPrefix + "firewire_id");
            }
            
            RescanDisk ();
        }

        public override void RescanDisk () 
        {
            volume_info.Rescan ();
        }

        public override void Eject () 
        {
            volume.Eject ();
        }

        private List<ArtworkFormat> LoadArtworkFormats () 
        {
            List<ArtworkFormat> formats = new List<ArtworkFormat> ();

            if (!ModelInfo.AlbumArtSupported) {
                return formats;
            }
            
            string [] formatList = volume.GetPropertyStringList (PodsleuthPrefix + "images.formats");

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
                    if (splitPair.Length != 2) {
                        continue;
                    }
                    
                    string value = splitPair[1];
                    switch (splitPair[0]) {
                        case "corr_id": correlationId = Int16.Parse (value); break;
                        case "width": width = Int16.Parse (value); break;
                        case "height": height = Int16.Parse (value); break;
                        case "rotation": rotation = Int16.Parse (value); break;
                        case "pixel_format":
                            switch (value) {
                                case "iyuv": pformat = PixelFormat.IYUV;  break;
                                case "rgb565": pformat = PixelFormat.Rgb565;  break;
                                case "rgb565be": pformat = PixelFormat.Rgb565BE; break;
                                case "unknown": pformat = PixelFormat.Unknown; break;
                            }
                            break;
                        case "image_type":
                            switch (value) {
                                case "photo": usage = ArtworkUsage.Photo; break;
                                case "album": usage = ArtworkUsage.Cover; break;
                                case "chapter": usage = ArtworkUsage.Chapter; break;
                            }
                            break;
                    }
                }

                if (pformat != PixelFormat.Unknown) {
                    formats.Add (new ArtworkFormat (usage, width, height, correlationId, size, pformat, rotation));
                }
            }

            return formats;
        }
     }
}

#endif
