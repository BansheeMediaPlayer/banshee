if EXTERNAL_NOTIFY_SHARP
NOTIFY_SHARP_LIBS = $(NOTIFY_SHARP_LIBS)
else
GMCS_FLAGS+="-define:INTERNAL_NOTIFY_SHARP"
NOTIFY_SHARP_LIBS = 
endif

if HAVE_GDK_2_22
GMCS_FLAGS+= "-define:HAVE_GDK_2_22"
endif

ASSEMBLY = Banshee.NotificationArea
TARGET = library
LINK = $(REF_EXTENSION_NOTIFICATIONAREA) $(NOTIFY_SHARP_LIBS)
INSTALL_DIR = $(EXTENSIONS_INSTALL_DIR)

SOURCES =  \
	Banshee.NotificationArea/GtkNotificationAreaBox.cs \
	Banshee.NotificationArea/INotificationAreaBox.cs \
	Banshee.NotificationArea/NotificationAreaService.cs \
	Banshee.NotificationArea/TrackInfoPopup.cs \
	Banshee.NotificationArea/X11NotificationArea.cs \
	Banshee.NotificationArea/X11NotificationAreaBox.cs \
	Notifications/Notification.cs \
	Notifications/Notifications.cs

RESOURCES =  \
	Banshee.NotificationArea.addin.xml \
	Resources/NotificationAreaMenu.xml

if PLATFORM_DARWIN
EXTRA_DIST = $(SOURCES) $(RESOURCES)
else
include $(top_srcdir)/build/build.mk
module_SCRIPTS += Banshee.NotificationArea.dll.config
EXTRA_DIST += Banshee.NotificationArea.dll.config $(SOURCES) $(RESOURCES)
endif
