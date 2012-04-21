//
// HomeView.cs
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
using System.Collections.Generic;
using System.Linq;

using Mono.Unix;
using Gtk;

using Hyena.Collections;
using Hyena.Data.Sqlite;

using Hyena.Data;
using Hyena.Data.Gui;
using Hyena.Widgets;

using Banshee.Base;
using Banshee.Collection;
using Banshee.Collection.Gui;
using Banshee.Collection.Database;
using Banshee.Configuration;
using Banshee.Database;
using Banshee.Gui;
using Banshee.Library;
using Banshee.MediaEngine;
using Banshee.PlaybackController;
using Banshee.Playlist;
using Banshee.Preferences;
using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Widgets;

using IA=InternetArchive;

namespace Banshee.InternetArchive
{
    public class HomeView : Gtk.HBox, Banshee.Sources.Gui.ISourceContents
    {
        private HomeSource source;

        public HomeView (HomeSource source)
        {
            this.source = source;

            //var sw = new Gtk.ScrolledWindow ();
            //sw.BorderWidth = 4;
            //sw.AddWithViewport (Build ());

            var frame = new Hyena.Widgets.RoundedFrame ();
            frame.Child = Build ();

            PackStart (frame, true, true, 0);
            ShowAll ();
        }

        private Widget Build ()
        {
            var hbox = new HBox () { Spacing = 8, BorderWidth = 4 };

            hbox.PackStart (BuildTiles (), false, false, 0);
            hbox.PackStart (BuildCenter (), true, true, 0);

            return hbox;
        }

        private Widget BuildCenter ()
        {
            var vbox = new VBox () { Spacing = 2 };

            // Search entry/button
            var search_box = new HBox () { Spacing = 6, BorderWidth = 4 };
            var entry = new SearchEntry () {
                Visible = true,
                EmptyMessage = String.Format (Catalog.GetString ("Search..."))
            };

            source.Properties.Set<SearchEntry> ("Nereid.SearchEntry", entry);

            // Make the search entry text nice and big
            var font = entry.InnerEntry.Style.FontDescription.Copy ();
            font.Size = (int) (font.Size * Pango.Scale.XLarge);
            entry.InnerEntry.ModifyFont (font);
            font.Dispose ();

            var button = new Hyena.Widgets.ImageButton (Catalog.GetString ("_Go"), Stock.Find);
            entry.Activated += (o, a) => { button.Activate (); };
            button.Clicked += (o, a) => source.SetSearch (new SearchDescription (null, entry.Query, IA.Sort.DownloadsDesc, null));

            search_box.PackStart (entry, true, true, 0);
            search_box.PackStart (button, false, false, 0);

            var example_searches = new SearchDescription [] {
                new SearchDescription (Catalog.GetString ("Staff Picks"), "pick:1", IA.Sort.WeekDesc, null),
                new SearchDescription (Catalog.GetString ("Creative Commons"), "license:creativecommons", IA.Sort.DownloadsDesc, null),
                new SearchDescription (Catalog.GetString ("History"), "subject:history", IA.Sort.DownloadsDesc, null),
                new SearchDescription (Catalog.GetString ("Classic Cartoons"), "", IA.Sort.DateCreatedAsc, IA.MediaType.Get ("animationandcartoons")),
                new SearchDescription (Catalog.GetString ("Speeches"), "subject:speeches OR title:speech", IA.Sort.DownloadsDesc, null),
                new SearchDescription (Catalog.GetString ("For Children"), "subject:children", IA.Sort.DownloadsDesc, null),
                new SearchDescription (Catalog.GetString ("Poetry"), "subject:poetry", IA.Sort.DownloadsDesc, null),
                new SearchDescription (Catalog.GetString ("Creator is United States"), "creator:\"United States\"", IA.Sort.DownloadsDesc, null),
                new SearchDescription (Catalog.GetString ("Old Movies"), "", IA.Sort.DateCreatedAsc, IA.MediaType.Get ("moviesandfilms")),
                new SearchDescription (Catalog.GetString ("New From LibriVox"), "publisher:LibriVox", IA.Sort.DateAddedDesc, IA.MediaType.Get ("audio")),
                new SearchDescription (Catalog.GetString ("Old Texts"), "", IA.Sort.DateCreatedAsc, IA.MediaType.Get ("texts")),
                new SearchDescription (Catalog.GetString ("Charlie Chaplin"), "\"Charlie Chaplin\"", IA.Sort.DownloadsDesc, null),
                new SearchDescription (Catalog.GetString ("NASA"), "NASA", IA.Sort.DownloadsDesc, null),
                new SearchDescription (Catalog.GetString ("Library of Congress"), "creator:\"Library of Congress\"", IA.Sort.DownloadsDesc, null)
            };

            var examples = new FlowBox () { Spacing = 0 };
            examples.Add (PaddingBox (new Label () { Markup = Catalog.GetString ("Examples:") }));

            foreach (var search in example_searches) {
                var this_search = search;
                var link = CreateLink (search.Name, search.Query);
                link.TooltipText = search.Query;
                link.Clicked += (o, a) => source.SetSearch (this_search);
                examples.Add (link);
            }

            // Intro text and visit button
            var intro_label = new Hyena.Widgets.WrapLabel () {
                Markup = Catalog.GetString ("The Internet Archive, a 501(c)(3) non-profit, is building a digital library of Internet sites and other cultural artifacts in digital form. Like a paper library, we provide free access to researchers, historians, scholars, and the general public.")
            };

            var visit_button = new LinkButton ("http://archive.org/", Catalog.GetString ("Visit the Internet Archive online at archive.org"));
            visit_button.Clicked += (o, a) => Banshee.Web.Browser.Open ("http://archive.org/");
            visit_button.Xalign = 0f;
            var visit_box = new HBox ();
            visit_box.PackStart (visit_button, false, false, 0);
            visit_box.PackStart (new Label () { Visible = true }, true, true, 0);

            // Packing
            vbox.PackStart (search_box, false, false, 0);
            vbox.PackStart (examples, false, false, 0);
            vbox.PackStart (PaddingBox (new HSeparator ()), false, false, 6);
            vbox.PackStart (PaddingBox (intro_label), false, false, 0);
            vbox.PackStart (visit_box, false, false, 0);

            return vbox;
        }

