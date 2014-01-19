include $(top_srcdir)/build/build.environment.mk

DIST_SUBDIRS = \
	Analyzer \
	Performance

if ENABLE_TESTS

TEST_ASSEMBLIES = \
	Hyena.dll \
	Hyena.Gui.dll \
	Hyena.Data.Sqlite.dll \
	Lastfm.dll \
	Migo.dll \
	Mono.Media.dll \
	Banshee.Core.dll \
	Banshee.Gnome.dll \
	Banshee.Services.dll \
	Banshee.ThickClient.dll \
	Banshee.Fixup.dll \
	Banshee.AmazonMp3.exe \
	Banshee.Dap.Mtp.dll

if ENABLE_GIO
TEST_ASSEMBLIES += Banshee.Gio.dll
endif

if ENABLE_UBUNTUONE
TEST_ASSEMBLIES += Banshee.UbuntuOneMusicStore.dll
endif

ENV_OPTIONS = TZ=America/Chicago LC_ALL=it_IT LANG=it_IT
MONO_OPTIONS = MONO_ENV_OPTIONS=--debug
NUNIT_CONSOLE = $$(echo $$(which nunit-console2 || which nunit-console))
RUNNER = \
	if [[ "x$(NUNIT_CONSOLE)" = "x" ]]; then \
		echo "NUnit runner not found; please install nunit-console or nunit-console2"; \
		exit 1; \
	fi; \
	GLOBAL_EXIT_CODE=0; \
	for asm in $${TEST_ASSEMBLIES}; do \
		echo -e "\033[1mRunning tests on $${asm}...\033[0m"; \
		$(ENV_OPTIONS) $(MONO_OPTIONS) $(NUNIT_CONSOLE) -nologo -noshadow $$asm \
			|| GLOBAL_EXIT_CODE=$$?; \
	done; \
	if [[ $${GLOBAL_EXIT_CODE} -ne 0 ]]; then \
		echo "One or more tests failed"; \
		exit $${GLOBAL_EXIT_CODE}; \
	fi

test:
	@pushd $(DIR_BIN) &>/dev/null; \
	TEST_ASSEMBLIES="$(TEST_ASSEMBLIES)"; $(RUNNER); \
	popd &>/dev/null;

%:
	@pushd $(DIR_BIN) &>/dev/null; \
	for i in *.dll; do \
		if [[ $$(echo "$$i" | tr "[:upper:]" "[:lower:]") = "$$(echo "$@" | tr "[:upper:]" "[:lower:]").dll" ]]; then \
			TEST_ASSEMBLIES="$$i"; $(RUNNER); \
		fi; \
	done;

else

test:
	echo "Please enable tests via --enable-tests at configuration"
	exit 1

endif

MAINTAINERCLEANFILES = Makefile.in

