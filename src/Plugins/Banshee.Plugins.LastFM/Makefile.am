include $(top_srcdir)/build/Common.Makefile

lastfm_resources = \
	Resources/lastfm.glade \
	Resources/Actions.xml \
	Resources/audioscrobbler.png \
	Resources/recommended.png

lastfm_resources_build = $(foreach res,$(lastfm_resources), $(addprefix -resource:,$(srcdir)/$(res)),$(notdir $(res)))

ASSEMBLY = Banshee.Plugins.LastFM.dll
ASSEMBLY_GCONF_SCHEMA = banshee-plugin-lastfm.schemas.in

SOURCES = \
	$(srcdir)/Editor.cs \
	$(srcdir)/Connection.cs \
	$(srcdir)/Plugin.cs \
	$(srcdir)/LastFMSource.cs \
	$(srcdir)/LastFMTrackInfo.cs \
	$(srcdir)/StationSource.cs \
	$(srcdir)/StationType.cs \
	$(top_srcdir)/src/AssemblyInfo.cs

plugindir = $(pkglibdir)/Banshee.Plugins
plugin_SCRIPTS = $(ASSEMBLY) $(ASSEMBLY).mdb

all: $(ASSEMBLY)

$(ASSEMBLY): $(SOURCES) $(lastfm_resources)
	$(BUILD_LIB_BANSHEE_CORE) -out:$@ $(LINK_LAST_FM) -r:System.Data -r:System.Web -r:System.Xml $(lastfm_resources_build) $(LINK_GTK) $(SOURCES)

include $(top_srcdir)/build/gconf-schema-rules

EXTRA_DIST = $(SOURCES) $(ASSEMBLY_GCONF_SCHEMA) $(lastfm_resources)
CLEANFILES = *.dll *.mdb
MAINTAINERCLEANFILES = Makefile.in *.pidb
DISTCLEANFILES = $(schema_DATA)

