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
using System.Threading;
using System.Data;
using System.Collections;
using System.Reflection;
using Mono.Unix;
using Gtk;
using Gdk;
using Glade;
using System.IO;

using Sql;
using Banshee.Widgets;
using Banshee.Base;
using Banshee.MediaEngine;
using Banshee.Dap;
using Banshee.Sources;

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
        private VolumeButton volumeButton;
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
    
        public Gtk.Window Window {
            get {
                return WindowPlayer;
            }
        }
        
        private long plLoaderMax, plLoaderCount;
        private bool startupLoadReady = false;
        private bool tickFromEngine = false;
        private uint setPositionTimeoutId;
        private bool updateEnginePosition = true;
        private int clickX, clickY;

        private int dapDiskUsageTextViewState;
        
        private SpecialKeys special_keys;

        private static TargetEntry [] playlistViewSourceEntries = 
            new TargetEntry [] {
                Dnd.TargetPlaylistRows,
                Dnd.TargetLibraryTrackIds,
                Dnd.TargetUriList
            };
            
        private static TargetEntry [] playlistViewDestEntries = 
            new TargetEntry [] {
                Dnd.TargetPlaylistRows,
                Dnd.TargetUriList
            };

        private static TargetEntry [] sourceViewSourceEntries = 
            new TargetEntry [] {
                Dnd.TargetSource
            };
            
        private static TargetEntry [] sourceViewDestEntries = 
            new TargetEntry [] {
                Dnd.TargetLibraryTrackIds,
            };

        private RemotePlayer banshee_dbus_object;

        public PlayerUI() 
        {
            gxml = new Glade.XML(null, "banshee.glade", "WindowPlayer", null);
            gxml.Autoconnect(this);
            InterfaceElements.MainWindow = WindowPlayer;

            ResizeMoveWindow();
            BuildWindow();   
            
            Globals.DBusRemote = new DBusRemote();
            banshee_dbus_object = new RemotePlayer(Window, this);
            Globals.DBusRemote.RegisterObject(banshee_dbus_object, "Player");
            
            PlayerEngineCore.EventChanged += OnPlayerEngineEventChanged;
            PlayerEngineCore.StateChanged += OnPlayerEngineStateChanged;

            DapCore.DapAdded += OnDapCoreDeviceAdded;
            LogCore.Instance.Updated += OnLogCoreUpdated;
            ImportManager.Instance.ImportRequested += OnImportManagerImportRequested;
            
            InitialLoadTimeout();
            WindowPlayer.Show();

            // Bind available methods to actions defined in ActionManager
            Globals.ActionManager.DapActions.Visible = false;
            Globals.ActionManager.AudioCdActions.Visible = false;
            Globals.ActionManager.SourceEjectActions.Visible = false;
            Globals.ActionManager.SongActions.Sensitive = false;
            Globals.ActionManager.PlaylistActions.Sensitive = false;
            
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
            
            Globals.UIManager.SourceViewContainer = gxml["SourceViewContainer"] as Box;
            Globals.UIManager.Initialize();
        }
   
        private bool InitialLoadTimeout()
        {
            Globals.Library.Reloaded += OnLibraryReloaded;
            Globals.Library.ReloadLibrary();
            
            foreach(DapDevice device in DapCore.Devices) {
                 device.PropertiesChanged += OnDapPropertiesChanged;
                 device.SaveFinished += OnDapSaveFinished;
            }
            
            return false;
        }
          
        // ---- Setup/Initialization Routines ----
          
        private void ResizeMoveWindow()
        {
            int x = 0, y = 0, width = 0, height = 0;
              
            try {
                x = (int)Globals.Configuration.Get(GConfKeys.WindowX);
                y = (int)Globals.Configuration.Get(GConfKeys.WindowY); 
                width = (int)Globals.Configuration.Get(GConfKeys.WindowWidth);
                height = (int)Globals.Configuration.Get(GConfKeys.WindowHeight);
            } catch(GConf.NoSuchKeyException) {
                width = 800;
                height = 600;
                x = 0;
                y = 0;
            }

            if(width != 0 && height != 0) {
                WindowPlayer.Resize(width, height);
            }

            if(x == 0 && y == 0) {
                WindowPlayer.SetPosition(Gtk.WindowPosition.Center);
            } else {
                WindowPlayer.Move(x, y);
            }
            
            try {
                if((bool)Globals.Configuration.Get(GConfKeys.WindowMaximized)) {
                    WindowPlayer.Maximize();
                } else {
                    WindowPlayer.Unmaximize();
                }
            } catch(GConf.NoSuchKeyException) {
            }
        }
        
        private void OnWindowStateEvent(object o, WindowStateEventArgs args)
        {
            if((args.Event.NewWindowState & Gdk.WindowState.Withdrawn) == 0) {
                Globals.Configuration.Set(GConfKeys.WindowMaximized,
                    (args.Event.NewWindowState & Gdk.WindowState.Maximized) != 0);
            }
        }
          
        private void BuildWindow()
        {
            IconThemeUtils.SetWindowIcon(WindowPlayer);

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
            volumeButton = new VolumeButton();
            (gxml["RightToolbarContainer"] as Box).PackStart(volumeButton, false, false, 0);
            volumeButton.Show();
            volumeButton.VolumeChanged += OnVolumeScaleChanged;
            
            // Footer 
            LabelStatusBar = new Label(Catalog.GetString("Banshee Music Player"));
            LabelStatusBar.Show();
            
            // Old Shuffle Button
/*            ActionToggleButton shuffle_button = new ActionToggleButton(
                Globals.ActionManager["ShuffleAction"], IconSize.Menu);
            shuffle_button.IconSize = IconSize.Menu;
            shuffle_button.Relief = ReliefStyle.None;
            shuffle_button.ShowAll();*/

            // Repeat/Shuffle buttons
            
            shuffle_toggle_button = new MultiStateToggleButton();
            shuffle_toggle_button.AddState(typeof(ShuffleDisabledToggleState),
                    Globals.ActionManager["ShuffleAction"] as ToggleAction);
            shuffle_toggle_button.AddState(typeof(ShuffleEnabledToggleState),
                    Globals.ActionManager["ShuffleAction"] as ToggleAction);
            shuffle_toggle_button.Relief = ReliefStyle.None;
            shuffle_toggle_button.ShowLabel = false;
            shuffle_toggle_button.ShowAll();
            
            repeat_toggle_button = new MultiStateToggleButton();
            repeat_toggle_button.AddState(typeof(RepeatNoneToggleState),
                Globals.ActionManager["RepeatNoneAction"] as ToggleAction);
            repeat_toggle_button.AddState(typeof(RepeatAllToggleState),
                Globals.ActionManager["RepeatAllAction"] as ToggleAction);
            repeat_toggle_button.AddState(typeof(RepeatSingleToggleState),
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
            (gxml["LeftContainer"] as Box).PackStart(cover_art_view, false, false, 0);
            
            // Source View
            Label sourceViewLoading = new Label();
            sourceViewLoading.Yalign = 0.15f;
            sourceViewLoading.Xalign = 0.5f;
            sourceViewLoading.Markup = "<big><i>" + Catalog.GetString("Loading...") + "</i></big>";
            sourceViewLoadingVP = new Viewport();
            sourceViewLoadingVP.ShadowType = ShadowType.None;
            sourceViewLoadingVP.Add(sourceViewLoading);
            sourceViewLoadingVP.ShowAll();
            ((Gtk.ScrolledWindow)gxml["SourceContainer"]).Add(sourceViewLoadingVP);
            
            sourceView = new SourceView();
            sourceView.ButtonPressEvent += OnSourceViewButtonPressEvent;
            sourceView.Sensitive = false;
            SourceManager.ActiveSourceChanged += OnSourceManagerActiveSourceChanged;
            SourceManager.SourceUpdated += OnSourceManagerSourceUpdated;
            SourceManager.SourceViewChanged += OnSourceManagerSourceViewChanged;
            SourceManager.SourceTrackAdded += OnSourceTrackAdded;
            SourceManager.SourceTrackRemoved += OnSourceTrackRemoved;
            
            /*sourceView.EnableModelDragSource(
                Gdk.ModifierType.Button1Mask | Gdk.ModifierType.Button3Mask,
                sourceViewSourceEntries, 
                DragAction.Copy | DragAction.Move);*/
        
            sourceView.EnableModelDragDest(
                sourceViewDestEntries, 
                DragAction.Copy | DragAction.Move);

            InterfaceElements.MainContainer = gxml["MainContainer"] as VBox;

            // Playlist View
            playlistModel = new PlaylistModel();
            playlistView = new PlaylistView(playlistModel);
            InterfaceElements.PlaylistView = playlistView;
            ((Gtk.ScrolledWindow)gxml["LibraryContainer"]).Add(playlistView);
            InterfaceElements.PlaylistContainer = gxml["LibraryContainer"] as Container;
            playlistView.Show();
            playlistModel.Updated += OnPlaylistUpdated;
            playlistModel.Stopped += OnPlaylistStopped;
            playlistView.KeyPressEvent += OnPlaylistViewKeyPressEvent;
            playlistView.ButtonPressEvent += OnPlaylistViewButtonPressEvent;
            playlistView.MotionNotifyEvent += OnPlaylistViewMotionNotifyEvent;
            playlistView.ButtonReleaseEvent += OnPlaylistViewButtonReleaseEvent;
            playlistView.DragDataReceived += OnPlaylistViewDragDataReceived;
            playlistView.DragDataGet += OnPlaylistViewDragDataGet;
            playlistView.DragDrop += OnPlaylistViewDragDrop;
            playlistView.Selection.Changed += OnPlaylistViewSelectionChanged;
                
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
            WindowPlayer.WindowStateEvent += OnWindowStateEvent;
            
            // Search Entry
            ArrayList fields = new ArrayList();
            fields.Add(Catalog.GetString("All"));
            fields.Add("-");
            fields.Add(Catalog.GetString("Song Name"));
            fields.Add(Catalog.GetString("Artist Name"));
            fields.Add(Catalog.GetString("Album Title"));
            fields.Add(Catalog.GetString("Genre"));
            
            searchEntry = new SearchEntry(fields);
            searchEntry.EnterPress += delegate(object o, EventArgs args) {
                if(!SourceManager.ActiveSource.HandlesSearch) {
                    if(playlistView.Selection.CountSelectedRows() == 0 && playlistModel.Count() > 0) {
                        playlistView.Selection.SelectPath(new TreePath(new int [] { 0 }));
                    }
                    playlistView.HasFocus = true;
                }
            };
            searchEntry.Changed += OnSimpleSearch;
            searchEntry.Show();
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
        }
        
        private void SetTip(Widget widget, string tip)
        {
            toolTips.SetTip(widget, tip, tip);
        }
          
        private void LoadSettings()
        {    
            try {
                volumeButton.Volume = (int)Globals.Configuration.Get(GConfKeys.Volume);
            } catch(GConf.NoSuchKeyException) {
                volumeButton.Volume = 80;
            }

            PlayerEngineCore.Volume = (ushort)volumeButton.Volume;
            
            try {
                int state = (int)Globals.Configuration.Get(GConfKeys.PlaylistRepeat);
                
                foreach(RadioAction radio in (Globals.ActionManager["RepeatAllAction"] as RadioAction).Group) {
                    if(radio.Value == state) {
                        radio.Active = true;
                        break;
                    }
                }
            } catch(Exception) {}
            
            try {
                (Globals.ActionManager["ShuffleAction"] as ToggleAction).Active = 
                    (bool)Globals.Configuration.Get(GConfKeys.PlaylistShuffle);
            } catch(Exception) {}
            
            try {
                bool active = (bool)Globals.Configuration.Get(GConfKeys.ShowCoverArt);
                cover_art_view.Enabled = active;
                (Globals.ActionManager["ShowCoverArtAction"] as ToggleAction).Active = active;
            } catch(Exception) {}
            
            try {
                SourceSplitter.Position = (int)Globals.Configuration.Get(GConfKeys.SourceViewWidth);
            } catch(GConf.NoSuchKeyException) {
                SourceSplitter.Position = 125;
            }
        }
        
        private void PromptForImport()
        {
            PromptForImport(true);
        }
        
        private void PromptForImport(bool startup)
        {
            if(startup) {
                try {
                    if(!(bool)Globals.Configuration.Get(GConfKeys.ShowInitialImportDialog)) {
                        return;
                    }
                } catch {
                }
            }
            
            Banshee.Gui.ImportDialog dialog = new Banshee.Gui.ImportDialog(startup);
            ResponseType response = dialog.Run();
            IImportSource import_source = dialog.ActiveSource;
            
            if(startup) {
                Globals.Configuration.Set(GConfKeys.ShowInitialImportDialog, !dialog.DoNotShowAgain);
            }
            
            dialog.Destroy();
            
            if(response != ResponseType.Ok) {
                return;
            }
            
            if(import_source != null) {
                import_source.Import();
            }
        }

        public void SelectAudioCd(string device)
        {
            foreach(Source source in SourceManager.Sources) {
                AudioCdSource audiocd_source = source as AudioCdSource;
                if(audiocd_source == null) {
                    continue;
                }
                
                if(audiocd_source.Disk.DeviceNode == device || audiocd_source.Disk.Udi == device) {
                    SourceManager.SetActiveSource(audiocd_source);
                    return;
                }
            }
            
            SourceManager.SetActiveSource(LibrarySource.Instance);
        }
        
        public void SelectDap(string device)
        {
            foreach(Source source in SourceManager.Sources) {
                DapSource dap_source = source as DapSource;
                if(dap_source == null) {
                    continue;
                }
                
                if(dap_source.Device.HalUdi == device) {
                    SourceManager.SetActiveSource(dap_source);
                    return;
                }
            }
            
            SourceManager.SetActiveSource(LibrarySource.Instance);
        }
        
        private void LoadSourceView()
        {        
            sourceView.Sensitive = true;
            ((Gtk.ScrolledWindow)gxml["SourceContainer"]).Remove(sourceViewLoadingVP);
            ((Gtk.ScrolledWindow)gxml["SourceContainer"]).Add(sourceView);
            sourceView.Show();
            
            gxml["SearchLabel"].Sensitive = true;
            searchEntry.Sensitive = true;
        }
        
        private void OnLibraryReloaded(object o, EventArgs args)
        {
            LoadSourceView();
            
            SourceManager.AddSource(LibrarySource.Instance, true);
            PlaylistUtil.LoadSources();

            if(LocalQueueSource.Instance.Count > 0) {
                SourceManager.AddSource(LocalQueueSource.Instance);
                SourceManager.SetActiveSource(LocalQueueSource.Instance);
            } else if(Globals.ArgumentQueue.Contains("audio-cd")) {
                SelectAudioCd(Globals.ArgumentQueue.Dequeue("audio-cd"));
            } else if(Globals.ArgumentQueue.Contains("dap")) {
                SelectDap(Globals.ArgumentQueue.Dequeue("dap"));
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
            
            if(Globals.Library.Tracks.Count <= 0) {
                Application.Invoke(delegate { 
                    PromptForImport();
                });
            }
        }
        
        private bool PromptForImportTimeout()
        {
            LoadSourceView();
            PromptForImport();
            
            return false;
        }
        
        // ---- Misc. Utility Routines ----
      
        public void Quit()
        {
            ActiveUserEventsManager.Instance.CancelAll();
            playlistView.Shutdown();
            PlayerEngineCore.Dispose();
            Globals.Configuration.Set(GConfKeys.SourceViewWidth, SourceSplitter.Position);
            Globals.DBusRemote.UnregisterObject(banshee_dbus_object);
            PlayerCore.Dispose();
            Globals.Dispose();
            Application.Quit();
        }

        public void UpdateMetaDisplay()
        {
            TrackInfo track = PlayerEngineCore.CurrentTrack;
            
            if(track == null) {
                WindowPlayer.Title = Catalog.GetString("Banshee Music Player");
                trackInfoHeader.Visible = false;
                return;
            }
        
            trackInfoHeader.Artist = track.DisplayArtist;
            trackInfoHeader.Title = track.DisplayTitle;
            trackInfoHeader.Album = track.DisplayAlbum;
            
            trackInfoHeader.Visible = true;
            
            WindowPlayer.Title = track.DisplayTitle + " (" + track.DisplayArtist + ")";
            
            try {
                trackInfoHeader.Cover.FileName = track.CoverArtFileName;
                cover_art_view.FileName = track.CoverArtFileName;
                trackInfoHeader.Cover.Label = String.Format("{0} - {1}", track.DisplayArtist, track.DisplayAlbum);
            } catch(Exception) {
            }
            
        }
        
        // ---- Window Event Handlers ----
        
        private void OnWindowPlayerDeleteEvent(object o, DeleteEventArgs args) 
        {
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
            
            Globals.Configuration.Set(GConfKeys.WindowX, x);
            Globals.Configuration.Set(GConfKeys.WindowY, y);
            Globals.Configuration.Set(GConfKeys.WindowWidth, width);
            Globals.Configuration.Set(GConfKeys.WindowHeight, height);
        }
        
        private bool accel_group_active = true;
        
        [GLib.ConnectBefore]
        private void OnKeyPressEvent(object o, KeyPressEventArgs args)
        {
            bool handled = false;
            
            if(WindowPlayer.Focus is Entry && Gtk.Global.CurrentEvent is Gdk.EventKey) {
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
            
            switch(args.Event.Key) {
                case Gdk.Key.J:
                case Gdk.Key.j:
                case Gdk.Key.S:
                case Gdk.Key.s:
                case Gdk.Key.F3:
                    if(!searchEntry.HasFocus && !sourceView.EditingRow) {
                        searchEntry.Focus();
                        handled = true;
                    }
                    break;
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
            Globals.Configuration.Set(GConfKeys.Volume, volume);
        }
        
        private void OnSeekRequested(object o, EventArgs args)
        {
            PlayerEngineCore.Position = (uint)seek_slider.Value;
        }
   
        // ---- Player Event Handlers ----
        
        private void OnPlayerEngineStateChanged(object o, PlayerEngineStateArgs args)
        {
            switch(args.State) {
                case PlayerEngineState.Loaded:
                    incrementedCurrentSongPlayCount = false;
                    seek_slider.Duration = PlayerEngineCore.CurrentTrack.Duration.TotalSeconds;
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
                    seek_slider.SetIdle();
                    trackInfoHeader.SetIdle();
                    
                    UpdateMetaDisplay();
                    
                    break;
                case PlayerEngineState.Paused:
                    Globals.ActionManager.UpdateAction("PlayPauseAction", Catalog.GetString("Play"), "media-playback-start");
                    break;
                case PlayerEngineState.Playing:
                    Globals.ActionManager.UpdateAction("PlayPauseAction", Catalog.GetString("Pause"), "media-playback-pause");
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
                    playlistModel.Continue();
                    playlistView.UpdateView();
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
                    break;
                case PlayerEngineEvent.Error:
                    LogCore.Instance.PushError(Catalog.GetString("Playback Error"), args.Message);
                    UpdateMetaDisplay();
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
            
            if(!have_selection) {
                Globals.ActionManager.SongActions.Sensitive = false;
                return;
            }
            
            Source source = SourceManager.ActiveSource;

            if(source == null) {
                return;
            }
            
            Globals.ActionManager.SongActions.Sensitive = true;
            Globals.ActionManager["WriteCDAction"].Sensitive = !(source is AudioCdSource);
            Globals.ActionManager["RemoveSongsAction"].Sensitive = !(source is AudioCdSource);
            Globals.ActionManager["DeleteSongsFromDriveAction"].Sensitive = 
                !(source is AudioCdSource || source is DapSource);
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
                
                if(playlistModel.Count() == 0 && args.Source.Count > 0) {
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
            
            gxml["PlaylistHeaderBox"].Show();
        }
        
        private void ShowSourceWidget()
        {
            Alignment alignment = gxml["LibraryAlignment"] as Alignment;
            
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
            
            gxml["PlaylistHeaderBox"].Visible = SourceManager.ActiveSource.ShowPlaylistHeader;
        }
        
        private void OnSourceTrackAdded(object o, TrackEventArgs args)
        {
            if(SourceManager.ActiveSource == o) {
                if(searchEntry.IsQueryAvailable && !DoesTrackMatchSearch(args.Track)) {
                    return;
                }
                
                playlistModel.AddTrack(args.Track);
            }
        }
        
        private void OnSourceTrackRemoved(object o, TrackEventArgs args)
        {
            if(SourceManager.ActiveSource == o) {
                playlistModel.RemoveTrack(args.Track);
            }
        }
        
        private void UpdateViewName(Source source)
        {
            (gxml["ViewNameLabel"] as Label).Markup = "<b>" + GLib.Markup.EscapeText(source.Name) + "</b>";
        }
        
        private void SensitizeActions(Source source)
        {
            Globals.ActionManager["WriteCDAction"].Visible = !(source is AudioCdSource);
            Globals.ActionManager.AudioCdActions.Visible = source is AudioCdSource;
            Globals.ActionManager["RenameSourceAction"].Sensitive = source.CanRename;
            Globals.ActionManager.PlaylistActions.Sensitive = source is PlaylistSource;
            Globals.ActionManager.SourceEjectActions.Visible = source.CanEject;
            Globals.ActionManager.DapActions.Visible = source is DapSource;
            Globals.ActionManager["SelectedSourcePropertiesAction"].Sensitive = source.HasProperties;
            
            if(source is IImportSource) {
                Globals.ActionManager["ImportSourceAction"].Visible = source is IImportSource;
                Globals.ActionManager["ImportSourceAction"].Label = Catalog.GetString("Import") + " '" + 
                    source.Name + "'";
            } else {
                Globals.ActionManager["ImportSourceAction"].Visible = false;
            }
            
            if(source is DapSource) {
                DapSource dapSource = source as DapSource;
                if (dapSource.Device.CanSynchronize) {
                    Globals.ActionManager["SyncDapAction"].Sensitive = !dapSource.Device.IsReadOnly;
                    Globals.ActionManager.SetActionLabel("SyncDapAction", String.Format("{0} {1}",
                        Catalog.GetString("Synchronize"), dapSource.Device.GenericName));
                } else {
                    Globals.ActionManager["SyncDapAction"].Visible = false;
                }

                Globals.ActionManager["RenameSourceAction"].Label = Catalog.GetString("Rename Device");
            } else {
                Globals.ActionManager["RenameSourceAction"].Label = Catalog.GetString("Rename Playlist");            
            }
        }
     
        // Called when SourceManager emits an ActiveSourceChanged event.
        private void HandleSourceChanged(object o, EventArgs args)
        {
            Source source = SourceManager.ActiveSource;
            if(source == null) {
                return;
            }

            searchEntry.CancelSearch();
            
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
            playlistView.RatingColumn.Hidden = (source is AudioCdSource);
            playlistView.PlaysColumn.Hidden = (source is AudioCdSource);
            playlistView.LastPlayedColumn.Hidden = (source is AudioCdSource);
                
            UpdateSourceView();
                
            OnPlaylistViewSelectionChanged(playlistView.Selection, new EventArgs());
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
            Application.Invoke(delegate {
                DapDevice device = o as DapDevice;
                
                foreach(object [] obj in (sourceView.Model as ListStore)) {
                    if(obj[0] is DapSource && (obj[0] as DapSource).Device == device) {
                        (obj[0] as DapSource).SetSourceName(device.Name);
                        sourceView.QueueDraw();
                    }
                }
                
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

        private void OnPlaylistUpdated(object o, EventArgs args)
        {
            long count = playlistModel.Count();
            TimeSpan span = playlistModel.TotalDuration;       
            string timeDisp = String.Empty;
            
            if(span.Days > 0) {
                timeDisp = String.Format(Catalog.GetPluralString("{0} day", "{0} days", span.Days), 
                    span.Days) + " ";
            }
            
            if(span.Days > 0 || span.Hours > 0) {
                timeDisp += String.Format("{0}:{1}:{2}", span.Hours, span.Minutes.ToString("00"), 
                    span.Seconds.ToString("00"));
            } else {   
                timeDisp += String.Format("{0}:{1}", span.Minutes, span.Seconds.ToString("00"));
            }
            
            ThreadAssist.ProxyToMain(delegate {
                if(count == 0 && SourceManager.ActiveSource == null) {
                    LabelStatusBar.Text = Catalog.GetString("Banshee Music Player");
                } else if(count == 0) {
                    LabelStatusBar.Text = String.Empty;
                } else {
                    string text = String.Format(Catalog.GetPluralString("{0} Item", "{0} Items", 
                        (int)count), count) + ", ";
                    text += String.Format(Catalog.GetString("{0} Total Play Time"), timeDisp);
                    LabelStatusBar.Text = text;
                }
            });
        }
        
        private void OnLogCoreUpdated(object o, LogCoreUpdatedArgs args)
        {
            if(!args.Entry.ShowUser || args.Entry.Type == LogEntryType.Debug) {
                return;
            }
            
            MessageType mtype;
            
            switch(args.Entry.Type) {
                case LogEntryType.Warning:
                    mtype = MessageType.Warning;
                    break;
                case LogEntryType.Error:
                default:
                    mtype = MessageType.Error;
                    break;
            }
              
            HigMessageDialog dialog = new HigMessageDialog(WindowPlayer, 
                DialogFlags.Modal,
                mtype,
                ButtonsType.Ok,
                args.Entry.ShortMessage,
                args.Entry.Details);
            
            dialog.Title = args.Entry.ShortMessage;
            IconThemeUtils.SetWindowIcon(dialog);
            
            dialog.Response += delegate(object o, ResponseArgs args)
            {
                (o as Dialog).Destroy();
            };
            
            dialog.ShowAll();
        }
     
        private uint popupTime;
        private Menu source_menu = null;
        
        [GLib.ConnectBefore]
        private void OnSourceViewButtonPressEvent(object o, ButtonPressEventArgs args)
        {
            if(args.Event.Button != 3) {
                return;
            }
                
            TreePath path;
            if(!sourceView.GetPathAtPos((int)args.Event.X, (int)args.Event.Y, out path)) {
                args.RetVal = true; 
                return;
            }
            
            sourceView.HighlightPath(path);
            Source source = sourceView.GetSource(path);
            
            if(source is LibrarySource) {
                args.RetVal = true;
                return;
            }

            SensitizeActions(source);
            
            if(source_menu == null) {
                source_menu = Globals.ActionManager.GetWidget("/SourceMenu") as Menu;
                source_menu.SelectionDone += delegate(object o, EventArgs args) {
                    SensitizeActions(SourceManager.ActiveSource);
                    sourceView.ResetHighlight();
                };
            }
            
            source_menu.Popup(null, null, null, 0, args.Event.Time);
            source_menu.Show();
            
            args.RetVal = true;
        }
      
        private bool DoesTrackMatchSearch(TrackInfo ti)
        {
            if(!searchEntry.IsQueryAvailable) {
                return false;
            }
            
            string query = searchEntry.Query.ToLower();
            string field = searchEntry.Field;
            string match = null;
            
            if(field == Catalog.GetString("Artist Name")) {
                match = ti.Artist;
            } else if(field == Catalog.GetString("Song Name")) {
                match = ti.Title;
            } else if(field == Catalog.GetString("Album Title")) {
                match = ti.Album;
            } else if(field == Catalog.GetString("Genre")) {
                match = ti.Genre;
            } else {
                string [] matches = {
                    ti.Artist,
                    ti.Album,
                    ti.Title,
                    ti.Genre
                };

                foreach(string m in matches) {
                    if(m == null || m == String.Empty) {
                        continue;
                    }

                    string ml = m.ToLower();
                    if(ml.IndexOf(query) >= 0 || ml.IndexOf("the " + query) >= 0) {
                        return true;
                    }
                }
                
                return false;
            }
                    
            match = match.ToLower();
            return match.IndexOf(query) >= 0 || match.IndexOf("the " + query) >= 0;
        }
        
        private void OnSimpleSearch(object o, EventArgs args)
        {
            if(SourceManager.ActiveSource.HandlesSearch) {
                return;
            }
            
            playlistModel.Clear();
            
            if(!searchEntry.IsQueryAvailable) {
                playlistModel.ReloadSource();
                return;
            }
            
            foreach(TrackInfo track in SourceManager.ActiveSource.Tracks) {
                try {
                    if(DoesTrackMatchSearch(track)) {
                        playlistModel.AddTrack(track);
                    }
                } catch(Exception) {
                    continue;
                }
            }
            
            playlistView.UpdateView();
        }
        
        // PlaylistView DnD
        
        [GLib.ConnectBefore]
        private void OnPlaylistViewButtonPressEvent(object o, 
            ButtonPressEventArgs args)
        {
            if (args.Event.Window != playlistView.BinWindow)
                return;

            if(args.Event.Button == 3) {
                //GLib.Timeout.Add(10, 
                //    new GLib.TimeoutHandler(PlaylistMenuPopupTimeout));
                PlaylistMenuPopupTimeout(args.Event.Time);
            }
            
            TreePath path;
            playlistView.GetPathAtPos((int)args.Event.X, 
                (int)args.Event.Y, out path);
        
            if(path == null)
                return;
            
            clickX = (int)args.Event.X;
            clickY = (int)args.Event.Y;
        
            switch(args.Event.Type) {
                case EventType.TwoButtonPress:
                    if(args.Event.Button != 1
                        || (args.Event.State &  (ModifierType.ControlMask 
                        | ModifierType.ShiftMask)) != 0)
                        return;
                    playlistView.Selection.UnselectAll();
                    playlistView.Selection.SelectPath(path);
                    playlistView.PlayPath(path);
                    return;
                case EventType.ButtonPress:
                    if(playlistView.Selection.PathIsSelected(path) &&
                   (args.Event.State & (ModifierType.ControlMask |
                            ModifierType.ShiftMask)) == 0)
                        args.RetVal = true;
                    return;
                default:
                    args.RetVal = false;
                    return;
            }
        }

        [GLib.ConnectBefore]
        private void OnPlaylistViewMotionNotifyEvent(object o, 
            MotionNotifyEventArgs args)
        {
            if((args.Event.State & ModifierType.Button1Mask) == 0)
                return;
            if(args.Event.Window != playlistView.BinWindow)
                return;
                    
            args.RetVal = true;
            if(!Gtk.Drag.CheckThreshold(playlistView, clickX, clickY,
                            (int)args.Event.X, (int)args.Event.Y))
                return;
            TreePath path;
            if (!playlistView.GetPathAtPos((int)args.Event.X, 
                               (int)args.Event.Y, out path))
                return;

           if(SourceManager.ActiveSource is AudioCdSource)
              return;
              
            Gtk.Drag.Begin(playlistView, new TargetList (playlistViewSourceEntries),
                       Gdk.DragAction.Move | Gdk.DragAction.Copy, 1, args.Event);
        }

        private void OnPlaylistViewButtonReleaseEvent(object o, 
            ButtonReleaseEventArgs args)
        {
            if(!Gtk.Drag.CheckThreshold(playlistView, clickX, clickY,
                            (int)args.Event.X, (int)args.Event.Y) &&
               ((args.Event.State & (ModifierType.ControlMask 
                         | ModifierType.ShiftMask)) == 0) &&
               playlistView.Selection.CountSelectedRows() > 1) {
                TreePath path;
                playlistView.GetPathAtPos((int)args.Event.X, 
                              (int)args.Event.Y, out path);
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
            
            SourceManager.AddSource(playlist);
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
        private MenuItem rating_menu_item = null;
        
        private bool PlaylistMenuPopupTimeout(uint time)
        {
            if(song_popup_menu == null) {
                song_popup_menu = Globals.ActionManager.GetWidget("/SongViewPopup") as Menu;
                add_to_playlist_menu_item = Globals.ActionManager.GetWidget(
                    "/SongViewPopup/AddToPlaylist") as MenuItem;
                rating_menu_item = Globals.ActionManager.GetWidget("/SongViewPopup/Rating") as MenuItem;
            }
          
            bool sensitive = playlistView.Selection.CountSelectedRows() > 0;

            if(sensitive && (SourceManager.ActiveSource is LibrarySource || SourceManager.ActiveSource is PlaylistSource)) {
                Globals.ActionManager["AddToPlaylistAction"].Visible = true;
                Globals.ActionManager["RatingAction"].Visible = true;
            
                Menu plMenu = new Menu();
                playlistMenuMap.Clear();
                
                ImageMenuItem newPlItem = new ImageMenuItem(Catalog.GetString("New Playlist"));
                newPlItem.Image = new Gtk.Image("gtk-new", IconSize.Menu);
                newPlItem.Activated += OnNewPlaylistFromSelectionActivated;
                plMenu.Append(newPlItem);
                
                if(PlaylistSource.PlaylistCount > 0) {
                    plMenu.Append(new SeparatorMenuItem());
                    
                    foreach(PlaylistSource playlist in PlaylistSource.Playlists) {
                        ImageMenuItem item = new ImageMenuItem(playlist.Name);
                        item.Image = new Gtk.Image(Pixbuf.LoadFromResource("source-playlist.png"));
                        item.Activated += OnItemAddToPlaylistActivated;
                        playlistMenuMap[item] = playlist;
                        plMenu.Append(item);
                    }
                }
                
                Menu ratingMenu = new Menu();
                
                MenuItem clearItem = new MenuItem(Catalog.GetString("Clear"));
                clearItem.Name = "0";
                clearItem.Activated += OnItemRatingActivated;
                
                ratingMenu.Append(clearItem);
                ratingMenu.Append(new SeparatorMenuItem());
                
                for(int i = 0; i < 5; i++) {
                    MenuItem item = new MenuItem();
                    HBox box = new HBox();
                    box.Spacing = 3;
                    
                    for(int j = 0; j < i + 1; j++) {
                        box.PackStart(new Gtk.Image(RatingRenderer.Star), false, false, 0);
                    }
                    
                    item.Add(box);
                    item.Name = String.Format("{0}", i + 1);
                    item.Activated += OnItemRatingActivated;
                    ratingMenu.Append(item);
                }
                
                add_to_playlist_menu_item.Submenu = plMenu;
                rating_menu_item.Submenu = ratingMenu;
                
                plMenu.ShowAll();
                ratingMenu.ShowAll();
            } else {
                Globals.ActionManager["AddToPlaylistAction"].Visible = false;
                Globals.ActionManager["RatingAction"].Visible = false;
            }
        
            song_popup_menu.ShowAll();
            song_popup_menu.Popup(null, null, null, 0, time);
            
            return false;
        }

        private void OnItemRatingActivated(object o, EventArgs args)
        {
            uint rating = Convert.ToUInt32((o as Widget).Name);
            foreach(TreePath path in playlistView.Selection.GetSelectedRows())
                playlistModel.PathTrackInfo(path).Rating = rating;
            playlistView.QueueDraw();
        }

        private void OnPlaylistViewDragDataReceived(object o, 
            DragDataReceivedArgs args)
        {
            TreePath destPath;
            TreeIter destIter;
            TreeViewDropPosition pos;
            bool haveDropPosition;
            
            string rawSelectionData = 
                Dnd.SelectionDataToString(args.SelectionData);            
            
            haveDropPosition = playlistView.GetDestRowAtPos(args.X, 
                args.Y, out destPath, out pos);
            
            if(haveDropPosition && 
                !playlistModel.GetIter(out destIter, destPath)) {
                Gtk.Drag.Finish(args.Context, true, false, args.Time);
                return;
            }

            switch(args.Info) {
                case (uint)Dnd.TargetType.UriList:
                    // AddFile needs to accept a Path for inserting
                    // If in Library view, we just append to Library
                    // If in Playlist view, we append Library *AND* PlayList
                    // If in SmartPlaylist View WE DO NOT ACCEPT DND
                
                    if(rawSelectionData != null) {
                        ImportManager.Instance.QueueSource(args.SelectionData);
                    }
                        
                    break;
                case (uint)Dnd.TargetType.PlaylistRows:
                    if(!haveDropPosition)
                        break;
                    
                    string [] paths = Dnd.SplitSelectionData(rawSelectionData);
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
            byte [] selData;
            
            switch(args.Info) {
                case (uint)Dnd.TargetType.PlaylistRows:                
                    selData = Dnd.TreeViewSelectionPathsToBytes(playlistView);
                    if(selData == null)
                        return;
                    
                    args.SelectionData.Set(
                        Gdk.Atom.Intern(Dnd.TargetPlaylistRows.Target, 
                        false), 8, selData);
                        
                    break;
                case (uint)Dnd.TargetType.LibraryTrackIds:
                    selData = Dnd.PlaylistSelectionTrackIdsToBytes(playlistView);
                    if(selData == null)
                        return;
                    
                    args.SelectionData.Set(
                        Gdk.Atom.Intern(Dnd.TargetLibraryTrackIds.Target,
                        false), 8, selData);
                        
                    break;
                case (uint)Dnd.TargetType.UriList:
                    selData = Dnd.PlaylistViewSelectionUrisToBytes(playlistView);
                    if(selData == null)
                        return;
            
                    args.SelectionData.Set(args.Context.Targets[0],
                        8, selData, selData.Length);
                        
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
        
        private void EjectSource(Source source)
        {
            if(source.CanEject) {
                try {
                    if(source.GetType() == typeof(DapSource)) {
                        if(PlayerEngineCore.CurrentTrack != null && PlayerEngineCore.CurrentTrack is DapTrackInfo) {
                            PlayerEngineCore.Close();
                        }
                    }
                    
                    if(!source.Eject()) {
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
        }
       
        private void OnImportManagerImportRequested(object o, ImportEventArgs args)
        {
            try {
                TrackInfo ti = new LibraryTrackInfo(args.FileName);
                args.ReturnMessage = String.Format("{0} - {1}", ti.Artist, ti.Title);
            } catch(Exception e) {
                args.ReturnMessage = Catalog.GetString("Scanning") + "...";
                
                switch(Path.GetExtension(args.FileName)) {
                    case ".m3u":
                    case ".jpg":
                    case ".jpeg":
                    case ".png":
                    case ".bmp":
                    case ".gif":
                        return;
                }
                
                if(e is ApplicationException) {
                    return;
                }
            
                Console.WriteLine(Catalog.GetString("Cannot Import: {0} ({1})"), args.FileName, e.GetType());
            }
        }
        
        private void DeleteSong(TrackInfo ti)
        {
            try {
                File.Delete(ti.Uri.LocalPath);
            } catch(Exception) {
                Console.WriteLine("Could not delete file: " + ti.Uri.LocalPath);
            }

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
                string msg = String.Empty;
                
                if(deleteFromFileSystem) {
                    msg = String.Format(
                    Catalog.GetPluralString(
                        "Are you sure you want to remove the selected song from your library <i><b>and</b></i> " +
                        "your drive? This action will permanently delete the file.",
                        "Are you sure you want to remove the selected <b>({0})</b> songs from your library " + 
                        "<i><b>and</b></i> your drive? This action will permanently delete the files.",
                        selCount),
                    selCount);
                } else {
                    msg = String.Format(
                    Catalog.GetPluralString(
                        "Are you sure you want to remove the selected song from your library?",
                        "Are you sure you want to remove the selected <b>({0})</b> songs from your library?",
                        selCount),
                    selCount);
                }
                    
                HigMessageDialog md = new HigMessageDialog(WindowPlayer, 
                    DialogFlags.DestroyWithParent, MessageType.Warning,
                    ButtonsType.YesNo,
                    Catalog.GetString("Remove Selected Songs from Library"),
                    msg);
                if(md.Run() != (int)ResponseType.Yes) {
                    md.Destroy();
                    return;
                }
        
                md.Destroy();
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
                SourceManager.ActiveSource.RemoveTrack(track);
                playlistModel.Remove(ref iters[i]);
                
                if(deleteFromFileSystem) {
                    DeleteSong(track);
                }
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
            SourceManager.AddSource(playlist);
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
                PlayerEngineCore.Open(new Uri(address));
                PlayerEngineCore.Play();
            } catch(Exception) {
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
            if(playlistView.Selection.CountSelectedRows() <= 0) {
                return;
            }
            
            string drive_id = null;
            try {
                drive_id = (string)Globals.Configuration.Get(GConfKeys.CDBurnerId);
            } catch {
            }
            
            BurnCore.DiskType disk_type = BurnCore.DiskType.Audio;
            
            try {
                string key = GConfKeys.CDBurnerRoot + drive_id + "/DiskFormat";
                disk_type = (BurnCore.DiskType)Globals.Configuration.Get(key);
            } catch {
            }
            
            BurnCore burnCore = new BurnCore(disk_type);
        
            foreach(TreePath path in playlistView.Selection.GetSelectedRows()) {
                burnCore.AddTrack(playlistModel.PathTrackInfo(path));
            }
            
            burnCore.Burn();
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
                    "may cause incompatability with iTunes!")) : ""),
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
        
        private void OnEjectSelectedSourceAction(object o, EventArgs args)
        {
            EjectSource(sourceView.HighlightedSource);
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
            
            if(source == null || !source.CanRename) {
                 return;
            }
            
            InputDialog input;
            
            if(source is PlaylistSource) {
                input = new InputDialog(
                    Catalog.GetString("Rename Playlist"),
                    Catalog.GetString("Enter new playlist name"),
                    Gdk.Pixbuf.LoadFromResource("playlist-icon-large.png"), source.Name);
            } else if(source is DapSource) {
                DapSource dap_source = source as DapSource;
                input = new InputDialog(
                    Catalog.GetString("Rename Device"),
                    Catalog.GetString("Enter new name for your device"),
                    dap_source.Device.GetIcon(48), source.Name);
            } else {
                return;
            }
                
            string newName = input.Execute();
            if(newName != null) {
                source.Rename(newName);
            }
            
            sourceView.QueueDraw();
        }

        private void OnDeletePlaylistAction(object o, EventArgs args)
        {
            Source source = sourceView.HighlightedSource;
            
            if(source == null || !(source is PlaylistSource)) {
                return;
            }
                
            PlaylistSource playlist = source as PlaylistSource;
            playlist.Delete();
            // TODO: sourceView.SelectLibrary();
        }
        
        private void OnSelectAllAction(object o, EventArgs args)
        {
            // Don't steal "Ctrl+A" from the search entry
            if (WindowPlayer.Focus is Entry &&
                Gtk.Global.CurrentEvent is Gdk.EventKey) {
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
            TrackProperties propEdit = new TrackProperties(playlistView.SelectedTrackInfoMultiple);
            propEdit.Saved += delegate(object o, EventArgs args) {
                playlistView.QueueDraw();
            };
        }
                
        private void OnPluginsAction(object o, EventArgs args)
        {
            Banshee.Plugins.PluginCore.ShowPluginDialog();
        }
        
        private void OnPreferencesAction(object o, EventArgs args)
        {
            new PreferencesWindow();
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
        }
        
        private void SetRepeatMode(RepeatMode mode)
        {
            playlistModel.Repeat = mode;
            Globals.Configuration.Set(GConfKeys.PlaylistRepeat, (int)mode);
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
            Globals.Configuration.Set(GConfKeys.PlaylistShuffle, action.Active);
        }
        
        // --- View Menu ---
        
        private void OnShowCoverArtAction(object o, EventArgs args)
        {
            ToggleAction action = o as ToggleAction;
            cover_art_view.Enabled = action.Active;
            Globals.Configuration.Set(GConfKeys.ShowCoverArt, action.Active);
        }
        
        private bool is_fullscreen = false;
        private void OnFullScreenAction(object o, EventArgs args)
        {
            if(is_fullscreen) {
                WindowPlayer.Unfullscreen();
                is_fullscreen = false;
            } else {
                WindowPlayer.Fullscreen();
                is_fullscreen = true;
            } 
        }
        
        private void OnColumnsAction(object o, EventArgs args)
        {
            playlistView.ColumnChooser();
        }
        
        private LogCoreViewer log_viewer = null;
        private void OnLoggedEventsAction(object o, EventArgs args)
        {
            if(log_viewer == null) {
                log_viewer = new LogCoreViewer(LogCore.Instance, WindowPlayer);
                
                log_viewer.Response += delegate(object o, ResponseArgs args) {
                    log_viewer.Hide();
                };
                
                log_viewer.DeleteEvent += delegate(object o, DeleteEventArgs args) {
                    log_viewer.Destroy();
                    log_viewer = null;
                };
            }
            
            log_viewer.Show();
        }
        
        // --- Help Menu ---
        
        private void OnVersionInformationAction(object o, EventArgs args)
        {
            VersionInformationDialog dialog = new VersionInformationDialog();
            dialog.Run();
            dialog.Destroy();
        }
        
        private void OnAboutAction(object o, EventArgs args)
        {
            BansheeAboutDialog about = new BansheeAboutDialog();
            about.Run();
            about.Destroy();
        }
    }
}
