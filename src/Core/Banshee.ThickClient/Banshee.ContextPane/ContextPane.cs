//
// ContextPane.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2009 Novell, Inc.
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

using Hyena.Gui;
using Hyena.Widgets;

using Banshee.Base;
using Banshee.Configuration;
using Banshee.Collection;
using Banshee.ServiceStack;
using Banshee.MediaEngine;
using Banshee.Gui;

namespace Banshee.ContextPane
{
    public class ContextPane : Gtk.HBox
    {
        private object tooltip_host = TooltipSetter.CreateHost ();

        private Gtk.Notebook notebook;
        private VBox vbox;
        private bool large = false;
        private bool initialized = false;

        private RoundedFrame no_active;
        private RoundedFrame loading;

        private RadioButton radio_group = new RadioButton (null, "");

        private Dictionary<BaseContextPage, RadioButton> pane_tabs = new Dictionary<BaseContextPage, RadioButton> ();
        private Dictionary<BaseContextPage, Widget> pane_pages = new Dictionary<BaseContextPage, Widget> ();
        private List<BaseContextPage> pages = new List<BaseContextPage> ();
        private BaseContextPage active_page;

        private Action<bool> expand_handler;
        public Action<bool> ExpandHandler {
            set { expand_handler = value; }
        }

        public bool Large {
            get { return large; }
        }
        
        public ContextPane ()
        {
            HeightRequest = 200;

            CreateContextNotebook ();
            CreateTabButtonBox ();

            new ContextPageManager (this);
            initialized = true;

            RestoreLastActivePage ();

            Enabled = ShowSchema.Get ();
            ShowAction.Activated += OnShowContextPane;

            ServiceManager.PlayerEngine.ConnectEvent (OnPlayerEvent, PlayerEvent.StartOfStream | PlayerEvent.TrackInfoUpdated);
        }

        private void RestoreLastActivePage ()
        {
            // TODO restore the last page
            string last_id = LastContextPageSchema.Get ();
            if (!String.IsNullOrEmpty (last_id)) {
                var page = pages.FirstOrDefault (p => p.Id == last_id);
                if (page != null) {
                    SetActivePage (page);
                    pane_tabs[page].Active = true;
                }
            }

            if (active_page == null) {
                ActivateFirstPage ();
            }
        }

        private void CreateTabButtonBox ()
        {
            vbox = new VBox ();

            HBox hbox = new HBox ();
            var max = new Button (new Image (IconThemeUtils.LoadIcon ("context-pane-maximize", 7)));
            max.Clicked += (o, a) => { large = !large; expand_handler (large); };
            TooltipSetter.Set (tooltip_host, max, Catalog.GetString ("Make the context pane larger or smaller"));
            
            var close = new Button (new Image (IconThemeUtils.LoadIcon ("context-pane-close", 7)));
            close.Clicked += (o, a) => ShowAction.Activate ();
            TooltipSetter.Set (tooltip_host, close, Catalog.GetString ("Hide context pane"));
            
            max.Relief = close.Relief = ReliefStyle.None;
            hbox.PackStart (max, false, false, 0);
            hbox.PackStart (close, false, false, 0);
            vbox.PackStart (hbox, false, false, 0);

            PackStart (vbox, false, false, 6);
            vbox.ShowAll ();
        }

        private void CreateContextNotebook ()
        {
            notebook = new Notebook () {
                ShowBorder = false,
                ShowTabs = false
            };

            // 'No active track' and 'Loading' widgets
            no_active = new RoundedFrame ();
            no_active.Add (new Label () {
                Markup = String.Format ("<b>{0}</b>", Catalog.GetString ("Waiting for playback to begin..."))
            });
            no_active.ShowAll ();
            notebook.Add (no_active);

            loading = new RoundedFrame ();
            loading.Add (new Label () { Markup = String.Format ("<b>{0}</b>", Catalog.GetString ("Loading...")) });
            loading.ShowAll ();
            notebook.Add (loading);

            PackStart (notebook, true, true, 0);
            notebook.Show ();
            
        }

        private void OnPlayerEvent (PlayerEventArgs args)
        {
            if (Enabled) {
                SetCurrentTrackForActivePage ();
            }
        }

        private void SetCurrentTrackForActivePage ()
        {
            TrackInfo track = ServiceManager.PlayerEngine.CurrentTrack;
            if (track != null && active_page != null) {
                active_page.SetTrack (track);
            }
        }

        private void OnActivePageStateChanged (ContextState state)
        {
            if (state == ContextState.NotLoaded)
                notebook.CurrentPage = notebook.PageNum (no_active);
            else if (state == ContextState.Loading)
                notebook.CurrentPage = notebook.PageNum (loading);
            else if (state == ContextState.Loaded)
                notebook.CurrentPage = notebook.PageNum (pane_pages[active_page]);
        }

