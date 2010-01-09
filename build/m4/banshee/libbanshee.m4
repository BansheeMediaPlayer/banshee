AC_DEFUN([BANSHEE_CHECK_LIBBANSHEE],
[
	AC_ISC_POSIX
	AC_PROG_CC

	AC_HEADER_STDC

	AM_PATH_GLIB_2_0

	LIBBANSHEE_LIBS=""
	LIBBANSHEE_CFLAGS=""

	GRAPHICS_SUBSYSTEM="Unknown"
	GTK_TARGET=$(pkg-config --variable=target gtk+-2.0)

	if test x$GTK_TARGET = xx11; then
		PKG_CHECK_MODULES(GDK_X11, gdk-x11-2.0 >= 2.8)
		SHAMROCK_CONCAT_MODULE(LIBBANSHEE, GDK_X11)
		GRAPHICS_SUBSYSTEM="X11"
	elif test x$GTK_TARGET = xquartz; then
		PKG_CHECK_MODULES(GDK_QUARTZ, gdk-quartz-2.0 >= 2.14)
		SHAMROCK_CONCAT_MODULE(LIBBANSHEE, GDK_QUARTZ)
		GRAPHICS_SUBSYSTEM="Quartz"
	else
		PKG_CHECK_MODULES(GTK, gtk+-2.0 >= 2.8)
	fi

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

	AM_CONDITIONAL(HAVE_X11, test "x$GRAPHICS_SUBSYSTEM" = "xX11")
	AM_CONDITIONAL(HAVE_QUARTZ, test "x$GRAPHICS_SUBSYSTEM" = "xQuartz")
	AM_CONDITIONAL(HAVE_CLUTTER, test "x$enable_clutter" = "xyes")

	AC_SUBST(GRAPHICS_SUBSYSTEM)
	AC_SUBST(LIBBANSHEE_CFLAGS)
	AC_SUBST(LIBBANSHEE_LIBS)
])

