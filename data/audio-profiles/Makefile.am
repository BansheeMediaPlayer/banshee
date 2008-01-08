audioprofilesdir = $(pkgdatadir)/audio-profiles
audioprofiles_in_files = \
	base.xml.in \
	flac.xml.in \
	mp3-lame.xml.in \
	mp3-xing.xml.in \
	vorbis.xml.in \
	wavpack.xml.in \
	wma.xml.in
audioprofiles_DATA = $(audioprofiles_in_files:.xml.in=.xml)

@INTLTOOL_XML_RULE@

EXTRA_DIST = $(audioprofiles_in_files)
DISTCLEANFILES = *.xml
MAINTAINERCLEANFILES = Makefile.in

