SUBDIRS = libbanshee

ASSEMBLY = Banshee.GStreamer
TARGET = library
LINK = $(REF_BACKEND_GSTREAMER)
SOURCES =  \
	Banshee.GStreamer/AudioCdRipper.cs \
	Banshee.GStreamer/BpmDetector.cs \
	Banshee.GStreamer/GstErrors.cs \
	Banshee.GStreamer/PlayerEngine.cs \
	Banshee.GStreamer/Service.cs \
	Banshee.GStreamer/TagList.cs \
	Banshee.GStreamer/Transcoder.cs
RESOURCES = Banshee.GStreamer.addin.xml
INSTALL_DIR = $(BACKENDS_INSTALL_DIR)

if ENABLE_GST_NATIVE
include $(top_srcdir)/build/build.mk
module_SCRIPTS += Banshee.GStreamer.dll.config
else
include $(top_srcdir)/build/build.dist.mk
endif

EXTRA_DIST += Banshee.GStreamer.dll.config
