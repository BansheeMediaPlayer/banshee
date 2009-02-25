dnl Automake setup
AC_INIT([banshee-1], [1.4.3])
DISPLAY_VERSION="1.4.3"
API_VERSION="1.4"
ASM_VERSION="$API_VERSION.0.0"
AC_SUBST(API_VERSION)
AC_SUBST(ASM_VERSION)
AC_SUBST(DISPLAY_VERSION)

DISTCHECK_CONFIGURE_FLAGS="--disable-docs --enable-mtp --enable-daap --enable-podcast --enable-release"
AC_SUBST(DISTCHECK_CONFIGURE_FLAGS)
AC_SUBST([ACLOCAL_AMFLAGS], ["-I build/m4/shamrock -I build/m4/banshee \${ACLOCAL_FLAGS}"])

AC_CANONICAL_SYSTEM
AC_PREREQ(2.13)
AM_INIT_AUTOMAKE([1.9 dist-bzip2 tar-ustar])
AM_MAINTAINER_MODE

dnl Pieces needed by autogen; can't be parsed from macros
AM_CONFIG_HEADER(config.h)
IT_PROG_INTLTOOL([0.35.0])
AC_PROG_LIBTOOL
AC_PROG_INSTALL

dnl Expansions
SHAMROCK_EXPAND_LIBDIR
SHAMROCK_EXPAND_BINDIR
SHAMROCK_EXPAND_DATADIR

dnl libbanshee/native dependencies (required)
BANSHEE_CHECK_LIBBANSHEE
BANSHEE_CHECK_GSTREAMER

BANSHEE_CHECK_NOW_PLAYING_X11

dnl Mono and gmcs
SHAMROCK_CHECK_MONO_MODULE(1.2.2)
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

dnl MediaWeb (optional through --enable-mediaweb)
BANSHEE_CHECK_MEDIAWEB

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
	enable_release=yes, enable_release=`test ! -d .svn && echo "yes" || echo "no"`)
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
		test -d .svn \
			&& vendor_build_id="svn-checkout" \
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

dnl generated files
AC_OUTPUT([
Makefile

build/Makefile
build/m4/Makefile
build/pkg-config/Makefile

data/Makefile
data/org.bansheeproject.Banshee.service
data/org.bansheeproject.CollectionIndexer.service
data/audio-profiles/Makefile
data/desktop-files/Makefile
data/icon-theme-hicolor/Makefile

docs/Makefile
docs/Hyena/Makefile
docs/Banshee/Makefile

po/Makefile.in

tests/Makefile

extras/Makefile

libbanshee/Makefile

gstreamer/Makefile
gstreamer/equalizer/Makefile

src/Makefile
src/AssemblyInfo.cs

src/Clients/Makefile
src/Clients/Beroe/Makefile
src/Clients/Booter/Makefile
src/Clients/Booter/banshee-1
src/Clients/Halie/Makefile
src/Clients/Muinshee/Makefile
src/Clients/Nereid/Makefile

src/Core/Makefile
src/Core/Banshee.CollectionIndexer/Makefile
src/Core/Banshee.Core/Makefile
src/Core/Banshee.Services/Makefile
src/Core/Banshee.ThickClient/Makefile
src/Core/Banshee.Widgets/Makefile

src/Backends/Makefile
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
src/Extensions/Banshee.AudioCd/Makefile
src/Extensions/Banshee.Bookmarks/Makefile
src/Extensions/Banshee.BooScript/Makefile
src/Extensions/Banshee.CoverArt/Makefile
src/Extensions/Banshee.Daap/Makefile
src/Extensions/Banshee.FileSystemQueue/Makefile
src/Extensions/Banshee.InternetRadio/Makefile
src/Extensions/Banshee.Lastfm/Makefile
src/Extensions/Banshee.MediaWeb/Makefile
src/Extensions/Banshee.MiniMode/Makefile
src/Extensions/Banshee.MultimediaKeys/Makefile
src/Extensions/Banshee.NotificationArea/Makefile
src/Extensions/Banshee.NowPlaying/Makefile
src/Extensions/Banshee.PlayQueue/Makefile
src/Extensions/Banshee.Podcasting/Makefile
src/Extensions/Banshee.SqlDebugConsole/Makefile
src/Extensions/Banshee.Sample/Makefile
src/Extensions/Banshee.Torrent/Makefile
src/Extensions/Banshee.RemoteAudio/Makefile
])

echo "
${PACKAGE}-${VERSION}

    Install Prefix:    ${prefix}
    Datadir:           ${expanded_datadir}
    Libdir:            ${expanded_libdir}

    C Compiler:        ${CC}
    Mono C# Compiler:  ${MCS} ${GMCS_FLAGS}
    Mono Runtime:      ${MONO}

    Digital Audio Player (DAP) Support:
      Mass Storage:    yes
      MTP:             ${enable_libmtp}
      iPod:            ${enable_ipodsharp}
      Karma:           ${enable_karmasharp}

    DAAP Support:      ${enable_daap}
    Podcast Support:   ${enable_podcast}
    Boo Scripting:     ${enable_boo}
    X11 Video Support: ${have_xvidmode}
    GNOME Support:     ${enable_gnome}
    OSX Support:       ${enable_osx}
    Graphics System:   ${GRAPHICS_SUBSYSTEM}

    Unit Tests:        ${do_tests}	(requires nunit >= ${NUNIT_REQUIRED})
    Release Build:     ${enable_release}
    Vendor Build ID:   ${vendor_build_id}
"

# Unstable/in-development features; only show them if they were manually enabled
if test "x$enable_moonlight" = "xyes";    then echo "    Moonlight Effects: ${enable_moonlight}"; fi
if test "x$enable_remote_audio" = "xyes"; then echo "    Remote Audio:      ${enable_remote_audio}"; fi
if test "x$enable_mediaweb" = "xyes";     then echo "    MediaWeb:          ${enable_mediaweb}"; fi
if test "x$enable_torrent" = "xyes";      then echo "    Torrent Podcasts:  ${enable_torrent}"; fi

if test -d ${expanded_libdir}/${PACKAGE}; then
	echo
	echo "WARNING: An existing Banshee install is in ${expanded_libdir}/${PACKAGE}"
	echo "         Remove the existing install before installing this build."
	echo "         Installing over an existing install will cause conflicts!"
	echo 
fi

