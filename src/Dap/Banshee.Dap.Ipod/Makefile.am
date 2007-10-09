include $(top_srcdir)/build/Common.Makefile

ASSEMBLY_NAME = Banshee.Dap.Ipod
ASSEMBLY = $(ASSEMBLY_NAME).dll

ASSEMBLY_SOURCES = \
	$(top_srcdir)/src/AssemblyInfo.cs \
	$(srcdir)/IpodDap.cs \
	$(srcdir)/IpodMetadataProvider.cs \
	$(srcdir)/IpodQueryJob.cs \
	$(srcdir)/IpodDapTrackInfo.cs \
	$(srcdir)/UnsupportedDatabaseView.cs \
	$(srcdir)/DatabaseRebuilder.cs \
	$(srcdir)/HalDevice.cs

if ENABLE_IPOD
bansheedapdir = $(pkglibdir)/Banshee.Dap
bansheedap_SCRIPTS = $(ASSEMBLY) $(ASSEMBLY).mdb ipod-sharp.dll.config
endif

$(ASSEMBLY): $(ASSEMBLY_SOURCES)
	$(BUILD_LIB_BANSHEE_CORE) -out:$@ $(LINK_TAGLIB) $(LINK_HAL) $(LINK_GTK) $(LINK_MONO_UNIX) $(IPODSHARP_LIBS) $(ASSEMBLY_SOURCES)

install-data-hook:
	for ASM in $(IPODSHARP_ASSEMBLIES); do \
		$(INSTALL) -m 0755 $$ASM $(DESTDIR)$(bansheedapdir); \
	done;
	
uninstall-hook:
	for ASM in $(IPODSHARP_ASSEMBLIES); do \
		rm -f $(DESTDIR)$(bansheedapdir)/`basename $$ASM`; \
	done;

EXTRA_DIST = $(ASSEMBLY_SOURCES) ipod-sharp.dll.config.in
CLEANFILES = $(ASSEMBLY) 
DISTCLEANFILES = $(ASSEMBLY).mdb
MAINTAINERCLEANFILES = Makefile.in

