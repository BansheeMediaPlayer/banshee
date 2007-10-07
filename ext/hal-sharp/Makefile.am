include $(top_srcdir)/build/Common.Makefile 

ASSEMBLY_NAME = Hal
ASSEMBLY = $(ASSEMBLY_NAME).dll

haldir = $(pkglibdir)
hal_SCRIPTS = $(ASSEMBLY) $(ASSEMBLY).mdb

ASSEMBLY_SOURCES = \
	$(srcdir)/Manager.cs \
	$(srcdir)/Device.cs \
	$(srcdir)/Volume.cs

all: $(ASSEMBLY)

$(ASSEMBLY): $(ASSEMBLY_SOURCES)
	$(BUILD_LIB) -out:$@ $(LINK_GTK) $(LINK_DBUS) $(ASSEMBLY_SOURCES)

EXTRA_DIST = $(ASSEMBLY_SOURCES)
CLEANFILES = $(hal_SCRIPTS) *.pidb
MAINTAINERCLEANFILES = Makefile.in

