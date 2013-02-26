AC_DEFUN([BANSHEE_CHECK_GCONF],
[
	AC_PATH_PROG(GCONFTOOL, gconftool-2, no)

	# libgconf check needed because its -devel pkg should contain AM_GCONF_SOURCE_2 macro, see bgo#604416
	PKG_CHECK_MODULES(LIBGCONF, gconf-2.0)

	# needed so autoconf doesn't complain before checking the existence of libgconf2-devel above
	m4_pattern_allow([AM_GCONF_SOURCE_2])

	AM_GCONF_SOURCE_2

	# dbus-glib is needed for the workaround for bgo#692374
	PKG_CHECK_MODULES(DBUS_GLIB, dbus-glib-1 >= 0.80, have_dbus_glib="yes", have_dbus_glib="no")
	if test "x$have_dbus_glib" = "xyes"; then
		AM_CONDITIONAL(HAVE_DBUS_GLIB, true)
	else
		AM_CONDITIONAL(HAVE_DBUS_GLIB, false)
	fi
])
