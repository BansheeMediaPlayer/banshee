//
// StationEditor.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
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
using Gtk;
using Mono.Unix;

using Banshee.ServiceStack;
using Banshee.Collection.Database;

using Hyena.Widgets;

namespace Banshee.InternetRadio
{
    public class StationEditor : Gtk.Dialog
    {
        private Button save_button;
        private Entry name_entry;
        private Entry description_entry;
        private Entry stream_entry;
        private Entry creator_entry;
        private ComboBoxText genre_entry;
        private RatingEntry rating_entry;
        private Alignment error_container;
        private Label error;
        private DatabaseTrackInfo track;

        Table table;

        public StationEditor (DatabaseTrackInfo track) : base()
        {
            AccelGroup accel_group = new AccelGroup ();
            AddAccelGroup (accel_group);

            Title = String.Empty;
            SkipTaskbarHint = true;
            Modal = true;

            this.track = track;

            string title = track == null
                ? Catalog.GetString ("Add new radio station")
                : Catalog.GetString ("Edit radio station");

            BorderWidth = 6;
            DefaultResponse = ResponseType.Ok;
            Modal = true;

            ContentArea.Spacing = 6;

            HBox split_box = new HBox ();
            split_box.Spacing = 12;
            split_box.BorderWidth = 6;

            Image image = new Image ();
            image.IconSize = (int)IconSize.Dialog;
            image.IconName = "radio";
            image.Yalign = 0.0f;
            image.Show ();

            VBox main_box = new VBox ();
            main_box.BorderWidth = 5;
            main_box.Spacing = 10;

            Label header = new Label ();
            header.Markup = String.Format ("<big><b>{0}</b></big>", GLib.Markup.EscapeText (title));
            header.Xalign = 0.0f;
            header.Show ();

            Label message = new Label ();
            message.Text = Catalog.GetString ("Enter the Genre, Title and URL of the radio station you wish to add. A description is optional.");
            message.Xalign = 0.0f;
            message.Wrap = true;
            message.Show ();

            table = new Table (5, 2, false);
            table.RowSpacing = 6;
            table.ColumnSpacing = 6;

            genre_entry = ComboBoxText.NewWithEntry ();

            foreach (string genre in ServiceManager.DbConnection.QueryEnumerable<string> ("SELECT DISTINCT Genre FROM CoreTracks ORDER BY Genre")) {
                if (!String.IsNullOrEmpty (genre)) {
                    genre_entry.AppendText (genre);
                }
            }

            if (track != null && !String.IsNullOrEmpty (track.Genre)) {
                genre_entry.Entry.Text = track.Genre;
            }

            AddRow (Catalog.GetString ("Station Genre:"), genre_entry);

            name_entry        = AddEntryRow (Catalog.GetString ("Station Name:"));
            stream_entry      = AddEntryRow (Catalog.GetString ("Stream URL:"));
            creator_entry     = AddEntryRow (Catalog.GetString ("Station Creator:"));
            description_entry = AddEntryRow (Catalog.GetString ("Description:"));

            rating_entry = new RatingEntry ();
            HBox rating_box = new HBox ();
            rating_box.PackStart (rating_entry, false, false, 0);
            AddRow (Catalog.GetString ("Rating:"), rating_box);

            table.ShowAll ();

            main_box.PackStart (header, false, false, 0);
            main_box.PackStart (message, false, false, 0);
            main_box.PackStart (table, false, false, 0);
            main_box.Show ();

            split_box.PackStart (image, false, false, 0);
            split_box.PackStart (main_box, true, true, 0);
            split_box.Show ();

            ContentArea.PackStart (split_box, true, true, 0);

            Button cancel_button = new Button (Stock.Cancel);
            cancel_button.CanDefault = false;
            cancel_button.UseStock = true;
            cancel_button.Show ();
            AddActionWidget (cancel_button, ResponseType.Close);

            cancel_button.AddAccelerator ("activate", accel_group, (uint)Gdk.Key.Escape,
                0, Gtk.AccelFlags.Visible);

            save_button = new Button (Stock.Save);
            save_button.CanDefault = true;
            save_button.UseStock = true;
            save_button.Sensitive = false;
            save_button.Show ();
            AddActionWidget (save_button, ResponseType.Ok);

            save_button.AddAccelerator ("activate", accel_group, (uint)Gdk.Key.Return,
                0, Gtk.AccelFlags.Visible);

            name_entry.HasFocus = true;

            if (track != null) {
                if (!String.IsNullOrEmpty (track.TrackTitle)) {
                    name_entry.Text = track.TrackTitle;
                }

                if (!String.IsNullOrEmpty (track.Uri.AbsoluteUri)) {
                    stream_entry.Text = track.Uri.AbsoluteUri;
                }

                if (!String.IsNullOrEmpty (track.Comment)) {
                    description_entry.Text = track.Comment;
                }

                if (!String.IsNullOrEmpty (track.ArtistName)) {
                    creator_entry.Text = track.ArtistName;
                }

                rating_entry.Value = track.Rating;
            }

            error_container = new Alignment (0.0f, 0.0f, 1.0f, 1.0f);
            error_container.TopPadding = 6;
            HBox error_box = new HBox ();
            error_box.Spacing = 4;

            Image error_image = new Image ();
            error_image.Stock = Stock.DialogError;
            error_image.IconSize = (int)IconSize.Menu;
            error_image.Show ();

            error = new Label ();
            error.Xalign = 0.0f;
            error.Show ();

            error_box.PackStart (error_image, false, false, 0);
            error_box.PackStart (error, true, true, 0);
            error_box.Show ();

            error_container.Add (error_box);

            table.Attach (error_container, 0, 2, 6, 7, AttachOptions.Expand | AttachOptions.Fill, AttachOptions.Shrink, 0, 0);

            genre_entry.Entry.Changed += OnFieldsChanged;
            name_entry.Changed += OnFieldsChanged;
            stream_entry.Changed += OnFieldsChanged;

            OnFieldsChanged (this, EventArgs.Empty);
        }

