AC_DEFUN([BANSHEE_CHECK_LIBWEBKIT],
[
	WEBKIT_MIN_VERSION=1.2.2
	SOUP_MIN_VERSION=2.26
	SOUP_GNOME_MIN_VERSION=2.26

	AC_ARG_ENABLE(webkit, AC_HELP_STRING([--disable-webkit], [Disable extensions which require WebKit]), , enable_webkit="yes")

	if test "x$enable_webkit" = "xyes"; then
		have_libwebkit=no
		PKG_CHECK_MODULES(LIBWEBKIT,
			webkit-1.0 >= $WEBKIT_MIN_VERSION
			libsoup-2.4 >= $SOUP_MIN_VERSION,
			have_libwebkit=yes, have_libwebkit=no)
		AC_SUBST(LIBWEBKIT_LIBS)
		AC_SUBST(LIBWEBKIT_CFLAGS)
		AM_CONDITIONAL(HAVE_LIBWEBKIT, [test x$have_libwebkit = xyes])

		PKG_CHECK_MODULES(LIBSOUP_2_38,
			libsoup-gnome-2.4 >= 2.38,
			have_libsoup_2_28=yes, have_libsoup_2_28=no)
		if test x$have_libsoup_2_28 = xyes; then
			AC_DEFINE(HAVE_LIBSOUP_2_38, 1, [libsoup-gnome-2.4 >= 2.38 detected])
		fi

		have_libsoup_gnome=no
		PKG_CHECK_MODULES(LIBSOUP_GNOME,
			libsoup-gnome-2.4 >= $SOUP_GNOME_MIN_VERSION,
			have_libsoup_gnome=$have_libwebkit, have_libsoup_gnome=no)
		AC_SUBST(LIBSOUP_GNOME_LIBS)
		AC_SUBST(LIBSOUP_GNOME_CFLAGS)
		AM_CONDITIONAL(HAVE_LIBSOUP_GNOME, [test x$have_libsoup_gnome = xyes])
		if test x$have_libsoup_gnome = xyes; then
			AC_DEFINE(HAVE_LIBSOUP_GNOME, 1, [libsoup-gnome-2.4 detected])
		fi
	else
		have_libwebkit=no
		have_libsoup_gnome=no
		AM_CONDITIONAL(HAVE_LIBWEBKIT, false)
		AM_CONDITIONAL(HAVE_LIBSOUP_GNOME, false)
	fi
])

