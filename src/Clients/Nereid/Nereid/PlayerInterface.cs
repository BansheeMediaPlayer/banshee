//
// PlayerInterface.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//   Gabriel Burt <gburt@novell.com>
//
// Copyright 2007-2010 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Linq;
using System.Collections.Generic;
using Mono.Unix;
using Gtk;

using Hyena;
using Hyena.Gui;
using Hyena.Data;
using Hyena.Data.Gui;
using Hyena.Widgets;

using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Database;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.MediaEngine;
using Banshee.Configuration;

using Banshee.Gui;
using Banshee.Gui.Widgets;
using Banshee.Gui.Dialogs;
using Banshee.Widgets;
using Banshee.Collection.Gui;
using Banshee.Sources.Gui;

namespace Nereid
{
    public class PlayerInterface : BaseClientWindow, IClientWindow, IDBusObjectName, IService, IDisposable, IHasSourceView
    {
        // Major Layout Components
        private VBox primary_vbox;
        private Table header_table;
        private MainMenu main_menu;
        private Toolbar header_toolbar;
        private HBox footer_toolbar;
        private HPaned views_pane;
        private ViewContainer view_container;
        private VBox source_box;
        private Widget track_info_container;
        private CoverArtDisplay cover_art_display;
        private Widget cover_art_container;
        private ConnectedSeekSlider seek_slider;

        // Major Interaction Components
        private SourceView source_view;
        private CompositeTrackSourceContents composite_view;
        private ObjectListSourceContents object_view;
        private Label status_label;

        public MainMenu MainMenu {
            get { return main_menu; }
        }

        public Toolbar HeaderToolbar {
            get { return header_toolbar; }
        }

        public Table HeaderTable {
            get { return header_table; }
        }

        protected PlayerInterface (IntPtr ptr) : base (ptr)
        {
        }

        private int? original_seek_width;
        private void SetSimple (bool simple)
        {
            var widgets = new Widget [] { main_menu, source_box, footer_toolbar, track_info_container };
            foreach (var w in widgets.Where (w => w != null)) {
                w.Visible = !simple;
            }

            original_seek_width = original_seek_width ?? seek_slider.SeekSlider.WidthRequest;
            seek_slider.SeekSlider.WidthRequest = original_seek_width.Value + (simple ? 100 : 0);
        }

        public PlayerInterface () : base (Catalog.GetString ("Banshee Media Player"), "player_window", 1024, 700)
        {
            // if (PlatformDetection.IsMeeGo) {
            //     Gdk.Window.AddFilterForAll (OnGdkEventFilter);
            // }
        }

        protected override void Initialize ()
        {
            InitialShowPresent ();
        }

        private bool interface_constructed;

        protected override void OnShown ()
        {
            if (interface_constructed) {
                base.OnShown ();
                return;
            }

            interface_constructed = true;
            uint timer = Log.DebugTimerStart ();

            if (PlatformDetection.IsMeeGo) {
                Decorated = false;
                Maximize ();
            }

            BuildPrimaryLayout ();
            ConnectEvents ();

            ActionService.SourceActions.SourceView = this;
            composite_view.TrackView.HasFocus = true;
            OnActiveSourceChanged (null);

            Log.DebugTimerPrint (timer, "Constructed Nereid interface: {0}");

            base.OnShown ();
        }

#region System Overrides

        public override void Dispose ()
        {
            lock (this) {
                Hide ();
                base.Dispose ();
                Gtk.Application.Quit ();
            }
        }

#endregion

#region Interface Construction

        private void BuildPrimaryLayout ()
        {
            primary_vbox = new VBox ();

            BuildHeader ();
            BuildViews ();
            BuildFooter ();

            primary_vbox.Show ();
            Add (primary_vbox);
        }

