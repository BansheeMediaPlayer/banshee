AC_DEFUN([BANSHEE_CHECK_GIO_SHARP],
[
	GNOMESHARP_REQUIRED=2.8
	
	AC_ARG_ENABLE(gio, AC_HELP_STRING([--disable-gio], [Disable GIO for IO operations]), ,enable_gio="yes")
	AC_ARG_ENABLE(gio_hardware, AC_HELP_STRING([--disable-gio-hardware], [Disable GIO Hardware backend]), ,enable_gio_hardware="yes")
	
	if test "x$enable_gio" = "xyes"; then
		PKG_CHECK_MODULES(GIOSHARP,
			gio-sharp-3.0 >= 2.99,
			enable_gio="$enable_gio", enable_gio=no)

		asms="`$PKG_CONFIG --variable=Libraries gio-sharp-3.0`"
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

		if test "x$enable_gio_hardware" = "xyes"; then
			PKG_CHECK_MODULES(GUDEV_SHARP,
				gudev-sharp-1.0 >= 0.1,
				enable_gio_hardware="$enable_gio", enable_gio_hardware=no)

			if test "x$enable_gio_hardware" = "xno"; then
				GUDEV_SHARP_LIBS=''
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

