//
// RecommendationPane.cs
//
// Authors:
//   Fredrik Hedberg
//   Aaron Bockover <aaron@abock.org>
//   Lukas Lipka
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2005-2008 Novell, Inc.
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
using System.IO;
using System.Net;
using System.Text;
using System.Security.Cryptography;

using Gtk;
using Mono.Unix;

using Hyena;
using Hyena.Gui;
using Hyena.Widgets;

using Lastfm;
using Lastfm.Data;
using Lastfm.Gui;

using Banshee.MediaEngine;
using Banshee.Base;
using Banshee.Configuration;
using Banshee.ServiceStack;
using Banshee.Gui;
using Banshee.Gui.Widgets;
using Banshee.Networking;

using Banshee.Collection;
using Banshee.Widgets;

using Browser = Lastfm.Browser;

namespace Banshee.Lastfm.Recommendations
{
    public class RecommendationPane : HBox
    {
        private ContextPage context_page;
        private HBox main_box;
        private MessagePane no_artists_pane;
        private TitledList artist_box;
        private TitledList album_box;
        private TitledList track_box;

        private Gtk.ScrolledWindow similar_artists_view_sw;

        private TileView similar_artists_view;
        private VBox album_list;
        private VBox track_list;

        private static string album_title_format = Catalog.GetString ("Top Albums by {0}");
        private static string track_title_format = Catalog.GetString ("Top Tracks by {0}");

        private static string[] special_artists = new string[] {
            "Unknown",
            "Unknown Artists",
            "Unknown Artist",
            "Various Artists",
            "[unknown]",
            "[no artist]",
            Catalog.GetString ("Unknown Artist"),
            Catalog.GetString ("Various Artists")
        };

        private bool ready = false;
        private bool refreshing = false;
        private bool show_when_ready = true;
        private bool ShowWhenReady {
            get { return show_when_ready; }
            set {
                show_when_ready = value;
                ShowIfReady ();

                if (!show_when_ready) {
                    CancelTasks ();
                } else if (!ready && !refreshing) {
                    RefreshRecommendations ();
                }
            }
        }

        private void ShowIfReady ()
        {
            if (ShowWhenReady && ready) {
                ShowAll ();
            }
        }

        private string artist;
        public string Artist {
            get { return artist; }
            set {
                if (artist == value) {
                    return;
                }

                ready = false;
                artist = value;

                foreach (string special_artist in special_artists) {
                    if (String.Compare (artist, special_artist, true) == 0) {
                        artist = null;
                        break;
                    }
                }

                if (!String.IsNullOrEmpty (artist)) {
                    RefreshRecommendations ();
                }
            }
        }

        private void RefreshRecommendations ()
        {
            CancelTasks ();

            if (show_when_ready && !String.IsNullOrEmpty (Artist)) {
                refreshing = true;
                context_page.SetState (Banshee.ContextPane.ContextState.Loading);
                Banshee.Kernel.Scheduler.Schedule (new RefreshRecommendationsJob (this, Artist));
            }
        }

        private void CancelTasks ()
        {
            Banshee.Kernel.Scheduler.Unschedule (typeof (RefreshRecommendationsJob));
            refreshing = false;
        }

        public void HideWithTimeout ()
        {
            GLib.Timeout.Add (200, OnHideTimeout);
        }

        private bool OnHideTimeout ()
        {
            if (!ShowWhenReady || !ready) {
                Hide ();
            }
            return false;
        }

        public RecommendationPane (ContextPage contextPage) : base ()
        {
            this.context_page = contextPage;
            main_box = this;
            main_box.BorderWidth = 5;

            artist_box = new TitledList (Catalog.GetString ("Recommended Artists"));
            artist_box.ShowAll ();
            similar_artists_view = new TileView (2);
            similar_artists_view_sw = new Gtk.ScrolledWindow ();
            similar_artists_view_sw.SetPolicy (PolicyType.Never, PolicyType.Automatic);
            similar_artists_view_sw.Add (similar_artists_view);
            similar_artists_view_sw.ShowAll ();
            artist_box.PackStart (similar_artists_view_sw, true, true, 0);

            album_box  = new TitledList (null);
            album_box.TitleWidthChars = 25;
            album_box.SizeAllocated += OnSideSizeAllocated;
            album_list = new VBox ();
            album_box.PackStart (album_list, false, false, 0);

            track_box  = new TitledList (null);
            track_box.SizeAllocated += OnSideSizeAllocated;
            track_box.TitleWidthChars = 25;
            track_list = new VBox ();
            track_box.PackStart (track_list, true, true, 0);

            no_artists_pane = new MessagePane ();
            no_artists_pane.NoShowAll = true;
            no_artists_pane.Visible = false;
            string no_results_message;

            if (!ApplicationContext.Debugging) {
                no_artists_pane.HeaderIcon = IconThemeUtils.LoadIcon (48, "face-sad", Stock.DialogError);
                no_results_message = Catalog.GetString("No similar artists found");
            } else {
                no_artists_pane.HeaderIcon = Gdk.Pixbuf.LoadFromResource ("no-results.png");
                no_results_message = "No one likes your music, fool!";
            }

            no_artists_pane.HeaderMarkup = String.Format ("<big><b>{0}</b></big>", GLib.Markup.EscapeText (no_results_message));
            artist_box.PackEnd (no_artists_pane, true, true, 0);

            main_box.PackStart (artist_box, true, true, 5);
            main_box.PackStart (new VSeparator (), false, false, 0);
            main_box.PackStart (album_box, false, false, 5);
            main_box.PackStart (new VSeparator (), false, false, 0);
            main_box.PackStart (track_box, false, false, 5);

            no_artists_pane.Hide ();
        }

