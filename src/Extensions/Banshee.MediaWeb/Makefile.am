ASSEMBLY = Banshee.MediaWeb
TARGET = library
LINK = $(LINK_BANSHEE_THICKCLIENT_DEPS) $(LINK_WEBKIT)
INSTALL_DIR = $(EXTENSIONS_INSTALL_DIR)

SOURCES =  \
	Banshee.MediaWeb/MediaWebSource.cs \
	Banshee.MediaWeb/MediaWebView.cs

RESOURCES = Banshee.MediaWeb.addin.xml

if HAVE_WEBKIT
include $(top_srcdir)/build/build.mk
else
EXTRA_DIST = $(SOURCES) $(RESOURCES)
endif