        private void BuildHeader ()
        {
            header_table = new Table (2, 2, false);
            header_table.Show ();
            primary_vbox.PackStart (header_table, false, false, 0);

            main_menu = new MainMenu ();

            if (!PlatformDetection.IsMac && !PlatformDetection.IsMeeGo) {
                main_menu.Show ();
                header_table.Attach (main_menu, 0, 1, 0, 1,
                    AttachOptions.Expand | AttachOptions.Fill,
                    AttachOptions.Shrink, 0, 0);
            }

            Alignment toolbar_alignment = new Alignment (0.0f, 0.0f, 1.0f, 1.0f);
            toolbar_alignment.TopPadding = PlatformDetection.IsMeeGo ? 0u : 3u;
            toolbar_alignment.BottomPadding = PlatformDetection.IsMeeGo ? 0u : 3u;

            header_toolbar = (Toolbar)ActionService.UIManager.GetWidget ("/HeaderToolbar");
            header_toolbar.ShowArrow = false;
            header_toolbar.ToolbarStyle = ToolbarStyle.BothHoriz;
            header_toolbar.Show ();

            if (PlatformDetection.IsMeeGo) {
                header_toolbar.IconSize = IconSize.LargeToolbar;
                header_toolbar.Name = "meego-toolbar";
            }

            toolbar_alignment.Add (header_toolbar);
            toolbar_alignment.Show ();

            header_table.Attach (toolbar_alignment, 0, 2, 1, 2,
                AttachOptions.Expand | AttachOptions.Fill,
                AttachOptions.Shrink, 0, 0);

            var next_button = new NextButton (ActionService, PlatformDetection.IsMeeGo);
            next_button.Show ();
            ActionService.PopulateToolbarPlaceholder (header_toolbar, "/HeaderToolbar/NextArrowButton", next_button);

            seek_slider = new ConnectedSeekSlider ();
            seek_slider.Show ();
            ActionService.PopulateToolbarPlaceholder (header_toolbar, "/HeaderToolbar/SeekSlider", seek_slider);

            var track_info_display = new ClassicTrackInfoDisplay ();
            track_info_display.Show ();
            track_info_container = TrackInfoDisplay.GetEditable (track_info_display);
            track_info_container.Show ();
            ActionService.PopulateToolbarPlaceholder (header_toolbar, "/HeaderToolbar/TrackInfoDisplay", track_info_container, true);

            if (PlatformDetection.IsMeeGo) {
                track_info_display.ArtworkSpacing = 5;
                seek_slider.LeftPadding = 20;
                seek_slider.RightPadding = 20;

                var menu = (Menu)(ActionService.UIManager.GetWidget ("/ToolbarMenu"));
                var menu_button = new Hyena.Widgets.MenuButton (new Image (Stock.Preferences, IconSize.LargeToolbar), menu, true);
                menu_button.Show ();
                ActionService.PopulateToolbarPlaceholder (header_toolbar, "/HeaderToolbar/ToolbarMenuPlaceholder", menu_button);

                var close_button = new Button (Image.NewFromIconName ("window-close", IconSize.LargeToolbar)) {
                    TooltipText = Catalog.GetString ("Close")
                };

                close_button.Clicked += (o, e) => Hide ();
                close_button.ShowAll ();
                ActionService.PopulateToolbarPlaceholder (header_toolbar, "/HeaderToolbar/ClosePlaceholder", close_button);
            } else {
                var volume_button = new ConnectedVolumeButton ();
                volume_button.Show ();
                ActionService.PopulateToolbarPlaceholder (header_toolbar, "/HeaderToolbar/VolumeButton", volume_button);
            }
        }

        private void BuildViews ()
        {
            source_box = new VBox ();

            views_pane = new HPaned ();
            PersistentPaneController.Control (views_pane, SourceViewWidth);
            view_container = new ViewContainer ();

            source_view = new SourceView ();
            composite_view = new CompositeTrackSourceContents ();

            Container source_scroll;
            if (PlatformDetection.IsMeeGo) {
                source_scroll = new Gtk.ScrolledWindow () {
                    HscrollbarPolicy = PolicyType.Never,
                    VscrollbarPolicy = PolicyType.Automatic,
                    ShadowType = ShadowType.None
                };
                source_scroll.Add (source_view);

                var color = new Gdk.Color ((byte)0xe6, (byte)0xe6, (byte)0xe6);
                Gdk.Colormap.System.AllocColor (ref color, true, true);
                source_view.ModifyBase (StateType.Normal, color);
            } else {
                var hyena_source_scroll = new Hyena.Widgets.ScrolledWindow ();
                hyena_source_scroll.AddWithFrame (source_view);
                source_scroll = hyena_source_scroll;
            }

            composite_view.TrackView.HeaderVisible = false;
            view_container.Content = composite_view;

            source_box.PackStart (source_scroll, true, true, 0);
            source_box.PackStart (new UserJobTileHost (), false, false, 0);

            UpdateCoverArtDisplay ();

            source_view.SetSizeRequest (125, -1);
            view_container.SetSizeRequest (425, -1);

            views_pane.Pack1 (source_box, false, false);
            views_pane.Pack2 (view_container, true, false);

            source_box.ShowAll ();
            view_container.Show ();
            views_pane.Show ();

            primary_vbox.PackStart (views_pane, true, true, 0);
        }

