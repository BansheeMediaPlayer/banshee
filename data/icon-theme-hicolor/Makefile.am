theme = hicolor
themedir = $(datadir)/icons/$(theme)
theme_icons = \
	apps,media-player-banshee-16.png \
	apps,media-player-banshee-22.png \
	apps,media-player-banshee-24.png \
	apps,media-player-banshee-32.png \
	apps,media-player-banshee-48.png \
	apps,media-player-banshee-192.png

all:
	(for icon_desc in $(theme_icons); do \
		category=$$(echo $$icon_desc | cut -f1 -d,); \
		src_file="$(srcdir)/$$(echo $$icon_desc | cut -f2 -d,)"; \
		dest_file=$$(echo $$src_file | ${SED} -r 's,\-[0-9]+,,'); \
		size=$$(echo $$src_file | ${SED} 's,[^0-9],,g'); \
		dest_dir="$(top_builddir)/bin/share/$(PACKAGE)/icons/hicolor/$${size}x$${size}/$${category}"; \
		mkdir -p "$${dest_dir}" && cp -f "$${src_file}" "$${dest_dir}/$${dest_file}"; \
	done;) &>/dev/null || true

install_icon_exec = $(top_srcdir)/build/icon-theme-installer \
	-t "$(theme)" \
	-s "$(srcdir)" \
	-d "x$(DESTDIR)" \
	-b "$(themedir)" \
	-m "$(mkinstalldirs)" \
	-x "$(INSTALL_DATA)"

install-data-local:
	@-$(install_icon_exec) -i $(theme_icons)

uninstall-hook: 
	@-$(install_icon_exec) -u $(theme_icons)
	
MAINTAINERCLEANFILES = Makefile.in
EXTRA_DIST = $(wildcard *.png *.svg) $(wildcard src/*.svg)