        private Widget PaddingBox (Widget child)
        {
            var box = new HBox () { BorderWidth = 4 };
            box.PackStart (child, true, true, 0);
            child.Show ();
            box.Show ();
            return box;
        }

        public class FlowBox : VBox
        {
            private List<HBox> rows = new List<HBox> ();
            private List<Widget> children = new List<Widget> ();

            private int hspacing;
            public int HSpacing {
                get { return hspacing; }
                set {
                    hspacing = value;
                    foreach (var box in rows) {
                        box.Spacing = hspacing;
                    }
                }
            }

            public FlowBox ()
            {
                HSpacing = 2;
                Spacing = 2;

                bool updating_layout = false;
                SizeAllocated += (o, a) => {
                    if (!updating_layout) {
                        updating_layout = true;
                        UpdateLayout ();
                        updating_layout = false;
                    }
                };
            }

            public new void Add (Widget widget)
            {
                children.Add (widget);
                UpdateLayout ();
            }

            private void UpdateLayout ()
            {
                if (Allocation.Width < 2)
                    return;

                int width = Allocation.Width;
                int y = 0;
                int x = 0;
                int j = 0;
                foreach (var widget in children) {
                    x += widget.Allocation.Width + hspacing;
                    if (x > width) {
                        y++;
                        j = 0;
                        x = widget.Allocation.Width;
                    }

                    Reparent (widget, GetRow (y), j);
                    j++;
                }

                for (int i = y + 1; i < rows.Count; i++) {
                    var row = GetRow (i);
                    rows.Remove (row);
                    Remove (row);
                }
            }

            private void Reparent (Widget widget, HBox box, int index)
            {
                if (widget.Parent == box) {
                    return;
                }

                if (widget.Parent == null) {
                    box.PackStart (widget, false, false, 0);
                } else {
                    widget.Reparent (box);
                    box.SetChildPacking (widget, false, false, 0, PackType.Start);
                }

                box.ReorderChild (widget, index);
                widget.Show ();
            }

            private HBox GetRow (int i)
            {
                if (i < rows.Count) {
                    return rows[i];
                } else {
                    var box = new HBox () { Spacing = HSpacing };
                    rows.Add (box);
                    PackStart (box, false, false, 0);
                    box.Show ();
                    return box;
                }
            }
        }

        private Button CreateLink (string title, string url)
        {
            var button = new LinkButton (url, "") {
                Relief = ReliefStyle.None,
            };

            var label = button.Child as Label;
            if (label != null) {
                label.Markup = title;//"<small>" + title + "</small>";
            }
            return button;
        }

        private class Category : SearchDescription
        {
            public long Count { get; private set; }
            public string IconName { get; private set; }

            public Category (string media_type, string name, int count, string icon_name)
                : this (media_type, name, null, count, icon_name) {}

            public Category (string media_type, string name, string query, int count, string icon_name)
                : base (name, query, IA.Sort.DownloadsDesc, IA.MediaType.Get (media_type))
            {
                Count = count;
                IconName = icon_name;
            }
        }

        private Widget BuildTiles ()
        {
            var vbox = new VBox () { Spacing = 12, BorderWidth = 4 };

            var categories = new Category [] {
                new Category ("audio_bookspoetry", Catalog.GetString ("Audiobooks"), 4300, "audio-x-generic"),
                new Category ("movies", Catalog.GetString ("Movies"), 200000, "video-x-generic"),
                new Category (null, Catalog.GetString ("Lectures"), "subject:ocw OR creator:university OR mediatype:education OR publisher:University", 1290, "x-office-presentation"),
                new Category ("etree", Catalog.GetString ("Concerts"), 69000, "audio-x-generic"),
                new Category ("texts", Catalog.GetString ("Books"), 1600000, "x-office-document")
            };

            foreach (var cat in categories.OrderBy (c => c.Name)) {
                var this_cat = cat;
                var tile = new ImageButton (cat.Name, cat.IconName) {
                    InnerPadding = 4
                };
                tile.LabelWidget.Xalign = 0;
                tile.Clicked += (o, a) => source.SetSearch (this_cat);

                vbox.PackStart (tile, false, false, 0);
            }

            return vbox;
        }

#region ISourceContents

        public bool SetSource (ISource source)
        {
            this.source = source as HomeSource;
            return this.source != null;
        }

        public void ResetSource ()
        {
            source = null;
        }

        public ISource Source { get { return source; } }

        public Widget Widget { get { return this; } }

#endregion

    }
}