        private void UpdateCoverArtDisplay ()
        {
            if (ShowCoverArt.Get ()) {
                if (cover_art_display == null && source_box != null) {
                    cover_art_display = new CoverArtDisplay () { Visible = true };
                    source_box.SizeAllocated += OnSourceBoxSizeAllocated;
                    cover_art_display.HeightRequest = SourceViewWidth.Get ();
                    source_box.PackStart (cover_art_container = TrackInfoDisplay.GetEditable (cover_art_display), false, false, 4);
                    source_box.ShowAll ();
                }
            } else if (cover_art_display != null) {
                cover_art_display.Hide ();
                source_box.Remove (cover_art_container);
                source_box.SizeAllocated -= OnSourceBoxSizeAllocated;
                cover_art_display.Dispose ();
                cover_art_display = null;
            }
        }

        private void OnSourceBoxSizeAllocated (object o, EventArgs args)
        {
            cover_art_display.HeightRequest = source_box.Allocation.Width;
        }

        private void BuildFooter ()
        {
            if (PlatformDetection.IsMeeGo) {
                return;
            }

            footer_toolbar = new HBox () { BorderWidth = 2 };

            Widget task_status = new Banshee.Gui.Widgets.TaskStatusIcon ();

            EventBox status_event_box = new EventBox ();
            status_event_box.ButtonPressEvent += OnStatusBoxButtonPress;

            status_label = new Label ();
            status_event_box.Add (status_label);

            HBox status_hbox = new HBox (true, 0);
            status_hbox.PackStart (status_event_box, false, false, 0);

            Alignment status_align = new Alignment (0.5f, 0.5f, 1.0f, 1.0f);
            status_align.Add (status_hbox);

            RepeatActionButton repeat_button = new RepeatActionButton ();
            repeat_button.SizeAllocated += delegate (object o, Gtk.SizeAllocatedArgs args) {
                status_align.LeftPadding = (uint)args.Allocation.Width;
            };

            footer_toolbar.PackStart (task_status, false, false, 0);
            footer_toolbar.PackStart (status_align, true, true, 0);
            footer_toolbar.PackStart (repeat_button, false, false, 0);

            footer_toolbar.ShowAll ();
            primary_vbox.PackStart (footer_toolbar, false, true, 0);
        }

        private void OnStatusBoxButtonPress (object o, ButtonPressEventArgs args)
        {
            Source source = ServiceManager.SourceManager.ActiveSource;
            if (source != null) {
                source.CycleStatusFormat ();
                UpdateSourceInformation ();
            }
        }

#endregion

#region Events and Logic Setup

