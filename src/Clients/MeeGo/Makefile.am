ASSEMBLY = MeeGo
TARGET = exe
LINK = $(REF_MEEGO)
SOURCES = Banshee.MeeGo.Client/MeeGoClient.cs 

if HAVE_MEEGO
include $(top_srcdir)/build/build.mk
else
EXTRA_DIST = $(SOURCES)
endif
