/***************************************************************************
 *  ActionManager.cs
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@abock.org>
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */
 
using System;
using System.Collections;
using Gtk;
using Mono.Unix;

namespace Banshee.Base
{
    public class ActionManager : IEnumerable
    {
        private UIManager ui = new UIManager();
        
        private ActionGroup global_actions = new ActionGroup("Global");
        private ActionGroup playlist_actions = new ActionGroup("Playlist");
        private ActionGroup song_actions = new ActionGroup("Song");
        private ActionGroup audio_cd_actions = new ActionGroup("AudioCD");
        private ActionGroup playback_actions = new ActionGroup("Playback");
        private ActionGroup playback_seek_actions = new ActionGroup("PlaybackSeek");
        private ActionGroup dap_actions = new ActionGroup("Dap");
        
        public ActionManager()
        {
            PopulateActionGroups();
        }
        
        public void LoadInterface()
        {
            ui.AddUiFromResource("UIManagerLayout.xml");
            Gtk.Window.DefaultIconName = "music-player-banshee";
        }
        
        private void PopulateActionGroups()
        {
            /* Global Actions */
        
            global_actions.Add(new ActionEntry [] {
                new ActionEntry("MusicMenuAction", null, 
                    Catalog.GetString("_Music"), null, null, null),
                
                new ActionEntry("NewPlaylistAction", Stock.New,
                    Catalog.GetString("_New Playlist"), "<control>N",
                    Catalog.GetString("Create a new empty playlist"), null),
                
                new ActionEntry("ImportFolderAction", Stock.Open,
                    Catalog.GetString("Import _Folder..."), "<control>O",
                    Catalog.GetString("Import the contents of an entire folder"), null),
                
                new ActionEntry("ImportFilesAction", Stock.Open,
                    Catalog.GetString("Import Files..."), null,
                    Catalog.GetString("Import files inside a folder"), null),
                    
                new ActionEntry("ImportMusicAction", Stock.Open,
                    Catalog.GetString("Import _Music..."), "<control>I",
                    Catalog.GetString("Import music from a variety of sources"), null),
                    
                new ActionEntry("OpenLocationAction", null, 
                    Catalog.GetString("Open _Location..."), "<control>L",
                    Catalog.GetString("Open a remote location for playback"), null),
                    
                new ActionEntry("WriteCDAction", null,
                    Catalog.GetString("Write CD"), null,
                    Catalog.GetString("Write selection to audio CD"), null),
                    
                new ActionEntry("ImportSourceAction", null,
                    Catalog.GetString("Import Source"), null,
                    Catalog.GetString("Import source to library"), null),
                    
                new ActionEntry("SelectedSourcePropertiesAction", Stock.Properties,
                    "Source Properties", null,
                    null, null),
                    
                new ActionEntry("ScriptsAction", null,
                    Catalog.GetString("User Scripts"), null,
                    Catalog.GetString("Run available user scripts"), null),
                    
                new ActionEntry("QuitAction", Stock.Quit,
                    Catalog.GetString("_Quit"), "<control>Q",
                    Catalog.GetString("Quit Banshee"), null),
                    
                new ActionEntry("EditMenuAction", null, 
                    Catalog.GetString("_Edit"), null, null, null),
                    
                new ActionEntry("RenameSourceAction", "gtk-edit", 
                    "Rename", "F2",
                    "Rename", null),

                new ActionEntry("UnmapSourceAction", Stock.Delete,
                    "Unmap", "<shift>Delete",
                    null, null),
                    
                new ActionEntry("SelectAllAction", null,
                    Catalog.GetString("Select _All"), "<control>A",
                    Catalog.GetString("Select all songs in song list"), null),
                    
                new ActionEntry("SelectNoneAction", null,
                    Catalog.GetString("Select _None"), "<control><shift>A",
                    Catalog.GetString("Unselect all songs in song list"), null),

                new ActionEntry("JumpToPlayingAction", null,
                    Catalog.GetString("_Jump to playing song"), "<control>J",
                    null, null),
                
                new ActionEntry("PluginsAction", null,
                    Catalog.GetString("Plu_gins..."), null,
                    Catalog.GetString("Configure Banshee plugins"), null),
                
                new ActionEntry("PreferencesAction", Stock.Preferences, null),
                
                new ActionEntry("ToolsMenuAction", null, 
                    Catalog.GetString("_Tools"), null, null, null),
                
                new ActionEntry("ViewMenuAction", null,
                    Catalog.GetString("_View"), null, null, null),

                new ActionEntry("ColumnsAction", null,
                    Catalog.GetString("_Columns..."), null,
                    Catalog.GetString("Select which columns to display in the song list"), null),
                
                new ActionEntry("ShellAction", null,
                    Catalog.GetString("_Boo Buddy..."), "<control><shift>S",
                    Catalog.GetString("Open Boo Buddy"), delegate {
                        BooBuddy.BooBuddyWindow boo_buddy = new BooBuddy.BooBuddyWindow();
                        boo_buddy.Show();
                    }),
                
                new ActionEntry("ShowEqualizerAction", null,
                    Catalog.GetString("Equalizer"), null,
                    Catalog.GetString("Display the equalizer."), null),
                    
                new ActionEntry("LoggedEventsAction", null,
                    Catalog.GetString("_Logged Events Viewer..."), null,
                    Catalog.GetString("View a detailed log of events"), null),
                    
                new ActionEntry("HelpMenuAction", null, 
                    Catalog.GetString("_Help"), null, null, null),
                    
                new ActionEntry("VersionInformationAction", null,
                    Catalog.GetString("_Version Information..."), null,
                    Catalog.GetString("View detailed version and configuration information"), null),
                    
                new ActionEntry("WebMenuAction", null,
                    Catalog.GetString("_Web Resources"), null, null, null),
                    
                new ActionEntry("WikiGuideAction", Stock.Help,
                    Catalog.GetString("Banshee _User Guide (Wiki)"), null,
                    Catalog.GetString("Learn about how to use Banshee"), delegate {
                        Banshee.Web.Browser.Open("http://banshee-project.org/Guide");
                    }),
                    
                new ActionEntry("WikiAction", null,
                    Catalog.GetString("Banshee _Home Page"), null,
                    Catalog.GetString("Visit the Banshee Home Page"), delegate {
                        Banshee.Web.Browser.Open("http://banshee-project.org/");
                    }),
                    
                new ActionEntry("WikiDeveloperAction", null,
                    Catalog.GetString("_Get Involved"), null,
                    Catalog.GetString("Become a contributor to Banshee"), delegate {
                        Banshee.Web.Browser.Open("http://banshee-project.org/Developers");
                    }),
                    
                new ActionEntry("AboutAction", "gtk-about", null),
                    
                new ActionEntry("PlaybackMenuAction", null,
                    Catalog.GetString("_Playback"), null, null, null),
                    
                new ActionEntry("SourceMenuAction", null, 
                    Catalog.GetString("Source"), null, null, null),
                    
                new ActionEntry("SongViewPopupAction", null, 
                    Catalog.GetString("Song Menu"), null, null, null),
                    
                new ActionEntry("DebugMenuAction", null,
                    Catalog.GetString("_Debug"), null, null, null),
                
                new ActionEntry("ImportPlaylistAction", null, 
                    Catalog.GetString("Import Playlist..."), null,
                    Catalog.GetString("Import a playlist"), null),

                new ActionEntry("ExportPlaylistAction", null, 
                    Catalog.GetString("Export Playlist..."), null,
                    Catalog.GetString("Export a playlist"), null)
            });
            
            global_actions.Add(new ToggleActionEntry [] {               
                new ToggleActionEntry("FullScreenAction", null,
                    Catalog.GetString("_Fullscreen"), "F11",
                    Catalog.GetString("Toggle Fullscreen Mode"), null, false),
                
                new ToggleActionEntry("ShowCoverArtAction", null,
                    Catalog.GetString("Show Cover _Art"), null,
                    Catalog.GetString("Toggle display of album cover art"), null, false),              
            });

            global_actions.GetAction("ShowEqualizerAction").Visible = false;

            ui.InsertActionGroup(global_actions, 0);

            /* Song Selected Actions */
            
            song_actions.Add(new ActionEntry [] {
                new ActionEntry("CopySongsAction", Stock.Copy,
                    Catalog.GetString("_Copy"), "<Control>C",
                    Catalog.GetString("Copy selected song(s) to clipboard"), null),
                    
                new ActionEntry("RemoveSongsAction", Stock.Remove,
                    Catalog.GetString("_Remove"), "Delete",
                    Catalog.GetString("Remove selected song(s) from library"), null),
                    
                new ActionEntry("DeleteSongsFromDriveAction", null,
                    Catalog.GetString("_Delete From Drive"), null,
                    Catalog.GetString("Permanently delete selected song(s) from storage medium"), null),
                    
                new ActionEntry("PropertiesAction", Stock.Edit,
                    Catalog.GetString("_Edit Song Metadata"), null,
                    Catalog.GetString("Edit metadata on selected songs"), null),

                new ActionEntry("SearchMenuAction", Stock.Find,
                    Catalog.GetString("_Search for Songs"), null,
                    Catalog.GetString("Search for songs matching certain criteria"), null),

                new ActionEntry("SearchForSameAlbumAction", null,
                    Catalog.GetString("By Matching _Album"), null,
                    Catalog.GetString("Search all songs of this album"), null),

                new ActionEntry("SearchForSameArtistAction", null,
                    Catalog.GetString("By Matching A_rtist"), null,
                    Catalog.GetString("Search all songs of this artist"), null),

                new ActionEntry("SearchForSameGenreAction", null,
                    Catalog.GetString("By Matching _Genre"), null,
                    Catalog.GetString("Search all songs of this genre"), null),
                    
                new ActionEntry("AddToPlaylistAction", Stock.Add,
                    Catalog.GetString("Add _to Playlist"), null,
                    Catalog.GetString("Append selected songs to playlist or create new playlist from selection"), null),
            });
            
            ui.InsertActionGroup(song_actions, 0);
            
            /* Audio CD Selected Actions */
            
            audio_cd_actions.Add(new ActionEntry [] {
                new ActionEntry("ImportCDAction", null,
                    Catalog.GetString("Import CD"), null,
                    Catalog.GetString("Import audio CD to library"), null)
            });
              
            ui.InsertActionGroup(audio_cd_actions, 0);
            
            /* Playback Actions */
            
            playback_actions.Add(new ActionEntry [] {
                new ActionEntry("PlayPauseAction", "media-playback-start",
                    Catalog.GetString("_Play"), "space",
                    Catalog.GetString("Play or pause the current song"), null),
                    
                new ActionEntry("NextAction", "media-skip-forward",
                    Catalog.GetString("_Next"), "N",
                    Catalog.GetString("Play the next song"), null),
                    
                new ActionEntry("PreviousAction", "media-skip-backward",
                    Catalog.GetString("Pre_vious"), "B",
                    Catalog.GetString("Play the previous song"), null),
            });
            
            playback_actions.Add(new RadioActionEntry [] {
                new RadioActionEntry("RepeatNoneAction", null, 
                    Catalog.GetString("Repeat N_one"), null,
                    Catalog.GetString("Do not repeat playlist"), 0),
                    
                new RadioActionEntry("RepeatAllAction", null,
                    Catalog.GetString("Repeat _All"), null,
                    Catalog.GetString("Play all songs before repeating playlist"), 1),
                    
                new RadioActionEntry("RepeatSingleAction", null,
                    Catalog.GetString("Repeat Si_ngle"), null,
                    Catalog.GetString("Repeat the current playing song"), 2)
            }, 0, null);
            
            playback_actions.Add(new ToggleActionEntry [] {
                new ToggleActionEntry("ShuffleAction", "media-playlist-shuffle",
                    Catalog.GetString("Shu_ffle"), null,
                    Catalog.GetString("Toggle between shuffle or continuous playback modes"), null, false),
                    
                new ToggleActionEntry("StopWhenFinishedAction", null,
                    Catalog.GetString("_Stop When Finished"), "<Shift>space",
                    Catalog.GetString("Stop playback after the current song finishes playing"), null, false)
            });

            ui.InsertActionGroup(playback_actions, 0);
            
            /* Playback Seeking Actions */
            
            playback_seek_actions.Add(new ActionEntry [] {
                new ActionEntry("SeekBackwardAction", "media-seek-backward",
                    Catalog.GetString("Seek _Backward"), "<control>Left",
                    Catalog.GetString("Seek backward in current song"), null),
                    
                new ActionEntry("SeekForwardAction", "media-seek-forward",
                    Catalog.GetString("Seek _Forward"), "<control>Right",
                    Catalog.GetString("Seek forward in current song"), null),
                    
                new ActionEntry("SeekToAction", null,
                    Catalog.GetString("Seek _to..."), "T",
                    Catalog.GetString("Seek to a specific location in current song"), null),
                    
                new ActionEntry("RestartSongAction", null,
                    Catalog.GetString("_Restart Song"), "R",
                    Catalog.GetString("Restart the current song"), null)
            });
            
            ui.InsertActionGroup(playback_seek_actions, 0);
            
            /* DAP Actions */
            
            dap_actions.Add(new ActionEntry [] {
                new ActionEntry("SyncDapAction", null,
                    Catalog.GetString("Synchronize"), null,
                    Catalog.GetString("Save changes to device or synchronize music library"), null)
            });
            
            ui.InsertActionGroup(dap_actions, 0);
            
            this["DebugMenuAction"].Visible = Globals.ArgumentQueue.Contains("debug");
            this["ShellAction"].Visible = Globals.ArgumentQueue.Contains("debug");
        }
        
