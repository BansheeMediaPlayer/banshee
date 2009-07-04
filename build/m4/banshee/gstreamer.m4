AC_DEFUN([BANSHEE_CHECK_GSTREAMER],
[
	GSTREAMER_REQUIRED_VERSION=0.10.3
	AC_SUBST(GSTREAMER_REQUIRED_VERSION)

	PKG_CHECK_MODULES(GST,
		gstreamer-0.10 >= $GSTREAMER_REQUIRED_VERSION
		gstreamer-base-0.10 >= $GSTREAMER_REQUIRED_VERSION
		gstreamer-plugins-base-0.10 >= $GSTREAMER_REQUIRED_VERSION
		gstreamer-controller-0.10 >= $GSTREAMER_REQUIRED_VERSION
		gstreamer-dataprotocol-0.10 >= $GSTREAMER_REQUIRED_VERSION
		gstreamer-fft-0.10 >= $GSTREAMER_REQUIRED_VERSION)

	GST_LIBS="$GST_LIBS -lgstvideo-0.10 -lgstinterfaces-0.10 -lgstcdda-0.10"

	PKG_CHECK_MODULES(GST_PBUTILS, gstreamer-plugins-base-0.10 >= 0.10.12, 
		gst_pbutils=yes, gst_pbutils=no)

	if test "x$gst_pbutils" = "xyes"; then
		GST_LIBS="$GST_LIBS -lgstpbutils-0.10"
		AC_DEFINE(HAVE_GST_PBUTILS, 1, 
			[Define if GSTreamer PB Utils is available])
	else
		AC_MSG_RESULT([no])
	fi
	
	AC_SUBST(GST_CFLAGS)
	AC_SUBST(GST_LIBS)

	dnl Builtin equalizer (optional)
	AC_ARG_ENABLE(builtin-equalizer,
		AC_HELP_STRING([--disable-builtin-equalizer],
			[Disable builtin equalizer]),
		, enable_builtin_equalizer="yes")
	AM_CONDITIONAL(ENABLE_BUILTIN_EQUALIZER, test "x$enable_builtin_equalizer" = "xyes")
])

