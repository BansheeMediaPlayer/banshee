include $(top_srcdir)/build/Common.Makefile 

ASSEMBLY_NAME = libmtp-sharp
ASSEMBLY = $(ASSEMBLY_NAME).dll

asmdir = $(pkglibdir)/Banshee.Dap
asm_SCRIPTS = $(ASSEMBLY) $(ASSEMBLY).mdb $(ASSEMBLY).config

ASSEMBLY_SOURCES = \
	$(srcdir)/Base/Enums.cs \
	$(srcdir)/Base/Playlist.cs \
	$(srcdir)/Base/DeviceStorage.cs \
	$(srcdir)/Base/TrackStruct.cs \
	$(srcdir)/Base/DeviceEntry.cs \
	$(srcdir)/Base/Album.cs \
	$(srcdir)/Base/FolderStruct.cs \
	$(srcdir)/Base/File.cs \
	$(srcdir)/Base/Error.cs \
	$(srcdir)/Base/FileSampleData.cs \
	$(srcdir)/Base/MtpDeviceStruct.cs \
	$(srcdir)/MtpDevice.cs \
	$(srcdir)/AssemblyInfo.cs \
	$(srcdir)/Track.cs \
	$(srcdir)/Folder.cs \
	$(srcdir)/Functions/AlbumManagement.cs \
	$(srcdir)/Functions/FunctionCalls.cs \
	$(srcdir)/Functions/FolderManagement.cs \
	$(srcdir)/Functions/PlaylistManagement.cs \
	$(srcdir)/Functions/FileManagement.cs \
	$(srcdir)/Functions/TrackManagement.cs

all: $(ASSEMBLY)

$(ASSEMBLY): $(ASSEMBLY_SOURCES)
	$(BUILD_LIB) -out:$@  $(ASSEMBLY_SOURCES)

EXTRA_DIST = $(ASSEMBLY_SOURCES) $(ASSEMBLY).config.in
CLEANFILES = $(asm_SCRIPTS) *.pidb
MAINTAINERCLEANFILES = Makefile.in

