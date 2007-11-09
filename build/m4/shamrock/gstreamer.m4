AC_DEFUN([SHAMROCK_CHECK_GSTREAMER_PLUGINS],
[
	AC_ARG_ENABLE(gstreamer_plugins_check,
		AC_HELP_STRING([--disable-gstreamer-plugins-check],
		[Don't check if the required GStreamer plugins are available]),
		with_gstreamer_plugins_check=no,
		with_gstreamer_plugins_check=yes)
	
	if test "x$with_gstreamer_plugins_check" = "xyes"; then
		gst_version=$1
		gst_pkg_name="gstreamer-$gst_version"
		gst_inspect="gst-inspect-$gst_version"
		gst_toolsdir=`$PKG_CONFIG --variable=toolsdir $gst_pkg_name`
		gst_inspect="$gst_toolsdir/$gst_inspect"

		AC_MSG_CHECKING([for gst-inspect])
		if ! test -x $gst_inspect; then
			AC_MSG_RESULT([no])
			AC_MSG_ERROR([Cannot find required gst-inspect tool.])
		else
			AC_MSG_RESULT([$gst_inspect])
		fi

		for element in $(echo "$*" | cut -d, -f2- | sed 's/\,/ /g'); do
			AC_MSG_CHECKING([for GStreamer $gst_version $element plugin])
			if $gst_inspect $element > /dev/null 2>/dev/null; then
				AC_MSG_RESULT([yes])
			else
				AC_MSG_RESULT([no])
				AC_MSG_ERROR([Cannot find required GStreamer-$gst_version plugin '$element'.])
			fi
		done;
	fi
])

AC_DEFUN([SHAMROCK_CHECK_GSTREAMER_0_10_PLUGINS],
[
	SHAMROCK_CHECK_GSTREAMER_PLUGINS(0.10, $*)
])

