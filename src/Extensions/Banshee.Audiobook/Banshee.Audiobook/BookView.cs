//
// BookView.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2010 Novell, Inc.
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

using Hyena;
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
using Banshee.Gui.Widgets;
using Banshee.Library;
using Banshee.MediaEngine;
using Banshee.PlaybackController;
using Banshee.Playlist;
using Banshee.Preferences;
using Banshee.ServiceStack;
using Banshee.Sources;
using Hyena.Gui.Theming;
using Banshee.Query;

namespace Banshee.Audiobook
{
    public class BookView : Hyena.Widgets.RoundedFrame, Banshee.Sources.Gui.ISourceContents, IDisposable
    {
        AudiobookLibrarySource library;

        WrapLabel title_label;
        BookCover cover;
        ImageButton resume_button;
        RatingEntry rating_entry;
        BaseTrackListView track_list;

        public BookView ()
        {
            BuildWidgets ();
            ShowAll ();
        }

        protected BookView (IntPtr ptr) : base (ptr)
        {
        }

        private DatabaseAlbumInfo Book { get; set; }

        public void SetBook (DatabaseAlbumInfo book)
        {
            ThreadAssist.AssertInMainThread ();
            Book = book;

            title_label.Markup = String.Format (
                "<span size=\"x-large\" weight=\"bold\">{0}</span>\n" +
                "{1}",
                GLib.Markup.EscapeText (book.Title),
                GLib.Markup.EscapeText (book.ArtistName)
            );

            var bookmark = library.GetLastPlayedBookmark (book.DbId);
            UpdateResumeButton (bookmark);

            UpdateCover ();

            /*var bookmarks = Bookmark.Provider.FetchAllMatching (
                "TrackID IN (SELECT TrackID FROM CoreTracks WHERE PrimarySourceID = ? AND AlbumID = ?)",
                library.DbId, book.DbId
            );*/

            rating_entry.Value = (int) Math.Round (ServiceManager.DbConnection.Query<double> (
                "SELECT AVG(RATING) FROM CoreTracks WHERE PrimarySourceID = ? AND AlbumID = ?", library.DbId, book.DbId
            ));
        }

        internal void UpdateResumeButton (Bookmark bookmark)
        {
            if (bookmark != null) {
                resume_button.LabelWidget.Markup = String.Format (
                    "<b>{0}</b>\n<small>{1}</small>",
                    GLib.Markup.EscapeText (Catalog.GetString ("Resume Playback")),
                    GLib.Markup.EscapeText (bookmark.Name)
                );
            } else {
                resume_button.LabelWidget.Markup = String.Format (
                    "<b>{0}</b>\n<small>{1}</small>",
                    GLib.Markup.EscapeText (Catalog.GetString ("No Bookmark Set")), "");
            }
        }

        private void UpdateCover ()
        {
            cover.LoadImage (
                TrackMediaAttributes.AudioStream | TrackMediaAttributes.AudioBook,
                CoverArtSpec.CreateArtistAlbumId (Book.ArtistName, Book.Title)
            );
        }

        public override void Dispose ()
        {
            if (cover != null) {
                cover.Dispose ();
                cover = null;
            }

            base.Dispose ();
        }

#region ISourceContents

        public bool SetSource (ISource source)
        {
            if (library != null)
                return true;

            library = source as AudiobookLibrarySource;
            if (library != null) {
                track_list.SetModel (library.TrackModel);
                library.Actions["AudiobookResume"].ConnectProxy (resume_button);
            }

            return library != null;
        }

        public void ResetSource ()
        {
        }

        public ISource Source { get { return library; } }

        public Widget Widget { get { return this; } }

#endregion

        private void BuildWidgets ()
        {
            var hbox = new HBox () { Spacing = 12 };

            // Left box - cover art, title, etc
            var left_box = new VBox () { BorderWidth = 12, Spacing = 12 };

            // Cover art
            cover = new BookCover (this) {
                WidthRequest = 256,
                HeightRequest = 256
            };

            var editable_cover = CoverArtEditor.For (
                cover, (x, y) => true,
                () => library.TrackModel[0],
                UpdateCover
            );

            // Title
            title_label = new WrapLabel ();

            resume_button = new ImageButton ("", null, IconSize.LargeToolbar);
            resume_button.ImageWidget.Stock = Stock.MediaPlay;
            resume_button.LabelWidget.Xalign = 0;
            resume_button.Spacing = 6;

            // FIXME the left padding on this is not right
            rating_entry = new RatingEntry () {
                AlwaysShowEmptyStars = true,
                HasFrame = false
            };
            var rating = new HBox ();
            rating.PackStart (rating_entry, false, false, 0);

            // Packing
            left_box.PackStart (editable_cover, false, false,  0);
            left_box.PackStart (title_label, false, false,  0);
            left_box.PackStart (resume_button, false, false,  0);
            //left_box.PackStart (rating, false, false,  0);

            hbox.PackStart (left_box, false, false, 0);

            // Right box - track list
            track_list = new BaseTrackListView () {
                HeaderVisible = true,
                IsEverReorderable = false
            };

            var columns = new DefaultColumnController ();
            var file_columns = new ColumnController ();
            var disc_column = DefaultColumnController.Create (
                    BansheeQuery.DiscNumberField, 0.02, false, new ColumnCellPositiveInt (null, false, 2, 2));

            file_columns.AddRange (
                columns.IndicatorColumn,
                disc_column,
                columns.TitleColumn,
                columns.DurationColumn
            );
            file_columns.SortColumn = columns.DiscNumberAndCountColumn;

            foreach (var col in file_columns) {
                col.Visible = true;
            }

            var track_sw = new Gtk.ScrolledWindow () {
                Child = track_list,
                ShadowType = ShadowType.None
            };

            track_list.ColumnController = file_columns;

            hbox.PackEnd (track_sw, true, true, 0);

            Child = hbox;
        }
    }
}
