REAL_NOTIFY_SHARP_SOURCES = Notifications/Notifications.cs Notifications/Notification.cs
if EXTERNAL_NOTIFY_SHARP
NOTIFY_SHARP_SOURCES = 
NOTIFY_SHARP_LIBS = $(NOTIFY_SHARP_LIBS)
else
NOTIFY_SHARP_SOURCES = $(REAL_NOTIFY_SHARP_SOURCES)
NOTIFY_SHARP_LIBS = 
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
	Banshee.NotificationArea/X11NotificationAreaBox.cs

SOURCES += $(NOTIFY_SHARP_SOURCES)

RESOURCES =  \
	Banshee.NotificationArea.addin.xml \
	Resources/NotificationAreaMenu.xml

if ENABLE_OSX
EXTRA_DIST = $(SOURCES) $(RESOURCES)
else
include $(top_srcdir)/build/build.mk
module_SCRIPTS += Banshee.NotificationArea.dll.config
EXTRA_DIST += Banshee.NotificationArea.dll.config $(REAL_NOTIFY_SHARP_SOURCES)
endif
