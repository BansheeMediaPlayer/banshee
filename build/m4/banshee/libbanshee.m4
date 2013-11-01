AC_DEFUN([BANSHEE_CHECK_LIBBANSHEE],
[
	AC_ISC_POSIX
	AC_PROG_CC

	AC_HEADER_STDC

	# needed so autoconf doesn't complain before checking the existence of glib-2.0 in configure.ac
	m4_pattern_allow([AM_PATH_GLIB_2_0])
	AM_PATH_GLIB_2_0

	LIBBANSHEE_LIBS=""
	LIBBANSHEE_CFLAGS=""

	PKG_CHECK_MODULES(GTK, gtk+-3.0 >= 3.0)
	SHAMROCK_CONCAT_MODULE(LIBBANSHEE, GTK)

	AC_ARG_ENABLE(clutter, AS_HELP_STRING([--enable-clutter],
		[Enable support for clutter video sink]), , enable_clutter="no")

	if test "x$enable_clutter" = "xyes"; then
		PKG_CHECK_MODULES(CLUTTER,
			clutter-1.0 >= 1.0.1,
			enable_clutter=yes)
		SHAMROCK_CONCAT_MODULE(LIBBANSHEE, CLUTTER)
		AC_DEFINE(HAVE_CLUTTER, 1,
			[Define if the video sink should be Clutter])
	fi

	AM_CONDITIONAL(HAVE_CLUTTER, test "x$enable_clutter" = "xyes")

	AC_SUBST(LIBBANSHEE_CFLAGS)
	AC_SUBST(LIBBANSHEE_LIBS)
])

