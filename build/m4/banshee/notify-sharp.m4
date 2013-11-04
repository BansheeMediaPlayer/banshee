AC_DEFUN([BANSHEE_CHECK_NOTIFY_SHARP],
[
	PKG_CHECK_MODULES(NOTIFY_SHARP, notify-sharp-3.0, have_system_notifysharp=yes, have_system_notifysharp=no)

	if test "x$have_system_notifysharp" = "xyes"; then
		AC_SUBST(NOTIFY_SHARP_LIBS)
		AM_CONDITIONAL(EXTERNAL_NOTIFY_SHARP, true)
	else
		AM_CONDITIONAL(EXTERNAL_NOTIFY_SHARP, false)
		AC_MSG_NOTICE([Using internal copy of notify-sharp])
	fi
])

