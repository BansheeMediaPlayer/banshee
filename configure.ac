dnl Granularly define the version components
dnl Remember to also update the Display Version
m4_define([banshee_version_major], [2])
m4_define([banshee_version_minor], [6])
m4_define([banshee_version_micro], [2])

dnl this can sometimes differ manually
m4_define([banshee_display_version], ["2.6.2"])

dnl set to 0 when doing an official release
m4_define([banshee_version_pre_release], [0])

m4_define([concat], $1$2$3$4)

dnl create base version string
m4_define([banshee_version], concat(banshee_version_major., banshee_version_minor., banshee_version_micro))

dnl create the release version
m4_define([banshee_version],
	m4_if(banshee_version_pre_release, [0],
		banshee_version,
		concat(banshee_version, banshee_version_pre_release)))

m4_define([banshee_api_version], [banshee_version_major.banshee_version_minor])
m4_define([banshee_asm_version], [banshee_api_version.0.0])

AC_PREREQ(2.52)
AC_INIT([banshee], banshee_version, [http://bugzilla.gnome.org/enter_bug.cgi?product=banshee])

AM_INIT_AUTOMAKE([1.10 no-dist-gzip dist-bzip2 tar-ustar foreign])
AM_MAINTAINER_MODE([enable])
AC_CONFIG_HEADERS([config.h])
AC_CONFIG_MACRO_DIR([build/m4])
AC_SUBST([ACLOCAL_AMFLAGS], ["-I build/m4/shamrock -I build/m4/banshee -I build/m4/shave \${ACLOCAL_FLAGS}"])

IT_PROG_INTLTOOL([0.35.0])
AC_PROG_LIBTOOL
AC_PROG_INSTALL

dnl How to configure for distcheck
DISTCHECK_CONFIGURE_FLAGS="--disable-docs --enable-mtp --enable-daap --enable-user-help --enable-release"
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

dnl For the new media backend using gstreamer-sharp
BANSHEE_CHECK_GSTREAMER_SHARP

BANSHEE_CHECK_NOW_PLAYING_X11

dnl User help
SHAMROCK_CHECK_GNOME_DOC_UTILS(0.17.3)

dnl Mono and gmcs
SHAMROCK_CHECK_MONO_MODULE(2.4.3)
SHAMROCK_FIND_MONO_2_0_COMPILER
SHAMROCK_FIND_MONO_RUNTIME
SHAMROCK_CHECK_MONO_2_0_GAC_ASSEMBLIES([Mono.Cairo Mono.Posix ICSharpCode.SharpZipLib])

dnl Check for GNOME >= 2.28 to see if we should remove
dnl 'Media Player' from the app Name, bgo#596242, except
dnl in MeeGo where GNOME >= 2.28, but we want the full name
PKG_CHECK_MODULES(GNOME_2_28, gnome-desktop-2.0 >= 2.28, HAVE_GNOME_2_28=yes, HAVE_GNOME_2_28=no)
PKG_CHECK_MODULES(MEEGO_PANEL, meego-panel, HAVE_MEEGO_PANEL=yes, HAVE_MEEGO_PANEL=no)
if test "x$HAVE_GNOME_2_28" = "xno" -o "x$HAVE_MEEGO_PANEL" = "xyes"; then
	BANSHEE_DESKTOP_NAME="_Name=Banshee Media Player"
else
	BANSHEE_DESKTOP_NAME="_Name=Banshee"
fi
AC_SUBST(BANSHEE_DESKTOP_NAME)

if test "x$HAVE_MEEGO_PANEL" = "xyes"; then
  MEEGO_PANELS_DIR=`$PKG_CONFIG --variable=meego_panel_panels_dir meego-panel`
else
  MEEGO_PANELS_DIR='${datadir}/mutter-meego/panels'
fi
AC_SUBST(MEEGO_PANELS_DIR)


dnl Check for gdk-x11 >= 2.22 for NotificationArea extension, bgo#638477
PKG_CHECK_MODULES(GDK_2_22, gdk-x11-2.0 >= 2.22, have_gdk_2_22=yes, have_gdk_2_22=no)
AM_CONDITIONAL(HAVE_GDK_2_22, test "x$have_gdk_2_22" = "xyes")

dnl DBus Sharp libraries
BANSHEE_CHECK_DBUS_SHARP

dnl Mono.Addins libraries
BANSHEE_CHECK_MONO_ADDINS

dnl notify-sharp library
BANSHEE_CHECK_NOTIFY_SHARP

dnl Boo
BANSHEE_CHECK_BOO

dnl Monodoc (optional through --disable-docs)
SHAMROCK_CHECK_MONODOC

dnl webkit (optional through --disable-webkit)
BANSHEE_CHECK_LIBWEBKIT

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

dnl Mono.Upnp libraries
BANSHEE_CHECK_MONO_UPNP

PKG_CHECK_MODULES(SQLITE, sqlite3 >= 3.4)

dnl GNOME (optional)
AC_ARG_ENABLE(gnome, AC_HELP_STRING([--disable-gnome], [Disable GNOME support]), , enable_gnome="yes")
if test "x$enable_gnome" = "xyes"; then
	BANSHEE_CHECK_GNOME_SHARP
	BANSHEE_CHECK_GCONF
	AM_CONDITIONAL(ENABLE_GNOME, true)
else
	AM_CONDITIONAL(GCONF_SCHEMAS_INSTALL, false)
	AM_CONDITIONAL(HAVE_DBUS_GLIB, false)
	AM_CONDITIONAL(ENABLE_GNOME, false)
fi

dnl DAP support (each module is optional)
BANSHEE_CHECK_DAP_MTP
BANSHEE_CHECK_DAP_APPLEDEVICE
BANSHEE_CHECK_DAP_KARMA

dnl DAAP support (optional)
BANSHEE_CHECK_DAAP

dnl RemoteAudio support (optional)
BANSHEE_CHECK_REMOTE_AUDIO

dnl MonoTorrent support (optional)
BANSHEE_CHECK_TORRENT

dnl MeeGo integration (optional)
BANSHEE_CHECK_MEEGO

dnl Sound menu integration (optional)
BANSHEE_CHECK_SOUNDMENU

dnl Ubuntu One Music Store (optional)
BANSHEE_CHECK_UBUNTUONE

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

AM_CONDITIONAL(HYENA_PKG_CONFIG, false)

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

help/Makefile

docs/Makefile
docs/Hyena/Makefile
docs/Banshee/Makefile

po/Makefile.in

tests/Makefile
tests/Analyzer/Makefile
tests/Performance/Makefile

extras/Makefile

libbanshee/Makefile

src/Hyena/Makefile
src/Hyena/build/Makefile
src/Hyena/build/pkg-config/Makefile
src/Hyena/build/m4/Makefile
src/Hyena/Hyena/Makefile
src/Hyena/Hyena.Data.Sqlite/Makefile
src/Hyena/Hyena.Gui/Makefile

src/Makefile
src/AssemblyInfo.cs

src/Clients/Makefile
src/Clients/Beroe/Makefile
src/Clients/Booter/Makefile
src/Clients/Booter/banshee.linux
src/Clients/Booter/banshee.darwin
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
src/Core/Banshee.WebBrowser/Makefile
src/Core/Banshee.WebBrowser/libossifer/Makefile

src/Backends/Makefile
src/Backends/Banshee.Gio/Makefile
src/Backends/Banshee.Gnome/Makefile
src/Backends/Banshee.GStreamer/Makefile
src/Backends/Banshee.GStreamerSharp/Makefile
src/Backends/Banshee.Unix/Makefile
src/Backends/Banshee.Osx/Makefile
src/Backends/Banshee.NowPlaying.X11/Makefile
src/Backends/Banshee.NowPlaying.X11/libbnpx11/Makefile
src/Backends/Banshee.Windows/Makefile

src/Libraries/Makefile
src/Libraries/Lastfm/Makefile
src/Libraries/Lastfm.Gui/Makefile
src/Libraries/Migo/Makefile
src/Libraries/Mono.Media/Makefile
src/Libraries/Mtp/Makefile
src/Libraries/Mtp/Mtp.dll.config
src/Libraries/MusicBrainz/Makefile

src/Dap/Makefile
src/Dap/Banshee.Dap/Makefile
src/Dap/Banshee.Dap.AppleDevice/Makefile
src/Dap/Banshee.Dap.MassStorage/Makefile
src/Dap/Banshee.Dap.Mtp/Makefile
src/Dap/Banshee.Dap.Karma/Makefile

src/Extensions/Makefile
src/Extensions/Banshee.AmazonMp3/Makefile
src/Extensions/Banshee.AmazonMp3.Store/Makefile
src/Extensions/Banshee.Audiobook/Makefile
src/Extensions/Banshee.BooScript/Makefile
src/Extensions/Banshee.Bpm/Makefile
src/Extensions/Banshee.CoverArt/Makefile
src/Extensions/Banshee.Daap/Makefile
src/Extensions/Banshee.Emusic/Makefile
src/Extensions/Banshee.Emusic.Store/Makefile
src/Extensions/Banshee.FileSystemQueue/Makefile
src/Extensions/Banshee.InternetArchive/Makefile
src/Extensions/Banshee.Fixup/Makefile
src/Extensions/Banshee.InternetRadio/Makefile
src/Extensions/Banshee.Lastfm/Makefile
src/Extensions/Banshee.LastfmStreaming/Makefile
src/Extensions/Banshee.LibraryWatcher/Makefile
src/Extensions/Banshee.MiniMode/Makefile
src/Extensions/Banshee.MiroGuide/Makefile
src/Extensions/Banshee.MeeGo/Makefile
src/Extensions/Banshee.Mpris/Makefile
src/Extensions/Banshee.MultimediaKeys/Makefile
src/Extensions/Banshee.NotificationArea/Makefile
src/Extensions/Banshee.NowPlaying/Makefile
src/Extensions/Banshee.OpticalDisc/Makefile
src/Extensions/Banshee.PlayQueue/Makefile
src/Extensions/Banshee.PlayerMigration/Makefile
src/Extensions/Banshee.Podcasting/Makefile
src/Extensions/Banshee.SoundMenu/Makefile
src/Extensions/Banshee.SqlDebugConsole/Makefile
src/Extensions/Banshee.Sample/Makefile
src/Extensions/Banshee.Torrent/Makefile
src/Extensions/Banshee.RemoteAudio/Makefile
src/Extensions/Banshee.UbuntuOneMusicStore/Makefile
src/Extensions/Banshee.UPnPClient/Makefile
src/Extensions/Banshee.Wikipedia/Makefile
src/Extensions/Banshee.YouTube/Makefile

extras/metrics/Makefile
])