        protected override void ConnectEvents ()
        {
            base.ConnectEvents ();

            // Service events
            ServiceManager.SourceManager.ActiveSourceChanged += OnActiveSourceChanged;
            ServiceManager.SourceManager.SourceUpdated += OnSourceUpdated;

            ActionService.TrackActions ["SearchForSameArtistAction"].Activated += OnProgrammaticSearch;
            ActionService.TrackActions ["SearchForSameAlbumAction"].Activated += OnProgrammaticSearch;

            (ActionService.ViewActions ["ShowCoverArtAction"] as Gtk.ToggleAction).Active = ShowCoverArt.Get ();
            ActionService.ViewActions ["ShowCoverArtAction"].Activated += (o, a) => {
                ShowCoverArt.Set ((o as Gtk.ToggleAction).Active);
                UpdateCoverArtDisplay ();
            };

            // UI events
            view_container.SearchEntry.Changed += OnSearchEntryChanged;
            views_pane.SizeRequested += delegate {
                SourceViewWidth.Set (views_pane.Position);
            };

            source_view.RowActivated += delegate {
                Source source = ServiceManager.SourceManager.ActiveSource;
                var handler = source.Properties.Get<System.Action> ("ActivationAction");
                if (handler != null) {
                    handler ();
                } else if (source is ITrackModelSource) {
                    ServiceManager.PlaybackController.NextSource = (ITrackModelSource)source;
                    // Allow changing the play source without stopping the current song by
                    // holding ctrl when activating a source. After the song is done, playback will
                    // continue from the new source.
                    if (GtkUtilities.NoImportantModifiersAreSet (Gdk.ModifierType.ControlMask)) {
                        ServiceManager.PlaybackController.Next ();
                    }
                }
            };

            if (!PlatformDetection.IsMeeGo) {
                header_toolbar.ExposeEvent += OnToolbarExposeEvent;
            }
        }

#endregion

#region Service Event Handlers

        private void OnProgrammaticSearch (object o, EventArgs args)
        {
            Source source = ServiceManager.SourceManager.ActiveSource;
            view_container.SearchEntry.Ready = false;
            view_container.SearchEntry.Query = source.FilterQuery;
            view_container.SearchEntry.Ready = true;
        }

        private Source previous_source = null;
        private TrackListModel previous_track_model = null;
        private void OnActiveSourceChanged (SourceEventArgs args)
        {
            ThreadAssist.ProxyToMain (delegate {
                Source source = ServiceManager.SourceManager.ActiveSource;

                view_container.SearchSensitive = source != null && source.CanSearch;

                if (source == null) {
                    return;
                }

                view_container.Title = source.Name;
                view_container.SearchEntry.Ready = false;
                view_container.SearchEntry.CancelSearch ();

                /* Translators: this is a verb (command), not a noun (things) */
                var msg = source.Properties.Get<string> ("SearchEntryDescription") ?? Catalog.GetString ("Search");
                view_container.SearchEntry.EmptyMessage = msg;
                view_container.SearchEntry.TooltipText = msg;

                if (source.FilterQuery != null) {
                    view_container.SearchEntry.Query = source.FilterQuery;
                    view_container.SearchEntry.ActivateFilter ((int)source.FilterType);
                }

                if (view_container.Content != null) {
                    view_container.Content.ResetSource ();
                }

                if (previous_track_model != null) {
                    previous_track_model.Reloaded -= HandleTrackModelReloaded;
                    previous_track_model = null;
                }

                if (source is ITrackModelSource) {
                    previous_track_model = (source as ITrackModelSource).TrackModel;
                    previous_track_model.Reloaded += HandleTrackModelReloaded;
                }

                if (previous_source != null) {
                    previous_source.Properties.PropertyChanged -= OnSourcePropertyChanged;
                }

                previous_source = source;
                previous_source.Properties.PropertyChanged += OnSourcePropertyChanged;

                UpdateSourceContents (source);

                UpdateSourceInformation ();
                view_container.SearchEntry.Ready = true;

                SetSimple (source.Properties.Get<bool> ("Nereid.SimpleUI"));
            });
        }

        private void OnSourcePropertyChanged (object o, PropertyChangeEventArgs args)
        {
            switch (args.PropertyName) {
                case "Nereid.SourceContents":
                    ThreadAssist.ProxyToMain (delegate {
                        UpdateSourceContents (previous_source);
                    });
                    break;

                case "FilterQuery":
                    var source = ServiceManager.SourceManager.ActiveSource;
                    var search_entry = source.Properties.Get<SearchEntry> ("Nereid.SearchEntry") ?? view_container.SearchEntry;
                    if (!search_entry.HasFocus) {
                        ThreadAssist.ProxyToMain (delegate {
                            view_container.SearchEntry.Ready = false;
                            view_container.SearchEntry.Query = source.FilterQuery;
                            view_container.SearchEntry.Ready = true;
                        });
                    }
                    break;
                case "Nereid.SimpleUI":
                    SetSimple (ServiceManager.SourceManager.ActiveSource.Properties.Get<bool> ("Nereid.SimpleUI"));
                    break;
            }
        }

