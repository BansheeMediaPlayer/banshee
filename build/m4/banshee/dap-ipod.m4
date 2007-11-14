AC_DEFUN([BANSHEE_CHECK_DAP_IPOD],
[
	IPODSHARP_REQUIRED=0.8.0

	AC_ARG_ENABLE(ipod, AC_HELP_STRING([--enable-ipod], 
		[Enable iPod DAP support]), , enable_ipod="yes")

	enable_ipodsharp="${enable_ipod}"

	PKG_CHECK_MODULES(IPODSHARP,
		ipod-sharp >= $IPODSHARP_REQUIRED \
		ipod-sharp-ui >= $IPODSHARP_REQUIRED,
		enable_ipodsharp="$enable_ipodsharp", enable_ipodsharp=no)

	if test "x$enable_ipodsharp" = "xyes"; then
		asms="`$PKG_CONFIG --variable=Libraries ipod-sharp` `$PKG_CONFIG --variable=Libraries ipod-sharp-ui`"
		for asm in $asms; do
			IPODSHARP_ASSEMBLIES="$IPODSHARP_ASSEMBLIES $asm $asm.mdb"
		done
		AC_SUBST(IPODSHARP_ASSEMBLIES)
		AC_SUBST(IPODSHARP_LIBS)
	fi
	
	AM_CONDITIONAL(ENABLE_IPOD, test "x$enable_ipodsharp" = "xyes")
])

