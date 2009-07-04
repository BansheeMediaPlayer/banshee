include $(top_srcdir)/build/build.environment.mk

DIST_SUBDIRS = \
	Analyzer \
	Performance

if ENABLE_TESTS

TEST_ASSEMBLIES = \
	Hyena.dll \
	Hyena.Gui.dll \
	Migo.dll \
	Mono.Media.dll \
	Banshee.Core.dll \
	Banshee.Services.dll \
	Banshee.Dap.Mtp.dll

ENV_OPTIONS = TZ=America/Chicago LC_ALL=it_IT LANG=it_IT
RUNNER = for asm in $${TEST_ASSEMBLIES}; do echo -e "\033[1mRunning tests on $${asm}...\033[0m"; $(ENV_OPTIONS) nunit-console2 -nologo -noshadow $$asm; done

test:
	@pushd $(DIR_BIN) &>/dev/null; \
	export TEST_ASSEMBLIES="$(TEST_ASSEMBLIES)"; $(RUNNER); \
	popd &>/dev/null;

%:
	@pushd $(DIR_BIN) &>/dev/null; \
	for i in *.dll; do \
		if [[ $$(echo "$$i" | tr "[:upper:]" "[:lower:]") = "$$(echo "$@" | tr "[:upper:]" "[:lower:]").dll" ]]; then \
			export TEST_ASSEMBLIES="$$i"; $(RUNNER); \
		fi; \
	done;

endif

MAINTAINERCLEANFILES = Makefile.in

