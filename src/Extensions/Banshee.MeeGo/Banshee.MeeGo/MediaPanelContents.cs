//
// MediaPanelContents.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright 2009-2010 Novell, Inc.
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
using Gtk;
using Mono.Unix;

// using Banshee.PlayQueue;

using Hyena;
using Hyena.Gui;
using Hyena.Data.Gui;

using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Sources.Gui;
using Banshee.Collection;
using Banshee.Collection.Gui;
using Banshee.Collection.Database;

namespace Banshee.MeeGo
{
    public class MediaPanelContents : VBox
    {
        private SourceComboBox source_combo_box;
        private HBox header_box;
        private SearchEntry search_entry;
        private MeeGoSourceContents source_contents;

        public MediaPanelContents ()
        {
            BorderWidth = 12;
            Spacing = 12;

            var header_label = new Label {
                Markup = String.Format ("<span font_desc=\"Droid Bold\" size=\"22000\" foreground=\"#606eff\">{0}</span>",
                    GLib.Markup.EscapeText (Catalog.GetString ("Media"))),
                Xalign = 0.0f
            };

            PackStart (header_label, false, false, 0);

            header_box = new HBox () {
                Spacing = 5,
                BorderWidth = 5
            };

            header_box.PackStart (source_combo_box = new SourceComboBox (), false, false, 0);

            var button = new Button (new Image () {
                IconSize = (int)IconSize.LargeToolbar,
                IconName = "media-player-banshee"
            }) {
                TooltipText = Catalog.GetString ("Launch the Banshee Media Player")
            };

            button.Clicked += (o, e) => {
                ServiceManager.SourceManager.SetActiveSource (ServiceManager.SourceManager.MusicLibrary);
                ServiceManager.Get<MeeGoService> ().PresentPrimaryInterface ();
            };

            header_box.PackStart (button, false, false, 0);
            header_box.PackStart (search_entry = new SearchEntry (), true, true, 0);
            header_box.PackStart (new PlaybackBox (), false, false, 0);

            PackStart (header_box, false, false, 0);
            PackStart (source_contents = new MeeGoSourceContents (), true, true, 0);

            ShowAll ();

            source_combo_box.Model.Filter = (source) =>
                source == ServiceManager.SourceManager.MusicLibrary ||
                source.Parent == ServiceManager.SourceManager.MusicLibrary ||
                source.GetType ().FullName == "Banshee.PlayQueue.PlayQueueSource";
            source_combo_box.Model.Refresh ();
            source_combo_box.UpdateActiveSource ();

            search_entry.Changed += OnSearchEntryChanged;

            source_contents.SetSource (ServiceManager.SourceManager.ActiveSource);
            ServiceManager.SourceManager.ActiveSourceChanged += OnActiveSourceChanged;
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

                source_contents.ResetSource ();
                source_contents.SetSource (source);

                search_entry.Ready = true;
            });
        }

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
            cr.Color = new Cairo.Color (0xe7 / (double)0xff,
                0xea / (double)0xff, 0xfd / (double)0xff);

            CairoExtensions.RoundedRectangle (cr,
                header_box.Allocation.X,
                header_box.Allocation.Y,
                header_box.Allocation.Width,
                header_box.Allocation.Height,
                5);

            cr.Fill ();

            CairoExtensions.DisposeContext (cr);
        }
    }
}
