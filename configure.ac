AC_INIT(src/Main.cs)

AM_CONFIG_HEADER(config.h)

AC_CANONICAL_SYSTEM
AC_PREREQ(2.13)

VERSION=0.10.9
ASM_VERSION="$VERSION.*"

AM_INIT_AUTOMAKE(banshee, $VERSION)
AC_SUBST(ASM_VERSION)

AM_MAINTAINER_MODE

AC_PROG_INTLTOOL([0.21])

AC_PROG_INSTALL
AC_ISC_POSIX
AM_PROG_LIBTOOL
AC_PROG_CC
AC_PROG_CXX

dnl check headers
AC_HEADER_STDC

GNOME_COMPILE_WARNINGS
AM_PATH_GLIB_2_0

dnl configure options
AC_ARG_ENABLE(helix, AC_HELP_STRING([--enable-helix], [Enable Helix/RealPlayer engine]), , enable_helix="yes")
AM_CONDITIONAL(ENABLE_HELIX, test "x$enable_helix" = "xyes")

AC_ARG_ENABLE(ipod, AC_HELP_STRING([--enable-ipod], [Enable iPod DAP support]), , enable_ipod="yes")
enable_ipodsharp="${enable_ipod}"

AC_ARG_ENABLE(njb, AC_HELP_STRING([--enable-njb], [Enable NJB DAP support]), , enable_njb="yes")
enable_njbsharp="${enable_njb}"

AC_ARG_ENABLE(vlc, AC_HELP_STRING([--enable-vlc], [Enable VLC Media Engine]), , enable_vlc="no")
AM_CONDITIONAL(ENABLE_VLC, test "x$enable_vlc" = "xyes")

AC_ARG_ENABLE(docs, AC_HELP_STRING([--disable-docs], [Do not build documentation]), with_docs=no, with_docs=yes)

AC_ARG_ENABLE(dev-tests, AC_HELP_STRING([--enable-dev-tests], [Whether or not to build various test programs for components in libbanshee and mediaengines]), , enable_dev_tests="no")
AM_CONDITIONAL(ENABLE_TESTS, test "x$enable_dev_tests" = "xyes")

AC_ARG_WITH(gstreamer-0-10, AC_HELP_STRING([--with-gstreamer-0-10], [Build the GStreamer 0.10 backends]), enable_gst_10="yes", enable_gst_10="no")
AM_CONDITIONAL(ENABLE_GST_10, test "x$enable_gst_10" = "xyes")

AC_ARG_ENABLE(daap, AC_HELP_STRING([--disable-daap], [Do not build with DAAP support]), enable_daap=no, enable_daap=yes)

if test "x$enable_helix" = "xyes"; then
	PKG_CHECK_MODULES(HELIX_REMOTE, helix-dbus-server >= 0.2)
fi

dnl pkg-config, install programs
AC_PATH_PROG(PKG_CONFIG, pkg-config, no)
if test "x$PKG_CONFIG" = "xno"; then
	AC_MSG_ERROR([You need to install pkg-config])
fi

AC_PATH_PROG(INSTALL, install, no)
if test "x$INSTALL" = "xno"; then
	AC_MSG_ERROR([You need to install 'install'])
fi

AC_SUBST(INSTALL)
AC_SUBST(PKG_CONFIG)

MONO_REQUIRED_VERSION=1.1.10
PKG_CHECK_MODULES(MONO, mono >= $MONO_REQUIRED_VERSION)

dnl Mono programs
MONODIR=`$PKG_CONFIG --variable=exec_prefix mono`
MONOBIN="$MONODIR/bin"
MONO="$MONOBIN/mono"
MCS="$MONOBIN/mcs"

AC_MSG_CHECKING([for mono.pc])
if test -z `$PKG_CONFIG --variable=prefix mono`; then
	AC_MSG_ERROR([missing the mono.pc file, usually found in the mono-devel package])
else
	AC_MSG_RESULT([found])
fi

required_assemblies="Mono.Data.SqliteClient Mono.Posix System.Runtime.Remoting System.Web System.Web.Services"

for asm in $required_assemblies; do
	AC_MSG_CHECKING([for $asm.dll])
	if test ! -e `$PKG_CONFIG --variable=prefix mono`/lib/mono/1.0/$asm.dll; then
		AC_MSG_ERROR([missing required Mono DLL: $asm.dll])
	else
		AC_MSG_RESULT([found])
	fi
