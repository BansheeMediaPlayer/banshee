AC_DEFUN([BANSHEE_CHECK_MOONLIGHT],
[
	AC_ARG_ENABLE(moonlight, AC_HELP_STRING([--enable-moonlight], [Enable Moonlight-enhanced effects [[default=auto]]]), , enable_moonlight="no")
	
	if test "x$enable_moonlight" = "xauto"; then
		PKG_CHECK_EXISTS(gtksilver >= 0.8 silverdesktop >= 0.8, enable_moonlight="yes", enable_moonlight="no")
	fi
	
	if test "x$enable_moonlight" = "xyes"; then
		PKG_CHECK_MODULES(MOONLIGHT, gtksilver >= 0.8 silverdesktop >= 0.8)
		AC_SUBST(MOONLIGHT_LIBS)
		
		AM_CONDITIONAL(HAVE_MOONLIGHT, true)
	else
		AM_CONDITIONAL(HAVE_MOONLIGHT, false)
	fi
])

