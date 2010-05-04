ASSEMBLY = Banshee.Lastfm
TARGET = library
LINK = $(REF_EXTENSION_LASTFM) -r:Mono.Security
INSTALL_DIR = $(EXTENSIONS_INSTALL_DIR)

SOURCES =  \
	Banshee.Lastfm.Audioscrobbler/AudioscrobblerService.cs \
	Banshee.Lastfm.Audioscrobbler/Queue.cs \
	Banshee.Lastfm.Radio/LastfmActions.cs \
	Banshee.Lastfm.Radio/LastfmColumnController.cs \
	Banshee.Lastfm.Radio/LastfmPreferences.cs \
	Banshee.Lastfm.Radio/LastfmSource.cs \
	Banshee.Lastfm.Radio/LastfmSourceContents.cs \
	Banshee.Lastfm.Radio/LastfmTrackInfo.cs \
	Banshee.Lastfm.Radio/StationEditor.cs \
	Banshee.Lastfm.Radio/StationSource.cs \
	Banshee.Lastfm.Radio/StationType.cs \
	Banshee.Lastfm.Recommendations/ContextPage.cs \
	Banshee.Lastfm.Recommendations/RecommendationPane.cs \
	Banshee.Lastfm.Recommendations/SimilarArtistTile.cs


RESOURCES =  \
	Banshee.Lastfm.addin.xml \
	Resources/ActiveSourceUI.xml \
	Resources/AudioscrobblerMenu.xml \
	Resources/GlobalUI.xml \
	Resources/LastfmTrackActions.xml \
	Resources/no-results.png \
	Resources/RecommendationMenu.xml

include $(top_srcdir)/build/build.mk

