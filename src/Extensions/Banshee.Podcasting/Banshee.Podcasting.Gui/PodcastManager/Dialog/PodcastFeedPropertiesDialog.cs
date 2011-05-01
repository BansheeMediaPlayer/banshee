/***************************************************************************
 *  PodcastFeedPropertiesDialog.cs
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

using Mono.Unix;

using Gtk;
using Pango;

using Migo.Syndication;
using Banshee.Base;
using Banshee.Collection;
using Banshee.Podcasting.Data;
using Banshee.Gui.TrackEditor;
using Banshee.Collection.Gui;
using Banshee.ServiceStack;

namespace Banshee.Podcasting.Gui
{
    internal class PodcastFeedPropertiesDialog : Dialog
    {
        PodcastSource source;
        Feed feed;
        Entry name_entry;

        Frame header_image_frame;
        Image header_image;
        FakeTrackInfo fake_track = new FakeTrackInfo ();

        CheckButton subscribed_check, download_check, archive_check;

        private VBox main_box;

        public PodcastFeedPropertiesDialog (PodcastSource source, Feed feed)
        {
            this.source = source;
            this.feed = feed;
            fake_track.Feed = feed;

            Title = feed.Title;
            HasSeparator = false;
            BorderWidth = 12;
            WidthRequest = 525;
            //IconThemeUtils.SetWindowIcon (this);

            BuildWindow ();

            DefaultResponse = Gtk.ResponseType.Cancel;
            ActionArea.Layout = Gtk.ButtonBoxStyle.End;

            Response += OnResponse;

            ShowAll ();
        }


        private FeedAutoDownload DownloadPref {
            get { return download_check.Active ? FeedAutoDownload.All : FeedAutoDownload.None; }
            set { download_check.Active = value != FeedAutoDownload.None; }
        }

        private int MaxItemCount {
            get { return archive_check.Active ? 1 : 0; }
            set { archive_check.Active = value > 0; }
        }

        private void BuildWindow()
        {
            VBox.Spacing = 12;
            main_box = VBox;

            var save_button = new Button ("gtk-save") { CanDefault = true };

            name_entry = new Entry ();
            name_entry.Text = feed.Title;
            name_entry.Changed += delegate {
                save_button.Sensitive = !String.IsNullOrEmpty (name_entry.Text);
            };

            subscribed_check = new CheckButton (Catalog.GetString ("Check periodically for new episodes")) {
                TooltipText = Catalog.GetString ("If checked, Banshee will check every hour to see if this podcast has new episodes")
            };

            download_check = new CheckButton (Catalog.GetString ("Download new episodes"));
            DownloadPref = feed.AutoDownload;

            archive_check = new CheckButton (Catalog.GetString ("Archive all episodes except the newest one"));
            MaxItemCount = (int)feed.MaxItemCount;

            subscribed_check.Toggled += delegate {
                download_check.Sensitive = archive_check.Sensitive = subscribed_check.Active;
            };
            subscribed_check.Active = feed.IsSubscribed;
            download_check.Sensitive = archive_check.Sensitive = subscribed_check.Active;

            var last_updated_text = new Label (feed.LastDownloadTime.ToString ("f")) {
                Justify = Justification.Left,
                Xalign = 0f
            };

            var feed_url_text = new Label () {
                Text = feed.Url.ToString (),
                Wrap = false,
                Selectable = true,
                Xalign = 0f,
                Justify = Justification.Left,
                Ellipsize = Pango.EllipsizeMode.End
            };

            string description_string = String.IsNullOrEmpty (feed.Description) ?
                                        Catalog.GetString ("No description available") :
                                        feed.Description;

            var header_box = new HBox () { Spacing = 6 };

            header_image_frame = new Frame ();
            header_image = new Image ();
            LoadCoverArt (fake_track);
            header_image_frame.Add (
                CoverArtEditor.For (header_image,
                    (x, y) => true,
                    () => fake_track,
                    () => LoadCoverArt (fake_track)
                )
            );
            header_box.PackStart (header_image_frame, false, false, 0);

            var table = new Hyena.Widgets.SimpleTable<int> ();
            table.XOptions[0] = AttachOptions.Fill;
            table.XOptions[1] = AttachOptions.Expand | AttachOptions.Fill;
            table.AddRow (0, HeaderLabel (Catalog.GetString ("Name:")), name_entry);
            table.AddRow (1, HeaderLabel (Catalog.GetString ("Website:")),
                new Gtk.Alignment (0f, 0f, 0f, 0f) {
                    Child = new LinkButton (feed.Link, Catalog.GetString ("Visit")) {
                        Image = new Gtk.Image (Gtk.Stock.JumpTo, Gtk.IconSize.Button)
                    }
            });
            header_box.PackStart (table, true, true, 0);

            main_box.PackStart (header_box, false, false, 0);

            Add (Catalog.GetString ("Subscription Options"), subscribed_check, download_check, archive_check);

            var details = new Banshee.Gui.TrackEditor.StatisticsPage ();
            details.AddItem (Catalog.GetString ("Feed URL:"), feed_url_text.Text);
            details.AddItem (Catalog.GetString ("Last Refreshed:"), last_updated_text.Text);
            details.AddItem (Catalog.GetString ("Description:"), description_string, true);
            details.AddItem (Catalog.GetString ("Category:"), feed.Category);
            details.AddItem (Catalog.GetString ("Keywords:"), feed.Keywords);
            details.AddItem (Catalog.GetString ("Copyright:"), feed.Copyright);
            details.HeightRequest = 120;
            Add (true, Catalog.GetString ("Details"), details);

            AddActionWidget (new Button ("gtk-cancel") { CanDefault = true }, ResponseType.Cancel);
            AddActionWidget (save_button, ResponseType.Ok);
        }

        private void Add (string header_txt, params Widget [] widgets)
        {
            Add (false, header_txt, widgets);
        }

        private Label HeaderLabel (string str)
        {
            return new Label () {
                Markup = String.Format ("<b>{0}</b>", GLib.Markup.EscapeText (str)),
                Xalign = 0f
            };
        }

        private void Add (bool filled, string header_txt, params Widget [] widgets)
        {
            var vbox = new VBox () { Spacing = 3 };

            vbox.PackStart (HeaderLabel (header_txt), false, false, 0);

            foreach (var child in widgets) {
                var align = new Gtk.Alignment (0, 0, 1, 1) { LeftPadding = 12, Child = child };
                vbox.PackStart (align, filled, filled, 0);
            }

            main_box.PackStart (vbox, filled, filled, 0);
        }

        private void OnResponse (object sender, ResponseArgs args)
        {
            if (args.ResponseId == Gtk.ResponseType.Ok) {
                bool changed = false;

                if (feed.IsSubscribed != subscribed_check.Active) {
                    feed.IsSubscribed = subscribed_check.Active;
                    changed = true;
                }

                if (feed.IsSubscribed) {
                    if (feed.AutoDownload != DownloadPref) {
                        feed.AutoDownload = DownloadPref;
                        changed = true;
                    }

                    if (feed.MaxItemCount != MaxItemCount) {
                        feed.MaxItemCount = MaxItemCount;
                        changed = true;
                    }
                }

                if (feed.Title != name_entry.Text) {
                    feed.Title = name_entry.Text;
                    source.Reload ();
                    changed = true;
                }

                if (changed) {
                    feed.Save ();
                }
            }

            (sender as Dialog).Response -= OnResponse;
            (sender as Dialog).Destroy();
        }

        void LoadCoverArt (TrackInfo current_track)
        {
            if (current_track == null || current_track.ArtworkId == null) {
                SetDefaultCoverArt ();
                return;
            }

            var artwork = ServiceManager.Get<ArtworkManager> ();
            var cover_art = artwork.LookupScalePixbuf (current_track.ArtworkId, 64);

            header_image.Clear ();
            header_image.Pixbuf = cover_art;

            if (cover_art == null) {
                SetDefaultCoverArt ();
            } else {
                header_image_frame.ShadowType = ShadowType.In;
                header_image.QueueDraw ();
            }
        }

        void SetDefaultCoverArt ()
        {
            header_image.IconName = "podcast";
            header_image.PixelSize = 64;
            header_image_frame.ShadowType = ShadowType.In;
            header_image.QueueDraw ();
        }

        class FakeTrackInfo : TrackInfo
        {
            public Feed Feed { get; set; }
            public override string ArtworkId {
                get { return Feed == null ? null : PodcastService.ArtworkIdFor (Feed); }
            }
        }
    }
}