cat <<EOF

${PACKAGE}-${VERSION}

  Build Environment
    Install Prefix:        ${prefix}
    Datadir:               ${expanded_datadir}
    Libdir:                ${expanded_libdir}

    C Compiler:            ${CC}
    Mono C# Compiler:      ${MCS} ${GMCS_FLAGS}
    Mono Runtime:          ${MONO}
    Gtk#:                  ${gtk_version}

  Video/Graphics:
    Graphics System:       ${GRAPHICS_SUBSYSTEM}
    X11 Video:             ${have_xvidmode}
    Clutter:               ${enable_clutter}

  Operating System/Desktop Environment:
    GNOME Support:         ${enable_gnome}
    OSX Support:           ${enable_osx}
    MeeGo Support:         ${enable_meego}

  Digital Audio Player (DAP) Support:
    Apple Device:          ${enable_appledevice} (libgpod-sharp)
    Karma:                 ${enable_karmasharp}
    Mass Storage:          yes
    MTP:                   ${enable_libmtp}

  Extra Features:
    Web Browser:           ${have_libwebkit} (webkit-1.0 >= $WEBKIT_MIN_VERSION, libsoup-2.4 >= $SOUP_MIN_VERSION)
      + GNOME Proxy        ${have_libsoup_gnome} (libsoup-gnome-2.4 >= $SOUP_GNOME_MIN_VERSION)
    Boo Scripting:         ${enable_boo}
    DAAP:                  ${enable_daap}
    GIO Backend:           ${enable_gio} (glib >= 2.22, gio-sharp >= 2.22.3, and gtk-sharp-beans)
    GIO Hardware:          ${enable_gio_hardware} (gudev-sharp, gkeyfile-sharp)
    YouTube:               ${enable_youtube} (gdata-sharp >= 1.4)
    Sound Menu:            ${enable_soundmenu}
    Ubuntu One Store:      ${enable_ubuntuone} (ubuntuone-sharp >= 0.3.2)
    UPnP:                  ${enable_upnp} (mono-upnp >= $MONOUPNP_REQUIRED)

  Build/Development:
    Unit Tests:            ${do_tests} (nunit >= ${NUNIT_REQUIRED})
    API Docs:              ${enable_docs} (monodocer and mdassembler)
    User Help:             ${enable_user_help} (gnome-doc-utils >= 0.17.3)
    Release Build:         ${enable_release}
    Vendor Build ID:       ${vendor_build_id}

EOF

# Unstable/in-development features; only show them if they were manually enabled
if test "x$enable_moonlight" = "xyes";    then br=yes; echo "  Moonlight Effects:   ${enable_moonlight}";    fi
if test "x$enable_remote_audio" = "xyes"; then br=yes; echo "  Remote Audio:        ${enable_remote_audio}"; fi
if test "x$enable_torrent" = "xyes";      then br=yes; echo "  Torrent Podcasts:    ${enable_torrent}";      fi
if test "x$enable_gst_sharp" = "xyes";    then br=yes; echo "  Gst# Backend:        ${enable_gst_sharp} (unfinished, experimental!)";      fi

if test -d ${expanded_libdir}/${PACKAGE}; then
	if test x$br = xyes; then echo; fi
	echo "  WARNING: An existing Banshee install is in ${expanded_libdir}/${PACKAGE}"
	echo "           Remove the existing install before installing this build."
	echo "           Installing over an existing install will cause conflicts!"
	echo 
fi
