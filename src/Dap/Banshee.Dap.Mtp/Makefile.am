include $(top_srcdir)/build/Common.Makefile

ASSEMBLY_NAME = Banshee.Dap.Mtp
ASSEMBLY = $(ASSEMBLY_NAME).dll

ASSEMBLY_SOURCES = \
	$(top_srcdir)/src/AssemblyInfo.cs \
	$(srcdir)/MtpDap.cs \
	$(srcdir)/MtpDapTrackInfo.cs \
	$(srcdir)/MtpDapPlaylistSource.cs

if ENABLE_MTP
bansheedapdir = $(pkglibdir)/Banshee.Dap
bansheedap_SCRIPTS = $(ASSEMBLY) $(ASSEMBLY).mdb
endif

$(ASSEMBLY): $(ASSEMBLY_SOURCES)
	$(BUILD_LIB_BANSHEE_CORE) -out:$@ $(LINK_HAL) $(LINK_GTK) $(LINK_MONO_UNIX) $(LIBGPHOTO2SHARP_LIBS) $(ASSEMBLY_SOURCES)

install-data-hook:
	for cur in $(LIBGPHOTO2SHARP_ASSEMBLIES); do \
		$(INSTALL) -m 0755 $$cur $(DESTDIR)$(bansheedapdir); \
		$(INSTALL) -m 0755 $${cur}.mdb $(DESTDIR)$(bansheedapdir); \
		$(INSTALL) -m 0755 $${cur}.config $(DESTDIR)$(bansheedapdir); \
	done;

uninstall-hook:
	for cur in $(LIBGPHOTO2SHARP_ASSEMBLIES); do \
		rm -f $(DESTDIR)$(bansheedapdir)/`basename $$cur`; \
		rm -f $(DESTDIR)$(bansheedapdir)/`basename $${cur}.mdb`; \
		rm -f $(DESTDIR)$(bansheedapdir)/`basename $${cur}.config`; \
	done;
	
EXTRA_DIST = $(ASSEMBLY_SOURCES)
CLEANFILES = $(ASSEMBLY) $(ASSEMBLY).mdb
DISTCLEANFILES = $(ASSEMBLY).mdb
MAINTAINERCLEANFILES = Makefile.in 

