dnl Granularly define the version components
dnl Remember to also update the Display Version
m4_define([banshee_version_major], [1])
m4_define([banshee_version_minor], [6])
m4_define([banshee_version_micro], [0])

dnl set to 0 when doing an official release
m4_define([banshee_version_pre_release], [0])

m4_define([concat], $1$2$3$4)

dnl create base version string
m4_define([banshee_version], concat(
	banshee_version_major.,
	banshee_version_minor.,
	banshee_version_micro))

dnl create the release version
m4_define([banshee_version],
	m4_if(banshee_version_pre_release, [0],
		banshee_version,
		concat(banshee_version, banshee_version_pre_release)))

dnl this can sometimes differ manually
m4_define([banshee_display_version],
	["1.6.0"])

m4_define([banshee_api_version],
	[banshee_version_major.banshee_version_minor])

m4_define([banshee_asm_version],
	[banshee_api_version.0.0])

AC_PREREQ(2.52)
AC_INIT([banshee-1], banshee_version,
	[http://bugzilla.gnome.org/enter_bug.cgi?product=banshee])

AM_INIT_AUTOMAKE([1.9 dist-bzip2 tar-ustar dist-zip foreign])
AM_MAINTAINER_MODE
AM_CONFIG_HEADER([config.h])
AC_SUBST([ACLOCAL_AMFLAGS], ["-I build/m4/shamrock -I build/m4/banshee -I build/m4/shave \${ACLOCAL_FLAGS}"])

IT_PROG_INTLTOOL([0.35.0])
AC_PROG_LIBTOOL
AC_PROG_INSTALL

dnl How to configure for distcheck
DISTCHECK_CONFIGURE_FLAGS="--disable-docs --enable-mtp --enable-daap --enable-podcast --enable-release"
AC_SUBST(DISTCHECK_CONFIGURE_FLAGS)

dnl Export Version Info
AC_SUBST([API_VERSION], [banshee_api_version])
AC_SUBST([ASM_VERSION], [banshee_asm_version])
AC_SUBST([DISPLAY_VERSION], [banshee_display_version])

dnl Expansions
SHAMROCK_EXPAND_LIBDIR
SHAMROCK_EXPAND_BINDIR
SHAMROCK_EXPAND_DATADIR

dnl libbanshee/native dependencies (required)
BANSHEE_CHECK_LIBBANSHEE
BANSHEE_CHECK_GSTREAMER

BANSHEE_CHECK_NOW_PLAYING_X11

dnl Mono and gmcs
SHAMROCK_CHECK_MONO_MODULE(1.9.1)
SHAMROCK_FIND_MONO_2_0_COMPILER
SHAMROCK_FIND_MONO_RUNTIME
SHAMROCK_CHECK_MONO_2_0_GAC_ASSEMBLIES([	
	System.Data
	Mono.Cairo
	Mono.Posix
	ICSharpCode.SharpZipLib
])

dnl Check for mono >= 2.4.3 for LibraryWatcher extension, with fix for bnc#322330
PKG_CHECK_MODULES(MONO_2_4_3, mono >= 2.4.3, HAVE_MONO_2_4_3=yes, HAVE_MONO_2_4_3=no)
AM_CONDITIONAL(ENABLE_LIBRARY_WATCHER, test "x$HAVE_MONO_2_4_3" = "xyes")

dnl Check for GNOME >= 2.28 to see if we should remove 'Media Player' from the app Name, bgo#596242
PKG_CHECK_MODULES(GNOME_LT_2_28, gnome-desktop-2.0 < 2.28, BANSHEE_DESKTOP_NAME="_Name=Banshee Media Player", BANSHEE_DESKTOP_NAME="_Name=Banshee")
AC_SUBST(BANSHEE_DESKTOP_NAME)

dnl NDesk DBus libraries
BANSHEE_CHECK_NDESK_DBUS

dnl Mono.Addins libraries
BANSHEE_CHECK_MONO_ADDINS

dnl notify-sharp library
BANSHEE_CHECK_NOTIFY_SHARP

dnl Boo
BANSHEE_CHECK_BOO

dnl Monodoc (optional through --disable-docs)
SHAMROCK_CHECK_MONODOC

dnl webkit (optional through --disable-webkit)
BANSHEE_CHECK_WEBKIT

dnl gdata (optional through --disable-youtube)
BANSHEE_CHECK_YOUTUBE

dnl gtk#-beans and gio#
BANSHEE_CHECK_GIO_SHARP

dnl Moonlight (optional through --enable-moonlight)
BANSHEE_CHECK_MOONLIGHT

dnl NUnit (optional)
SHAMROCK_CHECK_NUNIT

dnl External TagLib# libraries (optional)
BANSHEE_CHECK_TAGLIB_SHARP

dnl Required dependencies (managed/core)
BANSHEE_CHECK_GTK_SHARP

dnl OS X (optional through --enable-osx)
BANSHEE_CHECK_OSX

PKG_CHECK_MODULES(SQLITE, sqlite3 >= 3.4)

dnl GNOME (optional)
AC_ARG_ENABLE(gnome, 
	AC_HELP_STRING([--disable-gnome], 
		[Disable GNOME support]), 
	, enable_gnome="yes")
if test "x$enable_gnome" = "xyes"; then
	BANSHEE_CHECK_GNOME_SHARP
	AC_PATH_PROG(GCONFTOOL, gconftool-2, no)
	AM_GCONF_SOURCE_2
	AM_CONDITIONAL(ENABLE_GNOME, true)
else
	AM_CONDITIONAL(GCONF_SCHEMAS_INSTALL, false)
	AM_CONDITIONAL(ENABLE_GNOME, false)
fi

dnl DAP support (each module is optional)
BANSHEE_CHECK_DAP_MTP
BANSHEE_CHECK_DAP_IPOD
BANSHEE_CHECK_DAP_KARMA

dnl DAAP support (optional)
BANSHEE_CHECK_DAAP

dnl RemoteAudio support (optional)
BANSHEE_CHECK_REMOTE_AUDIO

dnl MonoTorrent support (optional)
BANSHEE_CHECK_TORRENT

dnl MeeGo integration (optional)
BANSHEE_CHECK_MEEGO

dnl Gapless, if we have a new-enough playbin2
ENABLE_GAPLESS="no"
AC_ARG_ENABLE(gapless-playback,
	AC_HELP_STRING([--enable-gapless-playback],
		[Enable gapless playback engine (requires gst-plugins-base > 0.10.25.2)]),
		enable_gapless=$enableval,
		enable_gapless=auto)
if test "x$enable_gapless" != "xno" ; then
   	PKG_CHECK_MODULES([GST_PLUGINS_BASE], [gstreamer-plugins-base-0.10 > 0.10.25.2], ENABLE_GAPLESS=yes, ENABLE_GAPLESS=no)
   	if test "x$enable_gapless" == "xyes" -a "x$ENABLE_GAPLESS" == "xno" ; then
	   	AC_MSG_ERROR([Gapless playback requires gstreamer-plugins-base > 0.10.25.2])
	fi
fi
AM_CONDITIONAL(ENABLE_GAPLESS, test "x$ENABLE_GAPLESS" = "xyes")   

dnl i18n
SHAMROCK_CONFIGURE_I18N($PACKAGE)

dnl the bird is the word
AC_ARG_ENABLE(podcast, 
	AC_HELP_STRING([--disable-podcast], 
		[Disable Podcasting support]), 
	, enable_podcast="yes")
AM_CONDITIONAL(ENABLE_PODCAST, test "x$enable_podcast" = "xyes")

AC_ARG_ENABLE(release,
	AC_HELP_STRING([--enable-release],
		[Use release configuration]),
	enable_release=yes, enable_release=`(test ! -d .svn && test ! -d .git) && echo "yes" || echo "no"`)
AM_CONDITIONAL(ENABLE_RELEASE, test "x$enable_release" = "xyes")
ENABLE_RELEASE=$enable_release
AC_SUBST(ENABLE_RELEASE)
if test "x$enable_release" = "xyes"; then
	GMCS_FLAGS="-define:RELEASE"
else
	GMCS_FLAGS="-warnaserror"
fi
AC_SUBST(GMCS_FLAGS)

AC_ARG_WITH(vendor-build-id,
	AC_HELP_STRING([--with-vendor-build-id=<vendor-build-id>],
		[Set a vendor build ID, recommended for packagers]),
	[vendor_build_id="$withval"], [
		test -d .git \
			&& vendor_build_id="git-checkout" \
			|| vendor_build_id="source-tarball"
	])
BUILD_VENDOR_ID="$vendor_build_id"
BUILD_HOST_OS="$host_os"
BUILD_HOST_CPU="$host_cpu"
BUILD_TIME=`date +"%F %T %Z"`
AC_SUBST(BUILD_VENDOR_ID)
AC_SUBST(BUILD_HOST_OS)
AC_SUBST(BUILD_HOST_CPU)
AC_SUBST(BUILD_TIME)

SHAVE_INIT([build/m4/shave], [enable])

dnl generated files
AC_OUTPUT([
Makefile

build/Makefile
build/m4/Makefile
build/m4/shave/shave
build/m4/shave/shave-libtool
build/pkg-config/Makefile

data/Makefile
data/org.bansheeproject.Banshee.service
data/org.bansheeproject.CollectionIndexer.service
data/audio-profiles/Makefile
data/desktop-files/Makefile
data/desktop-files/common.desktop
data/icon-theme-hicolor/Makefile

docs/Makefile
docs/Hyena/Makefile
docs/Banshee/Makefile

po/Makefile.in

tests/Makefile
tests/Analyzer/Makefile
tests/Performance/Makefile

extras/Makefile

libbanshee/Makefile

gstreamer/Makefile
gstreamer/equalizer/Makefile

src/Makefile
src/AssemblyInfo.cs

src/Clients/Makefile
src/Clients/Beroe/Makefile
src/Clients/Booter/Makefile
src/Clients/Booter/banshee-1.linux
src/Clients/Booter/banshee-1.darwin
src/Clients/Halie/Makefile
src/Clients/Muinshee/Makefile
src/Clients/Nereid/Makefile
src/Clients/MeeGo/Makefile

src/Core/Makefile
src/Core/Banshee.CollectionIndexer/Makefile
src/Core/Banshee.Core/Makefile
src/Core/Banshee.Services/Makefile
src/Core/Banshee.ThickClient/Makefile
src/Core/Banshee.Widgets/Makefile

src/Backends/Makefile
src/Backends/Banshee.Gio/Makefile
src/Backends/Banshee.Gnome/Makefile
src/Backends/Banshee.GStreamer/Makefile
src/Backends/Banshee.Hal/Makefile
src/Backends/Banshee.Unix/Makefile
src/Backends/Banshee.Osx/Makefile
src/Backends/Banshee.NowPlaying.X11/Makefile
src/Backends/Banshee.NowPlaying.X11/libbnpx11/Makefile

src/Libraries/Makefile
src/Libraries/Hyena/Makefile
src/Libraries/Hyena.Gui/Makefile
src/Libraries/Lastfm/Makefile
src/Libraries/Lastfm.Gui/Makefile
src/Libraries/Migo/Makefile
src/Libraries/Mono.Data.Sqlite/Makefile
src/Libraries/Mono.Media/Makefile
src/Libraries/Mtp/Makefile
src/Libraries/Mtp/Mtp.dll.config
src/Libraries/MusicBrainz/Makefile

src/Dap/Makefile
src/Dap/Banshee.Dap/Makefile
src/Dap/Banshee.Dap.Ipod/Makefile
src/Dap/Banshee.Dap.MassStorage/Makefile
src/Dap/Banshee.Dap.Mtp/Makefile
src/Dap/Banshee.Dap.Karma/Makefile

src/Extensions/Makefile
src/Extensions/Banshee.Audiobook/Makefile
src/Extensions/Banshee.AudioCd/Makefile
src/Extensions/Banshee.Bookmarks/Makefile
src/Extensions/Banshee.BooScript/Makefile
src/Extensions/Banshee.Bpm/Makefile
src/Extensions/Banshee.CoverArt/Makefile
src/Extensions/Banshee.Daap/Makefile
src/Extensions/Banshee.Emusic/Makefile
src/Extensions/Banshee.FileSystemQueue/Makefile
src/Extensions/Banshee.InternetArchive/Makefile
src/Extensions/Banshee.InternetRadio/Makefile
src/Extensions/Banshee.Lastfm/Makefile
src/Extensions/Banshee.LibraryWatcher/Makefile
src/Extensions/Banshee.MiniMode/Makefile
src/Extensions/Banshee.MeeGo/Makefile
src/Extensions/Banshee.MultimediaKeys/Makefile
src/Extensions/Banshee.NotificationArea/Makefile
src/Extensions/Banshee.NowPlaying/Makefile
src/Extensions/Banshee.PlayQueue/Makefile
src/Extensions/Banshee.PlayerMigration/Makefile
src/Extensions/Banshee.Podcasting/Makefile
src/Extensions/Banshee.SqlDebugConsole/Makefile
src/Extensions/Banshee.Sample/Makefile
src/Extensions/Banshee.Torrent/Makefile
src/Extensions/Banshee.RemoteAudio/Makefile
src/Extensions/Banshee.Wikipedia/Makefile
src/Extensions/Banshee.YouTube/Makefile

extras/metrics/Makefile
])

cat <<EOF

${PACKAGE}-${VERSION}

  Build Environment
    Install Prefix:    ${prefix}
    Datadir:           ${expanded_datadir}
    Libdir:            ${expanded_libdir}

    C Compiler:        ${CC}
    Mono C# Compiler:  ${MCS} ${GMCS_FLAGS}
    Mono Runtime:      ${MONO}

  Video/Graphics:
    Graphics System:   ${GRAPHICS_SUBSYSTEM}
    X11 Video:         ${have_xvidmode}
    Clutter:           ${enable_clutter}

  Operating System/Desktop Environment:
    GNOME Support:     ${enable_gnome}
    OSX Support:       ${enable_osx}
    MeeGo Support:     ${enable_meego}

  Digital Audio Player (DAP) Support:
    Mass Storage:      yes
    MTP:               ${enable_libmtp}
    iPod:              ${enable_ipodsharp}
    Karma:             ${enable_karmasharp}

  Extra Features:
    Boo Scripting:     ${enable_boo}
    Builtin Equalizer: ${enable_builtin_equalizer}
    DAAP:              ${enable_daap}
    GIO Backend:       ${enable_gio} (requires glib >= 2.22, gtk-sharp-beans, and gio-sharp)
    Library Watcher:   ${HAVE_MONO_2_4_3} (requires Mono >= 2.4.3)
    Podcasts:          ${enable_podcast}
    Wikipedia:         ${enable_webkit} (requires webkit-sharp)
    Gapless playback:  ${ENABLE_GAPLESS} (requires gstreamer-plugins-base > 0.10.25.2)
    YouTube extension: ${enable_youtube} (requires gdata-sharp >= 1.4)

  Build/Development:
    Unit Tests:        ${do_tests} (requires nunit >= ${NUNIT_REQUIRED})
    API Docs:          ${enable_docs} (requires monodocer and mdassembler)
    Custom a11y:       ${gtksharp_with_a11y} (requires gtk-sharp >= 2.12.10)
    Release Build:     ${enable_release}
    Vendor Build ID:   ${vendor_build_id}

EOF

# Unstable/in-development features; only show them if they were manually enabled
if test "x$enable_moonlight" = "xyes";    then br=yes; echo "  Moonlight Effects:   ${enable_moonlight}";    fi
if test "x$enable_remote_audio" = "xyes"; then br=yes; echo "  Remote Audio:        ${enable_remote_audio}"; fi
if test "x$enable_torrent" = "xyes";      then br=yes; echo "  Torrent Podcasts:    ${enable_torrent}";      fi

if test -d ${expanded_libdir}/${PACKAGE}; then
	if test x$br = xyes; then echo; fi
	echo "  WARNING: An existing Banshee install is in ${expanded_libdir}/${PACKAGE}"
	echo "           Remove the existing install before installing this build."
	echo "           Installing over an existing install will cause conflicts!"
	echo 
fi

