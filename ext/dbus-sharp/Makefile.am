include $(top_srcdir)/build/Common.Makefile

DBUS_ASSEMBLY = NDesk.DBus.dll
DBUS_GLIB_ASSEMBLY = NDesk.DBus.GLib.dll

DBUS_SRC = $(srcdir)/dbus-sharp/*.cs
DBUS_GLIB_SRC = $(srcdir)/dbus-sharp-glib/*.cs

if !EXTERNAL_NDESK_DBUS
dbusdir = $(pkglibdir)
dbus_SCRIPTS = \
	$(DBUS_ASSEMBLY) \
	$(DBUS_GLIB_ASSEMBLY) \
	$(DBUS_GLIB_ASSEMBLY).config


all: $(DBUS_ASSEMBLY) $(DBUS_GLIB_ASSEMBLY)

$(DBUS_ASSEMBLY): $(DBUS_SRC)
	$(BUILD_LIB) -out:$@ -unsafe $(LINK_MONO_UNIX) $(DBUS_SRC)

$(DBUS_GLIB_ASSEMBLY): $(DBUS_GLIB_SRC)
	$(BUILD_LIB) -out:$@ -r:$(DBUS_ASSEMBLY) $(DBUS_GLIB_SRC)
endif

EXTRA_DIST = $(DBUS_SRC) $(DBUS_GLIB_SRC) $(DBUS_GLIB_ASSEMBLY).config.in
CLEANFILES = $(DBUS_ASSEMBLY) $(DBUS_ASSEMBLY).mdb $(DBUS_GLIB_ASSEMBLY) $(DBUS_GLIB_ASSEMBLY).mdb
MAINTAINERCLEANFILES = Makefile.in

