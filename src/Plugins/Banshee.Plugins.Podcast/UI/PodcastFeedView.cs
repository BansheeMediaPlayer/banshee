/***************************************************************************
 *  PodcastFeedView.cs
 *
 *  Written by Mike Urbanski <michael.c.urbanski@gmail.com>
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

using Mono.Gettext;

using Gtk;
using Gdk;
using Pango;

using Banshee.Base;
using Banshee.Plugins;

namespace Banshee.Plugins.Podcast.UI
{
    internal class PodcastFeedView : TreeView
    {
    private enum Column :
        int {
            Activity = 0,
            Title = 1
        }

        private PodcastFeedModel model;

        private TreeViewColumn feed_title_column;
        private TreeViewColumn feed_activity_column;

        public PodcastFeedView (PodcastFeedModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException ("model");
            }

            RulesHint = true;
            Selection.Mode = SelectionMode.Single;

            this.model = model;
            this.model.FeedAdded += OnModelPodcastFeedAdded;

            feed_title_column = new TreeViewColumn();
            feed_activity_column = new TreeViewColumn();

            feed_activity_column.Expand = false;
            feed_activity_column.Resizable = false;
            feed_activity_column.Clickable = false;
            feed_activity_column.Reorderable = true;
            feed_activity_column.Visible = true;
            feed_activity_column.MinWidth = 32;

            feed_title_column.Title = Catalog.GetString ("Podcasts");
            feed_title_column.Expand = true;
            feed_title_column.Resizable = false;
            feed_title_column.Clickable = false;
            feed_title_column.Reorderable = true;
            feed_title_column.Visible = true;
            feed_title_column.Spacing = 24;

            CellRendererText feed_title_renderer = new CellRendererText();
            CellRendererText feed_status_renderer = new CellRendererText();
            CellRendererPixbuf feed_activity_renderer = new CellRendererPixbuf();

            feed_activity_column.PackStart (feed_activity_renderer, true);
            feed_activity_column.SetCellDataFunc(feed_activity_renderer,
                                                 new TreeCellDataFunc(TrackCellPodcastFeedActivity));

            feed_title_column.PackStart (feed_status_renderer, false);
            feed_title_column.SetCellDataFunc(feed_status_renderer,
                                              new TreeCellDataFunc(TrackCellPodcastFeedStatus));

            feed_title_column.PackStart(feed_title_renderer, true);
            feed_title_column.SetCellDataFunc(feed_title_renderer,
                                              new TreeCellDataFunc(TrackCellPodcastFeedTitle));

            this.model.DefaultSortFunc = PodcastFeedTitleTreeIterCompareFunc;

            this.model.SetSortFunc((int)Column.Title,
                                   new TreeIterCompareFunc(PodcastFeedTitleTreeIterCompareFunc));

            Model = this.model;

            this.model.SetSortColumnId ((int) Column.Title, SortType.Ascending);

            InsertColumn (feed_title_column, (int)Column.Title);
            InsertColumn (feed_activity_column, (int)Column.Activity);
        }

        public int PodcastFeedTitleTreeIterCompareFunc (TreeModel tree_model,
                TreeIter a,
                TreeIter b)
        {
            PodcastFeedInfo feed_a = tree_model.GetValue (a, 0) as PodcastFeedInfo;
            PodcastFeedInfo feed_b = tree_model.GetValue (b, 0) as PodcastFeedInfo;

            if (feed_a == PodcastFeedInfo.All ||
                    feed_b == PodcastFeedInfo.All)
            {
                return 1;
            }

            return feed_a.CompareTo (feed_b);
        }

        private void TrackCellPodcastFeedStatus(TreeViewColumn tree_column,
                                                CellRenderer cell, TreeModel tree_model, TreeIter iter)
        {
            PodcastFeedInfo feed = model.IterPodcastFeedInfo(iter);

            CellRendererText renderer = cell as CellRendererText;

            renderer.Style = Pango.Style.Normal;
            renderer.Weight = (int) Pango.Weight.Normal;

            if (feed == PodcastFeedInfo.All)
            {

                renderer.Text = Catalog.GetString ("All");
                renderer.Weight = (int) Pango.Weight.Bold;

            }
            else if (feed != null)
            {
                if (feed.IsSubscribed)
                {

                    renderer.Text = String.Format (
                                        "({0}/{1})",
                                        feed.DownloadedPodcasts, feed.TotalPodcasts
                                    );

                }
                else
                {
                    renderer.Text = String.Format (
                                        "({0})", feed.DownloadedPodcasts
                                    );
                }
            }
        }

        private void TrackCellPodcastFeedTitle(TreeViewColumn tree_column,
                                               CellRenderer cell, TreeModel tree_model, TreeIter iter)
        {
            PodcastFeedInfo feed = model.IterPodcastFeedInfo(iter);
            CellRendererText renderer = cell as CellRendererText;

            if (feed == PodcastFeedInfo.All)
            {

                renderer.Text = String.Format( 
				     Catalog.GetPluralString (
					"{0} Podcast", "{0} Podcasts", PodcastCore.Library.FeedCount
				     ),
                                     PodcastCore.Library.FeedCount
                                );

                renderer.Weight = (int) Pango.Weight.Bold;
                renderer.Style = Pango.Style.Normal;

            }
            else if (feed != null)
            {
                string title = (feed.Title == null || feed.Title == String.Empty)
                               ? feed.Url.ToString () : feed.Title;

                if (feed.NewPodcasts > 0)
                {
                    string episode_str = Catalog.GetPluralString (
                                             "Episode", "Episodes", feed.NewPodcasts
                                         );

                    renderer.Text = String.Format (
                                        "{0} - {1} {2} {3}",
                                        title, feed.NewPodcasts, Catalog.GetString ("New"), episode_str
                                    );

                }
                else
                {
                    renderer.Text = title;
                }

                if (!feed.IsSubscribed)
                {
                    renderer.Style = Pango.Style.Italic;
                    renderer.Weight = (int) Pango.Weight.Normal;
                }
                else
                {
                    renderer.Style = Pango.Style.Normal;

                    if (feed.IsUpdating || feed.NewPodcasts > 0)
                    {
                        renderer.Weight = (int) Pango.Weight.Bold;
                    }
                    else
                    {
                        renderer.Weight = (int) Pango.Weight.Normal;
                    }
                }
            }
        }

        private void TrackCellPodcastFeedActivity(TreeViewColumn tree_column,
                CellRenderer cell, TreeModel tree_model, TreeIter iter)
        {
            PodcastFeedInfo feed = model.IterPodcastFeedInfo(iter);
            CellRendererPixbuf renderer = cell as CellRendererPixbuf;

            renderer.Pixbuf = null;
            renderer.StockId = null;           		
            renderer.Sensitive = true;
            
            if (feed.IsUpdating)
            {
                renderer.StockId = Stock.Refresh;
            }
            else if (feed.ActiveDownloads > 0)
            {
                renderer.StockId = Stock.GoForward;
            }
            else if (feed.QueuedDownloads > 0)
            {
                renderer.Sensitive = false;            
                renderer.StockId = Stock.GoForward;
            }
        }

        public void SelectFeed (PodcastFeedInfo feed)
        {
            if (feed == null)
            {
                throw new ArgumentNullException ("feed");
            }

            Selection.SelectIter (feed.TreeIter);
            ScrollToIter (feed.TreeIter);
        }

        public void ScrollToIter (TreeIter iter)
        {
            if (iter.Equals (TreeIter.Zero) ||
                    !model.IterIsValid (iter))
            {
                return;
            }

            TreePath path;

            path = model.GetPath (iter);

            if (path != null)
            {
                ScrollToCell (path, null, false, 0, 0);
            }
        }

        private void OnModelPodcastFeedAdded (object sender, PodcastFeedEventArgs args)
        {
            PodcastFeedInfo feed = args.Feed;

            if (feed != null)
            {
                SelectFeed (feed);
            }
        }
    }
}
