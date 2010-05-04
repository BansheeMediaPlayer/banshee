//
// MediaPanelContents.cs
// 
// Author:
//   Aaron Bockover <abockover@novell.com>
// 
// Copyright 2010 Novell, Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;

using Mono.Unix;
using Gtk;

using Hyena;
using Hyena.Data;
using Hyena.Gui;
using Hyena.Data.Gui;

using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Sources.Gui;
using Banshee.Collection;
using Banshee.Collection.Gui;
using Banshee.MediaEngine;
using Banshee.Gui.Widgets;

namespace Banshee.MeeGo
{
    public class MediaPanelContents : Table, ITrackModelSourceContents
    {
        private ArtistListView artist_view;
        private AlbumListView album_view;
        private TerseTrackListView track_view;

        private SourceComboBox source_combo_box;
        private SearchEntry search_entry;
        private MeeGoTrackInfoDisplay track_info_display;

        private ISource source;
        private Dictionary<object, double> model_positions = new Dictionary<object, double> ();

        protected MediaPanelContents (IntPtr raw) : base (raw)
        {
        }

        public MediaPanelContents () : base (2, 2, false)
        {
            BorderWidth = 5;
            RowSpacing = 6;
            ColumnSpacing = 7;
            RedrawOnAllocate = true;
            AppPaintable = true;

            BuildHeader ();
            BuildLibrary ();
            BuildNowPlaying ();
            ConnectEvents ();

            SetSource (ServiceManager.SourceManager.ActiveSource);
        }

        private void ConnectEvents ()
        {
            ServiceManager.SourceManager.ActiveSourceChanged += OnActiveSourceChanged;

            ServiceManager.PlayerEngine.ConnectEvent ((args) => track_info_display.Visible =
                ServiceManager.PlayerEngine.CurrentState != PlayerState.Idle,
                PlayerEvent.StateChange | PlayerEvent.StartOfStream);

            source_combo_box.Model.Filter = (source) =>
                source == ServiceManager.SourceManager.MusicLibrary ||
                source.Parent == ServiceManager.SourceManager.MusicLibrary ||
                source.GetType ().FullName == "Banshee.PlayQueue.PlayQueueSource";
            source_combo_box.Model.Refresh ();
            source_combo_box.UpdateActiveSource ();

            search_entry.Changed += OnSearchEntryChanged;

            artist_view.SelectionProxy.Changed += OnBrowserViewSelectionChanged;
            album_view.SelectionProxy.Changed += OnBrowserViewSelectionChanged;
        }

#region UI Construction

        private void BuildHeader ()
        {
            Attach (new Label {
                    Markup = String.Format ("<span font_desc=\"Droid Sans Bold\" " +
                        "size=\"x-large\" foreground=\"#606eff\">{0}</span>",
                        GLib.Markup.EscapeText (Catalog.GetString ("Media"))),
                    Xalign = 0.0f
                },
                0, 2, 0, 1,
                AttachOptions.Fill | AttachOptions.Expand,
                AttachOptions.Shrink, 12, 0);
        }

        private void BuildLibrary ()
        {
            var box = new MeeGoHeaderBox () { Title = Catalog.GetString ("Library") };

            // Build the Library Header
            var header = new HBox () {
                Spacing = 5,
                BorderWidth = 5
            };

            var app_button = new Button (new Image () {
                IconSize = (int)IconSize.LargeToolbar,
                IconName = "media-player-banshee"
            }) {
                TooltipText = Catalog.GetString ("Launch the Banshee Media Player")
            };
            app_button.Clicked += (o, e) => {
                ServiceManager.SourceManager.SetActiveSource (ServiceManager.SourceManager.MusicLibrary);
                ServiceManager.Get<MeeGoService> ().PresentPrimaryInterface ();
            };

            header.PackStart (source_combo_box = new SourceComboBox (), false, false, 0);
            header.PackStart (app_button, false, false, 0);
            header.PackStart (search_entry = new SearchEntry (), true, true, 0);
            box.PackStartHighlighted (header, false, false, 0, MeeGoHeaderBox.HighlightFlags.Background);

            // Build the Library Views
            var views = new HBox () { Spacing = 5 };
            views.PackStart (SetupView (artist_view = new ArtistListView () {
                    Name = "meego-panel-artists",
                    WidthRequest = 150,
                    DoNotRenderNullModel = true
                }), false, false, 0);
            views.PackStart (SetupView (album_view = new AlbumListView () {
                    Name = "meego-panel-albums",
                    DoNotRenderNullModel = true
                }), true, true, 0);
            box.PackStart (views, true, true, 0);

            Attach (box, 0, 1, 1, 2,
                AttachOptions.Expand | AttachOptions.Fill,
                AttachOptions.Expand | AttachOptions.Fill,
                0, 0);
        }

        private void BuildNowPlaying ()
        {
            var box = new MeeGoHeaderBox () { Title = Catalog.GetString ("Now Playing") };

            var seek_slider = new ConnectedSeekSlider (SeekSliderLayout.Horizontal);
            seek_slider.StreamPositionLabel.FormatString = "<small>{0}</small>";

            track_info_display = new MeeGoTrackInfoDisplay () {
                HeightRequest = 64,
                NoShowAll = true
            };

            track_view = new TerseTrackListView () {
                Name = "meego-panel-tracks",
                WidthRequest = 220
            };
            track_view.ColumnController.Insert (new Column (null, "indicator",
                new ColumnCellStatusIndicator (null), 0.05, true, 20, 20), 0);

            box.PackStartHighlighted (track_info_display, false, false, 0, MeeGoHeaderBox.HighlightFlags.Background);
            box.PackStartHighlighted (seek_slider, false, false, 0, MeeGoHeaderBox.HighlightFlags.Background);
            box.PackStart (SetupView (track_view), true, true, 0);
            box.PackStartHighlighted (new PlaybackBox (), false, false, 0, MeeGoHeaderBox.HighlightFlags.TopLine);

            Attach (box, 1, 2, 1, 2,
                AttachOptions.Shrink,
                AttachOptions.Expand | AttachOptions.Fill,
                0, 0);
        }

