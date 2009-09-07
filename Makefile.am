SUBDIRS = \
	build \
	data \
	libbanshee \
	gstreamer \
	src \
	docs \
	tests \
	extras \
	po

# Important targets

clean-local:
	rm -rf $(top_builddir)/bin

uninstall-hook:
	if test `find $(pkglibdir) | wc -l` -eq 1; then \
		rmdir $(pkglibdir); \
	fi

dist-hook:
	@( \
	  echo "`date +%Y-%m-%d`  The Banshee Project  <http://banshee-project.org>"; \
	  echo; \
	  echo "@Banshee $(VERSION)"; \
	  echo; \
	  echo "@You probably are looking for the useful change summary detailed"; \
	  echo "@in the NEWS file, perfect for downstream maintainers."; \
	  echo; \
	  echo "@If you care about commit-level changes, use 'git log' against"; \
	  echo "@a clone of our git repository (git://git.gnome.org/banshee)."; \
	  echo; \
	) | tr '@' '\t' > $(top_distdir)/ChangeLog

# Developer targets

LC_BUILD = $(top_builddir)/bin/share/locale
PO_FILES = $(addprefix po/, $(addsuffix .gmo, $(ALL_LINGUAS)))
MO_FILES = $(patsubst po/%.gmo, $(LC_BUILD)/%/LC_MESSAGES/$(PACKAGE).mo, $(PO_FILES))

$(MO_FILES): $(LC_BUILD)/%/LC_MESSAGES/$(PACKAGE).mo: po/%.gmo
	$(mkdir_p) $(dir $@)
	cp -af '$<' '$@'

run: $(MO_FILES)
	(cd bin; $(MONO) --debug $(BANSHEE_DEV_MONO_OPTIONS) Nereid.exe --debug --uninstalled $(BANSHEE_DEV_OPTIONS) | tee last-run-log)

run-perf: run
	extras/perf-analyze-log bin/last-run-log

gdb:
	@pushd bin; \
	gdb mono --eval-command="handle SIGXCPU SIG35 SIGPWR nostop noprint" --eval-command="b g_return_if_fail_warning" --eval-command="r --debug Nereid.exe --debug --uninstalled"; \
	popd;

mdb:
	@pushd bin; \
	mdb -run Nereid.exe --debug --uninstalled $(BANSHEE_DEV_OPTIONS); \
	popd;

csharp:
	@pushd .; \
    echo ""; \
    echo "Starting csharp with Gtk#, Hyena, Banshee, etc referenced"; \
    csharp -lib:bin/ -pkg:glib-sharp-2.0 -pkg:gtk-sharp-2.0 -pkg:gconf-sharp-2.0 -pkg:dbus-sharp -pkg:taglib-sharp -pkg:ipod-sharp -pkg:mono-addins $(addprefix "-reference:", $(wildcard bin/*.dll)); \
	popd;

test:
	@pushd tests; \
	make test \
	popd;

hg:
	pushd bin; \
	echo "class Entry { static void Main () { Hyena.Gui.TestModuleRunner.Run (); } }" > tester.cs; \
	gmcs -r:Hyena.Gui.dll tester.cs; \
	mono --debug tester.exe; \
	rm tester.*; \
	popd; 

EXTRA_DIST = \
	intltool-extract.in \
	intltool-merge.in \
	intltool-update.in \
	AUTHORS \
	HACKING

CLEANFILES = \
	intltool-extract \
	intltool-update \
	intltool-merge

DISTCLEANFILES = \
	*.bak \
	*~ \
	*.pidb

MAINTAINERCLEANFILES = \
	compile \
	INSTALL \
	config.h.in \
	aclocal.m4 \
	ltmain.sh \
	Makefile.in \
	depcomp \
	missing \
	install-sh \
	configure \
	config.sub \
	config.guess \
	intltool-extract.in \
	intltool-merge.in   \
	intltool-update.in  \
	mkinstalldirs