        private void UpdateSourceContents (Source source)
        {
            if (source == null) {
                return;
            }

            // Connect the source models to the views if possible
            ISourceContents contents = source.GetProperty<ISourceContents> ("Nereid.SourceContents",
                source.GetInheritedProperty<bool> ("Nereid.SourceContentsPropagate"));

            view_container.ClearHeaderWidget ();
            view_container.ClearFooter ();

            if (contents != null) {
                if (view_container.Content != contents) {
                    view_container.Content = contents;
                }
                view_container.Content.SetSource (source);
                view_container.Show ();
            } else if (source is ITrackModelSource) {
                view_container.Content = composite_view;
                view_container.Content.SetSource (source);
                view_container.Show ();
            } else if (source is Hyena.Data.IObjectListModel) {
                if (object_view == null) {
                    object_view = new ObjectListSourceContents ();
                }

                view_container.Content = object_view;
                view_container.Content.SetSource (source);
                view_container.Show ();
            } else {
                view_container.Hide ();
            }

            // Associate the view with the model
            if (view_container.Visible && view_container.Content is ITrackModelSourceContents) {
                ITrackModelSourceContents track_content = view_container.Content as ITrackModelSourceContents;
                source.Properties.Set<IListView<TrackInfo>>  ("Track.IListView", track_content.TrackView);
            }

            view_container.Header.Visible = source.Properties.Contains ("Nereid.SourceContents.HeaderVisible") ?
                source.Properties.Get<bool> ("Nereid.SourceContents.HeaderVisible") : true;

            view_container.SetTitleWidget (source.Properties.Get<Widget> ("Nereid.SourceContents.TitleWidget"));

            Widget header_widget = null;
            if (source.Properties.Contains ("Nereid.SourceContents.HeaderWidget")) {
                header_widget = source.Properties.Get<Widget> ("Nereid.SourceContents.HeaderWidget");
            }

            if (header_widget != null) {
                view_container.SetHeaderWidget (header_widget);
            }

            Widget footer_widget = null;
            if (source.Properties.Contains ("Nereid.SourceContents.FooterWidget")) {
                footer_widget = source.Properties.Get<Widget> ("Nereid.SourceContents.FooterWidget");
            }

            if (footer_widget != null) {
                view_container.SetFooter (footer_widget);
            }
        }

        private void OnSourceUpdated (SourceEventArgs args)
        {
            if (args.Source == ServiceManager.SourceManager.ActiveSource) {
                ThreadAssist.ProxyToMain (delegate {
                    UpdateSourceInformation ();
                    view_container.Title = args.Source.Name;
                });
            }
        }

#endregion

#region UI Event Handlers

        private void OnSearchEntryChanged (object o, EventArgs args)
        {
            Source source = ServiceManager.SourceManager.ActiveSource;
            if (source == null)
                return;

            source.FilterType = (TrackFilterType)view_container.SearchEntry.ActiveFilterID;
            source.FilterQuery = view_container.SearchEntry.Query;
        }

#endregion

#region Implement Interfaces

        // IHasSourceView
        public Source HighlightedSource {
            get { return source_view.HighlightedSource; }
        }

        public void BeginRenameSource (Source source)
        {
            source_view.BeginRenameSource (source);
        }

        public void ResetHighlight ()
        {
            source_view.ResetHighlight ();
        }

        public override Box ViewContainer {
            get { return view_container; }
        }

#endregion

#region Gtk.Window Overrides

        private bool accel_group_active = true;

        private void OnEntryFocusOutEvent (object o, FocusOutEventArgs args)
        {
            if (!accel_group_active) {
                AddAccelGroup (ActionService.UIManager.AccelGroup);
                accel_group_active = true;
            }

            (o as Widget).FocusOutEvent -= OnEntryFocusOutEvent;
        }

