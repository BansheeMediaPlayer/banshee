MCS_FLAGS = -debug 
ASSEMBLY_NAME = Banshee.Dap.Njb
ASSEMBLY = $(ASSEMBLY_NAME).dll
EXTRA_DIST = $(ASSEMBLY_SOURCES) njb-sharp.dll.config.in

CLEANFILES = $(ASSEMBLY) *.dll *.exe
DISTCLEANFILES = *.mdb
MAINTAINERCLEANFILES = Makefile.in 

ASSEMBLY_SOURCES = \
	$(srcdir)/NjbDap.cs \
	$(srcdir)/NjbDapTrackInfo.cs

if ENABLE_NJB
bansheedapdir = $(pkglibdir)/Banshee.Dap
bansheedap_SCRIPTS = $(ASSEMBLY) $(ASSEMBLY).mdb njb-sharp.dll.config
endif

$(ASSEMBLY): $(ASSEMBLY_SOURCES)
	$(MCS) $(MCS_FLAGS) -target:library -out:$@ -r:$(top_builddir)/src/Banshee.Base/Banshee.Base.dll -r:$(top_builddir)/src/Banshee.Widgets/Banshee.Widgets.dll -r:$(top_builddir)/hal-sharp/hal-sharp.dll -r:Mono.Posix $(GTKSHARP_LIBS) $(NJBSHARP_LIBS) $(ASSEMBLY_SOURCES)

install-data-hook:
	for i in $(NJBSHARP_INSTALL_FILES); do \
		$(INSTALL) -m 0755 $$i $(DESTDIR)$(bansheedapdir); \
	done;

uninstall-hook:
	for i in $(NJBSHARP_INSTALL_FILES); do \
		rm -f $(DESTDIR)$(bansheedapdir)/`basename $$i`; \
	done;
	

