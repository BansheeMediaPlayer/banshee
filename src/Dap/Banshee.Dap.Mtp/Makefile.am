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
	$(BUILD_LIB_BANSHEE_CORE) -out:$@ $(LINK_HAL) $(LINK_GTK) $(LINK_MONO_UNIX) -r:$(top_builddir)/ext/libmtp-sharp/libmtp-sharp.dll $(ASSEMBLY_SOURCES)

EXTRA_DIST = $(ASSEMBLY_SOURCES)
CLEANFILES = $(ASSEMBLY) $(ASSEMBLY).mdb
DISTCLEANFILES = $(ASSEMBLY).mdb
MAINTAINERCLEANFILES = Makefile.in 