        protected override bool OnKeyPressEvent (Gdk.EventKey evnt)
        {
            bool focus_search = false;

            bool disable_keybindings = Focus is Gtk.Entry;
            if (!disable_keybindings) {
                var widget = Focus;
                while (widget != null) {
                    if (widget is IDisableKeybindings) {
                        disable_keybindings = true;
                        break;
                    }
                    widget = widget.Parent;
                }
            }

            // Don't disable them if ctrl is pressed
            disable_keybindings &= (evnt.State & Gdk.ModifierType.ControlMask) == 0 &&
                evnt.Key != Gdk.Key.Control_L && evnt.Key != Gdk.Key.Control_R;

            if (disable_keybindings) {
                if (accel_group_active) {
                    RemoveAccelGroup (ActionService.UIManager.AccelGroup);
                    accel_group_active = false;

                    // Reinstate the AccelGroup as soon as the focus leaves the entry
                    Focus.FocusOutEvent += OnEntryFocusOutEvent;
                }
            } else {
                if (!accel_group_active) {
                    AddAccelGroup (ActionService.UIManager.AccelGroup);
                    accel_group_active = true;
                }
            }

            switch (evnt.Key) {
                case Gdk.Key.f:
                    if (Gdk.ModifierType.ControlMask == (evnt.State & Gdk.ModifierType.ControlMask)) {
                        focus_search = true;
                    }
                    break;

                case Gdk.Key.S:
                case Gdk.Key.s:
                case Gdk.Key.slash:
                    if (!disable_keybindings) {
                        focus_search = true;
                    }
                    break;
                case Gdk.Key.F3:
                    focus_search = true;
                    break;
                case Gdk.Key.F11:
                    ActionService.ViewActions["FullScreenAction"].Activate ();
                    break;
            }

            // The source might have its own custom search entry - use it if so
            var src = ServiceManager.SourceManager.ActiveSource;
            var search_entry = src.Properties.Get<SearchEntry> ("Nereid.SearchEntry") ?? view_container.SearchEntry;
            if (focus_search && search_entry.Visible && !source_view.EditingRow) {
                search_entry.InnerEntry.GrabFocus ();
                search_entry.HasFocus = true;
                return true;
            }

            return base.OnKeyPressEvent (evnt);
        }

#endregion

#region Popup Status Bar
#if false

        private Gdk.FilterReturn OnGdkEventFilter (IntPtr xevent, Gdk.Event gdkevent)
        {
            if (!IsRealized || !IsMapped) {
                return Gdk.FilterReturn.Continue;
            }

            Gdk.ModifierType mask;
            int x, y;
            GdkWindow.GetPointer (out x, out y, out mask);
            return Gdk.FilterReturn.Continue;
        }

#endif
#endregion

#region Helper Functions

        private void HandleTrackModelReloaded (object sender, EventArgs args)
        {
            ThreadAssist.ProxyToMain (UpdateSourceInformation);
        }

        private void UpdateSourceInformation ()
        {
            if (status_label == null) {
                return;
            }

            Source source = ServiceManager.SourceManager.ActiveSource;
            if (source == null) {
                status_label.Text = String.Empty;
                return;
            }

            status_label.Text = source.GetStatusText ();

            // We need a bit longer delay between query character typed to search initiated
            // when the library is sufficiently big; see bgo #540835
            bool long_delay = source.FilteredCount > 6000 || (source.Parent ?? source).Count > 12000;
            view_container.SearchEntry.ChangeTimeoutMs = long_delay ? (uint)250 : (uint)25;
        }

#endregion

#region Configuration Schemas

        public static readonly SchemaEntry<int> SourceViewWidth = new SchemaEntry<int> (
            "player_window", "source_view_width",
            175,
            "Source View Width",
            "Width of Source View Column."
        );

        public static readonly SchemaEntry<bool> ShowCoverArt = new SchemaEntry<bool> (
            "player_window", "show_cover_art",
            false,
            "Show cover art",
            "Show cover art below source view if available"
        );

#endregion

        IDBusExportable IDBusExportable.Parent {
            get { return null; }
        }

        string IDBusObjectName.ExportObjectName {
            get { return "ClientWindow"; }
        }

        string IService.ServiceName {
            get { return "NereidPlayerInterface"; }
        }
    }
}
