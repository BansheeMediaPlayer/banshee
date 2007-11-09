AC_DEFUN([BANSHEE_CHECK_NDESK_DBUS],
[
	AC_ARG_ENABLE(external_ndesk_dbus, AC_HELP_STRING([--enable-external-ndesk-dbus], 
		[Use external NDesk DBus instead of the bundled one]), 
		enable_external_ndesk_dbus="yes", enable_external_ndesk_dbus="no")

	if test "x$enable_external_ndesk_dbus" = "xyes"; then
		PKG_CHECK_MODULES(NDESK_DBUS, ndesk-dbus-1.0 >= 0.4 \
			ndesk-dbus-glib-1.0 >= 0.3)
		AC_SUBST(NDESK_DBUS_LIBS)
		NDESK_DBUS_PC_REQUIRES="ndesk-dbus-1.0 ndesk-dbus-glib-1.0"
		AC_SUBST(NDESK_DBUS_PC_REQUIRES)
		AM_CONDITIONAL(EXTERNAL_NDESK_DBUS, true)
	else
		AC_MSG_RESULT([no])
		NDESK_DBUS_PC_LIBS="-r:${expanded_libdir}/banshee/NDesk.DBus.dll -r:${expanded_libdir}/banshee/NDesk.DBus.GLib.dll"
		AC_SUBST(NDESK_DBUS_PC_LIBS)
		AM_CONDITIONAL(EXTERNAL_NDESK_DBUS, false)
	fi
])

