AC_DEFUN([BANSHEE_CHECK_DAP_KARMA],
[
	KARMASHARP_REQUIRED=0.0.5
	
	AC_ARG_ENABLE(karma, AC_HELP_STRING([--enable-karma], 
		[Enable Rio Karma DAP support]), , enable_karma="yes")
	
	enable_karmasharp="${enable_karma}"

	PKG_CHECK_MODULES(KARMASHARP,
		karma-sharp >= $KARMASHARP_REQUIRED,
		enable_karmasharp="$enable_karmasharp", enable_karmasharp=no)

	if test "x$enable_karmasharp" = "xyes"; then
		asms="`$PKG_CONFIG --variable=Libraries karma-sharp`"
		for asm in $asms; do
			KARMASHARP_ASSEMBLIES="$KARMASHARP_ASSEMBLIES $asm"
			[[ -r "$asm.config" ]] && KARMASHARP_ASSEMBLIES="$KARMASHARP_ASSEMBLIES $asm.config"
			[[ -r "$asm.mdb" ]] && KARMASHARP_ASSEMBLIES="$KARMASHARP_ASSEMBLIES $asm.mdb"
		done
		AC_SUBST(KARMASHARP_ASSEMBLIES)
		AC_SUBST(KARMASHARP_LIBS)
	fi

	AM_CONDITIONAL(ENABLE_KARMA, test "x$enable_karmasharp" = "xyes")
])