done

if test ! -x "$MONO"; then
	AC_MSG_ERROR([No Mono runtime found])
fi

CS="C#"
if test ! -x "$MCS"; then
	AC_MSG_ERROR([No $CS compiler found])
fi

AC_SUBST(MCS)
AC_SUBST(MONO)

if test "x$with_docs" = "xyes"; then
	AC_PATH_PROG(MONODOCER, monodocer, no)
	if test "x$MONODOCER" = "xno"; then
		AC_MSG_ERROR([You need to install monodoc, or pass --disable-docs to configure to skip documentation installation])
	fi

	AC_PATH_PROG(MDASSEMBLER, mdassembler, no)
	if test "x$MDASSEMBLER" = "xno"; then
		AC_MSG_ERROR([You need to install mdassembler, or pass --disable-docs to configure to skip documentation installation])
	fi

	DOCDIR=`$PKG_CONFIG monodoc --variable=sourcesdir`
	AC_SUBST(DOCDIR)
	AM_CONDITIONAL(BUILD_DOCS, true)
else
	AC_MSG_NOTICE([not building Banshee API documentation])
	AM_CONDITIONAL(BUILD_DOCS, false)
fi

dnl gtk-sharp, other CLR deps
GTKSHARP_REQUIRED=2.7
PKG_CHECK_MODULES(GTKSHARP,
	gtk-sharp-2.0 >= $GTKSHARP_REQUIRED \
	gnome-sharp-2.0 >= $GTKSHARP_REQUIRED \
	gnome-vfs-sharp-2.0 >= $GTKSHARP_REQUIRED \
	glade-sharp-2.0 >= $GTKSHARP_REQUIRED \
	gconf-sharp-2.0 >= $GTKSHARP_REQUIRED)
AC_SUBST(GTKSHARP_LIBS)

dnl dap support
IPODSHARP_REQUIRED=0.5.15
PKG_CHECK_MODULES(IPODSHARP,
	ipod-sharp >= $IPODSHARP_REQUIRED,
	enable_ipodsharp="$enable_ipodsharp", enable_ipodsharp=no)
if test "x$enable_ipodsharp" = "xyes"; then
	IPODSHARP_ASSEMBLIES="`$PKG_CONFIG --variable=Libraries ipod-sharp`"
	AC_SUBST(IPODSHARP_ASSEMBLIES)
	AC_SUBST(IPODSHARP_LIBS)
fi
AM_CONDITIONAL(ENABLE_IPOD, test "x$enable_ipodsharp" = "xyes")

NJBSHARP_REQUIRED=0.2.1
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
	AC_SUBST(NJBSHARP_INSTALL_FILES)
	AC_SUBST(NJBSHARP_LIBS)
fi
AM_CONDITIONAL(ENABLE_NJB, test "x$enable_njbsharp" = "xyes")

dnl dbus
PKG_CHECK_MODULES(DBUSSHARP, dbus-sharp)
AC_SUBST(DBUSSHARP)

dnl sqlite devel dep
PKG_CHECK_MODULES(SQLITE, sqlite3 >= 3.2)

dnl musicbrainz
PKG_CHECK_MODULES(MUSICBRAINZ, libmusicbrainz >= 2.1.1)

dnl avahi-sharp for DAAP
AC_ARG_ENABLE(mdnsd, AC_HELP_STRING([--enable-mdnsd], [Use mDNSResponder instead of Avahi for DAAP]), enable_mdnsd="yes", enable_mdnsd="no")
AM_CONDITIONAL(ENABLE_MDNSD, test "x$enable_mdnsd" = "yes")

AC_ARG_ENABLE(avahi, AC_HELP_STRING([--enable-avahi], [Use Avahi for DAAP]), enable_avahi="yes", enable_avahi="no")