        public Action FindActionByName(string actionName)
        {
            foreach(ActionGroup group in ui.ActionGroups) {
                foreach(Action action in group.ListActions()) {
                    if(action.Name == actionName) {
                        return action;
                    }
                }
            }
            
            return null;
        }
        
        public Action this [string widgetPathOrActionName] {
            get {
                Action action = FindActionByName(widgetPathOrActionName);
                if(action == null) {
                    return ui.GetAction(widgetPathOrActionName);
                }
                return action;
            }
        }

        public Widget GetWidget(string widgetPath)
        {
            return ui.GetWidget(widgetPath);
        }
        
        public void SetActionLabel(string actionName, string label)
        {
            this[actionName].Label = label;
            Banshee.Widgets.ActionButton.SyncButtons();
        }
        
        public void SetActionIcon(string actionName, string icon)
        {
            this[actionName].StockId = icon;
            Banshee.Widgets.ActionButton.SyncButtons();
        }
        
        public void UpdateAction(string actionName, string label, string icon)
        {
            Action action = this[actionName];
            action.Label = label;
            action.StockId = icon;
            Banshee.Widgets.ActionButton.SyncButtons();
        }

        public IEnumerator GetEnumerator()
        {
            foreach(ActionGroup group in ui.ActionGroups) {
                foreach(Action action in group.ListActions()) {
                    yield return action;
                }
            }
        }
        
        public UIManager UI {
            get { return ui; }
        }
        
        public ActionGroup GlobalActions {
            get { return global_actions; }
        }
        
        public ActionGroup PlaylistActions {
            get { return playlist_actions; }
        }
        
        public ActionGroup SongActions { 
            get { return song_actions; }
        }
        
        public ActionGroup AudioCdActions { 
            get { return audio_cd_actions; }
        }
        
        public ActionGroup PlaybackActions {
            get { return playback_actions; }
        }
        
        public ActionGroup PlaybackSeekActions {
            get { return playback_seek_actions; }
        }
        
        public ActionGroup DapActions {
            get { return dap_actions; }
        }
    }
}
