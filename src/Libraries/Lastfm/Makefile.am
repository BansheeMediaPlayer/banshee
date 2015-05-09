ASSEMBLY = Lastfm
TARGET = library
LINK = $(REF_LASTFM)

SOURCES =  \
	Lastfm.Data/DataCore.cs \
	Lastfm.Data/DataEntry.cs \
	Lastfm.Data/DataEntryCollection.cs \
	Lastfm.Data/ILastfmInfo.cs \
	Lastfm.Data/LastfmAlbumData.cs \
	Lastfm.Data/LastfmArtistData.cs \
	Lastfm.Data/LastfmData.cs \
	Lastfm.Data/LastfmUserData.cs \
	Lastfm/Account.cs \
	Lastfm/AudioscrobblerConnection.cs \
	Lastfm/Browser.cs \
	Lastfm/IQueue.cs \
	Lastfm/LastfmCore.cs \
	Lastfm/LastfmRequest.cs \
	Lastfm/RadioConnection.cs \
	Lastfm/Tests/LastfmRequestTests.cs

include $(top_srcdir)/build/build.mk

test-lastfm: Test.cs
	$(MCS) -r:$(top_builddir)/bin/Lastfm.dll -out:TestLastfm.exe Test.cs && \
	mv TestLastfm.exe $(top_builddir)/bin/
	@pushd $(top_builddir)/bin/; mono --debug TestLastfm.exe; \
	popd;
