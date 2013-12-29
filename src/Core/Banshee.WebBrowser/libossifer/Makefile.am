SOURCES = \
	ossifer-web-view.c ossifer-web-view.h \
	ossifer-session.c

if HAVE_LIBWEBKIT
ossiferdir = $(pkglibdir)
ossifer_LTLIBRARIES = libossifer.la

libossifer_la_SOURCES = $(SOURCES)
libossifer_la_CFLAGS = $(LIBWEBKIT_CFLAGS) -D_FORTIFY_SOURCE=2 -ggdb3 -Wall
libossifer_la_LIBADD = $(LIBWEBKIT_LIBS)
libossifer_la_LDFLAGS = -avoid-version -module

all: $(top_builddir)/bin/libossifer.so

$(top_builddir)/bin/libossifer.so: libossifer.la
	mkdir -p $(top_builddir)/bin
	cp -f .libs/$(notdir $@) $@

CLEANFILES = $(top_builddir)/bin/libossifer.so
else
EXTRA_DIST = $(SOURCES)
endif

MAINTAINERCLEANFILES = Makefile.in
