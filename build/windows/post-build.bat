REM This file is called/ran after Banshee.Core.dll is built, see src/Core/Banshee.Core/Banshee.Core.csproj

mkdir bin\share\icons\hicolor\128x128\devices
mkdir bin\share\icons\hicolor\16x16\actions
mkdir bin\share\icons\hicolor\16x16\categories
mkdir bin\share\icons\hicolor\16x16\devices
mkdir bin\share\icons\hicolor\16x16\emblems
mkdir bin\share\icons\hicolor\16x16\status
mkdir bin\share\icons\hicolor\22x22\actions
mkdir bin\share\icons\hicolor\22x22\categories
mkdir bin\share\icons\hicolor\22x22\devices
mkdir bin\share\icons\hicolor\24x24\categories
mkdir bin\share\icons\hicolor\24x24\devices
mkdir bin\share\icons\hicolor\256x256\categories
mkdir bin\share\icons\hicolor\256x256\devices
mkdir bin\share\icons\hicolor\32x32\actions
mkdir bin\share\icons\hicolor\32x32\categories
mkdir bin\share\icons\hicolor\32x32\devices
mkdir bin\share\icons\hicolor\48x48
mkdir bin\share\icons\hicolor\48x48\actions
mkdir bin\share\icons\hicolor\48x48\categories
mkdir bin\share\icons\hicolor\48x48\devices
copy /Y "src\Backends\Banshee.Gnome\ThemeIcons\16x16\actions\media-write-cd.png" bin\share\icons\hicolor\16x16\actions\media-write-cd.png
copy /Y "src\Backends\Banshee.Gnome\ThemeIcons\22x22\actions\media-write-cd.png" bin\share\icons\hicolor\22x22\actions\media-write-cd.png
copy /Y "src\Backends\Banshee.Gnome\ThemeIcons\32x32\actions\media-write-cd.png" bin\share\icons\hicolor\32x32\actions\media-write-cd.png
copy /Y "src\Backends\Banshee.Gnome\ThemeIcons\48x48\actions\media-write-cd.png" bin\share\icons\hicolor\48x48\actions\media-write-cd.png
copy /Y "src\Core\Banshee.ThickClient\ThemeIcons\16x16\actions\media-repeat-all.png" bin\share\icons\hicolor\16x16\actions\media-repeat-all.png
copy /Y "src\Core\Banshee.ThickClient\ThemeIcons\16x16\actions\media-repeat-none.png" bin\share\icons\hicolor\16x16\actions\media-repeat-none.png
copy /Y "src\Core\Banshee.ThickClient\ThemeIcons\16x16\actions\media-repeat-single.png" bin\share\icons\hicolor\16x16\actions\media-repeat-single.png
copy /Y "src\Core\Banshee.ThickClient\ThemeIcons\16x16\categories\source-playlist.png" bin\share\icons\hicolor\16x16\categories\source-playlist.png
copy /Y "src\Core\Banshee.ThickClient\ThemeIcons\16x16\categories\source-smart-playlist.png" bin\share\icons\hicolor\16x16\categories\source-smart-playlist.png
copy /Y "src\Core\Banshee.ThickClient\ThemeIcons\16x16\emblems\creative-commons-by.png" bin\share\icons\hicolor\16x16\emblems\creative-commons-by.png
copy /Y "src\Core\Banshee.ThickClient\ThemeIcons\16x16\emblems\creative-commons-nc.png" bin\share\icons\hicolor\16x16\emblems\creative-commons-nc.png
copy /Y "src\Core\Banshee.ThickClient\ThemeIcons\16x16\emblems\creative-commons-nd.png" bin\share\icons\hicolor\16x16\emblems\creative-commons-nd.png
copy /Y "src\Core\Banshee.ThickClient\ThemeIcons\16x16\emblems\creative-commons-pd.png" bin\share\icons\hicolor\16x16\emblems\creative-commons-pd.png
copy /Y "src\Core\Banshee.ThickClient\ThemeIcons\16x16\emblems\creative-commons-sa.png" bin\share\icons\hicolor\16x16\emblems\creative-commons-sa.png
copy /Y "src\Core\Banshee.ThickClient\ThemeIcons\22x22\actions\encode.png" bin\share\icons\hicolor\22x22\actions\encode.png
copy /Y "src\Core\Banshee.ThickClient\ThemeIcons\22x22\categories\source-playlist.png" bin\share\icons\hicolor\22x22\categories\source-playlist.png
copy /Y "src\Core\Banshee.ThickClient\ThemeIcons\22x22\categories\source-smart-playlist.png" bin\share\icons\hicolor\22x22\categories\source-smart-playlist.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\16x16\devices\multimedia-player-ipod-mini-blue.png" bin\share\icons\hicolor\16x16\devices\multimedia-player-ipod-mini-blue.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\16x16\devices\multimedia-player-ipod-mini-gold.png" bin\share\icons\hicolor\16x16\devices\multimedia-player-ipod-mini-gold.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\16x16\devices\multimedia-player-ipod-mini-green.png" bin\share\icons\hicolor\16x16\devices\multimedia-player-ipod-mini-green.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\16x16\devices\multimedia-player-ipod-mini-pink.png" bin\share\icons\hicolor\16x16\devices\multimedia-player-ipod-mini-pink.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\16x16\devices\multimedia-player-ipod-mini-silver.png" bin\share\icons\hicolor\16x16\devices\multimedia-player-ipod-mini-silver.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\16x16\devices\multimedia-player-ipod-nano-black.png" bin\share\icons\hicolor\16x16\devices\multimedia-player-ipod-nano-black.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\16x16\devices\multimedia-player-ipod-nano-white.png" bin\share\icons\hicolor\16x16\devices\multimedia-player-ipod-nano-white.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\16x16\devices\multimedia-player-ipod-shuffle.png" bin\share\icons\hicolor\16x16\devices\multimedia-player-ipod-shuffle.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\16x16\devices\multimedia-player-ipod-standard-color.png" bin\share\icons\hicolor\16x16\devices\multimedia-player-ipod-standard-color.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\16x16\devices\multimedia-player-ipod-standard-monochrome.png" bin\share\icons\hicolor\16x16\devices\multimedia-player-ipod-standard-monochrome.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\16x16\devices\multimedia-player-ipod-U2-color.png" bin\share\icons\hicolor\16x16\devices\multimedia-player-ipod-U2-color.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\16x16\devices\multimedia-player-ipod-U2-monochrome.png" bin\share\icons\hicolor\16x16\devices\multimedia-player-ipod-U2-monochrome.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\16x16\devices\multimedia-player-ipod-video-black.png" bin\share\icons\hicolor\16x16\devices\multimedia-player-ipod-video-black.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\16x16\devices\multimedia-player-ipod-video-white.png" bin\share\icons\hicolor\16x16\devices\multimedia-player-ipod-video-white.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\22x22\devices\multimedia-player-ipod-mini-blue.png" bin\share\icons\hicolor\22x22\devices\multimedia-player-ipod-mini-blue.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\22x22\devices\multimedia-player-ipod-mini-gold.png" bin\share\icons\hicolor\22x22\devices\multimedia-player-ipod-mini-gold.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\22x22\devices\multimedia-player-ipod-mini-green.png" bin\share\icons\hicolor\22x22\devices\multimedia-player-ipod-mini-green.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\22x22\devices\multimedia-player-ipod-mini-pink.png" bin\share\icons\hicolor\22x22\devices\multimedia-player-ipod-mini-pink.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\22x22\devices\multimedia-player-ipod-mini-silver.png" bin\share\icons\hicolor\22x22\devices\multimedia-player-ipod-mini-silver.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\22x22\devices\multimedia-player-ipod-nano-black.png" bin\share\icons\hicolor\22x22\devices\multimedia-player-ipod-nano-black.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\22x22\devices\multimedia-player-ipod-nano-white.png" bin\share\icons\hicolor\22x22\devices\multimedia-player-ipod-nano-white.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\22x22\devices\multimedia-player-ipod-shuffle.png" bin\share\icons\hicolor\22x22\devices\multimedia-player-ipod-shuffle.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\22x22\devices\multimedia-player-ipod-standard-color.png" bin\share\icons\hicolor\22x22\devices\multimedia-player-ipod-standard-color.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\22x22\devices\multimedia-player-ipod-standard-monochrome.png" bin\share\icons\hicolor\22x22\devices\multimedia-player-ipod-standard-monochrome.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\22x22\devices\multimedia-player-ipod-U2-color.png" bin\share\icons\hicolor\22x22\devices\multimedia-player-ipod-U2-color.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\22x22\devices\multimedia-player-ipod-U2-monochrome.png" bin\share\icons\hicolor\22x22\devices\multimedia-player-ipod-U2-monochrome.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\22x22\devices\multimedia-player-ipod-video-black.png" bin\share\icons\hicolor\22x22\devices\multimedia-player-ipod-video-black.png
copy /Y "src\Dap\Banshee.Dap.AppleDevice\ThemeIcons\22x22\devices\multimedia-player-ipod-video-white.png" bin\share\icons\hicolor\22x22\devices\multimedia-player-ipod-video-white.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\128x128\devices\phone-google-nexus-one.png" bin\share\icons\hicolor\128x128\devices\phone-google-nexus-one.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\128x128\devices\phone-htc-g1-white.png" bin\share\icons\hicolor\128x128\devices\phone-htc-g1-white.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\128x128\devices\phone-nokia-n900.png" bin\share\icons\hicolor\128x128\devices\phone-nokia-n900.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\128x128\devices\phone-nook.png" bin\share\icons\hicolor\128x128\devices\phone-nook.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\128x128\devices\phone-xperia-arc.png" bin\share\icons\hicolor\128x128\devices\phone-xperia-arc.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\16x16\categories\amazon-mp3-source.png" bin\share\icons\hicolor\16x16\categories\amazon-mp3-source.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\16x16\devices\phone-google-nexus-one.png" bin\share\icons\hicolor\16x16\devices\phone-google-nexus-one.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\16x16\devices\phone-htc-g1-white.png" bin\share\icons\hicolor\16x16\devices\phone-htc-g1-white.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\16x16\devices\phone-nokia-n900.png" bin\share\icons\hicolor\16x16\devices\phone-nokia-n900.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\16x16\devices\phone-nook.png" bin\share\icons\hicolor\16x16\devices\phone-nook.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\16x16\devices\phone-palm-pre.png" bin\share\icons\hicolor\16x16\devices\phone-palm-pre.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\16x16\devices\phone-xperia-arc.png" bin\share\icons\hicolor\16x16\devices\phone-xperia-arc.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\22x22\categories\amazon-mp3-source.png" bin\share\icons\hicolor\22x22\categories\amazon-mp3-source.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\22x22\devices\phone-google-nexus-one.png" bin\share\icons\hicolor\22x22\devices\phone-google-nexus-one.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\22x22\devices\phone-htc-g1-white.png" bin\share\icons\hicolor\22x22\devices\phone-htc-g1-white.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\22x22\devices\phone-nokia-n900.png" bin\share\icons\hicolor\22x22\devices\phone-nokia-n900.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\22x22\devices\phone-nook.png" bin\share\icons\hicolor\22x22\devices\phone-nook.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\22x22\devices\phone-palm-pre.png" bin\share\icons\hicolor\22x22\devices\phone-palm-pre.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\22x22\devices\phone-xperia-arc.png" bin\share\icons\hicolor\22x22\devices\phone-xperia-arc.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\24x24\devices\phone-google-nexus-one.png" bin\share\icons\hicolor\24x24\devices\phone-google-nexus-one.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\24x24\devices\phone-htc-g1-white.png" bin\share\icons\hicolor\24x24\devices\phone-htc-g1-white.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\24x24\devices\phone-nokia-n900.png" bin\share\icons\hicolor\24x24\devices\phone-nokia-n900.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\24x24\devices\phone-nook.png" bin\share\icons\hicolor\24x24\devices\phone-nook.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\24x24\devices\phone-palm-pre.png" bin\share\icons\hicolor\24x24\devices\phone-palm-pre.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\24x24\devices\phone-xperia-arc.png" bin\share\icons\hicolor\24x24\devices\phone-xperia-arc.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\256x256\devices\phone-google-nexus-one.png" bin\share\icons\hicolor\256x256\devices\phone-google-nexus-one.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\256x256\devices\phone-nook.png" bin\share\icons\hicolor\256x256\devices\phone-nook.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\256x256\devices\phone-palm-pre.png" bin\share\icons\hicolor\256x256\devices\phone-palm-pre.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\256x256\devices\phone-xperia-arc.png" bin\share\icons\hicolor\256x256\devices\phone-xperia-arc.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\32x32\devices\phone-google-nexus-one.png" bin\share\icons\hicolor\32x32\devices\phone-google-nexus-one.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\32x32\devices\phone-htc-g1-white.png" bin\share\icons\hicolor\32x32\devices\phone-htc-g1-white.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\32x32\devices\phone-nokia-n900.png" bin\share\icons\hicolor\32x32\devices\phone-nokia-n900.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\32x32\devices\phone-nook.png" bin\share\icons\hicolor\32x32\devices\phone-nook.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\32x32\devices\phone-palm-pre.png" bin\share\icons\hicolor\32x32\devices\phone-palm-pre.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\32x32\devices\phone-xperia-arc.png" bin\share\icons\hicolor\32x32\devices\phone-xperia-arc.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\48x48\devices\phone-google-nexus-one.png" bin\share\icons\hicolor\48x48\devices\phone-google-nexus-one.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\48x48\devices\phone-htc-g1-white.png" bin\share\icons\hicolor\48x48\devices\phone-htc-g1-white.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\48x48\devices\phone-nokia-n900.png" bin\share\icons\hicolor\48x48\devices\phone-nokia-n900.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\48x48\devices\phone-nook.png" bin\share\icons\hicolor\48x48\devices\phone-nook.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\48x48\devices\phone-palm-pre.png" bin\share\icons\hicolor\48x48\devices\phone-palm-pre.png
copy /Y "src\Dap\Banshee.Dap.MassStorage\ThemeIcons\48x48\devices\phone-xperia-arc.png" bin\share\icons\hicolor\48x48\devices\phone-xperia-arc.png
copy /Y "src\Extensions\Banshee.AmazonMp3.Store\ThemeIcons\16x16\categories\amazon-mp3-store-source.png" bin\share\icons\hicolor\16x16\categories\amazon-mp3-store-source.png
copy /Y "src\Extensions\Banshee.AmazonMp3.Store\ThemeIcons\22x22\categories\amazon-mp3-store-source.png" bin\share\icons\hicolor\22x22\categories\amazon-mp3-store-source.png
copy /Y "src\Extensions\Banshee.Audiobook\ThemeIcons\16x16\categories\audiobook.png" bin\share\icons\hicolor\16x16\categories\audiobook.png
copy /Y "src\Extensions\Banshee.Audiobook\ThemeIcons\22x22\categories\audiobook.png" bin\share\icons\hicolor\22x22\categories\audiobook.png
copy /Y "src\Extensions\Banshee.Audiobook\ThemeIcons\256x256\categories\audiobook.png" bin\share\icons\hicolor\256x256\categories\audiobook.png
copy /Y "src\Extensions\Banshee.Audiobook\ThemeIcons\48x48\categories\audiobook.png" bin\share\icons\hicolor\48x48\categories\audiobook.png
copy /Y "src\Extensions\Banshee.InternetArchive\ThemeIcons\16x16\categories\internet-archive.png" bin\share\icons\hicolor\16x16\categories\internet-archive.png
copy /Y "src\Extensions\Banshee.InternetArchive\ThemeIcons\22x22\categories\internet-archive.png" bin\share\icons\hicolor\22x22\categories\internet-archive.png
copy /Y "src\Extensions\Banshee.InternetRadio\ThemeIcons\16x16\categories\radio.png" bin\share\icons\hicolor\16x16\categories\radio.png
copy /Y "src\Extensions\Banshee.InternetRadio\ThemeIcons\22x22\categories\radio.png" bin\share\icons\hicolor\22x22\categories\radio.png
copy /Y "src\Extensions\Banshee.InternetRadio\ThemeIcons\48x48\categories\radio.png" bin\share\icons\hicolor\48x48\categories\radio.png
copy /Y "src\Extensions\Banshee.Lastfm\ThemeIcons\16x16\categories\lastfm-audioscrobbler.png" bin\share\icons\hicolor\16x16\categories\lastfm-audioscrobbler.png
copy /Y "src\Extensions\Banshee.Lastfm\ThemeIcons\16x16\categories\lastfm-neighbour.png" bin\share\icons\hicolor\16x16\categories\lastfm-neighbour.png
copy /Y "src\Extensions\Banshee.Lastfm\ThemeIcons\16x16\categories\lastfm-personal.png" bin\share\icons\hicolor\16x16\categories\lastfm-personal.png
copy /Y "src\Extensions\Banshee.Lastfm\ThemeIcons\16x16\categories\lastfm-recommended.png" bin\share\icons\hicolor\16x16\categories\lastfm-recommended.png
copy /Y "src\Extensions\Banshee.Lastfm\ThemeIcons\16x16\categories\lastfm-tag.png" bin\share\icons\hicolor\16x16\categories\lastfm-tag.png
copy /Y "src\Extensions\Banshee.Lastfm\ThemeIcons\22x22\categories\lastfm-audioscrobbler.png" bin\share\icons\hicolor\22x22\categories\lastfm-audioscrobbler.png
copy /Y "src\Extensions\Banshee.Lastfm\ThemeIcons\22x22\categories\lastfm-neighbour.png" bin\share\icons\hicolor\22x22\categories\lastfm-neighbour.png
copy /Y "src\Extensions\Banshee.Lastfm\ThemeIcons\22x22\categories\lastfm-personal.png" bin\share\icons\hicolor\22x22\categories\lastfm-personal.png
copy /Y "src\Extensions\Banshee.Lastfm\ThemeIcons\22x22\categories\lastfm-recommended.png" bin\share\icons\hicolor\22x22\categories\lastfm-recommended.png
copy /Y "src\Extensions\Banshee.Lastfm\ThemeIcons\22x22\categories\lastfm-tag.png" bin\share\icons\hicolor\22x22\categories\lastfm-tag.png
copy /Y "src\Extensions\Banshee.Lastfm\ThemeIcons\48x48\generic-artist.png" bin\share\icons\hicolor\48x48\generic-artist.png
copy /Y "src\Extensions\Banshee.MiroGuide\ThemeIcons\16x16\categories\miro-guide-source.png" bin\share\icons\hicolor\16x16\categories\miro-guide-source.png
copy /Y "src\Extensions\Banshee.MiroGuide\ThemeIcons\22x22\categories\miro-guide-source.png" bin\share\icons\hicolor\22x22\categories\miro-guide-source.png
copy /Y "src\Extensions\Banshee.MiroGuide\ThemeIcons\24x24\categories\miro-guide-source.png" bin\share\icons\hicolor\24x24\categories\miro-guide-source.png
copy /Y "src\Extensions\Banshee.MiroGuide\ThemeIcons\32x32\categories\miro-guide-source.png" bin\share\icons\hicolor\32x32\categories\miro-guide-source.png
copy /Y "src\Extensions\Banshee.MiroGuide\ThemeIcons\48x48\categories\miro-guide-source.png" bin\share\icons\hicolor\48x48\categories\miro-guide-source.png
copy /Y "src\Extensions\Banshee.OpticalDisc\ThemeIcons\16x16\actions\media-import-audio-cd.png" bin\share\icons\hicolor\16x16\actions\media-import-audio-cd.png
copy /Y "src\Extensions\Banshee.OpticalDisc\ThemeIcons\22x22\actions\media-import-audio-cd.png" bin\share\icons\hicolor\22x22\actions\media-import-audio-cd.png
copy /Y "src\Extensions\Banshee.Podcasting\ThemeIcons\16x16\categories\podcast.png" bin\share\icons\hicolor\16x16\categories\podcast.png
copy /Y "src\Extensions\Banshee.Podcasting\ThemeIcons\16x16\status\podcast-new.png" bin\share\icons\hicolor\16x16\status\podcast-new.png
copy /Y "src\Extensions\Banshee.Podcasting\ThemeIcons\22x22\categories\podcast.png" bin\share\icons\hicolor\22x22\categories\podcast.png
copy /Y "src\Extensions\Banshee.Podcasting\ThemeIcons\48x48\categories\podcast.png" bin\share\icons\hicolor\48x48\categories\podcast.png
copy /Y "src\Extensions\Banshee.UbuntuOneMusicStore\ThemeIcons\16x16\categories\ubuntuone.png" bin\share\icons\hicolor\16x16\categories\ubuntuone.png
copy /Y "src\Extensions\Banshee.UbuntuOneMusicStore\ThemeIcons\22x22\categories\ubuntuone.png" bin\share\icons\hicolor\22x22\categories\ubuntuone.png
copy /Y "src\Extensions\Banshee.Wikipedia\ThemeIcons\16x16\categories\wikipedia.png" bin\share\icons\hicolor\16x16\categories\wikipedia.png
copy /Y "src\Extensions\Banshee.Wikipedia\ThemeIcons\22x22\categories\wikipedia.png" bin\share\icons\hicolor\22x22\categories\wikipedia.png
copy /Y "src\Extensions\Banshee.Wikipedia\ThemeIcons\32x32\categories\wikipedia.png" bin\share\icons\hicolor\32x32\categories\wikipedia.png
copy /Y "src\Extensions\Banshee.Wikipedia\ThemeIcons\48x48\categories\wikipedia.png" bin\share\icons\hicolor\48x48\categories\wikipedia.png
copy /Y "src\Extensions\Banshee.YouTube\ThemeIcons\16x16\categories\youtube.png" bin\share\icons\hicolor\16x16\categories\youtube.png
copy /Y "src\Extensions\Banshee.YouTube\ThemeIcons\22x22\categories\youtube.png" bin\share\icons\hicolor\22x22\categories\youtube.png
copy /Y "src\Extensions\Banshee.YouTube\ThemeIcons\24x24\categories\youtube.png" bin\share\icons\hicolor\24x24\categories\youtube.png
mkdir bin\share\icons\hicolor\16x16\apps
mkdir bin\share\icons\hicolor\192x192\apps
mkdir bin\share\icons\hicolor\22x22\apps
mkdir bin\share\icons\hicolor\24x24\apps
mkdir bin\share\icons\hicolor\32x32\apps
mkdir bin\share\icons\hicolor\48x48\apps
copy /Y data\icon-theme-hicolor\media-player-banshee-16.png bin\share\icons\hicolor\16x16\apps\media-player-banshee.png
copy /Y data\icon-theme-hicolor\media-player-banshee-192.png bin\share\icons\hicolor\192x192\apps\media-player-banshee.png
copy /Y data\icon-theme-hicolor\media-player-banshee-22.png bin\share\icons\hicolor\22x22\apps\media-player-banshee.png
copy /Y data\icon-theme-hicolor\media-player-banshee-24.png bin\share\icons\hicolor\24x24\apps\media-player-banshee.png
copy /Y data\icon-theme-hicolor\media-player-banshee-32.png bin\share\icons\hicolor\32x32\apps\media-player-banshee.png
copy /Y data\icon-theme-hicolor\media-player-banshee-48.png bin\share\icons\hicolor\48x48\apps\media-player-banshee.png