        private Entry AddEntryRow (string title)
        {
            Entry entry = new Entry ();
            AddRow (title, entry);
            return entry;
        }

        private uint row = 0;
        private void AddRow (string title, Widget entry)
        {
            Label label = new Label (title);
            label.Xalign = 0.0f;

            table.Attach (label, 0, 1, row, row + 1, AttachOptions.Fill, AttachOptions.Fill | AttachOptions.Expand, 0, 0);
            table.Attach (entry, 1, 2, row, row + 1, AttachOptions.Fill | AttachOptions.Expand, AttachOptions.Shrink, 0, 0);
            row++;
        }

        private void OnFieldsChanged (object o, EventArgs args)
        {
            save_button.Sensitive = genre_entry.Entry.Text.Trim ().Length > 0 &&
                name_entry.Text.Trim ().Length > 0 && stream_entry.Text.Trim ().Length > 0;
        }

        public void FocusUri ()
        {
            stream_entry.HasFocus = true;
            stream_entry.SelectRegion (0, stream_entry.Text.Length);
        }

        public DatabaseTrackInfo Track {
            get { return track; }
        }

        public string Genre {
            get { return genre_entry.Entry.Text.Trim (); }
        }

        public string StationCreator {
            get { return creator_entry.Text.Trim (); }
        }

        public string StationTitle {
            get { return name_entry.Text.Trim (); }
        }

        public string StreamUri {
            get { return stream_entry.Text.Trim (); }
        }

        public string Description {
            get { return description_entry.Text.Trim (); }
        }

        public int Rating {
            get { return rating_entry.Value; }
        }

        public string ErrorMessage {
            set {
                if (value == null) {
                    error_container.Hide ();
                } else {
                    error.Text = value;
                    error_container.Show ();
                }
            }
        }
    }
}
