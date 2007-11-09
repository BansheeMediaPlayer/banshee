AC_DEFUN([BANSHEE_CHECK_TAGLIB_SHARP],
[
	AC_ARG_ENABLE(external_taglib_sharp, AC_HELP_STRING([--enable-external-taglib-sharp], 
		[Use external TagLib# instead of the bundled one]), 
		enable_external_taglib_sharp="yes", enable_external_taglib_sharp="no")

	if test "x$enable_external_taglib_sharp" = "xyes"; then
		PKG_CHECK_MODULES(TAGLIB_SHARP, taglib-sharp >= 2.0.0)
		AC_SUBST(TAGLIB_SHARP_LIBS)
		TAGLIB_SHARP_PC_REQUIRES=taglib-sharp
		AC_SUBST(TAGLIB_SHARP_PC_REQUIRES)
		AM_CONDITIONAL(EXTERNAL_TAGLIB_SHARP, true)
	else
		AC_MSG_RESULT([no])
		TAGLIB_SHARP_PC_LIBS=-r:${expanded_libdir}/banshee/TagLib.dll
		AC_SUBST(TAGLIB_SHARP_PC_LIBS)
		AM_CONDITIONAL(EXTERNAL_TAGLIB_SHARP, false)
	fi
])

