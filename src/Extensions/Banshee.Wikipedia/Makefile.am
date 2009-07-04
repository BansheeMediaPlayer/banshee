ASSEMBLY = Banshee.Wikipedia
TARGET = library
LINK = $(LINK_BANSHEE_THICKCLIENT_DEPS) $(LINK_WEBKIT)
INSTALL_DIR = $(EXTENSIONS_INSTALL_DIR)

SOURCES =  \
	Banshee.Wikipedia/ContextPage.cs \
	Banshee.Wikipedia/WikipediaView.cs

RESOURCES = Banshee.Wikipedia.addin.xml

if HAVE_WEBKIT
include $(top_srcdir)/build/build.mk
else
EXTRA_DIST = $(SOURCES) $(RESOURCES)
endif

