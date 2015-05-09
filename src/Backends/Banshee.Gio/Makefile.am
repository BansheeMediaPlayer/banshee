ASSEMBLY = Banshee.Gio
TARGET = library
LINK = $(REF_BACKEND_GIO)
INSTALL_DIR = $(BACKENDS_INSTALL_DIR)

if ENABLE_GIO_HARDWARE
MCS_FLAGS += "-define:ENABLE_GIO_HARDWARE"
endif

SOURCES =  \
	Banshee.Hardware.Gio/BlockDevice.cs \
	Banshee.Hardware.Gio/CdromDevice.cs \
	Banshee.Hardware.Gio/Device.cs \
	Banshee.Hardware.Gio/DeviceMediaCapabilities.cs \
	Banshee.Hardware.Gio/DiscVolume.cs \
	Banshee.Hardware.Gio/HardwareManager.cs \
	Banshee.Hardware.Gio/LowLevel/GioDriveMetadataSource.cs \
	Banshee.Hardware.Gio/LowLevel/GioMetadataSource.cs \
	Banshee.Hardware.Gio/LowLevel/GioVolumeMetadataSource.cs \
	Banshee.Hardware.Gio/LowLevel/IMetadataSource.cs \
	Banshee.Hardware.Gio/LowLevel/IRawDevice.cs \
	Banshee.Hardware.Gio/LowLevel/Manager.cs \
	Banshee.Hardware.Gio/LowLevel/RawBlockDevice.cs \
	Banshee.Hardware.Gio/LowLevel/RawDevice.cs \
	Banshee.Hardware.Gio/LowLevel/RawUsbDevice.cs \
	Banshee.Hardware.Gio/LowLevel/RawVolume.cs \
	Banshee.Hardware.Gio/LowLevel/UdevMetadataSource.cs \
	Banshee.Hardware.Gio/UsbDevice.cs \
	Banshee.Hardware.Gio/UsbVolume.cs \
	Banshee.Hardware.Gio/Volume.cs \
	Banshee.IO.Gio/DemuxVfs.cs \
	Banshee.IO.Gio/Directory.cs \
	Banshee.IO.Gio/File.cs \
	Banshee.IO.Gio/Provider.cs \
	Banshee.IO.Gio/Tests.cs

RESOURCES = Banshee.Gio.addin.xml

if ENABLE_GIO
include $(top_srcdir)/build/build.mk
else
include $(top_srcdir)/build/build.dist.mk
endif