        private void OnSideSizeAllocated (object o, SizeAllocatedArgs args)
        {
            SetSizeRequest (-1, args.Allocation.Height + (Allocation.Height - args.Allocation.Height));
        }

        protected override void OnStyleUpdated ()
        {
            base.OnStyleUpdated ();
            similar_artists_view.OverrideBackgroundColor (StateFlags.Normal,
                                                          StyleContext.GetBackgroundColor (StateFlags.Normal));
        }

        private class RefreshRecommendationsJob : Banshee.Kernel.Job
        {
            private RecommendationPane pane;
            private string artist;

            public RefreshRecommendationsJob (RecommendationPane pane, string artist)
            {
                this.pane = pane;
                this.artist = artist;
            }

            protected override void RunJob ()
            {
                pane.UpdateForArtist (artist);
            }
        }

        private void UpdateForArtist (string artist)
        {
            try {
                LastfmArtistData artist_data = new LastfmArtistData (artist);

                // Make sure all the album art is downloaded
                foreach (SimilarArtist similar in artist_data.SimilarArtists) {
                    DataCore.DownloadContent (similar.SmallImageUrl);
                }

                UpdateForArtist (artist, artist_data.SimilarArtists, artist_data.TopAlbums, artist_data.TopTracks);
            } catch (Exception e) {
                Log.Error (e);
            }
        }

        private void UpdateForArtist (string artist_name, LastfmData<SimilarArtist> similar_artists,
            LastfmData<ArtistTopAlbum> top_albums, LastfmData<ArtistTopTrack> top_tracks)
        {
            ThreadAssist.ProxyToMain (delegate {
                album_box.Title = String.Format (album_title_format, artist);
                track_box.Title = String.Format (track_title_format, artist);

                similar_artists_view.ClearWidgets ();
                ClearBox (album_list);
                ClearBox (track_list);

                // Similar Artists
                var artists = similar_artists.Take (20);

                if (artists.Any ()) {
                    int artist_name_max_len = 2 * (int) artists.Select (a => a.Name.Length).Average ();
                    foreach (var similar_artist in artists) {
                        SimilarArtistTile tile = new SimilarArtistTile (similar_artist);

                        tile.PrimaryLabel.WidthChars = artist_name_max_len;
                        tile.PrimaryLabel.Ellipsize = Pango.EllipsizeMode.End;

                        tile.ShowAll ();
                        similar_artists_view.AddWidget (tile);
                    }

                    no_artists_pane.Hide ();
                    similar_artists_view_sw.ShowAll ();
                } else {
                    similar_artists_view_sw.Hide ();
                    no_artists_pane.ShowAll ();
                }

                for (int i = 0; i < Math.Min (5, top_albums.Count); i++) {
                    ArtistTopAlbum album = top_albums[i];
                    Button album_button = new Button ();
                    album_button.Relief = ReliefStyle.None;

                    Label label = new Label ();
                    label.OverrideColor (StateFlags.Normal, StyleContext.GetColor (StateFlags.Normal));
                    label.Ellipsize = Pango.EllipsizeMode.End;
                    label.Xalign = 0;
                    label.Markup = String.Format ("{0}. {1}", i+1, GLib.Markup.EscapeText (album.Name));
                    album_button.Add (label);

                    album_button.Clicked += delegate {
                        Banshee.Web.Browser.Open (album.Url);
                    };
                    album_list.PackStart (album_button, false, true, 0);
                }
                album_box.ShowAll ();

                for (int i = 0; i < Math.Min (5, top_tracks.Count); i++) {
                    ArtistTopTrack track = top_tracks[i];
                    Button track_button = new Button ();
                    track_button.Relief = ReliefStyle.None;

                    HBox box = new HBox ();

                    Label label = new Label ();
                    label.OverrideColor (StateFlags.Normal, StyleContext.GetColor (StateFlags.Normal));
                    label.Ellipsize = Pango.EllipsizeMode.End;
                    label.Xalign = 0;
                    label.Markup = String.Format ("{0}. {1}", i+1, GLib.Markup.EscapeText (track.Name));

                    /*if(node.SelectSingleNode("track_id") != null) {
                        box.PackEnd(new Image(now_playing_arrow), false, false, 0);
                        track_button.Clicked += delegate {
                            //PlayerEngineCore.OpenPlay(Globals.Library.GetTrack(
                                //Convert.ToInt32(node.SelectSingleNode("track_id").InnerText)));
                        };
                    } else {*/
                        track_button.Clicked += delegate {
                            Banshee.Web.Browser.Open (track.Url);
                        };
                    //}

                    box.PackStart (label, true, true, 0);
                    track_button.Add (box);
                    track_list.PackStart (track_button, false, true, 0);
                }
                track_box.ShowAll ();

                ready = true;
                refreshing = false;
                context_page.SetState (Banshee.ContextPane.ContextState.Loaded);
            });
        }

        private static void ClearBox (Box box)
        {
            while (box.Children.Length > 0) {
                box.Remove (box.Children[0]);
            }
        }
    }
}
