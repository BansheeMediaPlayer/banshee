dnl Automake setup

AC_INIT([banshee], [0.13.1])
AC_CANONICAL_SYSTEM
AC_PREREQ(2.13)
AM_INIT_AUTOMAKE([1.9 dist-bzip2 tar-ustar])
AM_MAINTAINER_MODE

DISTCHECK_CONFIGURE_FLAGS="--disable-docs"
AC_SUBST(DISTCHECK_CONFIGURE_FLAGS)

ASM_VERSION="$VERSION.*"
AC_SUBST(ASM_VERSION)

dnl Pieces needed by gnome-autogen; can't be parsed from macros
AM_CONFIG_HEADER(config.h)
AC_PROG_INTLTOOL([0.21])
AC_PROG_LIBTOOL
AC_PROG_INSTALL
GNOME_COMPILE_WARNINGS

dnl libbanshee/native dependencies (required)
BANSHEE_CHECK_LIBBANSHEE
BANSHEE_CHECK_NAUTILUS_BURN
BANSHEE_CHECK_GSTREAMER
SHAMROCK_CHECK_GSTREAMER_0_10_PLUGINS([
	decodebin
	playbin
	gnomevfssink
	gnomevfssrc
	audioconvert
	gconfaudiosink
])

dnl GConf
AC_PATH_PROG(GCONFTOOL, gconftool-2, no)
AM_GCONF_SOURCE_2

dnl Mono and gmcs
SHAMROCK_CHECK_MONO_MODULE(1.1.10)
SHAMROCK_FIND_MONO_2_0_COMPILER
SHAMROCK_FIND_MONO_RUNTIME
SHAMROCK_CHECK_MONO_2_0_GAC_ASSEMBLIES([	
	System.Data
	System.Web
	System.Web.Services
	Mono.Cairo
	Mono.Data.SqliteClient
	Mono.Posix
	ICSharpCode.SharpZipLib
])

dnl Monodoc (optional through --disable-docs)
SHAMROCK_CHECK_MONODOC

dnl NUnit (optional)
SHAMROCK_CHECK_MONO_NUNIT

dnl Boo libraries and compiler (optional)
BANSHEE_CHECK_BOO

dnl External NDesk DBus libraries (optional)
BANSHEE_CHECK_NDESK_DBUS

dnl Required dependencies (managed/core)
BANSHEE_CHECK_GTK_SHARP
PKG_CHECK_MODULES(SQLITE, sqlite3 >= 3.2)

dnl DAP support (each module is optional)
BANSHEE_CHECK_DAP_IPOD
BANSHEE_CHECK_DAP_NJB
BANSHEE_CHECK_DAP_MTP
BANSHEE_CHECK_DAP_KARMA

dnl DAAP support (optional)
BANSHEE_CHECK_DAAP

dnl Expansions
SHAMROCK_EXPAND_LIBDIR
SHAMROCK_EXPAND_BINDIR
SHAMROCK_EXPAND_DATADIR

dnl i18n
SHAMROCK_CONFIGURE_I18N(banshee)

dnl generated files
AC_OUTPUT([
Makefile

build/Makefile
build/m4/Makefile
build/banshee.pc

data/Makefile
data/org.gnome.Banshee.service
data/audio-profiles/Makefile
data/images/Makefile

docs/Makefile
tests/Makefile
extras/Makefile
extras/tools/Makefile
extras/tools/banshee-import
branding/Makefile
po/Makefile.in

libbanshee/Makefile

ext/Makefile
ext/hal-sharp/Makefile
ext/taglib-sharp/Makefile
ext/dbus-sharp/Makefile
ext/dbus-sharp/NDesk.DBus.GLib.dll.config

src/Makefile
src/AssemblyInfo.cs

src/Core/Makefile
src/Core/Banshee/Makefile
src/Core/Banshee/banshee
src/Core/Banshee/banshee.exe.config
src/Core/Banshee.Base/Makefile
src/Core/Banshee.Base/Banshee.Base.dll.config
src/Core/Banshee.Base/ConfigureDefines.cs
src/Core/Banshee.Hyena/Makefile
src/Core/Banshee.Widgets/Makefile
src/Core/Banshee.Widgets/Banshee.Widgets.dll.config

src/Dap/Makefile
src/Dap/Banshee.Dap.Ipod/Makefile
src/Dap/Banshee.Dap.Ipod/ipod-sharp.dll.config
src/Dap/Banshee.Dap.Karma/Makefile
src/Dap/Banshee.Dap.MassStorage/Makefile
src/Dap/Banshee.Dap.Mtp/Makefile
src/Dap/Banshee.Dap.Njb/Makefile
src/Dap/Banshee.Dap.Njb/njb-sharp.dll.config

src/Engines/Makefile
src/Engines/Banshee.MediaEngine.GStreamer/Makefile

src/Extras/Makefile
src/Extras/Boo/Makefile
src/Extras/BooBuddy/Makefile
src/Extras/Gnome.Keyring/Makefile
src/Extras/Last.FM/Makefile
src/Extras/MusicBrainz/Makefile
src/Extras/MusicBrainz/MusicBrainz.dll.config

src/Plugins/Makefile
src/Plugins/Banshee.Plugins.Audioscrobbler/Makefile
src/Plugins/Banshee.Plugins.Audioscrobbler/Banshee.Plugins.Audioscrobbler.dll.config
src/Plugins/Banshee.Plugins.Bookmarks/Makefile
src/Plugins/Banshee.Plugins.Daap/Makefile
src/Plugins/Banshee.Plugins.Daap/Banshee.Plugins.Daap.dll.config
src/Plugins/Banshee.Plugins.MetadataSearch/Makefile
src/Plugins/Banshee.Plugins.MetadataSearch/Banshee.Plugins.MetadataSearch.dll.config
src/Plugins/Banshee.Plugins.MiniMode/Makefile
src/Plugins/Banshee.Plugins.MMKeys/Makefile
src/Plugins/Banshee.Plugins.MMKeys/Banshee.Plugins.MMKeys.dll.config
src/Plugins/Banshee.Plugins.NotificationAreaIcon/Makefile
src/Plugins/Banshee.Plugins.NotificationAreaIcon/Banshee.Plugins.NotificationAreaIcon.dll.config
src/Plugins/Banshee.Plugins.Podcast/Makefile
src/Plugins/Banshee.Plugins.Radio/Makefile
src/Plugins/Banshee.Plugins.Recommendation/Makefile
src/Plugins/Banshee.Plugins.Sample/Makefile
])

if test "x${enable_daap}" = "xyes"; then
	zeroconf_lib=", Avahi"
	if test "x${enable_mdnsd}" = "xyes"; then
		zeroconf_lib=", mDNSResponder"
	fi
fi

echo "
banshee-$VERSION

    Install Prefix:    ${prefix}
    Datadir:           ${expanded_datadir}
    Libdir:            ${expanded_libdir}

    C Compiler:        ${CC}
    Mono C# Compiler:  ${MCS}
    Mono Runtime:      ${MONO}
 
    Digital Audio Players (DAP):
      iPod:            ${enable_ipodsharp}
      NJB:             ${enable_njbsharp}
      MTP:             ${enable_mtp_dap}
      Karma:           ${enable_karmasharp}

    DAAP Support:      ${enable_daap}${zeroconf_lib}
"
if test -d ${expanded_libdir}/banshee; then
	echo "WARNING: An existing Banshee install is in ${expanded_libdir}/banshee"
	echo "         Remove the existing install before installing this build."
	echo "         Installing over an existing install will cause conflicts!"
	echo 
fi

