/***************************************************************************
 *  PlayerInterface.cs
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
using System.IO;
using System.Text;
using System.Threading;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Mono.Unix;
using Gtk;
using Gdk;
using Glade;

using Banshee.Widgets;
using Banshee.Base;
using Banshee.MediaEngine;
using Banshee.Dap;
using Banshee.Sources;
using Banshee.Gui;
using Banshee.Gui.DragDrop;
using Banshee.Configuration.Schema;
using Banshee.Playlists;
using Banshee.Playlists.Formats;

namespace Banshee
{
    public class PlayerUI
    {
        public static readonly uint SkipDelta = 10;
        public static readonly int VolumeDelta = 10;
        
        private Glade.XML gxml;

        [Widget] private Gtk.Window WindowPlayer;
        [Widget] private HPaned SourceSplitter;
        [Widget] private Button HeaderCycleButton;

        private PlaylistModel playlistModel;

        private Label LabelStatusBar;
        private Bacon.VolumeButton volumeButton;
        private PlaylistView playlistView;
        private SourceView sourceView;
        private ImageAnimation spinner;
        private TrackInfoHeader trackInfoHeader;
        private CoverArtView cover_art_view;
        private SearchEntry searchEntry;
        private Tooltips toolTips;
        private Hashtable playlistMenuMap;
        private Viewport sourceViewLoadingVP;
        
        private MultiStateToggleButton repeat_toggle_button;
        private MultiStateToggleButton shuffle_toggle_button;
                
        private ActionButton playpause_button;
        private ActionButton next_button;
        private ActionButton previous_button;
        private ActionButton burn_button;
        private ActionButton rip_button;
        
        private SeekSlider seek_slider;
        private StreamPositionLabel stream_position_label;
        
        private ActionButton sync_dap_button;
        [Widget] private ProgressBar dapDiskUsageBar;
        
        private bool incrementedCurrentSongPlayCount;

        private bool suspendSearch;
    
        public Gtk.Window Window {
            get {
                return WindowPlayer;
            }
        }
        
        private long plLoaderMax, plLoaderCount;
        private uint setPositionTimeoutId;
        private int clickX, clickY;

        private int dapDiskUsageTextViewState;
        
        private SpecialKeys special_keys;

        private static TargetEntry [] coverArtDestEntries = 
            new TargetEntry [] {
                Banshee.Gui.DragDrop.DragDropTarget.UriList
            };

        private static TargetEntry [] coverArtSourceEntries = 
            new TargetEntry [] {
                Banshee.Gui.DragDrop.DragDropTarget.UriList
            };
            
        private static TargetEntry [] playlistViewSourceEntries = 
            new TargetEntry [] {
                Banshee.Gui.DragDrop.DragDropTarget.PlaylistRows,
                Banshee.Gui.DragDrop.DragDropTarget.TrackInfoObjects,
                Banshee.Gui.DragDrop.DragDropTarget.UriList
            };
            
        private static TargetEntry [] playlistViewDestEntries = 
            new TargetEntry [] {
                Banshee.Gui.DragDrop.DragDropTarget.PlaylistRows,
                Banshee.Gui.DragDrop.DragDropTarget.UriList
            };
            
        private static TargetEntry [] nautilus_file_copy_entries = 
            new TargetEntry [] { 
                new TargetEntry("x-special/gnome-copied-files", 0, 0)
            };

        public PlayerUI() 
        {
            gxml = new Glade.XML(null, "banshee.glade", "WindowPlayer", null);
            gxml.Autoconnect(this);
            InterfaceElements.MainWindow = WindowPlayer;

            Globals.ActionManager.LoadInterface();
            ResizeMoveWindow();
            BuildWindow();   
            
            Globals.ShutdownRequested += OnShutdownRequested; 
            
            PlayerEngineCore.EventChanged += OnPlayerEngineEventChanged;
            PlayerEngineCore.StateChanged += OnPlayerEngineStateChanged;

            DapCore.DapAdded += OnDapCoreDeviceAdded;
            LogCore.Instance.Updated += OnLogCoreUpdated;
            
            Globals.DBusPlayer.UIAction += OnDBusPlayerUIAction;
            
            InitialLoadTimeout();
            
            // Bind available methods to actions defined in ActionManager
            Globals.ActionManager.DapActions.Visible = false;
            Globals.ActionManager.AudioCdActions.Visible = false;
            Globals.ActionManager.SongActions.Sensitive = false;
            Globals.ActionManager.PlaylistActions.Sensitive = false;
            Globals.ActionManager["JumpToPlayingAction"].Visible = false;
            
            foreach(Action action in Globals.ActionManager) {
                string method_name = "On" + action.Name;
                MethodInfo method = GetType().GetMethod(method_name,
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.InvokeMethod);
                
                EventInfo action_event = action.GetType().GetEvent("Activated");
                 
                if(method == null || action_event == null) {
                    //Console.WriteLine("No method defined for action `{0}'", action.Name);
                    continue;
                }
                
                action_event.AddEventHandler(action, 
                    Delegate.CreateDelegate(typeof(EventHandler), this, method_name));
            }
            
            WindowPlayer.AddAccelGroup(Globals.ActionManager.UI.AccelGroup);
            Globals.ActionManager.PlaybackSeekActions.Sensitive = false;
            
            LoadSettings();
            
            Globals.StartupInitializer.RunFinished += OnStartupRunFinished;
            
            Globals.UIManager.SourceViewContainer = gxml["SourceViewContainer"] as Box;
            Globals.UIManager.Initialize();
            
            if(!Globals.ArgumentQueue.Contains("hide")) {
                WindowPlayer.Show();
            }
        }
   
        private bool InitialLoadTimeout()
        {
            if(Globals.Library.IsLoaded) {
                OnLibraryReloaded(Globals.Library, new EventArgs());
            } else {
                Globals.Library.Reloaded += OnLibraryReloaded;
            }
            
            foreach(DapDevice device in DapCore.Devices) {
                 device.PropertiesChanged += OnDapPropertiesChanged;
                 device.SaveFinished += OnDapSaveFinished;
            }
            
            return false;
        }
          
        // ---- Setup/Initialization Routines ----
          
        private void ResizeMoveWindow()
        {
            int x = PlayerWindowSchema.XPos.Get();
            int y = PlayerWindowSchema.YPos.Get(); 
            int width = PlayerWindowSchema.Width.Get();
            int height = PlayerWindowSchema.Height.Get();
           
            if(width != 0 && height != 0) {
                WindowPlayer.Resize(width, height);
            }

            if(x == 0 && y == 0) {
                WindowPlayer.SetPosition(Gtk.WindowPosition.Center);
            } else {
                WindowPlayer.Move(x, y);
            }
            
            if(PlayerWindowSchema.Maximized.Get()) {
                WindowPlayer.Maximize();
            } else {
                WindowPlayer.Unmaximize();
            }
        }
        
        private void OnDBusPlayerUIAction(object o, DBusPlayer.UICommandArgs args)
        {
            switch(args.Command) {
                case DBusPlayer.UICommand.HideWindow:
                    WindowPlayer.Hide();
                    break;
                case DBusPlayer.UICommand.ShowWindow:
                    WindowPlayer.Show();
                    break;
                case DBusPlayer.UICommand.PresentWindow:
                    WindowPlayer.Present();
                    break;
            }
        }
        
        private void OnWindowStateEvent(object o, WindowStateEventArgs args)
        {
            if((args.Event.NewWindowState & Gdk.WindowState.Withdrawn) == 0) {
                PlayerWindowSchema.Maximized.Set((args.Event.NewWindowState & Gdk.WindowState.Maximized) != 0);
            }
        }
          
        private void BuildWindow()
        {
            IconThemeUtils.SetWindowIcon(WindowPlayer);
            WindowPlayer.Title = Branding.ApplicationLongName;
            
            // Main Menu
            (gxml["MainMenuContainer"] as Container).Add(Globals.ActionManager.GetWidget("/MainMenu"));
      
            // Seek Slider/Position Label
            seek_slider = new SeekSlider();
            seek_slider.SetSizeRequest(125, -1);
            seek_slider.SeekRequested += OnSeekRequested;
            
            stream_position_label = new StreamPositionLabel(seek_slider);
            
            (gxml["SeekContainer"] as Box).PackStart(seek_slider, false, false, 0);
            (gxml["SeekContainer"] as Box).PackStart(stream_position_label, false, false, 0);
            gxml["SeekContainer"].ShowAll();
      
            // Playback Buttons
            HBox playback_box = gxml["LeftToolbarContainer"] as HBox;
            
            previous_button = new ActionButton(Globals.ActionManager["PreviousAction"]);
            previous_button.LabelVisible = false;
            previous_button.Padding = 1;
            previous_button.ButtonPressEvent += OnButtonPreviousPressed;
            
            next_button = new ActionButton(Globals.ActionManager["NextAction"]);
            next_button.LabelVisible = false;
            next_button.Padding = 1;
            
            playpause_button = new ActionButton(Globals.ActionManager["PlayPauseAction"]);
            playpause_button.LabelVisible = false;
            playpause_button.Padding = 1;
            
            playback_box.PackStart(previous_button, false, false, 0);
            playback_box.PackStart(playpause_button, false, false, 0);
            playback_box.PackStart(next_button, false, false, 0);
      
            trackInfoHeader = new TrackInfoHeader();
            trackInfoHeader.Cover.DragDataReceived += OnCoverArtDragDataReceived;
            Gtk.Drag.DestSet(trackInfoHeader.Cover, DestDefaults.All, coverArtDestEntries, DragAction.Copy | DragAction.Move);
            trackInfoHeader.Cover.DragDataGet += OnCoverArtDragDataGet;
            Gtk.Drag.SourceSet(trackInfoHeader.Cover, Gdk.ModifierType.Button1Mask, coverArtSourceEntries, DragAction.Copy);
            trackInfoHeader.Show();
            ((HBox)gxml["HeaderBox"]).PackStart(trackInfoHeader, true, true, 0);
            
            // Burn Button
            burn_button = new ActionButton(Globals.ActionManager["WriteCDAction"]);
            burn_button.Pixbuf = Gdk.Pixbuf.LoadFromResource("cd-action-burn-24.png");
            (gxml["RightToolbarContainer"] as Box).PackStart(burn_button, false, false, 0);
                        
            // Rip Button
            rip_button = new ActionButton(Globals.ActionManager["ImportCDAction"]);
            rip_button.Pixbuf = Gdk.Pixbuf.LoadFromResource("cd-action-rip-24.png");
            (gxml["RightToolbarContainer"] as Box).PackStart(rip_button, false, false, 0);
            
            sync_dap_button = new ActionButton(Globals.ActionManager["SyncDapAction"]);
            (gxml["RightToolbarContainer"] as Box).PackStart(sync_dap_button, false, false, 0);
            
            // Volume Button
            volumeButton = new Bacon.VolumeButton();
            (gxml["RightToolbarContainer"] as Box).PackStart(volumeButton, false, false, 0);
            volumeButton.Show();
            volumeButton.VolumeChanged += OnVolumeScaleChanged;
            
            Gtk.HBox additional_action_buttons_box = new HBox ();
            additional_action_buttons_box.Spacing = 6;
            additional_action_buttons_box.Show ();
            
            (gxml["RightToolbarContainer"] as Box).PackStart(
                additional_action_buttons_box, false, false, 0);
            (gxml["RightToolbarContainer"] as Box).ReorderChild (additional_action_buttons_box, 0);
            
            InterfaceElements.ActionButtonBox = additional_action_buttons_box;
            
            // Footer 
            LabelStatusBar = new Label(Branding.ApplicationLongName);
            LabelStatusBar.Show();
            
            // Old Shuffle Button
/*            ActionToggleButton shuffle_button = new ActionToggleButton(
                Globals.ActionManager["ShuffleAction"], IconSize.Menu);
            shuffle_button.IconSize = IconSize.Menu;
            shuffle_button.Relief = ReliefStyle.None;
            shuffle_button.ShowAll();*/

            // Repeat/Shuffle buttons
            
            shuffle_toggle_button = new MultiStateToggleButton();
            shuffle_toggle_button.AddState(typeof(Banshee.Gui.ShuffleDisabledToggleState),
                    Globals.ActionManager["ShuffleAction"] as ToggleAction);
            shuffle_toggle_button.AddState(typeof(Banshee.Gui.ShuffleEnabledToggleState),
                    Globals.ActionManager["ShuffleAction"] as ToggleAction);
            shuffle_toggle_button.Relief = ReliefStyle.None;
            shuffle_toggle_button.ShowLabel = false;
            shuffle_toggle_button.ShowAll();
            
            repeat_toggle_button = new MultiStateToggleButton();
            repeat_toggle_button.AddState(typeof(Banshee.Gui.RepeatNoneToggleState),
                Globals.ActionManager["RepeatNoneAction"] as ToggleAction);
            repeat_toggle_button.AddState(typeof(Banshee.Gui.RepeatAllToggleState),
                Globals.ActionManager["RepeatAllAction"] as ToggleAction);
            repeat_toggle_button.AddState(typeof(Banshee.Gui.RepeatSingleToggleState),
                Globals.ActionManager["RepeatSingleAction"] as ToggleAction);
            repeat_toggle_button.Relief = ReliefStyle.None;
            repeat_toggle_button.ShowLabel = false;
            repeat_toggle_button.ShowAll();
            
            ActionButton song_properties_button = new ActionButton(
                Globals.ActionManager["PropertiesAction"], IconSize.Menu);
            song_properties_button.IconSize = IconSize.Menu;
            song_properties_button.Relief = ReliefStyle.None;
            song_properties_button.LabelVisible = false;
            song_properties_button.ShowAll();
            
            (gxml["BottomToolbar"] as Box).PackStart(shuffle_toggle_button, false, false, 0);
            (gxml["BottomToolbar"] as Box).PackStart(repeat_toggle_button, false, false, 0);
            (gxml["BottomToolbar"] as Box).PackStart(LabelStatusBar, true, true, 0);
            (gxml["BottomToolbar"] as Box).PackStart(song_properties_button, false, false, 0);
            
            // Cover Art View
            cover_art_view = new CoverArtView();
            cover_art_view.Hide();
            cover_art_view.DragDataReceived += OnCoverArtDragDataReceived;
            Gtk.Drag.DestSet(cover_art_view, DestDefaults.All, coverArtDestEntries, DragAction.Copy | DragAction.Move);
            cover_art_view.DragDataGet += OnCoverArtDragDataGet;
            Gtk.Drag.SourceSet(cover_art_view, Gdk.ModifierType.Button1Mask, coverArtSourceEntries, DragAction.Copy);
            (gxml["LeftContainer"] as Box).PackStart(cover_art_view, false, false, 0);
            
            // Source View
            sourceView = new SourceView();
            sourceView.SourceDoubleClicked += delegate {
                playlistModel.PlayingIter = TreeIter.Zero;
                playlistModel.Advance();
                playlistView.UpdateView();
            };
            
            sourceView.Sensitive = true;
            sourceView.Show();
            
            SourceManager.ActiveSourceChanged += OnSourceManagerActiveSourceChanged;
            SourceManager.SourceUpdated += OnSourceManagerSourceUpdated;
            SourceManager.SourceViewChanged += OnSourceManagerSourceViewChanged;
            SourceManager.SourceTrackAdded += OnSourceTrackAdded;
            SourceManager.SourceTrackRemoved += OnSourceTrackRemoved;
            
            ((Gtk.ScrolledWindow)gxml["SourceContainer"]).Add(sourceView);

            InterfaceElements.MainContainer = gxml["MainContainer"] as VBox;

            // Playlist View
            EventBox evbox = (Gtk.EventBox)gxml["playlist_header_event_box"];
            Widget evchild = evbox.Child;
            evbox.Remove(evchild);
            FadingAlignment fadalign = new FadingAlignment();
            fadalign.BottomPadding = 4;
            fadalign.TopPadding = 5;
            fadalign.RightPadding = 5;
            fadalign.LeftPadding = 6;
            fadalign.Add(evchild);
            fadalign.Show();
            evbox.Add(fadalign);
             
            playlistModel = new PlaylistModel();
            playlistView = new PlaylistView(playlistModel);
            InterfaceElements.PlaylistView = playlistView;
            ((Gtk.ScrolledWindow)gxml["LibraryContainer"]).Add(playlistView);
            InterfaceElements.PlaylistContainer = gxml["LibraryContainer"] as Container;
            playlistView.Show();
            playlistModel.Updated += OnPlaylistUpdated;
            playlistModel.Stopped += OnPlaylistStopped;
            playlistView.KeyPressEvent += OnPlaylistViewKeyPressEvent;
            playlistView.PopupMenu += OnPlaylistViewPopupMenu;
            playlistView.ButtonPressEvent += OnPlaylistViewButtonPressEvent;
            playlistView.MotionNotifyEvent += OnPlaylistViewMotionNotifyEvent;
            playlistView.ButtonReleaseEvent += OnPlaylistViewButtonReleaseEvent;
            playlistView.DragDataReceived += OnPlaylistViewDragDataReceived;
            playlistView.DragDataGet += OnPlaylistViewDragDataGet;
            playlistView.DragDrop += OnPlaylistViewDragDrop;
            playlistView.Selection.Changed += OnPlaylistViewSelectionChanged;
            playlistView.Vadjustment.Changed += OnPlaylistViewVadjustmentChanged;
                
            playlistView.EnableModelDragSource(
                Gdk.ModifierType.Button1Mask | Gdk.ModifierType.Button3Mask,
                playlistViewSourceEntries, 
                DragAction.Copy | DragAction.Move);
        
            playlistView.EnableModelDragDest( 
                playlistViewDestEntries, 
                DragAction.Copy | DragAction.Move);
            
            (gxml["LeftContainer"] as VBox).PackStart(new ActiveUserEventsManager(), false, false, 0);

            // Window Events
            WindowPlayer.KeyPressEvent += OnKeyPressEvent;
            WindowPlayer.ConfigureEvent += OnWindowPlayerConfigureEvent;
            Window.DeleteEvent += OnWindowPlayerDeleteEvent;
            WindowPlayer.WindowStateEvent += OnWindowStateEvent;
            
            searchEntry = new SearchEntry();
            searchEntry.Changed += OnSimpleSearch;
            searchEntry.FilterChanged += OnSimpleSearchFilterChange;
            searchEntry.SetSizeRequest(200, -1);
            searchEntry.AddFilterOption((int)TrackFilterType.None, Catalog.GetString("All Columns"));
            searchEntry.AddFilterSeparator();
            searchEntry.AddFilterOption((int)TrackFilterType.SongName, Catalog.GetString("Song Name"));
            searchEntry.AddFilterOption((int)TrackFilterType.ArtistName, Catalog.GetString("Artist Name"));
            searchEntry.AddFilterOption((int)TrackFilterType.AlbumTitle, Catalog.GetString("Album Title"));
            searchEntry.AddFilterOption((int)TrackFilterType.Genre, Catalog.GetString("Genre"));
            searchEntry.AddFilterOption((int)TrackFilterType.Year, Catalog.GetString("Year"));
            
            searchEntry.Activated += delegate {
                if(!SourceManager.ActiveSource.HandlesSearch) {
                    if(playlistView.Selection.CountSelectedRows() == 0 && playlistModel.Count() > 0) {
                        playlistView.Selection.SelectPath(new TreePath(new int [] { 0 }));
                    }
                    playlistView.HasFocus = true;
                }
            };
            searchEntry.Show();
            gxml["SearchLabel"].Sensitive = true;
            searchEntry.Sensitive = true;
            InterfaceElements.SearchEntry = searchEntry;
            ((HBox)gxml["PlaylistHeaderBox"]).PackStart(searchEntry, false, false, 0);
                
            gxml["SearchLabel"].Sensitive = false;
            searchEntry.Sensitive = false;
                
            toolTips = new Tooltips();
            
            SetTip(burn_button, Catalog.GetString("Write selection to CD"));
            SetTip(rip_button, Catalog.GetString("Import CD into library"));
            SetTip(previous_button, Catalog.GetString("Play previous song"));
            SetTip(playpause_button, Catalog.GetString("Play/pause current song"));
            SetTip(next_button, Catalog.GetString("Play next song"));
            SetTip(dapDiskUsageBar, Catalog.GetString("Device disk usage"));
            SetTip(sync_dap_button, Catalog.GetString("Synchronize music library to device"));
            SetTip(volumeButton, Catalog.GetString("Adjust volume"));
            SetTip(repeat_toggle_button, Catalog.GetString("Change repeat playback mode"));
            SetTip(shuffle_toggle_button, Catalog.GetString("Toggle shuffle playback mode"));
            SetTip(song_properties_button, Catalog.GetString("Edit and view metadata of selected songs"));
            
            playlistMenuMap = new Hashtable();
            
            playlistView.HasFocus = true;
        }
        
        private void SetTip(Widget widget, string tip)
        {
            toolTips.SetTip(widget, tip, tip);
        }
          
        private void LoadSettings()
        {  
            volumeButton.Volume = PlayerEngineCore.VolumeSchema.Get();
            PlayerEngineCore.Volume = (ushort)volumeButton.Volume;
            
            try {
                int state = (int)PlayerWindowSchema.PlaybackRepeat.Get();
                
                foreach(RadioAction radio in (Globals.ActionManager["RepeatAllAction"] as RadioAction).Group) {
                    if(radio.Value == state) {
                        radio.Active = true;
                        break;
                    }
                }
            } catch {
            }
            
            try {
                (Globals.ActionManager["ShuffleAction"] as ToggleAction).Active 
                    = PlayerWindowSchema.PlaybackShuffle.Get();
            } catch {
            }
            
            cover_art_view.Enabled = PlayerWindowSchema.ShowCoverArt.Get();
            (Globals.ActionManager["ShowCoverArtAction"] as ToggleAction).Active = cover_art_view.Enabled; 
            SourceSplitter.Position = PlayerWindowSchema.SourceViewWidth.Get();
        }
        
        private void PromptForImport()
        {
            PromptForImport(true);
        }
        
        private void PromptForImport(bool startup)
        {
            if(startup && !ImportSchema.ShowInitialImportDialog.Get()) {
                return;
            }
            
            Banshee.Gui.ImportDialog dialog = new Banshee.Gui.ImportDialog(startup);
            ResponseType response = dialog.Run();
            IImportSource import_source = dialog.ActiveSource;
            
            if(startup) {
                ImportSchema.ShowInitialImportDialog.Set(!dialog.DoNotShowAgain);
            }
            
            dialog.Destroy();
            
            if(response != ResponseType.Ok) {
                return;
            }
            
            if(import_source != null) {
                import_source.Import();
            }
        }

        private void OnLibraryReloaded(object o, EventArgs args)
        {
            if(Globals.Library.Tracks.Count <= 0) {
                Application.Invoke(delegate { 
                    PromptForImport();
                });
            }
        }
        
        private void OnStartupRunFinished(object o, EventArgs args)
        {
            if(LocalQueueSource.Instance.Count > 0) {
                SourceManager.SetActiveSource(LocalQueueSource.Instance);
            } else if(Globals.ArgumentQueue.Contains("audio-cd")) {
                Globals.DBusPlayer.SelectAudioCd(Globals.ArgumentQueue.Dequeue("audio-cd"));
            } else if(Globals.ArgumentQueue.Contains("dap")) {
                Globals.DBusPlayer.SelectDap(Globals.ArgumentQueue.Dequeue("dap"));
            } else {
                SourceManager.SetActiveSource(LibrarySource.Instance);
            }

            if(Globals.ArgumentQueue.Contains("play") || 
                (Globals.ArgumentQueue.Contains("play-enqueued") && LocalQueueSource.Instance.Count > 0)) {
                GLib.Timeout.Add(1500, delegate {
                    PlayPause();
                    return false;
                });
            }
            
            GLib.Timeout.Add(1500, delegate {
                foreach(LogEntry entry in LogCore.Instance) {
                    ShowLogCoreEntry(entry);
                }
                return false;
            });
        } 

        // ---- Misc. Utility Routines ----
      
        private bool OnShutdownRequested()
        {
            WindowPlayer.Hide();
            ActiveUserEventsManager.Instance.CancelAll();
            playlistView.SaveColumns();
            PlayerEngineCore.Dispose();
            PlayerWindowSchema.SourceViewWidth.Set(SourceSplitter.Position);
            Application.Quit();
            return true;
        }
      
        private void Quit()
        {
            Globals.Shutdown();
        }

        public void UpdateMetaDisplay()
        {
            TrackInfo track = PlayerEngineCore.CurrentTrack;
            
            if(track == null) {
                WindowPlayer.Title = Branding.ApplicationLongName;
                trackInfoHeader.Visible = false;
                cover_art_view.FileName = null;
                return;
            }
        
            trackInfoHeader.Artist = track.DisplayArtist;
            trackInfoHeader.Title = track.DisplayTitle;
            trackInfoHeader.Album = track.Album;
            trackInfoHeader.MoreInfoUri = track.MoreInfoUri != null ? track.MoreInfoUri.AbsoluteUri : null;
            
            trackInfoHeader.Visible = true;
            
            WindowPlayer.Title = track.DisplayTitle + ((track.Artist != null && track.Artist != String.Empty) 
                ? (" (" + track.DisplayArtist + ")")
                : "");
            
            try {
                trackInfoHeader.Cover.FileName = track.CoverArtFileName;
                cover_art_view.FileName = track.CoverArtFileName;				
                trackInfoHeader.Cover.Label = String.Format("{0} - {1}", track.DisplayArtist, track.DisplayAlbum);
            } catch(Exception) {
            }
            
        }

        private void OnCoverArtDragDataReceived (object o, DragDataReceivedArgs args)
        {
            TrackInfo track = PlayerEngineCore.CurrentTrack;
            bool success = false;
            
            try {
                string data = DragDropUtilities.SelectionDataToString(args.SelectionData);
                string [] uri_list = DragDropUtilities.SplitSelectionData(data);
                SafeUri uri = new SafeUri(uri_list[0]);
                string extension = Path.GetExtension(uri.ToString()).ToLower().Substring(1);

                if (Array.IndexOf(TrackInfo.CoverExtensions, extension) != -1) {
                    // Remove the old cover art to avoid having multiple coverart in different formats
                    string old_filename = track.CoverArtFileName;
                    if (old_filename != null && old_filename.Contains(Paths.CoverArtDirectory)) {
                        File.Delete(old_filename);
                    }

                    string album_artist_id = TrackInfo.CreateArtistAlbumID(track.Artist, track.Album, false);
                    string coverFileName = Paths.CoverArtDirectory + album_artist_id + "." + extension;
                    track.CoverArtFileName = coverFileName;
                    track.Save();
               
                    File.Copy(uri.LocalPath, coverFileName, true); 
                    
                    success = true;
                    UpdateMetaDisplay();
                }
            } catch {
            }
            
            Gtk.Drag.Finish (args.Context, success, false, args.Time);
        }

        private void OnCoverArtDragDataGet (object o, DragDataGetArgs args)
        {
            try {
                TrackInfo track = PlayerEngineCore.CurrentTrack;
                string filename = track.CoverArtFileName;
                if (filename == null)
                    return;

                byte [] selection_data = System.Text.Encoding.UTF8.GetBytes(new UriList(new string[] {filename}).ToString());
                args.SelectionData.Set(args.Context.Targets[0], 8, selection_data, selection_data.Length);
            } catch {
            }
        }

        // ---- Window Event Handlers ----
        
        private void OnWindowPlayerDeleteEvent(object o, DeleteEventArgs args) 
        {
            if(InterfaceElements.PrimaryWindowClose != null) {
                if(InterfaceElements.PrimaryWindowClose()) {
                    args.RetVal = true;
                    return;
                }
            }
            
            Quit();
            args.RetVal = true;
        }
        
        [GLib.ConnectBefore]
        private void OnWindowPlayerConfigureEvent(object o, ConfigureEventArgs args)
        {
            int x, y, width, height;

            if((WindowPlayer.GdkWindow.State & Gdk.WindowState.Maximized) != 0) {
                return;
            }
            
            WindowPlayer.GetPosition(out x, out y);
            WindowPlayer.GetSize(out width, out height);
           
            PlayerWindowSchema.XPos.Set(x);
            PlayerWindowSchema.YPos.Set(y);
            PlayerWindowSchema.Width.Set(width);
            PlayerWindowSchema.Height.Set(height);
        }
        
        private static Gdk.ModifierType [] important_modifiers = new Gdk.ModifierType [] {
            Gdk.ModifierType.ControlMask,
            Gdk.ModifierType.ShiftMask
        };
            
        private static bool NoImportantModifiersAreSet()
        {
            Gdk.ModifierType state = (Gtk.Global.CurrentEvent as Gdk.EventKey).State;
            foreach(Gdk.ModifierType modifier in important_modifiers) {
                if((state & modifier) == modifier) {
                    return false;
                }
            }
            return true;
        }
        
        private bool accel_group_active = true;
        
        [GLib.ConnectBefore]
        private void OnKeyPressEvent(object o, KeyPressEventArgs args)
        {
            bool handled = false;
            
            if(WindowPlayer.Focus is Entry && Gtk.Global.CurrentEvent is Gdk.EventKey &&
                NoImportantModifiersAreSet()) {
                if(accel_group_active) {
                    WindowPlayer.RemoveAccelGroup(Globals.ActionManager.UI.AccelGroup);
                    accel_group_active = false;
                 }
            } else {
                if(!accel_group_active) {
                    WindowPlayer.AddAccelGroup(Globals.ActionManager.UI.AccelGroup);
                    accel_group_active = true;
                }
            }
            
            bool focus_search = false;
            switch(args.Event.Key) {
                case Gdk.Key.f:
                    if(ModifierType.ControlMask == (args.Event.State & ModifierType.ControlMask))
                        focus_search = true;
                    break;

                case Gdk.Key.slash:
                case Gdk.Key.J:
                case Gdk.Key.j:
                case Gdk.Key.S:
                case Gdk.Key.s:
                case Gdk.Key.F3:
                    focus_search = true;
                    break;
            }

            if(focus_search && !searchEntry.HasFocus && !sourceView.EditingRow) {
                searchEntry.HasFocus = true;
                handled = true;
            }
            
            args.RetVal = handled;
        }
        
        [GLib.ConnectBefore]
        private void OnPlaylistViewKeyPressEvent(object o, KeyPressEventArgs args)
        {
            switch(args.Event.Key) {
                case Gdk.Key.Return:
                    playlistView.PlaySelected();
                    args.RetVal = true;
                    break;
            }
        }       

        [GLib.ConnectBefore]
        private void OnPlaylistViewPopupMenu(object o, PopupMenuArgs args)
        {
            PlaylistMenuPopupTimeout(Gtk.Global.CurrentEventTime);
            args.RetVal = true;
        }
              
        // ---- Playback Event Handlers ----
        
        public void TogglePlaying()
        {
            if(PlayerEngineCore.CurrentState == PlayerEngineState.Playing) {
                PlayerEngineCore.Pause();
            } else {
                PlayerEngineCore.Play();
            }
        }
        
        public void PlayPause()
        {
            if(PlayerEngineCore.CurrentState != PlayerEngineState.Idle) {
                TogglePlaying();
            } else {
                if(SourceManager.ActiveSource.HandlesStartPlayback) {
                    SourceManager.ActiveSource.StartPlayback();
                    return;
                }
                
                if(!playlistView.PlaySelected()) {
                    playlistModel.Advance();
                    playlistView.UpdateView();
                }
            }
        }
        
        public void Previous()
        {
            playlistModel.Regress();
            playlistView.UpdateView();
        }
        
        public void Next()
        {
            playlistModel.Advance();
            playlistView.UpdateView();
        }
         
        private void OnVolumeScaleChanged(int volume)
        {
            PlayerEngineCore.Volume = (ushort)volume;
            PlayerEngineCore.VolumeSchema.Set(volume);
        }
        
        private void OnSeekRequested(object o, EventArgs args)
        {
            PlayerEngineCore.Position = (uint)seek_slider.Value;
        }
   
        // ---- Player Event Handlers ----
                
        private void SetPlayButton()
        {
            if(PlayerEngineCore.CanPause) {
                Globals.ActionManager.UpdateAction("PlayPauseAction", Catalog.GetString("Pause"), "media-playback-pause");
            } else {
                Globals.ActionManager.UpdateAction("PlayPauseAction", Catalog.GetString("Stop"), "media-playback-stop");
            }
        }
        
        private void OnPlayerEngineStateChanged(object o, PlayerEngineStateArgs args)
        {
            switch(args.State) {
                case PlayerEngineState.Contacting:
                    stream_position_label.IsContacting = true;
                    seek_slider.SetIdle();
                    SetPlayButton();
                    break;
                case PlayerEngineState.Loaded:
                    incrementedCurrentSongPlayCount = false;
                    seek_slider.Duration = PlayerEngineCore.CurrentTrack.Duration.TotalSeconds;
                    Globals.ActionManager["JumpToPlayingAction"].Visible = true;
                    UpdateMetaDisplay();
                    playlistView.QueueDraw();
                    
                    if(!PlayerEngineCore.CurrentTrack.CanPlay) {
                        LogCore.Instance.PushWarning(
                            Catalog.GetString("Cannot Play Song"), 
                            String.Format(Catalog.GetString("{0} cannot be played by Banshee. " +
                                "The most common reasons for this are:\n\n" +
                                "  <big>\u2022</big> Song is protected (DRM)\n" +
                                "  <big>\u2022</big> Song is on a DAP that does not support playback\n"),
                                PlayerEngineCore.CurrentTrack.Title));
                    }
                    
                    break;
                case PlayerEngineState.Idle:
                    Globals.ActionManager.UpdateAction("PlayPauseAction", Catalog.GetString("Play"), "media-playback-start");
                    Globals.ActionManager["JumpToPlayingAction"].Visible = false;
                    seek_slider.SetIdle();
                    trackInfoHeader.SetIdle();
                    stream_position_label.IsContacting = false;
                    UpdateMetaDisplay();
                    
                    break;
                case PlayerEngineState.Paused:
                    Globals.ActionManager.UpdateAction("PlayPauseAction", Catalog.GetString("Play"), "media-playback-start");
                    break;
                case PlayerEngineState.Playing:
                    SetPlayButton();
                    break;
            }
            
            Globals.ActionManager.PlaybackSeekActions.Sensitive = args.State != PlayerEngineState.Idle;
        }
        
        private void OnPlayerEngineEventChanged(object o, PlayerEngineEventArgs args)
        {
            switch(args.Event) {
                case PlayerEngineEvent.Iterate:
                    OnPlayerEngineTick();
                    break;
                case PlayerEngineEvent.EndOfStream:
                    ToggleAction action = Globals.ActionManager["StopWhenFinishedAction"] as ToggleAction;
                    
                    if(!action.Active) {
                        playlistModel.Continue();
                    } else {
                        OnPlaylistStopped(null, null);
                    }
                    
                    playlistView.UpdateView();
                    action.Active = false;
                    
                    break;
                case PlayerEngineEvent.StartOfStream:
                    //seek_slider.CanSeek = PlayerEngineCore.CanSeek;
                    seek_slider.CanSeek = true;
                    break;
                case PlayerEngineEvent.Volume:
                    volumeButton.Volume = PlayerEngineCore.Volume;
                    break;
                case PlayerEngineEvent.Buffering:
                    if(args.BufferingPercent >= 1.0) {
                        stream_position_label.IsBuffering = false;
                        break;
                    }
                    
                    stream_position_label.IsBuffering = true;
                    stream_position_label.BufferingProgress = args.BufferingPercent;
                    seek_slider.SetIdle();
                    break;
                case PlayerEngineEvent.Error:
                    RepeatMode old_repeat_mode = playlistModel.Repeat;
                    bool old_shuffle_mode = playlistModel.Shuffle;
                    
                    playlistModel.Repeat = RepeatMode.ErrorHalt;
                    playlistModel.Shuffle = false;
                    
                    Next();
                    
                    playlistModel.Repeat = old_repeat_mode;
                    playlistModel.Shuffle = old_shuffle_mode;
                    break;
                case PlayerEngineEvent.TrackInfoUpdated:
                    UpdateMetaDisplay();
                    playlistView.QueueDraw();
                    break;
            }
        }

        private void OnPlayerEngineTick()
        {
            uint stream_length = PlayerEngineCore.Length;
            uint stream_position = PlayerEngineCore.Position;
            
            stream_position_label.IsContacting = false;
            seek_slider.CanSeek = PlayerEngineCore.CanSeek;
            seek_slider.Duration = stream_length;
            seek_slider.SeekValue = stream_position;
            
            if(PlayerEngineCore.CurrentTrack == null) {
                return;
            }
           
            if(stream_length > 0 && PlayerEngineCore.CurrentTrack.Duration.TotalSeconds != (double)stream_length) {
                PlayerEngineCore.CurrentTrack.Duration = new TimeSpan(stream_length * TimeSpan.TicksPerSecond);
                PlayerEngineCore.CurrentTrack.Save();
                playlistView.QueueDraw();
            }
            
            if(stream_length > 0 && stream_position > stream_length / 2 && !incrementedCurrentSongPlayCount) {
                PlayerEngineCore.CurrentTrack.IncrementPlayCount();
                incrementedCurrentSongPlayCount = true;
                playlistView.QueueDraw();
            }
        }
        
        // ---- Playlist Event Handlers ----
        
        private void OnPlaylistObjectUpdated(object o, EventArgs args)
        {
            sourceView.ThreadedQueueDraw();
        }

        private void OnPlaylistViewSelectionChanged(object o, EventArgs args)
        {
            int count = playlistView.Selection.CountSelectedRows();
            bool have_selection = count > 0;
            Source source = SourceManager.ActiveSource;

            if(!have_selection) {
                Globals.ActionManager.SongActions.Sensitive = false;
                Globals.ActionManager["WriteCDAction"].Sensitive = source is Banshee.Burner.BurnerSource;
                return;
            } else if(source == null) {
                return;
            }
            
            Globals.ActionManager.SongActions.Sensitive = true;
            Globals.ActionManager["WriteCDAction"].Sensitive = !(source is AudioCdSource);
            Globals.ActionManager["RemoveSongsAction"].Sensitive = !(source is AudioCdSource || source is Banshee.SmartPlaylist.SmartPlaylistSource);
            Globals.ActionManager["DeleteSongsFromDriveAction"].Sensitive = 
                !(source is AudioCdSource || source is DapSource);
            Globals.ActionManager["DeleteSongsFromDriveAction"].Visible =
                !(source is PlaylistSource);
        }
        
        private void OnPlaylistViewVadjustmentChanged(object o, EventArgs args)
        {
            double max_offset = playlistView.Vadjustment.Upper - playlistView.Vadjustment.PageSize;
            
            if(playlistView.Vadjustment.Value > max_offset) {
                playlistView.Vadjustment.Value = max_offset;
            }
        }
        
        private void OnSourceManagerActiveSourceChanged(SourceEventArgs args)
        {
            ThreadAssist.ProxyToMain(HandleSourceChanged);
        }
        
        private uint source_update_draw_timeout = 0;
        
        private void OnSourceManagerSourceUpdated(SourceEventArgs args)
        {
            if(args.Source == SourceManager.ActiveSource) {
                UpdateViewName(args.Source);
                
                if(playlistModel.Count() == 0 && args.Source.Count > 0
                   && !searchEntry.IsQueryAvailable) {
                    playlistModel.ReloadSource();
                } else if(source_update_draw_timeout == 0) {
                    source_update_draw_timeout = GLib.Timeout.Add(500, delegate {
                        playlistView.QueueDraw();
                        source_update_draw_timeout = 0;
                        return false;
                    });
                }
                
                gxml["SearchLabel"].Sensitive = args.Source.SearchEnabled;
                searchEntry.Sensitive = gxml["SearchLabel"].Sensitive;
            }
        }
        
        private void OnSourceManagerSourceViewChanged(SourceEventArgs args)
        {
            if(args.Source == SourceManager.ActiveSource) {
                UpdateSourceView();
            }
        }
        
        private void UpdateSourceView()
        {
            if(SourceManager.ActiveSource.ViewWidget != null) {
                ShowSourceWidget();
            } else {
                ShowPlaylistView();
            }
        }
        
        private void ShowPlaylistView()
        {
            Alignment alignment = gxml["LibraryAlignment"] as Alignment;
            ScrolledWindow playlist_container = gxml["LibraryContainer"] as ScrolledWindow;
            
            if(alignment.Child == playlist_container) {
                return;
            } else if(alignment.Child != null) {
                alignment.Remove(alignment.Child);
            }
            
            InterfaceElements.DetachPlaylistContainer();
            
            alignment.Add(playlist_container);
            alignment.ShowAll();
            
            gxml["playlist_header_event_box"].Show();
        }
        
        private void ShowSourceWidget()
        {
            Alignment alignment = gxml["LibraryAlignment"] as Alignment;
            
            gxml["playlist_header_event_box"].Visible = SourceManager.ActiveSource.ShowPlaylistHeader;
            
            if(alignment.Child == SourceManager.ActiveSource.ViewWidget) {
                return;
            }
            
            if(SourceManager.ActiveSource.ViewWidget == null) {
                ShowPlaylistView();
                return;
            } 
            
            if(alignment.Child != null) {
                alignment.Remove(alignment.Child);
            }
            
            alignment.Add(SourceManager.ActiveSource.ViewWidget);
            alignment.Show();
        }
        
        private void OnSourceTrackAdded(object o, TrackEventArgs args)
        {
            if(SourceManager.ActiveSource == o) {
                // We only want to update the playlist view once
                if (args.Track != null) {
                    AddTrackToModel(args.Track, (args.Tracks == null || args.Tracks.Count == 0));
                }

                if (args.Tracks != null) {
                    if (args.Tracks.Count > 3) {
                        playlistModel.ClearSortOrder();
                    }

                    int i = 1, last = args.Tracks.Count;
                    foreach (TrackInfo track in args.Tracks) {
                        AddTrackToModel(track, i++ == last);
                    }

                    if (args.Tracks.Count > 3) {
                        playlistModel.RestoreSortOrder();
                    }
                }
            }
        }

        private void AddTrackToModel(TrackInfo track, bool update)
        {
            if(searchEntry.IsQueryAvailable && !DoesTrackMatchSearch(track)) {
                return;
            }
            
            playlistModel.AddTrack(track, update);
        }
        
        private void OnSourceTrackRemoved(object o, TrackEventArgs args)
        {
            if(SourceManager.ActiveSource == o) {
                if (args.Track != null) {
                    RemoveTrackFromModel(args.Track);
                }

                if (args.Tracks != null) {
                    if (args.Tracks.Count > 3) {
                        playlistModel.ClearSortOrder();
                    }

                    foreach (TrackInfo track in args.Tracks) {
                        RemoveTrackFromModel(track);
                    }

                    if (args.Tracks.Count > 3) {
                        playlistModel.RestoreSortOrder();
                    }
                }
            }
        }

        private void RemoveTrackFromModel(TrackInfo track)
        {
            playlistModel.RemoveTrack(track);
        }
        
        private void UpdateViewName(Source source)
        {
            (gxml["ViewNameLabel"] as Label).Markup = "<b>" + GLib.Markup.EscapeText(source.Name) + "</b>";
        }
        
        private void SensitizeActions(Source source)
        {
            SourceManager.SensitizeActions(source);
        }
     
        // Called when SourceManager emits an ActiveSourceChanged event.
        private void HandleSourceChanged(object o, EventArgs args)
        {
            Source source = SourceManager.ActiveSource;
            if(source == null) {
                return;
            }

            searchEntry.Ready = false;
            searchEntry.CancelSearch();
            
            if(source.FilterQuery != null && source.FilterType != TrackFilterType.None) {
                searchEntry.Query = source.FilterQuery;
                searchEntry.ActivateFilter((int)source.FilterType);
            }
            
            if(source is DapSource) {
                DapSource dap_source = source as DapSource;
                UpdateDapDiskUsageBar(dap_source);
            }
            
            UpdateViewName(source);   // Bold label below track info
            SensitizeActions(source); // Right-click actions, buttons above search

            if(source is DapSource) { // Show disk usage bar for DAPs
                gxml["DapContainer"].ShowAll();
                sync_dap_button.Pixbuf = (source as DapSource).Device.GetIcon(22);
            } else {
                gxml["DapContainer"].Hide();
            }
            
            // Make some choices for audio CDs, they can't be rated, nor have plays or
            // last-played info. Only show the rip button for audio CDs
            gxml["SearchLabel"].Sensitive = source.SearchEnabled;
            searchEntry.Sensitive = gxml["SearchLabel"].Sensitive;
            playlistView.RipColumn.Visible = source is AudioCdSource;
                
            UpdateSourceView();
                
            OnPlaylistViewSelectionChanged(playlistView.Selection, new EventArgs());

            searchEntry.Ready = true;
        }
        
        private void UpdateDapDiskUsageBar(DapSource dapSource)
        {
            Application.Invoke(delegate {
                dapDiskUsageBar.Fraction = dapSource.DiskUsageFraction;
                dapDiskUsageBar.Text = dapSource.DiskUsageString;
                string tooltip = dapSource.DiskUsageString + " (" + dapSource.DiskAvailableString + ")";
                toolTips.SetTip(dapDiskUsageBar, tooltip, tooltip);
            });
        }

        private void OnDapPropertiesChanged(object o, EventArgs args)
        {
            ThreadAssist.ProxyToMain(delegate {
                DapDevice device = o as DapDevice;
                
                device.Source.SetSourceName(device.Name);
                sourceView.QueueDraw();

                if(SourceManager.ActiveSource is DapSource && (SourceManager.ActiveSource as DapSource).Device == device) {
                    UpdateDapDiskUsageBar(SourceManager.ActiveSource as DapSource);
                    (gxml["ViewNameLabel"] as Label).Markup = "<b>" 
                        + GLib.Markup.EscapeText(device.Name) + "</b>";
                    sourceView.QueueDraw();
                }
            });
        }
        
        private void OnDapCoreDeviceAdded(object o, DapEventArgs args)
        {
            args.Dap.PropertiesChanged += OnDapPropertiesChanged;
            args.Dap.SaveFinished += OnDapSaveFinished;
        }
        
        private void OnDapSaveFinished(object o, EventArgs args)
        {
            if(SourceManager.ActiveSource is DapSource
                && !(SourceManager.ActiveSource as DapSource).IsSyncing) {
                playlistModel.ReloadSource();
                UpdateDapDiskUsageBar(SourceManager.ActiveSource as DapSource);
            }
            
            sourceView.QueueDraw();
            playlistView.QueueDraw();
        }

        private void OnPlaylistStopped(object o, EventArgs args)
        {
            PlayerEngineCore.Close();
            UpdateMetaDisplay();
        }

        private uint status_bar_update_timeout = 0;
        
        private void OnPlaylistUpdated(object o, EventArgs args)
        {
            if(status_bar_update_timeout == 0) {
                status_bar_update_timeout = GLib.Timeout.Add(200, delegate {
                    UpdateStatusBar();
                    status_bar_update_timeout = 0;
                    return false;
                });
            }
        }

        private void UpdateStatusBar()
        {
            long count = playlistModel.Count();
            
            if(count == 0 && SourceManager.ActiveSource == null) {
                LabelStatusBar.Text = Branding.ApplicationLongName;
                return;
            } else if(count == 0) {
                LabelStatusBar.Text = String.Empty;
                return;
            } 
            
            TimeSpan span = playlistModel.TotalDuration;       
            StringBuilder builder = new StringBuilder();
            
            builder.AppendFormat(Catalog.GetPluralString("{0} Item", "{0} Items", (int)count), count);
            builder.Append(", ");
            
            if(span.Days > 0) {
                builder.AppendFormat(Catalog.GetPluralString("{0} day", "{0} days", span.Days), span.Days);
                builder.Append(", ");
            }
            
            if(span.Hours > 0) {
                builder.AppendFormat(Catalog.GetPluralString("{0} hour", "{0} hours", span.Hours), span.Hours);
                builder.Append(", ");
            }
            
            builder.AppendFormat(Catalog.GetPluralString("{0} minute", "{0} minutes", span.Minutes), span.Minutes);
            builder.Append(", ");
            builder.AppendFormat(Catalog.GetPluralString("{0} second", "{0} seconds", span.Seconds), span.Seconds);
            
            LabelStatusBar.Text = builder.ToString();
        }
        
        private void OnLogCoreUpdated(object o, LogCoreUpdatedArgs args)
        {
            ShowLogCoreEntry(args.Entry);
        }
        
        private void ShowLogCoreEntry(LogEntry entry)
        {
            if(!entry.ShowUser || entry.Type == LogEntryType.Debug) {
                return;
            }
            
            MessageType mtype;
            
            switch(entry.Type) {
                case LogEntryType.Warning:
                    mtype = MessageType.Warning;
                    break;
                case LogEntryType.Information:
                    mtype = MessageType.Info;
                    break;
                case LogEntryType.Error:
                default:
                    mtype = MessageType.Error;
                    break;
            }
              
            HigMessageDialog dialog = new HigMessageDialog(WindowPlayer, 
                DialogFlags.Modal,
                mtype,
                ButtonsType.Close,
                entry.ShortMessage,
                entry.Details);
            
            dialog.Title = String.Empty;
            IconThemeUtils.SetWindowIcon(dialog);
            
            dialog.Response += delegate(object obj, ResponseArgs response_args)
            {
                (obj as Dialog).Destroy();
            };
            
            dialog.ShowAll();
        }

        private bool DoesTrackMatchSearch(TrackInfo ti)
        {
            if(!searchEntry.IsQueryAvailable) {
                return false;
            }
            
            string query = searchEntry.Query;
            TrackFilterType filter_type = (TrackFilterType)searchEntry.ActiveFilterID;
            string [] matches;
            
            if((filter_type & TrackFilterType.ArtistName) == TrackFilterType.ArtistName) {
                matches = new string [] { ti.Artist };
            } else if((filter_type & TrackFilterType.SongName) == TrackFilterType.SongName) {
                matches = new string [] { ti.Title };
            } else if((filter_type & TrackFilterType.AlbumTitle) == TrackFilterType.AlbumTitle) {
                matches = new string [] { ti.Album };
            } else if((filter_type & TrackFilterType.Genre) == TrackFilterType.Genre) {
                matches = new string [] { ti.Genre };
            } else if((filter_type & TrackFilterType.Year) == TrackFilterType.Year) {
                matches = new string [] { ti.Year.ToString() };
            } else {
                matches = new string [] {
                    ti.Artist,
                    ti.Album,
                    ti.Title,
                    ti.Genre,
                    ti.Year.ToString()
                };
            }
            
            List<string> words_include = new List<string>();
            List<string> words_exclude = new List<string>();
            
            Array.ForEach<string>(Regex.Split(query, @"\s+"), delegate(string word) {
                bool exclude = word.StartsWith("-");
                if(exclude && word.Length > 1) {
                    words_exclude.Add(word.Substring(1));
                } else if(!exclude) {
                    words_include.Add(word);
                }
            });
            
            foreach(string word in words_exclude) {
                foreach(string match in matches) {
                    if(match == null || match == String.Empty) {
                        continue;
                    }
                
                    if(StringUtil.RelaxedIndexOf(match, word) >= 0) {
                        return false;
                    }
                }
            }
            
            bool found;
            
            foreach(string word in words_include) {
                found = false;
                
                foreach(string match in matches) {
                    if(match == null || match == string.Empty) {
                        continue;
                    }
                
                    if(StringUtil.RelaxedIndexOf(match, word) >= 0) {
                        found = true;
                        break;
                    }
                }
                
                if(!found) {
                    return false;
                }
            }
            
            return true;
        }
        
        private void OnSimpleSearchFilterChange(object o, EventArgs args)
        {
            searchEntry.EmptyMessage = String.Format(Catalog.GetString("Filter on {0}"), 
                searchEntry.GetLabelForFilterID(searchEntry.ActiveFilterID));
        }
        
        private void OnSimpleSearch(object o, EventArgs args)
        {
            SourceManager.ActiveSource.FilterType = (TrackFilterType)searchEntry.ActiveFilterID;
            SourceManager.ActiveSource.FilterQuery = searchEntry.Query;
        
            if(SourceManager.ActiveSource.HandlesSearch || suspendSearch || 
                (searchEntry.IsQueryAvailable && searchEntry.Query.Length < 3)) {
                return;
            }

            playlistModel.ClearModel();
            
            if(!searchEntry.IsQueryAvailable) {
                playlistModel.ReloadSource();
                while(Application.EventsPending()) {
                    Application.RunIteration();
                }
                playlistView.UpdateView();
                return;
            }
            
            lock(SourceManager.ActiveSource.TracksMutex) {
                foreach(TrackInfo track in SourceManager.ActiveSource.Tracks) {
                    try {
                        if(DoesTrackMatchSearch(track)) {
                            playlistModel.AddTrack(track);
                        }
                    } catch(Exception) {
                        continue;
                    }
                }
            }
            
            playlistView.UpdateView();
        }
        
        // PlaylistView DnD
        
        [GLib.ConnectBefore]
        private void OnPlaylistViewButtonPressEvent(object o, ButtonPressEventArgs args)
        {
            if(args.Event.Window != playlistView.BinWindow) {
                return;
            } else if(args.Event.Button == 3) {
                PlaylistMenuPopupTimeout(args.Event.Time);
            }
            
            TreePath path;
            TreeViewColumn column;
            playlistView.GetPathAtPos((int)args.Event.X, (int)args.Event.Y, out path, out column);
        
            if(path == null) {
                return;
            }
            
            clickX = (int)args.Event.X;
            clickY = (int)args.Event.Y;
        
            switch(args.Event.Type) {
                case EventType.TwoButtonPress:
                    if(args.Event.Button != 1 || (args.Event.State & 
                        (ModifierType.ControlMask | ModifierType.ShiftMask)) != 0) {
                        return;
                    }
                    
                    playlistView.Selection.UnselectAll();
                    playlistView.Selection.SelectPath(path);
                    playlistView.PlayPath(path);
                    return;
                case EventType.ButtonPress:
                    if(playlistView.Selection.PathIsSelected(path) && (args.Event.State & 
                        (ModifierType.ControlMask | ModifierType.ShiftMask)) == 0) {
                        if (column != null && args.Event.Button == 1 && column.CellRenderers.Length == 1) {
                            CellRenderer renderer = column.CellRenderers[0];
                            Gdk.Rectangle background_area = playlistView.GetBackgroundArea(path, column);
                            Gdk.Rectangle cell_area = playlistView.GetCellArea(path, column);
                            
                            renderer.Activate(args.Event,
                                              playlistView,
                                              path.ToString(),
                                              background_area,
                                              cell_area,
                                              CellRendererState.Selected);
                            
                            TreeIter iter;
                            if (playlistModel.GetIter(out iter, path)) {
                                playlistModel.EmitRowChanged(path, iter);
                            }
                        }
                        
                        args.RetVal = true;
                    }
                    
                    return;
                default:
                    args.RetVal = false;
                    return;
            }
        }

        [GLib.ConnectBefore]
        private void OnPlaylistViewMotionNotifyEvent(object o, MotionNotifyEventArgs args)
        {
            if((args.Event.State & ModifierType.Button1Mask) == 0) {
                return;
            } else if(args.Event.Window != playlistView.BinWindow) {
                return;
            }
                    
            args.RetVal = true;
            
            if(!Gtk.Drag.CheckThreshold(playlistView, clickX, clickY, (int)args.Event.X, (int)args.Event.Y)) {
                return;
            }
            
            TreePath path;
            if(!playlistView.GetPathAtPos((int)args.Event.X, (int)args.Event.Y, out path)) {
                return;
            }

            if(SourceManager.ActiveSource is AudioCdSource) {
                return;
            }
              
            Gtk.Drag.Begin(playlistView, new TargetList(playlistViewSourceEntries), 
                Gdk.DragAction.Move | Gdk.DragAction.Copy, 1, args.Event);
        }

        private void OnPlaylistViewButtonReleaseEvent(object o, ButtonReleaseEventArgs args)
        {
            if(!Gtk.Drag.CheckThreshold(playlistView, clickX, clickY, (int)args.Event.X, (int)args.Event.Y) &&
                ((args.Event.State & (ModifierType.ControlMask | ModifierType.ShiftMask)) == 0) &&
                playlistView.Selection.CountSelectedRows() > 1) {
                
                TreePath path;
                playlistView.GetPathAtPos((int)args.Event.X, (int)args.Event.Y, out path);
                playlistView.Selection.UnselectAll();
                playlistView.Selection.SelectPath(path);
            }
        }
        
        private void OnNewPlaylistFromSelectionActivated(object o, EventArgs args)
        {
            PlaylistSource playlist = new PlaylistSource();
            
            foreach(TreePath path in playlistView.Selection.GetSelectedRows()) {
                playlist.AddTrack(playlistModel.PathTrackInfo(path));
            }
            
            playlist.Rename(PlaylistUtil.GoodUniqueName(playlist.Tracks));
            playlist.Commit();
            
            LibrarySource.Instance.AddChildSource(playlist);
        }
        
        private void OnItemAddToPlaylistActivated(object o, EventArgs args)
        {
            PlaylistSource playlist = playlistMenuMap[o] as PlaylistSource;
            
            if(playlist == null)
                return;
                
            foreach(TreePath path in playlistView.Selection.GetSelectedRows()) {
                TrackInfo track = playlistModel.PathTrackInfo(path);
                if(track != null) {
                    playlist.AddTrack(track);
                }
            }
            
            playlist.Commit();
        }

        private Menu song_popup_menu = null;
        private MenuItem add_to_playlist_menu_item = null;
        private RatingMenuItem rating_menu_item = null;
        
        private bool PlaylistMenuPopupTimeout(uint time)
        {
            if(song_popup_menu == null) {
                song_popup_menu = Globals.ActionManager.GetWidget("/SongViewPopup") as Menu;
                add_to_playlist_menu_item = Globals.ActionManager.GetWidget("/SongViewPopup/AddToPlaylist") as MenuItem;
                
                for(int i = 0; i < song_popup_menu.Children.Length; i++) {
                    if(song_popup_menu.Children[i] == add_to_playlist_menu_item) {
                        rating_menu_item = new RatingMenuItem();
                        rating_menu_item.Activated += OnItemRatingActivated;
                        rating_menu_item.Visible = false;
                        song_popup_menu.Insert(rating_menu_item, i + 1);
                        break;
                    }
                }
            }
          
            int selected_row_count = playlistView.Selection.CountSelectedRows();
            bool sensitive = selected_row_count > 0;

            if(sensitive && (SourceManager.ActiveSource is LibrarySource ||
                    SourceManager.ActiveSource is PlaylistSource ||
                    SourceManager.ActiveSource is Banshee.SmartPlaylist.SmartPlaylistSource)) {

                Globals.ActionManager["AddToPlaylistAction"].Visible = true;
                rating_menu_item.Visible = true;
            
                Menu plMenu = new Menu();
                playlistMenuMap.Clear();
                
                ImageMenuItem newPlItem = new ImageMenuItem(Catalog.GetString("New Playlist"));
                newPlItem.Image = new Gtk.Image("gtk-new", IconSize.Menu);
                newPlItem.Activated += OnNewPlaylistFromSelectionActivated;
                plMenu.Append(newPlItem);
                
                if(PlaylistSource.PlaylistCount > 0) {
                    plMenu.Append(new SeparatorMenuItem());
                    
                    foreach(ChildSource playlist in LibrarySource.Instance.Children) {               
                        if (playlist is PlaylistSource) {
                            ImageMenuItem item = new ImageMenuItem(playlist.Name);
                            item.Image = new Gtk.Image(Pixbuf.LoadFromResource("source-playlist-16.png"));
                            item.Activated += OnItemAddToPlaylistActivated;
                            playlistMenuMap[item] = playlist;
                            plMenu.Append(item);
                        }
                    }
                }
                
                add_to_playlist_menu_item.Submenu = plMenu;
                plMenu.ShowAll();
                
                // FIXME: The selection/popup order is fux0red
                // if(selected_row_count == 1) {
                //    rating_menu_item.Reset((int)playlistView.SelectedTrackInfo.Rating);
                // } else {
                //    rating_menu_item.Reset(0);
                // }
                
                rating_menu_item.Reset(0);
            } else {
                Globals.ActionManager["AddToPlaylistAction"].Visible = false;
                rating_menu_item.Visible = false;
            }
        
            song_popup_menu.Show();
            song_popup_menu.Popup(null, null, null, 0, time);
            
            return false;
        }

        private void OnItemRatingActivated(object o, EventArgs args)
        {
            uint rating = (uint)(o as RatingMenuItem).Value;
            
            foreach(TreePath path in playlistView.Selection.GetSelectedRows()) {
                playlistModel.PathTrackInfo(path).Rating = rating;
            }
            
            playlistView.QueueDraw();
        }

        private void OnPlaylistViewDragDataReceived(object o, 
            DragDataReceivedArgs args)
        {
            TreePath destPath;
            TreeIter destIter;
            TreeViewDropPosition pos;
            bool haveDropPosition;
            
            string rawSelectionData = DragDropUtilities.SelectionDataToString(args.SelectionData);            
            
            haveDropPosition = playlistView.GetDestRowAtPos(args.X, 
                args.Y, out destPath, out pos);
            
            if(haveDropPosition && 
                !playlistModel.GetIter(out destIter, destPath)) {
                Gtk.Drag.Finish(args.Context, true, false, args.Time);
                return;
            }

            switch(args.Info) {
                case (uint)DragDropTargetType.UriList:
                    // AddFile needs to accept a Path for inserting
                    // If in Library view, we just append to Library
                    // If in Playlist view, we append Library *AND* PlayList
                    // If in SmartPlaylist View WE DO NOT ACCEPT DND
                
                    if(rawSelectionData != null) {
                        Banshee.Library.Import.QueueSource(args.SelectionData);
                    }
                        
                    break;
                case (uint)DragDropTargetType.PlaylistRows:
                    if(!haveDropPosition)
                        break;
                    
                    string [] paths = DragDropUtilities.SplitSelectionData(rawSelectionData);
                    if(paths.Length <= 0) 
                        break;
                        
                    ArrayList iters = new ArrayList();
                    foreach(string path in paths) {
                        if(path == null || path.Length == 0)
                            continue;
                        
                        TreeIter iter;
                        if(!playlistModel.GetIter(out iter, new TreePath(path)))
                            continue;
                            
                        iters.Add(iter);
                    }
                     
                    int reorder_count = 0;
                    
                    foreach(TreeIter iter in iters) {
                        if(!playlistModel.IterIsValid(destIter))
                            break;
                            
                        if(pos == TreeViewDropPosition.After ||
                            pos == TreeViewDropPosition.IntoOrAfter) {
                            playlistModel.MoveAfter(iter, destIter);
                            //destIter = iter.Copy();
                            destIter = (TreeIter)iter;
                        } else {
                            playlistModel.MoveBefore(iter, destIter);
                        }
                        
                        SourceManager.ActiveSource.Reorder(playlistModel.IterTrackInfo(iter), 
                            playlistModel.GetIterIndex(iter));
                        reorder_count++;
                    }
                    
                    if(reorder_count > 0) {
                        SourceManager.ActiveSource.Commit();
                    }
                                    
                    break;
            }

            Gtk.Drag.Finish(args.Context, true, false, args.Time);
        }
        
        private void OnPlaylistViewDragDataGet(object o, DragDataGetArgs args)
        {
            byte [] selection_data;
            
            switch(args.Info) {
                case (uint)DragDropTargetType.PlaylistRows:                
                    selection_data = DragDropUtilities.TreeViewSelectionPathsToBytes(playlistView);
                    if(selection_data == null) {
                        return;
                    }
                    
                    args.SelectionData.Set(Gdk.Atom.Intern(DragDropTarget.PlaylistRows.Target, 
                        false), 8, selection_data);
                    break;
                case (uint)DragDropTargetType.TrackInfoObjects:
                    if(playlistView.Selection.CountSelectedRows() <= 0) {
                        return;
                    };
                   
                    DragDropList<TrackInfo> track_dnd = new DragDropList<TrackInfo>();
                    foreach(TreePath path in playlistView.Selection.GetSelectedRows()) {
                        track_dnd.Add(playlistModel.PathTrackInfo(path));
                    }
                    
                    track_dnd.AssignToSelection(args.SelectionData, 
                        Gdk.Atom.Intern(DragDropTarget.TrackInfoObjects.Target, false));
                        
                    break;
                case (uint)DragDropTargetType.UriList:
                    if(playlistView.Selection.CountSelectedRows() <= 0) {
                        return;
                    }
                
                    string selection_data_str = null;
                    foreach(TreePath path in playlistView.Selection.GetSelectedRows()) {
                        selection_data_str += playlistModel.PathTrackInfo(path).Uri + "\r\n";
                    }
                
                    selection_data = System.Text.Encoding.ASCII.GetBytes(selection_data_str);
                    if(selection_data == null) {
                        return;
                    }
                    
                    args.SelectionData.Set(args.Context.Targets[0], 8, selection_data, 
                        selection_data.Length);
                    break;
            }
        }
        
        private void OnPlaylistViewDragDrop(object o, DragDropArgs args)
        {
            Gtk.Drag.Finish(args.Context, true, false, args.Time);
            
            // major weird hack
            TreePath [] selrows = playlistView.Selection.GetSelectedRows();
            playlistView.Selection.UnselectAll();
            playlistView.Selection.SelectPath(new TreePath("0"));
            playlistView.Selection.UnselectAll();
            foreach(TreePath path in selrows)
                playlistView.Selection.SelectPath(path);
        }
     
        /*private void OnAudioCdRipperTrackRipped(object o, HaveTrackInfoArgs args)
        {
            if(SourceManager.ActiveSource is LibrarySource) {
                ThreadAssist.ProxyToMain(delegate {
                    if(searchEntry.IsQueryAvailable && !DoesTrackMatchSearch(args.TrackInfo)) {
                        return;
                    }

                    playlistModel.AddTrack(args.TrackInfo);
                });
            }
        }*/
        
        /*private void EjectSource(Source source)
        {
            if(source.CanEject) {
                try {
                    if(source.GetType() == typeof(DapSource)) {
                        if(PlayerEngineCore.CurrentTrack != null && PlayerEngineCore.CurrentTrack is DapTrackInfo) {
                            PlayerEngineCore.Close();
                        }
                    }
                    
                    if(!source.Unmap()) {
                        return;
                    }
                    
                    if(source == SourceManager.ActiveSource) {
                        SourceManager.SetActiveSource(LibrarySource.Instance);
                    }
                } catch(Exception e) {
                    HigMessageDialog.RunHigMessageDialog(null, 
                        DialogFlags.Modal, MessageType.Error, ButtonsType.Ok, 
                        Catalog.GetString("Could Not Eject"),
                        e.Message);
                }
            }
        }*/

        private void DeleteSong(TrackInfo ti)
        {
            File.Delete(ti.Uri.LocalPath);

            // trim empty parent directories
            try {
                string old_dir = Path.GetDirectoryName(ti.Uri.LocalPath);
                while(old_dir != null && old_dir != String.Empty) {
                    Directory.Delete(old_dir);
                    old_dir = Path.GetDirectoryName(old_dir);
                }
            } catch(Exception) {}
        }
        
        private void RemoveSongs(bool deleteFromFileSystem)
        {
            // Don't steal "Del" key from the search entry
            if(WindowPlayer.Focus is Entry && Gtk.Global.CurrentEvent is Gdk.EventKey) {
                Gtk.Bindings.ActivateEvent(WindowPlayer.Focus, (Gdk.EventKey)Gtk.Global.CurrentEvent);
                return;
            }

            int selCount = playlistView.Selection.CountSelectedRows();
        
            if(selCount <= 0 || !SourceManager.ActiveSource.CanRemoveTracks) {
                return;
            }
            
            if(SourceManager.ActiveSource is LibrarySource) {
                string header = null;
                string message = null;
                string button_label = null;
                
                if(deleteFromFileSystem) {
                    header = String.Format(Catalog.GetPluralString(
                        "Are you sure you want to permanently delete this song?",
                        "Are you sure you want to permanently delete the selected {0} songs?",
                        selCount),
                    selCount);
                    message = Catalog.GetString("If you delete the selection, it will be permanently lost.");
                    button_label = "gtk-delete";
                } else {
                    header = Catalog.GetString("Remove selection from library");
                    message = String.Format(Catalog.GetPluralString(
                        "Are you sure you want to remove the selected song from your library?",
                        "Are you sure you want to remove the selected {0} songs from your library?",
                        selCount),
                    selCount);
                    button_label = "gtk-remove";
                }
                    
                HigMessageDialog md = new HigMessageDialog(WindowPlayer, 
                    DialogFlags.DestroyWithParent, MessageType.Warning,
                    ButtonsType.None,
                    header, message);
                md.AddButton("gtk-cancel", ResponseType.No, false);
                md.AddButton(button_label, ResponseType.Yes, false);
                
                try {
                    if(md.Run() != (int)ResponseType.Yes) {
                        return;
                    }
                } finally {
                    md.Destroy();
                }
            } else {
                deleteFromFileSystem = false;
            }
        
            TreeIter [] iters = new TreeIter[selCount];
            int i = 0;
            
            foreach(TreePath path in playlistView.Selection.GetSelectedRows()) {
                playlistModel.GetIter(out iters[i++], path);
            }

            for(i = 0; i < iters.Length; i++) {
                TrackInfo track = playlistModel.IterTrackInfo(iters[i]);

                if (track == PlayerEngineCore.CurrentTrack
                    && PlayerEngineCore.CurrentState == PlayerEngineState.Playing) {
                    Globals.ActionManager["NextAction"].Activate();
                }

                try {
                    if(deleteFromFileSystem) {
                        DeleteSong(track);
                    }
                } catch (UnauthorizedAccessException) {
                    string header = Catalog.GetString ("Delete songs from drive");
                    string msg = String.Format (Catalog.GetString ("You do not have the required permissions to delete '{0}'"), track.Uri.LocalPath);
                    HigMessageDialog error = new HigMessageDialog (WindowPlayer,
                                                                   DialogFlags.DestroyWithParent, MessageType.Error,
                                                                   ButtonsType.Close,
                                                                   header, msg);
                    error.Run ();
                    error.Destroy ();
                    break;
                }
                
                SourceManager.ActiveSource.RemoveTrack(track);
                playlistModel.RemoveTrack(ref iters[i], track);
            }
                
            SourceManager.ActiveSource.Commit();
            sourceView.QueueDraw();
            playlistView.QueueDraw();
        }
        
        private void OnLibraryTrackRemoveFinished(object o, EventArgs args)
        {
        }
        
        [GLib.ConnectBefore]
        private void OnButtonPreviousPressed(object o, ButtonPressEventArgs args)
        {
            if((args.Event.State & Gdk.ModifierType.ShiftMask) != 0) {
                Previous();
                args.RetVal = true;
            }
        }
        
        // ---------------------------------------------------------------------------------
        // ActionManager Callbacks --- All entries here should be in order with the MainMenu
        // defined in UIManagerLayout.xml. NO CODE that is not ActionManager related may be
        // added below this section! Trying to keep it clean!
        // ---------------------------------------------------------------------------------

        // --- Music Menu ---

        private void OnNewPlaylistAction(object o, EventArgs args)
        {
            PlaylistSource playlist = new PlaylistSource();
            playlist.Rename(PlaylistUtil.UniqueName);
            LibrarySource.Instance.AddChildSource(playlist);
        }
        
        private void OnImportFolderAction(object o, EventArgs args)
        {
            FolderImportSource.Instance.Import();
        }
        
        private void OnImportFilesAction(object o, EventArgs args)
        {
            FileImportSource.Instance.Import();
        }
        
        private void OnImportMusicAction(object o, EventArgs args)
        {
            PromptForImport(false);
        }
        
        private void OnImportPlaylistAction(object o, EventArgs args)
        {
            // Prompt user for location of the playlist.
            Banshee.Gui.Dialogs.FileChooserDialog chooser = new Banshee.Gui.Dialogs.FileChooserDialog(
                Catalog.GetString("Import Playlist"),
                WindowPlayer,
                FileChooserAction.Open
            );
                         
            chooser.DefaultResponse = ResponseType.Ok;
            chooser.SelectMultiple = false;

            chooser.AddButton(Stock.Cancel, ResponseType.Cancel);
            chooser.AddButton(Catalog.GetString("Import"), ResponseType.Ok);
            
            string playlist_uri = null;
            int response = chooser.Run();            

            if(response == (int) ResponseType.Ok) {                    
                playlist_uri = SafeUri.UriToFilename(chooser.Uri);              
                chooser.Destroy(); 
            } else {
                // User cancelled import.
                chooser.Destroy();                 
                return;
            } 

            // Read the contents of the playlist.
            string[] uris = null;
            try {                    
                uris = PlaylistFileUtil.ImportPlaylist(playlist_uri);                    
            } catch (Exception e) {
                HigMessageDialog md = new HigMessageDialog(WindowPlayer, 
                    DialogFlags.DestroyWithParent, 
                    MessageType.Error,  
                    ButtonsType.Ok,
                    Catalog.GetString("Unable to Import Playlist"),
                    e.Message);

                md.Run();
                md.Destroy();
                return;
            }

            // Import the tracks specified in the playlist.
            if (uris != null) {
                ImportPlaylistWorker worker = new ImportPlaylistWorker(uris);
                Thread t = new Thread(new ThreadStart(worker.Import));                        
                t.Start();
            } else {
                HigMessageDialog md = new HigMessageDialog(WindowPlayer, 
                    DialogFlags.DestroyWithParent, 
                    MessageType.Error,  
                    ButtonsType.Ok,
                    Catalog.GetString("Unable to Import Playlist"),
                    Catalog.GetString("Banshee was unable to find any valid tracks to import.  " +
                        "Please check the playlist and try again."));

                md.Run();
                md.Destroy();
                return;
            }
        }
        
        private void OnExportPlaylistAction(object o, EventArgs args)
        {   
            Source source = sourceView.HighlightedSource;
            if (!PlaylistFileUtil.IsSourceExportSupported(source)) {
                return;
            }

            PlaylistExportDialog chooser = new PlaylistExportDialog(source.Name, WindowPlayer);

            string uri = null;
            PlaylistFormatDescription format = null;
            int response = chooser.Run();            
            if(response == (int) ResponseType.Ok) {                    
                uri = chooser.Uri;
                // Get the format that the user selected.
                format = chooser.GetExportFormat();
            }             
            chooser.Destroy(); 

            if (uri == null) {
                // User cancelled export.
                return;
            }
            
            try {
                IPlaylistFormat playlist = (IPlaylistFormat)Activator.CreateInstance(format.Type);
                SafeUri suri = new SafeUri(uri);
                if(suri.IsLocalPath) {
                    playlist.BaseUri = new Uri(Path.GetDirectoryName(suri.LocalPath));
                    Console.WriteLine(playlist.BaseUri.LocalPath);
                }
                playlist.Save(Banshee.IO.IOProxy.File.OpenWrite(new SafeUri(uri), true), source);
            } catch(Exception e) {
                Console.WriteLine(e);
                LogCore.Instance.PushError(Catalog.GetString("Could not export playlist"), e.Message);
            }
        } 
        
        private void OnOpenLocationAction(object o, EventArgs args)
        {
            Banshee.Gui.OpenLocationDialog dialog = new Banshee.Gui.OpenLocationDialog();
            ResponseType response = dialog.Run();
            string address = dialog.Address;
            dialog.Destroy();
            
            if(response != ResponseType.Ok) {
                return;
            }
            
            try {
                RadioTrackInfo radio_track = new RadioTrackInfo(new SafeUri(address));
                radio_track.ParsingPlaylistEvent += delegate {
                    if(radio_track.PlaybackError != TrackPlaybackError.None) {
                        LogCore.Instance.PushError(Catalog.GetString("Error opening stream"), Catalog.GetString("Could not open stream or playlist"));
                        radio_track = null;
                    }
                };
                radio_track.Play();
            } catch {
                LogCore.Instance.PushError(Catalog.GetString("Error opening stream"), Catalog.GetString("Problem parsing playlist"));
            }
        }
        
        private void OnImportSourceAction(object o, EventArgs args)
        {
            if(sourceView.HighlightedSource is IImportSource) {
                ((IImportSource)sourceView.HighlightedSource).Import();
            }
        }
        
        private void OnImportCDAction(object o, EventArgs args)
        {
            if(SourceManager.ActiveSource is AudioCdSource) {
                ((IImportSource)SourceManager.ActiveSource).Import();
            }
        }
        
        private void OnWriteCDAction(object o, EventArgs args) 
        {
            if(SourceManager.ActiveSource is Banshee.Burner.BurnerSource) {
                (SourceManager.ActiveSource as Banshee.Burner.BurnerSource).Burn();
                return;
            } else if(playlistView.Selection.CountSelectedRows() <= 0) {
                return;
            }
            
            Banshee.Burner.BurnerSource source = Banshee.Burner.BurnerCore.CreateOrFindEmptySource();
            
            if(source == null) {
                return;
            }
            
            foreach(TreePath path in playlistView.Selection.GetSelectedRows()) {
                source.AddTrack(playlistModel.PathTrackInfo(path));
            }
            
            source.Rename(NamingUtil.GenerateTrackCollectionName(source.Tracks, 
                Catalog.GetString("New CD")));
         
            SourceManager.SetActiveSource(source);
            source.Burn();
        }
        
        private void OnSyncDapAction(object o, EventArgs args)
        {
            if(!(sourceView.HighlightedSource is DapSource)) {
                return;
            }
                
            DapSource dapSource = sourceView.HighlightedSource as DapSource;
        
            if(dapSource == null) {
                return;
            }
        
            HigMessageDialog md = new HigMessageDialog(WindowPlayer, 
                DialogFlags.DestroyWithParent, MessageType.Question,
                // Translators: {0} is the name of the DAP device (i.e. 'iPod')
                String.Format(Catalog.GetString("Synchronize {0}"), dapSource.Device.GenericName),
                String.Format(Catalog.GetString("You have made changes to your {0}. Please choose " +
                    "a method for updating the contents of your {0}.\n\n" + 
                    "<big>\u2022</big> <i>Synchronize Library</i>: synchronize Banshee library to {0}\n" +
                    "<big>\u2022</big> <i>Save Manual Changes</i>: save only the manual changes you made"), 
                    dapSource.Device.GenericName) + (dapSource.Device.GenericName.ToLower() == "ipod" ?
                    ("\n\n" + 
                    Catalog.GetString("<b>Warning:</b> Actions will alter or erase existing iPod contents and " +
                    "may cause incompatibility with iTunes!")) : ""),
                Catalog.GetString("Synchronize Library"));
            
            md.AddButton(Catalog.GetString("Save Manual Changes"), Gtk.ResponseType.Apply, true);
            md.Image = dapSource.Device.GetIcon(48);
            md.Icon = md.Image;
            
            switch(md.Run()) {
                case (int)ResponseType.Ok:
                    dapSource.Device.Save(Globals.Library.Tracks.Values);
                    break;
                case (int)ResponseType.Apply:
                    dapSource.Device.Save();
                    break;
            }

            md.Destroy();
        }
        
        private void OnSelectedSourcePropertiesAction(object o, EventArgs args)
        {
            sourceView.HighlightedSource.ShowPropertiesDialog();
        }
        
        private void OnQuitAction(object o, EventArgs args)
        {
            Quit();
        }
        
        // --- Edit Menu ---
        
        private List<TrackInfo> clipboard_tracks = new List<TrackInfo>();
        
        private void OnCopySongsAction(object o, EventArgs args)
        {
            clipboard_tracks.Clear();
            
            foreach(TrackInfo track in playlistView.SelectedTrackInfoMultiple) {
                clipboard_tracks.Add(track);
            }
            
            Clipboard clipboard = Clipboard.Get(Gdk.Selection.Clipboard);
            clipboard.SetWithData(nautilus_file_copy_entries, OnGetClipboard, OnClearClipboard);   
        }
        
        private void OnGetClipboard(Clipboard clipboard, SelectionData selection, uint info)
        {
            StringBuilder uris = new StringBuilder();
            
            uris.Append("copy\n");
            
            foreach(TrackInfo track in clipboard_tracks) {
                uris.Append(track.Uri);
                uris.Append("\n");
            }
            
            byte [] raw_selection_data = Encoding.UTF8.GetBytes(uris.ToString());
            selection.Set(selection.Target, 8, raw_selection_data, raw_selection_data.Length);
        }

        private void OnClearClipboard(Clipboard clipboard)
        {   
            clipboard_tracks.Clear();
        }

        private void OnRemoveSongsAction(object o, EventArgs args)
        {
            RemoveSongs(false);
        }
        
        private void OnDeleteSongsFromDriveAction(object o, EventArgs args)
        {
            RemoveSongs(true);
        }
        
        private void OnRenameSourceAction(object o, EventArgs args)
        {
            Source source = sourceView.HighlightedSource;
            if(source != null && source.CanRename) {
                sourceView.BeginRenameSource(source);
            }
        }

        private void OnUnmapSourceAction(object o, EventArgs args)
        {
            Source source = sourceView.HighlightedSource;
            
            if(source == null || !source.CanUnmap)
                return;
                
            source.Unmap();
        }
        
        private void OnSelectAllAction(object o, EventArgs args)
        {
            // Don't steal "Ctrl+A" from the search entry
            if (Gtk.Global.CurrentEvent is Gdk.EventKey) {
                Gtk.Bindings.ActivateEvent(WindowPlayer.Focus, (Gdk.EventKey)Gtk.Global.CurrentEvent);
                return;
            }

            playlistView.Selection.SelectAll();
        }
        
        private void OnSelectNoneAction(object o, EventArgs args)
        {
            playlistView.Selection.UnselectAll();
        }
        
        private void OnPropertiesAction(object o, EventArgs args)
        {
            Banshee.Gui.Dialogs.TrackEditor propEdit = 
                new Banshee.Gui.Dialogs.TrackEditor(playlistView.SelectedTrackInfoMultiple);
            propEdit.Saved += delegate {
                playlistView.QueueDraw();
            };
        }
                
        private void OnPluginsAction(object o, EventArgs args)
        {
            Banshee.Plugins.PluginCore.ShowPluginDialog();
        }
        
        private void OnPreferencesAction(object o, EventArgs args)
        {
            Banshee.Gui.Dialogs.PreferencesDialog dialog = new Banshee.Gui.Dialogs.PreferencesDialog();
            dialog.Run();
            dialog.Destroy();
        }
        
        // -- Playback Menu ---
        
        private void OnPlayPauseAction(object o, EventArgs args)
        {
            PlayPause();
        }
        
        private void OnPreviousAction(object o, EventArgs args)
        {
            if(PlayerEngineCore.Position < 3) {
                Previous();
            } else {
                PlayerEngineCore.Position = 0;
            }
        }
        
        private void OnNextAction(object o, EventArgs args)
        {
            Next();
        }
        
        private void OnSeekForwardAction(object o, EventArgs args)
        {
            PlayerEngineCore.Position += SkipDelta;
        }
        
        private void OnSeekBackwardAction(object o, EventArgs args)
        {
            PlayerEngineCore.Position -= SkipDelta;
        }
        
        private void OnSeekToAction(object o, EventArgs args)
        {
            Banshee.Gui.SeekDialog dialog = new Banshee.Gui.SeekDialog();
            dialog.Dialog.Show();
            dialog.Dialog.Response += delegate {
                dialog.Destroy();
            };
        }
        
        private void OnRestartSongAction(object o, EventArgs args)
        {
            PlayerEngineCore.Position = 0;
            if(PlayerEngineCore.CurrentState == PlayerEngineState.Paused) {
                PlayerEngineCore.Play();
            }
        }
        
        private void SetRepeatMode(RepeatMode mode)
        {
            playlistModel.Repeat = mode;
            PlayerWindowSchema.PlaybackRepeat.Set((int)mode);
        }
        
        private void OnRepeatNoneAction(object o, EventArgs args)
        {
            SetRepeatMode(RepeatMode.None);
        }
        
        private void OnRepeatAllAction(object o, EventArgs args)
        {
            SetRepeatMode(RepeatMode.All);
        }
        
        private void OnRepeatSingleAction(object o, EventArgs args)
        {
            SetRepeatMode(RepeatMode.Single);
        }
        
        private void OnShuffleAction(object o, EventArgs args)
        {
            ToggleAction action = o as ToggleAction;
            playlistModel.Shuffle = action.Active;
            PlayerWindowSchema.PlaybackShuffle.Set(action.Active);
        }
        
        // --- View Menu ---
        
        private void OnShowCoverArtAction(object o, EventArgs args)
        {
            ToggleAction action = o as ToggleAction;
            cover_art_view.Enabled = action.Active;
            PlayerWindowSchema.ShowCoverArt.Set(action.Active);
        }
        
        private void OnFullScreenAction(object o, EventArgs args)
        {
            if(!(o as ToggleAction).Active) {
                WindowPlayer.Unfullscreen();
            } else {
                WindowPlayer.Fullscreen();
            } 
        }
        
        private void OnShowEqualizerAction(object o, EventArgs args)
        {
            Banshee.Equalizer.Gui.EqualizerEditor eqwin = new Banshee.Equalizer.Gui.EqualizerEditor();
            eqwin.Window.Show();
        }

        private void OnColumnsAction(object o, EventArgs args)
        {
            playlistView.ColumnChooser();
        }
        
        private Banshee.Gui.Dialogs.LogCoreDialog log_viewer = null;
        private void OnLoggedEventsAction(object o, EventArgs args)
        {
            if(log_viewer == null) {
                log_viewer = new Banshee.Gui.Dialogs.LogCoreDialog(LogCore.Instance, WindowPlayer);
                
                log_viewer.Response += delegate {
                    log_viewer.Hide();
                };
                
                log_viewer.DeleteEvent += delegate {
                    log_viewer.Destroy();
                    log_viewer = null;
                };
            }
            
            log_viewer.Show();
        }

        private void OnJumpToPlayingAction(object o, EventArgs args)
        {
            playlistView.ScrollToPlaying();
            playlistView.SelectPlaying();
        }

        private enum SearchTrackCriteria 
        {
            Artist,
            Album,
            Genre
        }

        private void OnSearchForSameAlbumAction(object o, EventArgs args)
        {
            SearchBySelectedTrack(SearchTrackCriteria.Album);
        }

        private void OnSearchForSameArtistAction(object o, EventArgs args)
        {
            SearchBySelectedTrack(SearchTrackCriteria.Artist);
        }

        private void OnSearchForSameGenreAction(object o, EventArgs args)
        {
            SearchBySelectedTrack(SearchTrackCriteria.Genre);
        }

        private void SearchBySelectedTrack(SearchTrackCriteria criteria)
        {
            if(playlistView.Selection.CountSelectedRows() <= 0) {
                return;
            }
            
            TrackInfo track = playlistView.SelectedTrackInfo;

            if(track == null) {
                return;
            }

            // suspend the search functionality (for performance reasons)
            suspendSearch = true;

            switch(criteria) {
                case SearchTrackCriteria.Album:
                    if (track.Album != null && track.Album != String.Empty) {
                        searchEntry.ActivateFilter((int)TrackFilterType.AlbumTitle);
                        searchEntry.Query = track.Album;
                    }
                    break;
                case SearchTrackCriteria.Artist:
                    if (track.Artist != null && track.Artist != String.Empty) {
                        searchEntry.ActivateFilter((int)TrackFilterType.ArtistName);
                        searchEntry.Query = track.Artist;
                    }
                    break;
                case SearchTrackCriteria.Genre:
                    if (track.Genre != null && track.Genre != String.Empty) {
                      searchEntry.ActivateFilter((int)TrackFilterType.Genre);
                      searchEntry.Query = track.Genre;
                    }
                    break;
            }

            suspendSearch = false;
            
            playlistView.HasFocus = true;
        }
        
        // --- Help Menu ---
        
        private void OnVersionInformationAction(object o, EventArgs args)
        {
            Banshee.Gui.Dialogs.VersionInformationDialog dialog = new Banshee.Gui.Dialogs.VersionInformationDialog();
            dialog.Run();
            dialog.Destroy();
        }
        
        private void OnAboutAction(object o, EventArgs args)
        {
            Banshee.Gui.Dialogs.AboutDialog about = new Banshee.Gui.Dialogs.AboutDialog();
            about.Run();
            about.Destroy();
        }
    }
}