        private Gtk.ToggleAction ShowAction {
            get { return ServiceManager.Get<InterfaceActionService> ().ViewActions["ShowContextPaneAction"] as ToggleAction; }
        }

        private void OnShowContextPane (object o, EventArgs args)
        {
            Enabled = ShowAction.Active;
        }

        private bool Enabled {
            get { return ShowSchema.Get (); }
            set {
                ShowSchema.Set (value);
                SetCurrentTrackForActivePage ();
                UpdateVisibility ();
            }
        }

        private void UpdateVisibility ()
        {
            int npages = pages.Count;
            bool enabled = Enabled;

            ShowAction.Sensitive = npages > 0;

            if (enabled && npages > 0) {
                Show ();
            } else {
                if (expand_handler != null) {
                    expand_handler (false);
                }
                large = false;
                Hide ();
            }
            
            vbox.Visible = true;//enabled && npages > 1;
        }

        private void SetActivePage (BaseContextPage page)
        {
            if (page == null || page == active_page)
                return;

            if (active_page != null)
                active_page.StateChanged -= OnActivePageStateChanged;

            active_page = page;
            active_page.StateChanged += OnActivePageStateChanged;
            LastContextPageSchema.Set (page.Id);
            OnActivePageStateChanged (active_page.State);
            SetCurrentTrackForActivePage ();
        }

        public void AddPage (BaseContextPage page)
        {
            Hyena.Log.DebugFormat ("Adding context page {0}", page.Id);

            // TODO delay adding the page.Widget until the page is first activated,
            // that way we don't even create those objects unless used
            var frame = new Hyena.Widgets.RoundedFrame ();
            frame.Add (page.Widget);
            frame.Show ();

            // TODO implement DnD?
            /*if (page is ITrackContextPage) {
                Gtk.Drag.DestSet (frame, DestDefaults.Highlight | DestDefaults.Motion,
                                  new TargetEntry [] { Banshee.Gui.DragDrop.DragDropTarget.UriList },
                                  Gdk.DragAction.Default);
                frame.DragDataReceived += delegate(object o, DragDataReceivedArgs args) {
                };
            }*/
            
            page.Widget.Show ();
            notebook.AppendPage (frame, null);
            pane_pages[page] = frame;

            // Setup the tab-like button that switches the notebook to this page
            var tab_image = new Image (IconThemeUtils.LoadIcon (22, page.IconNames));
            var toggle_button = new RadioButton (radio_group) {
                Child = tab_image,
                DrawIndicator = false,
                Relief = ReliefStyle.None
            };
            TooltipSetter.Set (tooltip_host, toggle_button, page.Name);
            toggle_button.Clicked += (s, e) => {
                if (pane_pages.ContainsKey (page)) {
                    if (page.State == ContextState.Loaded) {
                        notebook.CurrentPage = notebook.PageNum (pane_pages[page]);
                    }
                    SetActivePage (page);
                }
            };
            toggle_button.ShowAll ();
            vbox.PackStart (toggle_button, false, false, 0);
            pane_tabs[page] = toggle_button;

            pages.Add (page);

            if (initialized && pages.Count == 1) {
                SetActivePage (page);
                toggle_button.Active = true;
            }

            UpdateVisibility ();
        }

        public void RemovePage (BaseContextPage page)
        {
            Hyena.Log.DebugFormat ("Removing context page {0}", page.Id);
            // Remove the notebook page
            notebook.RemovePage (notebook.PageNum (pane_pages[page]));
            pane_pages.Remove (page);

            // Remove the tab button
            bool was_active = pane_tabs[page].Active;
            vbox.Remove (pane_tabs[page]);
            pane_tabs.Remove (page);

            pages.Remove (page);

            // Set a new page as the default
            if (was_active) {
                ActivateFirstPage ();
            }

            UpdateVisibility ();
        }

        private void ActivateFirstPage ()
        {
            if (pages.Count > 0) {
                SetActivePage (pages[0]);
                pane_tabs[active_page].Active = true;
            }
        }

        internal static readonly SchemaEntry<bool> ShowSchema = new SchemaEntry<bool>(
            "interface", "show_context_pane",
            true,
            "Show context pane",
            "Show context pane for the currently playing track"
        );

        private static readonly SchemaEntry<string> LastContextPageSchema = new SchemaEntry<string>(
            "interface", "last_context_page",
            null,
            "The id of the last context page",
            "The string id of the last context page, which will be defaulted to when Banshee starts"
        );
    }
}