if test "x$enable_daap" = "xyes"; then 
	if test "x$enable_avahi" = "xno"; then
		AC_PATH_PROG(MDNSRESPONDER, mdnsd, no, /sbin:/usr/sbin:/usr/bin)
		if test ! "x$MDNSRESPONDER" = "xno"; then
			enable_mdnsd="yes"
		fi
	fi

	if test "x$enable_mdnsd" = "xyes"; then
		DAAPSHARP_FLAGS="-define:ENABLE_MDNSD"
		AC_SUBST(DAAPSHARP_FLAGS)
	else
		PKG_CHECK_MODULES(AVAHISHARP, avahi-sharp)
		DAAPSHARP_FLAGS=$AVAHISHARP_LIBS
		AC_SUBST(DAAPSHARP_FLAGS)
	fi
	AM_CONDITIONAL(DAAP_ENABLED, true)
else
	AM_CONDITIONAL(DAAP_ENABLED, false)
fi

dnl gstreamer devel deps
if test "x$enable_gst_10" = "xyes"; then
	GSTREAMER_REQUIRED_VERSION=0.10.3
	AC_SUBST(GSTREAMER_REQUIRED_VERSION)
	PKG_CHECK_MODULES(GST,
		gstreamer-0.10 >= $GSTREAMER_REQUIRED_VERSION
		gstreamer-base-0.10 >= $GSTREAMER_REQUIRED_VERSION
		gstreamer-plugins-base-0.10 >= $GSTREAMER_REQUIRED_VERSION
		gstreamer-controller-0.10 >= $GSTREAMER_REQUIRED_VERSION
		gstreamer-dataprotocol-0.10 >= $GSTREAMER_REQUIRED_VERSION)

	gst_pkg_name="gstreamer-0.10"
	gst_inspect="gst-inspect-0.10"
	gst_version="0.10"
	gst_required_plugins="decodebin playbin cdparanoia gnomevfssink gnomevfssrc audioconvert gconfaudiosink"
else
	GSTREAMER_REQUIRED_VERSION=0.8.11
	GSTREAMER_PLUGINS_REQUIRED_VERSION=0.8.11
	AC_SUBST(GSTREAMER_REQUIRED_VERSION)
	AC_SUBST(GSTREAMER_PLUGINS_REQUIRED_VERSION)

	gst_pkg_name="gstreamer"
	gst_inspect="gst-inspect"

	PKG_CHECK_MODULES(GST,
		gstreamer >= $GSTREAMER_REQUIRED_VERSION
		gstreamer-gconf >= $GSTREAMER_PLUGINS_REQUIRED_VERSION
		gstreamer-interfaces >= $GSTREAMER_PLUGINS_REQUIRED_VERSION
		gstreamer-play >= $GSTREAMER_PLUGINS_REQUIRED_VERSION
		gstreamer-plugins >= $GSTREAMER_PLUGINS_REQUIRED_VERSION,
		enable_gst=yes, enable_gst=no)

	if test "x$enable_gst" = "xno" ; then
		PKG_CHECK_MODULES(GST,
			gstreamer-0.8 >= $GSTREAMER_REQUIRED_VERSION
			gstreamer-gconf-0.8 >= $GSTREAMER_PLUGINS_REQUIRED_VERSION
			gstreamer-interfaces-0.8 >= $GSTREAMER_PLUGINS_REQUIRED_VERSION
			gstreamer-play-0.8 >= $GSTREAMER_PLUGINS_REQUIRED_VERSION
			gstreamer-plugins-0.8 >= $GSTREAMER_PLUGINS_REQUIRED_VERSION,
			enable_gst=yes, enable_gst=no)

		gst_pkg_name="gstreamer-0.8"
		gst_inspect="gst-inspect-0.8"

		if test "x$enable_gst" = "xno" ; then
			AC_MSG_ERROR([Install gstreamer, gstreamer-plugins gstreamer-gconf, gstreamer-interfaces, and gstreamer-play])
		fi
	fi

	gst_version="0.8"
	gst_required_plugins="spider cdparanoia gnomevfssink gnomevfssrc audioconvert playbin gconfaudiosink"
fi

AC_SUBST(GST_CFLAGS)
AC_SUBST(GST_LIBS)

dnl gstreamer plugins

gst_toolsdir=`$PKG_CONFIG --variable=toolsdir $gst_pkg_name`
gst_inspect="$gst_toolsdir/$gst_inspect"

AC_MSG_CHECKING([for gst-inspect])
if ! test -x $gst_inspect; then
	AC_MSG_RESULT([no])
	AC_MSG_ERROR([Cannot find required gst-inspect tool.])
else
	AC_MSG_RESULT([yes])
fi

