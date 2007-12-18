include $(top_srcdir)/build/Common.Makefile

BANSHEE = banshee
ASSEMBLY = $(BANSHEE).exe
ASSEMBLY_GCONF_SCHEMA = banshee-interface.schemas.in

bansheedir = $(pkglibdir)
bin_SCRIPTS = $(BANSHEE)
banshee_SCRIPTS = \
	$(ASSEMBLY) \
	$(ASSEMBLY).mdb \
	$(ASSEMBLY).config 

BANSHEE_SOURCE_IN = $(top_srcdir)/src/AssemblyInfo.cs.in
BANSHEE_GENERATED_SOURCE = $(BANSHEE_SOURCE_IN:.in=)
	
BANSHEE_SOURCE = \
	$(BANSHEE_GENERATED_SOURCE) \
	Main.cs \
	StockIcons.cs \
	PlayerInterface.cs \
	PlaylistView.cs \
	PlaylistModel.cs \
	TrackViewColumnWindow.cs \
	Banshee.TrackView.Columns/TrackViewColumn.cs \
	Banshee.TrackView.Columns/TrackNumberColumn.cs \
	Banshee.TrackView.Columns/ArtistColumn.cs \
	Banshee.TrackView.Columns/TitleColumn.cs \
	Banshee.TrackView.Columns/AlbumColumn.cs \
	Banshee.TrackView.Columns/GenreColumn.cs \
	Banshee.TrackView.Columns/YearColumn.cs \
	Banshee.TrackView.Columns/DurationColumn.cs \
	Banshee.TrackView.Columns/PlayCountColumn.cs \
	Banshee.TrackView.Columns/LastPlayedColumn.cs \
	Banshee.TrackView.Columns/RatingColumn.cs \
	Banshee.TrackView.Columns/UriColumn.cs \
	Banshee.TrackView.Columns/DateAddedColumn.cs
	
BANSHEE_RESOURCES = \
	data/banshee.glade \
	data/images/advanced-icon-32.png \
	data/images/blue-speaker.png \
	data/images/busy-spinner-36.png \
	data/images/busy-spinner.png \
	data/images/encoding-icon-32.png \
	data/images/icon-album.png \
	data/images/icon-artist.png \
	data/images/icon-title.png \
	data/images/library-icon-32.png \
	data/images/media-eject.png \
	data/images/media-skip-forward.png \
	data/images/media-playback-pause.png \
	data/images/media-playback-start.png \
	data/images/media-skip-backward.png \
	data/images/now-playing-arrow.png \
	data/images/playlist-icon-large.png \
	data/images/song-drm.png \
	data/images/source-cd-audio.png \
	data/images/source-library.png \
	data/images/source-playlist-16.png \
	data/images/source-playlist-22.png \
	data/images/source-localqueue.png \
	data/images/sync-needed.png \
	data/images/tray-icon.png \
	data/images/audio-volume-decrease.png \
	data/images/audio-volume-increase.png \
	data/images/audio-volume-high.png \
	data/images/audio-volume-medium.png \
	data/images/audio-volume-low.png \
	data/images/audio-volume-muted.png \
	data/images/cd-action-rip-16.png \
	data/images/cd-action-rip-24.png \
	data/images/cd-action-burn-24.png \
	data/images/cd-action-burn-32.png \
	data/images/cd-action-burn-48.png \
	data/images/encode-action-24.png

BANSHEE_EXPANDED_RESOURCES = $(foreach resource,$(BANSHEE_RESOURCES), $(top_srcdir)/$(resource))

BANSHEE_EXPANDED_RESOURCES_BUILD = $(foreach resource,$(BANSHEE_EXPANDED_RESOURCES), $(addprefix -resource:,$(resource)),$(notdir $(resource)))

BANSHEE_SOURCE_BUILD = $(addprefix $(srcdir)/, $(BANSHEE_SOURCE))

$(ASSEMBLY): $(BANSHEE_SOURCE_BUILD) $(BANSHEE_EXPANDED_RESOURCES)
	$(BUILD_UI_BANSHEE) -out:$@ -nowarn:0169 $(BANSHEE_EXPANDED_RESOURCES_BUILD) $(BANSHEE_SOURCE_BUILD)

include $(top_srcdir)/build/gconf-schema-rules

run: $(ASSEMBLY)
	$(RUN_PATH) $(MONO) $(ASSEMBLY)

run-nocheck: 
	$(RUN_PATH) $(MONO) $(ASSEMBLY)

EXTRA_DIST = \
	$(BANSHEE_SOURCE) \
	$(BANSHEE_SOURCE_IN) \
	$(BANSHEE).in \
	$(ASSEMBLY).config.in \
	$(ASSEMBLY_GCONF_SCHEMA)

CLEANFILES = $(BANSHEE) $(ASSEMBLY) *.mdb *.dll *.pidb
DISTCLEANFILES = $(BANSHEE_GENERATED_SOURCE) $(schema_DATA)
MAINTAINERCLEANFILES = Makefile.in

