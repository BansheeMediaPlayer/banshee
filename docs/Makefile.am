DOC_UPDATER = @MONODOCER@ -delete
DOC_ASSEMBLER = @MDASSEMBLER@ --out banshee-docs --ecma

ASSEMBLIES = \
	$(top_builddir)/hal-sharp/hal-sharp.dll \
	$(top_builddir)/src/Banshee.Base/Banshee.Base.dll \
	$(top_builddir)/src/Banshee.Widgets/Banshee.Widgets.dll \
	$(top_builddir)/src/MusicBrainz/MusicBrainz.dll

ASSEMBLIES_BUILD = $(foreach asm,$(ASSEMBLIES),$(addprefix -assembly:,$(asm)))

if BUILD_DOCS
all: MonodocNodeConfig.exe

MonodocNodeConfig.exe: MonodocNodeConfig.cs
	mcs -out:$@ -r:System.Xml $<

monodocdir = $(DOCDIR)
monodoc_DATA = \
	banshee-docs.zip \
	banshee-docs.tree \
	banshee-docs.source

update-docs: $(ASSEMBLIES)
	$(DOC_UPDATER) $(ASSEMBLIES_BUILD) -path:en

update-cvs:
	cd en/; \
	for i in `find . | grep \.remove$$`; do \
		name=`echo $$i | sed -e 's/.remove//g'`; \
		cvs remove $$name && rm $$i; \
	done; \
	for i in `find .`; do \
		if [ -d $$i ]; then \
			if [ ! "x`basename $$i`" = "xCVS" ]; then \
				cvs add $$i/*.xml; \
			fi \
		fi \
	done; \
	cd ..;

update-html:
	if [ -d banshee ]; then \
		rm -rf banshee; \
	fi; \
	mkdir banshee; \
	monodocs2html --source en --dest banshee; 

upload-html:
	scp -r banshee banshee@banshee-project.org:/home/banshee/public_html/files/documentation/;
	rm -rf banshee

banshee-docs.zip banshee-docs.tree: $(srcdir)/en/*/*.xml $(srcdir)/en/*.xml
	$(DOC_ASSEMBLER) $(srcdir)/en

install-data-hook:
	$(MONO) $(top_builddir)/docs/MonodocNodeConfig.exe --insert "Banshee Libraries" classlib-banshee $(DOCDIR)/../monodoc.xml

uninstall-hook:
	$(MONO) $(top_builddir)/docs/MonodocNodeConfig.exe --remove classlib-banshee $(DOCDIR)/../monodoc.xml
endif

EXTRA_DIST = \
	$(srcdir)/en/*.xml \
	$(srcdir)/en/*/*.xml \
	banshee-docs.source \
	MonodocNodeConfig.cs
	
DISTCLEANFILES = \
	banshee-docs.zip \
	banshee-docs.tree

MAINTAINERCLEANFILES = \
	Makefile.in

CLEANFILES = \
	MonodocNodeConfig.exe