for element in $gst_required_plugins; do
	AC_MSG_CHECKING([for GStreamer $gst_version $element plugin])
	if $gst_inspect $element > /dev/null 2>/dev/null; then
		AC_MSG_RESULT([yes])
	else
		AC_MSG_RESULT([no])
		AC_MSG_ERROR([Cannot find required GStreamer-$gst_version plugin '$element'.])
	fi
done;


dnl GTK+-2.0 C devel deps
GTK_REQUIRED=2.8
PKG_CHECK_MODULES(GTK,
	glib-2.0 \
	libgnome-2.0 \
	libgnomeui-2.0 \
	gconf-2.0 \
	gdk-pixbuf-2.0 \
	gnome-vfs-2.0 \
	gnome-desktop-2.0 \
	gtk+-2.0 >= $GTK_REQUIRED)
AC_SUBST(GTK_CFLAGS)
AC_SUBST(GTK_LIBS)

dnl HAL devel deps
PKG_CHECK_MODULES(HAL,
	gobject-2.0 \
	glib-2.0 \
	dbus-1 \
	dbus-glib-1 \
	hal >= 0.5.2)
AC_SUBST(HAL_CFLAGS)
AC_SUBST(HAL_LIBS)

dnl DBus devel detect .so version for dll maps
PKG_CHECK_MODULES(DBUS, dbus-1 >= 0.60, dbus60=yes, dbus60=no)
if test "x$dbus60" = "xyes"; then
	DBUS_SOVERSION=2
else
	DBUS_SOVERSION=1
fi
AC_SUBST(DBUS_SOVERSION)


dnl libnautilus-burn devel deps
PKG_CHECK_MODULES(LIBNAUTILUS_BURN, libnautilus-burn >= 2.13, lnb13=yes, lnb13=no)
if test "x$lnb13" = "xyes"; then
	LNB_SOVERSION=3
else
	PKG_CHECK_MODULES(LIBNAUTILUS_BURN, libnautilus-burn >= 2.12, lnb12=yes, lnb12=no)
	if test "x$lnb12" = "xyes"; then
		LNB_SOVERSION=2
	else
		AC_MSG_ERROR([You need libnautilus-burn 2.12 or better])
	fi
fi
AC_SUBST(LNB_SOVERSION)
AC_SUBST(LIBNAUTILUS_BURN_CFLAGS)
AC_SUBST(LIBNAUTILUS_BURN_LIBS)

dnl GConf

AC_PATH_PROG(GCONFTOOL, gconftool-2, no)
AM_GCONF_SOURCE_2

dnl Find the real @libdir@

expanded_libdir=`( case $prefix in NONE) prefix=$ac_default_prefix ;; *) ;; esac
		   case $exec_prefix in NONE) exec_prefix=$prefix ;; *) ;; esac
		   eval echo $libdir )`
AC_SUBST(expanded_libdir)

expanded_datadir=`( case $prefix in NONE) prefix=$ac_default_prefix ;; *) ;; esac
		   case $exec_prefix in NONE) exec_prefix=$prefix ;; *) ;; esac
		   eval echo $datadir )`
AC_SUBST(expanded_datadir)

dnl Defines for C Exec Wrapper (${prefix}/bin/banshee)

AC_DEFINE_UNQUOTED([PREFIX], ["${prefix}"], [install prefix])
AC_DEFINE_UNQUOTED([LIBDIR], ["${expanded_libdir}"], [libdir])
AC_DEFINE_UNQUOTED([MONO], ["${MONO}"], [Mono Runtime])
AC_DEFINE_UNQUOTED([MONO_PATH], ["${MONO_PATH}"], [MONO_PATH Env])
AC_DEFINE_UNQUOTED([HELIX_LIBRARY_PATH], ["${HELIX_LIBRARY_PATH}"], [Helix Library Path])

dnl Intl

ALL_LINGUAS="bg ca cs da en_CA en_GB es fi fr it ja lt nb nl pt_BR sv th zh_CN"
GETTEXT_PACKAGE=banshee
AC_SUBST(GETTEXT_PACKAGE)
AC_DEFINE_UNQUOTED(GETTEXT_PACKAGE, "$GETTEXT_PACKAGE", [Gettext Package])
AM_GLIB_GNU_GETTEXT

