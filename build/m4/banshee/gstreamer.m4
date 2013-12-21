AC_DEFUN([BANSHEE_CHECK_GSTREAMER],
[
	GSTREAMER_SHARP_REQUIRED_VERSION=0.99.0

	AC_ARG_ENABLE(gst_native, AC_HELP_STRING([--enable-gst-native], [Enable GStreamer native backend]), , enable_gst_native="no")

	if test "x$enable_gst_native" = "xyes"; then
		BANSHEE_CHECK_LIBBANSHEE

		GSTREAMER_REQUIRED_VERSION=1.0.0
		
		PKG_CHECK_MODULES(GST,
			gstreamer-1.0 >= $GSTREAMER_REQUIRED_VERSION
			gstreamer-base-1.0 >= $GSTREAMER_REQUIRED_VERSION
			gstreamer-controller-1.0 >= $GSTREAMER_REQUIRED_VERSION
			gstreamer-plugins-base-1.0 >= $GSTREAMER_REQUIRED_VERSION
			gstreamer-audio-1.0 >= $GSTREAMER_REQUIRED_VERSION
			gstreamer-fft-1.0 >= $GSTREAMER_REQUIRED_VERSION
			gstreamer-pbutils-1.0 >= $GSTREAMER_REQUIRED_VERSION
			gstreamer-tag-1.0 >= $GSTREAMER_REQUIRED_VERSION
			gstreamer-video-1.0 >= $GSTREAMER_REQUIRED_VERSION)

		AC_SUBST(GST_CFLAGS)
		AC_SUBST(GST_LIBS)
		AM_CONDITIONAL(ENABLE_GST_NATIVE, true)

		AC_ARG_ENABLE(gst_sharp, AC_HELP_STRING([--enable-gst-sharp], [Enable Gst# backend]), , enable_gst_sharp="no")
	else
		AM_CONDITIONAL(ENABLE_GST_NATIVE, false)

		AC_ARG_ENABLE(gst_sharp, AC_HELP_STRING([--disable-gst-sharp], [Disable Gst# backend]), , enable_gst_sharp="yes")
	fi


	if test "x$enable_gst_sharp" = "xyes"; then
		PKG_CHECK_MODULES(GST_SHARP, gstreamer-sharp-1.0 >= $GSTREAMER_SHARP_REQUIRED_VERSION)
		AC_SUBST(GST_SHARP_LIBS)

		AM_CONDITIONAL(ENABLE_GST_SHARP, true)
	else
		AM_CONDITIONAL(ENABLE_GST_SHARP, false)
	fi
])
