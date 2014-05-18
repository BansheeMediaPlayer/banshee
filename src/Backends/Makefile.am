SUBDIRS = \
	Banshee.Gio \
	Banshee.Gnome \
	Banshee.GStreamer \
	Banshee.GStreamerSharp \
	Banshee.Unix \
	Banshee.Osx \
	Banshee.Windows

MAINTAINERCLEANFILES = Makefile.in

uninstall-hook:
	if test -d "$(pkglibdir)/Backends"; then \
		rmdir "$(pkglibdir)/Backends"; \
	fi

