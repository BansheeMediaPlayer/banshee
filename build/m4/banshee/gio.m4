AC_DEFUN([BANSHEE_CHECK_GIO_SHARP],
[
	GNOMESHARP_REQUIRED=2.8
	
	AC_ARG_ENABLE(gio, AC_HELP_STRING([--disable-gio], [Disable GIO for IO operations]), ,enable_gio="yes")
	AC_ARG_ENABLE(gio_hardware, AC_HELP_STRING([--disable-gio-hardware], [Disable GIO Hardware backend]), ,enable_gio_hardware="yes")
	
	if test "x$enable_gio" = "xyes"; then
		PKG_CHECK_MODULES(GTKSHARP_BEANS,
			gtk-sharp-beans-2.0 >= $GNOMESHARP_REQUIRED,
			enable_gio=yes, enable_gio=no)

		PKG_CHECK_MODULES(GIOSHARP,
			gio-sharp-2.0 >= 2.22.3,
			enable_gio="$enable_gio", enable_gio=no)

		asms="`$PKG_CONFIG --variable=Libraries gio-sharp-2.0` `$PKG_CONFIG --variable=Libraries gtk-sharp-beans-2.0`"
		for asm in $asms; do
			FILENAME=`basename $asm`
			if [[ "`echo $SEENBEFORE | grep $FILENAME`" = "" ]]; then
				GIOSHARP_ASSEMBLIES="$GIOSHARP_ASSEMBLIES $asm"
				[[ -r "$asm.config" ]] && GIOSHARP_ASSEMBLIES="$GIOSHARP_ASSEMBLIES $asm.config"
				[[ -r "$asm.mdb" ]] && GIOSHARP_ASSEMBLIES="$GIOSHARP_ASSEMBLIES $asm.mdb"
				SEENBEFORE="$SEENBEFORE $FILENAME"
			fi
		done
		AC_SUBST(GIOSHARP_ASSEMBLIES)

		PKG_CHECK_MODULES(GLIB_2_22,
			glib-2.0 >= 2.22,
			enable_gio="$enable_gio", enable_gio=no)

		if test "x$enable_gio_hardware" = "xyes"; then
			PKG_CHECK_MODULES(GUDEV_SHARP,
				gudev-sharp-1.0 >= 0.1,
				enable_gio_hardware="$enable_gio", enable_gio_hardware=no)

			PKG_CHECK_MODULES(GKEYFILE_SHARP,
				gkeyfile-sharp >= 0.1,
				enable_gio_hardware="$enable_gio_hardware", enable_gio_hardware=no)

			if test "x$enable_gio_hardware" = "xno"; then
				GUDEV_SHARP_LIBS=''
				GKEYFILE_SHARP_LIBS=''
			fi
		fi

		AM_CONDITIONAL(ENABLE_GIO, test "x$enable_gio" = "xyes")
		AM_CONDITIONAL(ENABLE_GIO_HARDWARE, test "x$enable_gio_hardware" = "xyes")
	else
		enable_gio_hardware="no"
		AM_CONDITIONAL(ENABLE_GIO, false)
		AM_CONDITIONAL(ENABLE_GIO_HARDWARE, false)
	fi
])

