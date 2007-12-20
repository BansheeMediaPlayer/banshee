if DAAP_ENABLED
include $(top_srcdir)/build/Common.Makefile

ASSEMBLY_NAME = Banshee.Plugins.Daap
ASSEMBLY = $(ASSEMBLY_NAME).dll
ASSEMBLY_GCONF_SCHEMA = banshee-plugin-daap.schemas.in

daapdir = $(pkglibdir)/Banshee.Plugins
daap_SCRIPTS = $(ASSEMBLY) $(ASSEMBLY).mdb $(ASSEMBLY).config

ASSEMBLY_SOURCES = \
	$(top_srcdir)/src/AssemblyInfo.cs \
	$(srcdir)/DaapPlugin.cs \
	$(srcdir)/DaapCore.cs \
	$(srcdir)/DaapContainerSource.cs \
	$(srcdir)/DaapSource.cs \
	$(srcdir)/DaapTrackInfo.cs \
	$(srcdir)/DaapLoginDialog.cs \
	$(srcdir)/DatabaseProxy.cs \
	$(srcdir)/DaapPlaylistSource.cs \
	$(srcdir)/DaapProxyWebServer.cs \
	$(srcdir)/DaapConfigPage.cs \
	$(srcdir)/DaapErrorView.cs \
	$(srcdir)/daap-sharp/*.cs

$(ASSEMBLY): $(ASSEMBLY_SOURCES) 
	$(BUILD_LIB_BANSHEE_CORE) -out:$@  $(LINK_MONOZEROCONF) $(LINK_GTK) $(LINK_MONO_UNIX) -r:ICSharpCode.SharpZipLib -r:System.Web -resource:$(srcdir)/daap-sharp/content-codes,content-codes $(ASSEMBLY_SOURCES)

include $(top_srcdir)/build/gconf-schema-rules

endif

EXTRA_DIST = \
	$(ASSEMBLY_SOURCES) \
	$(ASSEMBLY).config.in \
	$(schema_in_files) \
	$(srcdir)/daap-sharp/content-codes \
	$(ASSEMBLY_GCONF_SCHEMA)

CLEANFILES = $(ASSEMBLY) *.dll *.exe
DISTCLEANFILES = *.mdb $(schema_DATA)
MAINTAINERCLEANFILES = Makefile.in
