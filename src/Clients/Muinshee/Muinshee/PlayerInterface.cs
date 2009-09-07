// 
// PlayerInterface.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2007-2008 Novell, Inc.
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
using System.Collections.Generic;
using Mono.Unix;
using Gtk;

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

using Banshee.PlayQueue;

namespace Muinshee
{
    public class PlayerInterface : BaseClientWindow, IClientWindow, IDBusObjectName, IService, IDisposable
    {
        // Major Layout Components
        private VBox content_vbox;
        private VBox main_vbox;
        private Toolbar header_toolbar;

        private MuinsheeActions actions;
        
        // Major Interaction Components
        private TerseTrackListView track_view;
        private Label list_label;
        
        public PlayerInterface () : base (Catalog.GetString ("Banshee Media Player"), "muinshee", -1, 450)
        {
        }

        protected override void Initialize ()
        {
            FindPlayQueue ();
        }

        private void FindPlayQueue ()
        {
            Banshee.ServiceStack.ServiceManager.SourceManager.SourceAdded += delegate (SourceAddedArgs args) {
                if (args.Source is Banshee.PlayQueue.PlayQueueSource) {
                    InitPlayQueue (args.Source as Banshee.PlayQueue.PlayQueueSource);
                }
            };

            foreach (Source src in ServiceManager.SourceManager.Sources) {
                if (src is Banshee.PlayQueue.PlayQueueSource) {
                    InitPlayQueue (src as Banshee.PlayQueue.PlayQueueSource);
                }
            }
        }

        private void InitPlayQueue (PlayQueueSource play_queue)
        {
            if (actions == null) {
                actions = new MuinsheeActions (play_queue);
                actions.Actions.AddActionGroup (actions);
                ServiceManager.SourceManager.SetActiveSource (play_queue);
                play_queue.TrackModel.Reloaded += HandleTrackModelReloaded;

                BuildPrimaryLayout ();
                ConnectEvents ();

                track_view.SetModel (play_queue.TrackModel);

                InitialShowPresent ();
            }
        }

#region System Overrides 
        
        public override void Dispose ()
        {
            lock (this) {
                Hide ();
                if (actions != null) {
                    actions.Dispose ();
                }
                base.Dispose ();
                Gtk.Application.Quit ();
            }
        }
        
#endregion        

#region Interface Construction
        
        private void BuildPrimaryLayout ()
        {
            main_vbox = new VBox ();
            
            Widget menu = new MainMenu ();
            menu.Show ();
            main_vbox.PackStart (menu, false, false, 0);
           
            BuildHeader ();

            content_vbox = new VBox ();
            content_vbox.Spacing = 6;
            Alignment content_align = new Alignment (0f, 0f, 1f, 1f);
            content_align.LeftPadding = content_align.RightPadding = 6;
            content_align.Child = content_vbox;
            main_vbox.PackStart (content_align, true, true, 0);

            BuildTrackInfo ();
            BuildViews ();
            
            main_vbox.ShowAll ();
            Add (main_vbox);
        }
        
        private void BuildHeader ()
        {
            Alignment toolbar_alignment = new Alignment (0.0f, 0.0f, 1.0f, 1.0f);
            toolbar_alignment.TopPadding = 3;
            toolbar_alignment.BottomPadding = 3;
            
            header_toolbar = (Toolbar)ActionService.UIManager.GetWidget ("/MuinsheeHeaderToolbar");
            header_toolbar.ShowArrow = false;
            header_toolbar.ToolbarStyle = ToolbarStyle.BothHoriz;
            
            toolbar_alignment.Add (header_toolbar);
            toolbar_alignment.ShowAll ();
            
            main_vbox.PackStart (toolbar_alignment, false, false, 0);
            
            Widget next_button = new NextButton (ActionService);
            next_button.Show ();
            ActionService.PopulateToolbarPlaceholder (header_toolbar, "/MuinsheeHeaderToolbar/NextArrowButton", next_button);
            
            ConnectedVolumeButton volume_button = new ConnectedVolumeButton ();
            volume_button.Show ();
            ActionService.PopulateToolbarPlaceholder (header_toolbar, "/MuinsheeHeaderToolbar/VolumeButton", volume_button);
        }

        private const int info_height = 64;
        private void BuildTrackInfo ()
        {
            TrackInfoDisplay track_info_display = new MuinsheeTrackInfoDisplay ();
            if (track_info_display.HeightRequest < info_height) {
                track_info_display.HeightRequest = info_height;
            }
            track_info_display.Show ();
            content_vbox.PackStart (track_info_display, false, false, 0);

            ConnectedSeekSlider seek_slider = new ConnectedSeekSlider (SeekSliderLayout.Horizontal);
            seek_slider.LeftPadding = seek_slider.RightPadding = 0;
            content_vbox.PackStart (seek_slider, false, false, 0);
        }

        private void BuildViews ()
        {
            track_view = new TerseTrackListView ();
            track_view.HasFocus = true;
            track_view.ColumnController.Insert (new Column (null, "indicator", new ColumnCellStatusIndicator (null), 0.05, true, 20, 20), 0);

            Hyena.Widgets.ScrolledWindow sw = new Hyena.Widgets.ScrolledWindow ();
            sw.Add (track_view);
            /*window.Add (view);
            window.HscrollbarPolicy = PolicyType.Automatic;
            window.VscrollbarPolicy = PolicyType.Automatic;*/

            list_label = new Label ();
            list_label.Xalign = 0f;
            content_vbox.PackStart (list_label, false, false, 0);
            content_vbox.PackStart (sw, true, true, 0);
            content_vbox.PackStart (new UserJobTileHost (), false, false, 0);
            track_view.SetSizeRequest (425, -1);
        }

#endregion
        
#region Events and Logic Setup
        
        protected override void ConnectEvents ()
        {
            base.ConnectEvents ();
            ServiceManager.SourceManager.SourceUpdated += OnSourceUpdated;
            header_toolbar.ExposeEvent += OnToolbarExposeEvent;
        }
        
#endregion

#region Service Event Handlers

        private void OnSourceUpdated (SourceEventArgs args)
        {
            if (args.Source == ServiceManager.SourceManager.ActiveSource) {
                UpdateSourceInformation ();
            }
        }
        
#endregion
        
#region Helper Functions

        private void HandleTrackModelReloaded (object sender, EventArgs args)
        {
            UpdateSourceInformation ();
        }

        private void UpdateSourceInformation ()
        {
            DatabaseSource source = ServiceManager.SourceManager.ActiveSource as DatabaseSource;
            if (source != null) {
                System.Text.StringBuilder sb = new System.Text.StringBuilder ();
                Source.DurationStatusFormatters[source.CurrentStatusFormat] (sb, source.Duration);
                list_label.Markup = String.Format ("<b>{0}</b> ({1})",
                    source.Name, String.Format (Catalog.GetString ("{0} remaining"), sb.ToString ())
                );
            }
        }

#endregion

#region Configuration Schemas

#endregion

        IDBusExportable IDBusExportable.Parent {
            get { return null; }
        }
        
        string IDBusObjectName.ExportObjectName {
            get { return "MuinsheeClientWindow"; }
        }

        string IService.ServiceName {
            get { return "MuinsheePlayerInterface"; }
        }
    }
}
