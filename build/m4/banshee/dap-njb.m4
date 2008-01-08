AC_DEFUN([BANSHEE_CHECK_DAP_NJB],
[
	NJBSHARP_REQUIRED=0.3.0

	AC_ARG_ENABLE(njb, AC_HELP_STRING([--enable-njb], 
		[Enable NJB DAP support]), , enable_njb="yes")

	enable_njbsharp="${enable_njb}"

	PKG_CHECK_MODULES(NJBSHARP,
		njb-sharp >= $NJBSHARP_REQUIRED,
		enable_njbsharp="$enable_njbsharp", enable_njbsharp=no)
			
	if test "x$enable_njbsharp" = "xyes"; then
		NJBSHARP_INSTALL_FILES=""
		for i in `$PKG_CONFIG --variable=LibraryBase njb-sharp`*; do	
			if test -z "`echo \"$i\" | grep config`"; then	
				NJBSHARP_INSTALL_FILES="$NJBSHARP_INSTALL_FILES \"$i\"";
			fi
		done;
		if test `$PKG_CONFIG --variable=LibraryBase njb-sharp` != `$PKG_CONFIG --variable=libdir njb-sharp`/njb-sharp ; then
			for i in `$PKG_CONFIG --variable=libdir njb-sharp`/njb-sharp/*; do
				if test -z "`echo \"$i\" | grep config`"; then
					NJBSHARP_INSTALL_FILES="$NJBSHARP_INSTALL_FILES \"$i\"";
				fi
			done;
		fi
		AC_SUBST(NJBSHARP_INSTALL_FILES)
		AC_SUBST(NJBSHARP_LIBS)	
	fi

	AM_CONDITIONAL(ENABLE_NJB, test "x$enable_njbsharp" = "xyes")
])

