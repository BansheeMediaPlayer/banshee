if EXTERNAL_NOTIFY_SHARP
REF_NOTIFY_SHARP_LIBS = $(NOTIFY_SHARP_LIBS)
else
MCS_FLAGS+="-define:INTERNAL_NOTIFY_SHARP"
REF_NOTIFY_SHARP_LIBS = $(LINK_EXTENSION_NOTIFICATIONAREA)
endif

ASSEMBLY = Banshee.SoundMenu
TARGET = library
LINK = $(REF_EXTENSION_SOUNDMENU) $(REF_NOTIFY_SHARP_LIBS)
INSTALL_DIR = $(EXTENSIONS_INSTALL_DIR)

SOURCES =  \
	Banshee.SoundMenu/SoundMenuProxy.cs \
	Banshee.SoundMenu/SoundMenuService.cs

RESOURCES = Banshee.SoundMenu.addin.xml

if ENABLE_SOUNDMENU
include $(top_srcdir)/build/build.mk
else
include $(top_srcdir)/build/build.dist.mk
endif
