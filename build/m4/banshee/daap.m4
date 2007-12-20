AC_DEFUN([BANSHEE_CHECK_DAAP],
[
	AC_ARG_ENABLE(daap, AC_HELP_STRING([--disable-daap], 
		[Do not build with DAAP support]), 
		enable_daap=no, enable_daap=yes)

	if test "x$enable_daap" = "xyes"; then 
        PKG_CHECK_MODULES(MONOZEROCONF, mono-zeroconf)
        AC_SUBST(MONOZEROCONF_LIBS)
		AM_CONDITIONAL(DAAP_ENABLED, true)
	else
		AM_CONDITIONAL(DAAP_ENABLED, false)
	fi
])

