AC_DEFUN([BANSHEE_CHECK_GIO_SHARP],
[
	GNOMESHARP_REQUIRED=2.8
	GIOSHARP_REQUIRED=2.22.3
	
	AC_ARG_ENABLE(gio, AC_HELP_STRING([--disable-gio], [Disable GIO for IO operations]), ,enable_gio="yes")
	AC_ARG_ENABLE(gio_hardware, AC_HELP_STRING([--disable-gio-hardware], [Disable GIO Hardware backend]), ,enable_gio_hardware="yes")
	
	if test "x$enable_gio" = "xyes"; then

		has_gtk_beans=no
		PKG_CHECK_MODULES(GTKSHARP_BEANS,
			gtk-sharp-beans-2.0 >= $GNOMESHARP_REQUIRED,
			has_gtk_beans=yes, has_gtk_beans=no)

		if test "x$has_gtk_beans" = "xno"; then
			AC_MSG_ERROR([gtk-sharp-beans-2.0 was not found or is not up to date. Please install gtk-sharp-beans-2.0 of at least version $GNOMESHARP_REQUIRED, or disable GIO support by passing --disable-gio])
		fi

		has_gio_sharp=no
		PKG_CHECK_MODULES(GIOSHARP,
			gio-sharp-2.0 >= $GIOSHARP_REQUIRED,
			has_gio_sharp=yes, has_gio_sharp=no)
		if test "x$has_gio_sharp" = "xno"; then
			AC_MSG_ERROR([gio-sharp-2.0 was not found or is not up to date. Please install gio-sharp-2.0 of at least version $GIOSHARP_REQUIRED, or disable GIO support by passing --disable-gio])
		fi

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
				enable_gio_hardware=yes, enable_gio_hardware=no)

			PKG_CHECK_MODULES(GKEYFILE_SHARP,
				gkeyfile-sharp >= 0.1,
				enable_gio_hardware="$enable_gio_hardware", enable_gio_hardware=no)

			if test "x$enable_gio_hardware" = "xno"; then
				GUDEV_SHARP_LIBS=''
				GKEYFILE_SHARP_LIBS=''
			fi
		fi

		AM_CONDITIONAL(ENABLE_GIO, true)
		AM_CONDITIONAL(ENABLE_GIO_HARDWARE, test "x$enable_gio_hardware" = "xyes")
	else
		enable_gio_hardware="no"
		AM_CONDITIONAL(ENABLE_GIO, false)
		AM_CONDITIONAL(ENABLE_GIO_HARDWARE, false)
	fi
])

