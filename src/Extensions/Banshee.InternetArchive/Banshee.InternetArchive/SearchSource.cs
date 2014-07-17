//
// SearchSource.cs
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

using Hyena;
using Hyena.Collections;
using Hyena.Data;

using Banshee.Base;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Configuration;
using Banshee.Database;
using Banshee.Gui;
using Banshee.Library;
using Banshee.MediaEngine;
using Banshee.PlaybackController;
using Banshee.Playlist;
using Banshee.ServiceStack;
using Banshee.Sources;

using IA=InternetArchive;

namespace Banshee.InternetArchive
{
    public class SearchSource : Banshee.Sources.Source
    {
        private static string name = Catalog.GetString ("Search Results");
        private MemoryListModel<IA.SearchResult> model = new MemoryListModel<IA.SearchResult> ();

        private Gtk.Widget header_widget;
        private IA.Search search;
        private string status_text = "";

        private int total_results = 0;
        public int TotalResults { get { return total_results; } }

        public IA.Search Search { get { return search; } }
        public SearchDescription SearchDescription { get; private set; }

        public IListModel<IA.SearchResult> Model { get { return model; } }

        public IA.SearchResult FocusedItem {
            get {
                int focus = model.Selection.FocusedIndex;
                if (focus >= 0 && focus < model.Count) {
                    return model[focus];
                }

                return null;
            }
        }

        public SearchSource () : base (name, name, 190, "internet-archive-search")
        {
            IA.Search.UserAgent = Banshee.Web.Browser.UserAgent;
            IA.Search.TimeoutMs = 12*1000;

            search = new IA.Search () { NumResults = 100 };

            Properties.SetStringList ("Icon.Name", "search", "gtk-search");

            Properties.SetString ("ActiveSourceUIResource", "SearchSourceActiveUI.xml");
            Properties.SetString ("GtkActionPath", "/IaSearchSourcePopup");

            if (header_widget == null) {
                header_widget = new HeaderFilters (this);
                header_widget.ShowAll ();
                Properties.Set<Gtk.Widget> ("Nereid.SourceContents.HeaderWidget", header_widget);
            }

            Properties.Set<Banshee.Sources.Gui.ISourceContents> ("Nereid.SourceContents", new Banshee.Sources.Gui.LazyLoadSourceContents<SearchView> (this));
        }

        public void SetSearch (SearchDescription settings)
        {
            SearchDescription = settings;
            settings.ApplyTo (Search);
            Reload ();
            OnUpdated ();
            ServiceManager.SourceManager.SetActiveSource (this);
        }

        public void Reload ()
        {
            model.Clear ();
            ThreadAssist.SpawnFromMain (delegate {
                ThreadedFetch (0);
            });
        }

        public void FetchMore ()
        {
            ThreadAssist.SpawnFromMain (delegate {
                ThreadedFetch (search.Page + 1);
            });
        }

        private void ThreadedFetch (int page)
        {
            bool success = false;
            total_results = 0;
            status_text = "";
            Exception err = null;
            int old_page = search.Page;
            search.Page = page;

            ThreadAssist.ProxyToMain (delegate {
                SetStatus (Catalog.GetString ("Searching the Internet Archive"), false, true, "gtk-find");
            });

            IA.SearchResults results = null;

            try {
                results = search.GetResults ();
                total_results = results.TotalResults;
            } catch (System.Net.WebException e) {
                Log.Error ("Error searching the Internet Archive", e);
                results = null;
                err = e;
            }

            if (results != null) {
                try {

                    foreach (var result in results) {
                        model.Add (result);

                        // HACK to remove ugly empty description
                        //if (track.Comment == "There is currently no description for this item.")
                            //track.Comment = null;
                    }

                    success = true;
                } catch (Exception e) {
                    err = e;
                    Log.Error ("Error searching the Internet Archive", e);
                }
            }

            if (success) {
                int count = model.Count;
                if (total_results == 0) {
                    ThreadAssist.ProxyToMain (delegate {
                        SetStatus (Catalog.GetString ("No matches."), false, false, "gtk-info");
                    });
                } else {
                    ThreadAssist.ProxyToMain (ClearMessages);
                    status_text = String.Format (Catalog.GetPluralString (
                        "Showing 1 match", "Showing 1 to {0:N0} of {1:N0} total matches", total_results),
                        count, total_results
                    );
                }
            } else {
                search.Page = old_page;
                ThreadAssist.ProxyToMain (delegate {
                    var web_e = err as System.Net.WebException;
                    if (web_e != null && web_e.Status == System.Net.WebExceptionStatus.Timeout) {
                        SetStatus (Catalog.GetString ("Timed out searching the Internet Archive"), true);
                        CurrentMessage.AddAction (new MessageAction (Catalog.GetString ("Try Again"), (o, a) => {
                            if (page == 0) Reload (); else FetchMore ();
                        }));
                    } else {
                        SetStatus (Catalog.GetString ("Error searching the Internet Archive"), true);
                    }
                });
            }

            ThreadAssist.ProxyToMain (delegate {
                model.Reload ();
                OnUpdated ();
            });
        }

        public override string PreferencesPageId {
            get { return Parent.PreferencesPageId; }
        }

        public override int Count {
            get { return 0; }
        }

        protected override int StatusFormatsCount { get { return 1; } }

        public override string GetStatusText ()
        {
            return status_text;
        }
    }
}
