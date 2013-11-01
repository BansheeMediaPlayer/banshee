AC_DEFUN([BANSHEE_CHECK_GSTREAMER],
[
	AC_ARG_ENABLE(gst_native, AC_HELP_STRING([--disable-gst-native], [Disable GStreamer native backend]), , enable_gst_native="yes")

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
	else
		AM_CONDITIONAL(ENABLE_GST_NATIVE, false)
	fi
])
