AC_DEFUN([BANSHEE_CHECK_OSX],
[
	enable_osx="no"
	if test "x${host_os%${host_os#??????}}" = "xdarwin"; then
		enable_osx="yes"
		PKG_CHECK_MODULES(GTKMACINTEGRATION, gtk-mac-integration-gtk3 >= 2.0.7)
		PKG_CHECK_MODULES(MONOMAC, monomac >= 0.7)
		MONOMAC_ASSEMBLIES=`$PKG_CONFIG --variable=Libraries monomac`
		AC_SUBST(MONOMAC_LIBS)
		AC_SUBST(MONOMAC_ASSEMBLIES)
	fi

	AM_CONDITIONAL([PLATFORM_DARWIN], [test "x$enable_osx" = "xyes"])
])
