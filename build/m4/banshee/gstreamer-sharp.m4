AC_DEFUN([BANSHEE_CHECK_GSTREAMER_SHARP],
[
	AC_ARG_ENABLE(gst_sharp, AC_HELP_STRING([--enable-gst-sharp], [Enable Gst# backend]), , enable_gst_sharp="no")

	if test "x$enable_gst_sharp" = "xyes"; then
		PKG_CHECK_MODULES(GST_SHARP, gstreamer-sharp-0.10)
		AC_SUBST(GST_SHARP_LIBS)
		AM_CONDITIONAL(ENABLE_GST_SHARP, true)
	else
		AM_CONDITIONAL(ENABLE_GST_SHARP, false)
	fi
])

