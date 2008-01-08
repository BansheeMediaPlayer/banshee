AC_DEFUN([BANSHEE_CHECK_DAP_MTP],
[
	LIBMTP_REQUIRED=0.2.0

	AC_ARG_ENABLE(mtp, AC_HELP_STRING([--enable-mtp], 
		[Enable MTP DAP support]), , enable_mtp="no")
	
	enable_mtp_dap="${enable_mtp}"

	PKG_CHECK_MODULES(LIBMTP,
		libmtp >= $LIBMTP_REQUIRED,
		enable_mtp_dap="$enable_mtp_dap", enable_mtp_dap=no)
		
	if test "x$enable_mtp" != "xno" -a "x$enable_mtp_dap" = "xno"; then
		AC_MSG_ERROR([libmtp was not found or is not up to date. Please install libmtp of at least version $LIBMTP_REQUIRED, or disable MTP support by not passing --enable-mtp])
	fi

	if test "x$enable_mtp_dap" = "xyes"; then
		LIBMTP_SO_MAP=$(basename $(find $($PKG_CONFIG --variable=libdir libmtp) -maxdepth 1 -regex '.*libmtp\.so\.\w+$' | sort | tail -n 1))
	fi

	AC_SUBST(LIBMTP_SO_MAP)
	AM_CONDITIONAL(ENABLE_MTP, test "x$enable_mtp_dap" = "xyes")
])

