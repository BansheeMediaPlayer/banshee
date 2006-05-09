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

docdir = $(DESTDIR)$(DOCDIR)
doc_DATA = \
	banshee-docs.zip \
	banshee-docs.tree \
	banshee-docs.source

update-docs: $(ASSEMBLIES)
	$(DOC_UPDATER) $(ASSEMBLIES_BUILD) -path:en

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

