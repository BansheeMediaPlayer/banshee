UNIQUE_FILTER_PIPE = tr [:space:] \\n | sort | uniq
BUILD_DATA_DIR = $(top_builddir)/bin/share/$(PACKAGE)

INSTALL_ICONS = $(top_srcdir)/build/private-icon-theme-installer "$(mkinstalldirs)" "$(INSTALL_DATA)"

INSTALL_DIR_RESOLVED = $(firstword $(subst , $(DEFAULT_INSTALL_DIR), $(INSTALL_DIR)))

FILTERED_LINK = $(shell echo "$(LINK)" | $(UNIQUE_FILTER_PIPE))
DEP_LINK = $(shell echo "$(LINK)" | $(UNIQUE_FILTER_PIPE) | sed s,-r:,,g | grep '$(top_builddir)/bin/')
DLL_MAP_VERIFIER_ASSEMBLY_NAME = dll-map-verifier.exe
DLL_MAP_VERIFIER_ASSEMBLY = $(top_srcdir)/build/$(DLL_MAP_VERIFIER_ASSEMBLY_NAME)

moduledir = $(INSTALL_DIR_RESOLVED)
module_SCRIPTS = $(OUTPUT_FILES)

all: $(ALL_TARGETS)

run:
	@pushd $(top_builddir); \
	make run; \
	popd;

test:
	@pushd $(top_builddir)/tests; \
	make $(ASSEMBLY); \
	popd;

build-debug:
	@echo $(DEP_LINK)

$(DLL_MAP_VERIFIER_ASSEMBLY): $(top_srcdir)/build/DllMapVerifier.cs
	$(MCS) -out:$@ $<

$(ASSEMBLY_FILE).mdb: $(ASSEMBLY_FILE)

$(ASSEMBLY_FILE): $(SOURCES_BUILD) $(RESOURCES_EXPANDED) $(DEP_LINK) $(DLL_MAP_VERIFIER_ASSEMBLY)
	@mkdir -p $(top_builddir)/bin
	@if [ ! "x$(ENABLE_RELEASE)" = "xyes" ]; then \
		$(top_srcdir)/build/dll-map-makefile-verifier $(srcdir)/Makefile.am $(srcdir)/$(notdir $@.config) && \
		$(MONO) $(top_builddir)/build/$(DLL_MAP_VERIFIER_ASSEMBLY_NAME) \
			$(srcdir)/$(notdir $@.config) \
			-iwinmm \
			-ilibbanshee \
			-ilibbnpx11 \
			-ilibc \
			-ilibc.so.6 \
			-iintl \
			-ilibmtp.dll \
			-ilibgtkmacintegration-gtk3.dylib \
			-iCFRelease \
			$(SOURCES_BUILD); \
	fi;
	$(MCS) \
		$(MCS_FLAGS) \
		$(ASSEMBLY_BUILD_FLAGS) \
		$$warn \
		-debug -target:$(TARGET) -out:$@ \
		$(BUILD_DEFINES) $(ENABLE_TESTS_FLAG) \
		$(FILTERED_LINK) $(RESOURCES_BUILD) $(SOURCES_BUILD)
	@if [ -e $(srcdir)/$(notdir $@.config) ]; then \
		cp $(srcdir)/$(notdir $@.config) $(top_builddir)/bin; \
	fi;
	@if [ ! -z "$(EXTRA_BUNDLE)" ]; then \
		cp $(EXTRA_BUNDLE) $(top_builddir)/bin; \
	fi;

theme-icons: $(THEME_ICONS_SOURCE)
	@$(INSTALL_ICONS) -il "$(BUILD_DATA_DIR)" "$(srcdir)" $(THEME_ICONS_RELATIVE)

install-data-hook: $(THEME_ICONS_SOURCE)
	@$(INSTALL_ICONS) -i "$(DESTDIR)$(pkgdatadir)" "$(srcdir)" $(THEME_ICONS_RELATIVE)
	$(EXTRA_INSTALL_DATA_HOOK)

uninstall-hook: $(THEME_ICONS_SOURCE)
	@$(INSTALL_ICONS) -u "$(DESTDIR)$(pkgdatadir)" "$(srcdir)" $(THEME_ICONS_RELATIVE)
	$(EXTRA_UNINSTALL_HOOK)