        private ScrolledWindow SetupView (Widget view)
        {
            var scrolled = new ScrolledWindow () {
                VscrollbarPolicy = PolicyType.Automatic,
                HscrollbarPolicy = PolicyType.Never,
                ShadowType = ShadowType.None
            };
            scrolled.Add (view);
            return scrolled;
        }

#endregion

#region Background Rendering

        protected override void OnParentSet (Widget previous)
        {
            base.OnParentSet (previous);

            if (Parent != null) {
                Parent.ModifyBg (StateType.Normal, Style.White);
            }
        }

        protected override bool OnExposeEvent (Gdk.EventExpose evnt)
        {
            if (!Visible || !IsMapped) {
                return true;
            }

            RenderBackground (evnt.Window, evnt.Region);
            foreach (var child in Children) {
                PropagateExpose (child, evnt);
            }

            return true;
        }

        private void RenderBackground (Gdk.Window window, Gdk.Region region)
        {
            var cr = Gdk.CairoHelper.Create (window);

            var grad = new Cairo.LinearGradient (0, 0, 0, Allocation.Height);
            grad.AddColorStop (0, CairoExtensions.RgbToColor (0xffffff));
            grad.AddColorStop (1, CairoExtensions.RgbToColor (0xc3c3c3));
            cr.Pattern = grad;
            cr.Rectangle (0, 0, Allocation.Width, Allocation.Height);
            cr.Fill ();
            grad.Destroy ();

            CairoExtensions.DisposeContext (cr);
        }

#endregion

#region Event Handlers

        private void OnBrowserViewSelectionChanged (object o, EventArgs args)
        {
            // Scroll the raising filter view to the top if "all" is selected
            var selection = (Hyena.Collections.Selection)o;
            if (!selection.AllSelected) {
                return;
            }

            if (artist_view.Selection == selection) {
                artist_view.ScrollTo (0);
            } else if (album_view.Selection == selection) {
                album_view.ScrollTo (0);
            }
        }

        private void OnSearchEntryChanged (object o, EventArgs args)
        {
            var source = ServiceManager.SourceManager.ActiveSource;
            if (source == null) {
                return;
            }

            source.FilterType = (TrackFilterType)search_entry.ActiveFilterID;
            source.FilterQuery = search_entry.Query;
        }

        private void OnActiveSourceChanged (SourceEventArgs args)
        {
            ThreadAssist.ProxyToMain (delegate {
                var source = ServiceManager.SourceManager.ActiveSource;

                search_entry.Ready = false;
                search_entry.CancelSearch ();
                search_entry.SearchSensitive = source != null && source.CanSearch;

                if (source != null && source.FilterQuery != null) {
                    search_entry.Query = source.FilterQuery;
                    search_entry.ActivateFilter ((int)source.FilterType);
                }

                ResetSource ();
                SetSource (source);

                search_entry.Ready = true;
            });
        }

        internal void SyncSearchEntry ()
        {
            OnActiveSourceChanged (null);
        }

#endregion

#region View<->Model binding

        private void SetModel<T> (IListModel<T> model)
        {
            ListView<T> view = FindListView <T> ();
            if (view != null) {
                SetModel (view, model);
            } else {
                Hyena.Log.DebugFormat ("Unable to find view for model {0}", model);
            }
        }

        private void SetModel<T> (ListView<T> view, IListModel<T> model)
        {
            if (view.Model != null) {
                model_positions[view.Model] = view.Vadjustment.Value;
            }

            if (model == null) {
                view.SetModel (null);
                return;
            }

            if (!model_positions.ContainsKey (model)) {
                model_positions[model] = 0.0;
            }

            view.SetModel (model, model_positions[model]);
        }

        private ListView<T> FindListView<T> ()
        {
            foreach (var view in new IListView [] { artist_view, album_view, track_view }) {
                if (view is ListView<T>) {
                    return (ListView<T>)view;
                }
            }
            return null;
        }

#endregion

#region ISourceContents

        public bool SetSource (ISource source)
        {
            var track_source = source as ITrackModelSource;
            var filterable_source = source as IFilterableSource;
            if (track_source == null) {
                return false;
            }

            this.source = source;

            SetModel (track_view, track_source.TrackModel);

            if (filterable_source != null && filterable_source.CurrentFilters != null) {
                foreach (var model in filterable_source.CurrentFilters) {
                    if (model is IListModel<ArtistInfo>) {
                        SetModel (artist_view, (model as IListModel<ArtistInfo>));
                    } else if (model is IListModel<AlbumInfo>) {
                        SetModel (album_view, (model as IListModel<AlbumInfo>));
                    }
                }
            }

            return true;
        }

        public void ResetSource ()
        {
            source = null;
            SetModel (track_view, null);
            SetModel (artist_view, null);
            SetModel (album_view, null);
            track_view.HeaderVisible = false;
        }

        public ISource Source {
            get { return source; }
        }

        public Widget Widget {
            get { return this; }
        }

#endregion

#region ITrackModelSourceContents

        public IListView<TrackInfo> TrackView {
            get { return track_view; }
        }

#endregion

    }
}