dnl generated files
AC_OUTPUT([
Makefile
banshee.pc
entagged-sharp/Makefile
hal-sharp/Makefile
hal-sharp/hal-sharp.dll.config
libbanshee/Makefile
burn-sharp/Makefile
burn-sharp/burn-sharp.dll.config
src/banshee
src/Makefile
src/AssemblyInfo.cs
src/banshee.exe.config
src/MusicBrainz/Makefile
src/MusicBrainz/MusicBrainz.dll.config
src/Banshee.MediaEngine/Makefile
src/Banshee.MediaEngine/Gstreamer/Makefile
src/Banshee.MediaEngine/Gstreamer/gst-player.dll.config
src/Banshee.MediaEngine/Helix/Makefile
src/Banshee.MediaEngine/Helix/HelixRemotePlayerEngine.dll.config
src/Banshee.MediaEngine/Vlc/Makefile
src/Banshee.MediaEngine/Vlc/vlc-player.dll.config
src/Banshee.Widgets/Makefile
src/Banshee.Widgets/Banshee.Widgets.dll.config
src/Banshee.Base/Makefile
src/Banshee.Base/Banshee.Base.dll.config
src/Banshee.Base/ConfigureDefines.cs
src/Banshee.Dap/Makefile
src/Banshee.Dap/Ipod/Makefile
src/Banshee.Dap/Ipod/ipod-sharp.dll.config
src/Banshee.Dap/MassStorage/Makefile
src/Banshee.Dap/Njb/Makefile
src/Banshee.Dap/Njb/njb-sharp.dll.config
src/Banshee.Plugins/Makefile
src/Banshee.Plugins/Audioscrobbler/Makefile
src/Banshee.Plugins/Audioscrobbler/Audioscrobbler.dll.config
src/Banshee.Plugins/FileSystemMonitor/Makefile
src/Banshee.Plugins/FileSystemMonitor/FileSystemMonitor.dll.config
src/Banshee.Plugins/MetadataSearch/Makefile
src/Banshee.Plugins/MetadataSearch/MetadataSearch.dll.config
src/Banshee.Plugins/MMKeys/Makefile
src/Banshee.Plugins/MMKeys/MMKeys.dll.config
src/Banshee.Plugins/Daap/Makefile
src/Banshee.Plugins/Daap/Daap.dll.config
src/Banshee.Plugins/NotificationAreaIcon/Makefile
src/Banshee.Plugins/NotificationAreaIcon/NotificationAreaIcon.dll.config
data/Makefile
data/org.gnome.Banshee.service
data/images/Makefile
docs/Makefile
po/Makefile.in
])

gst_version="0.8";
if test "x${enable_gst_10}" = "xyes"; then
	gst_version="0.10";
fi;

if test "x${enable_daap}" = "xyes"; then
	zeroconf_lib=", Avahi"
	if test "x${enable_mdnsd}" = "xyes"; then
		zeroconf_lib=", mDNSResponder"
	fi
fi

echo "
banshee-$VERSION

    Install Prefix:    ${prefix}
    C Compiler:        ${CC}
    Mono C# Compiler:  ${MCS}
    Mono Runtime:      ${MONO}

    Engines:
      GStreamer:       yes, ${gst_version}
      Helix Remote:    ${enable_helix}
      VLC:             ${enable_vlc}

    Digital Audio Players (DAP):
      iPod:            ${enable_ipodsharp}
      NJB:             ${enable_njbsharp}

    DAAP Support:      ${enable_daap}${zeroconf_lib}
"

if test "x${enable_vlc}" = "xyes"; then
	if test ! -e ./src/Banshee.MediaEngine/Vlc/libvlc.so; then
		echo ""
		echo "    *****************************************************"
		echo "    NOTICE: You have enabled the VLC engine, but have not"
		echo "    provided a libvlc.so in ./src/Banshee.MediaEngine/Vlc/"
		echo ""
		echo "    You must build your own libvlc.so library and copy it"
		echo "    to ./src/Banshee.MediaEngine/Vlc/libvlc.so before running"
		echo "    'make install' or copy libvlc.so after the install to"
		echo "    ${expanded_libdir}/banshee/src/Banshee.MediaEngine/Vlc/libvlc.so"
		echo ""
		echo "    libvlc.so is not included in Banshee."
		echo ""
	fi;
fi;

