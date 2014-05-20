//
// Actions.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2009-2010 Novell, Inc.
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
using Hyena.Widgets;

using Banshee.ServiceStack;
using Banshee.Collection.Database;
using Banshee.Gui;

namespace Banshee.Audiobook
{
    public class Actions : Banshee.Gui.BansheeActionGroup
    {
        private AudiobookLibrarySource library;

        public Actions (AudiobookLibrarySource library) : base ("Audiobook")
        {
            this.library = library;

            Add (
                new ActionEntry ("AudiobookBookPopup", null, null, null, null, (o, a) => ShowContextMenu ("/AudiobookBookPopup")),
                new ActionEntry ("AudiobookOpen", null, Catalog.GetString ("Open Book"), null, null, OnOpen),
                new ActionEntry ("AudiobookMerge", null, Catalog.GetString ("Merge Discs..."), null, null, OnMerge),
                new ActionEntry ("AudiobookSwitchToGrid", null, Catalog.GetString ("Go to Audiobooks"), "Escape", null, (o, a) => library.SwitchToGridView ()),
                new ActionEntry ("AudiobookEdit", Stock.Edit,
                    Catalog.GetString ("_Edit Track Information"), "E", null, OnEdit),
                new ActionEntry ("AudiobookResumeSelected", Stock.MediaPlay,
                    Catalog.GetString ("Resume"), null, Catalog.GetString ("Resume playback of this audiobook"), OnResume)
            );

            Add (new ActionEntry ("AudiobookResume", Stock.MediaPlay,
                Catalog.GetString ("Resume"), null, Catalog.GetString ("Resume playback of this audiobook"), OnResume));

            AddUiFromFile ("GlobalUI.xml");

            Register ();

            UpdateActions ();
            library.BooksModel.Selection.Changed += (o, a) => UpdateActions ();
            library.BooksModel.Selection.FocusChanged += (o, a) => UpdateActions ();

            this["AudiobookSwitchToGrid"].Visible = false;
        }

        internal void UpdateActions ()
        {
            var selection = library.BooksModel.Selection;
            bool has_selection = selection.Count > 0;
            bool has_single_selection = selection.Count == 1;

            UpdateAction ("AudiobookMerge", !has_single_selection, true);
            UpdateAction ("AudiobookEdit", true, has_selection);

            bool can_resume = false;
            if (has_single_selection) {
                var book = library.ActiveBook;
                if (book != null && library.GetLastPlayedBookmark (book.DbId) != null) {
                    var playback_book = library.PlaybackSource.Book;
                    if (playback_book == null || book.DbId != playback_book.DbId) {
                        can_resume = true;
                    }
                }
            }
            UpdateAction ("AudiobookResume", library.CurrentViewBook != null, can_resume);
            UpdateAction ("AudiobookResumeSelected", can_resume, true);
            UpdateAction ("AudiobookSwitchToGrid", library.CurrentViewBook != null, true);
        }

        private void OnResume (object to, EventArgs a)
        {
            var book = library.ActiveBook;
            var bookmark = library.GetLastPlayedBookmark (book.DbId);
            if (bookmark != null) {
                Log.DebugFormat ("Audiobook Library jumping to last-played position in active book: {0}", bookmark.Name);
                library.PlaybackSource.Book = book;
                ServiceManager.PlaybackController.Source = library;
                bookmark.JumpTo ();
            }
        }

        private void OnOpen (object o, EventArgs a)
        {
            var index = library.BooksModel.Selection.FocusedIndex;
            if (index > -1) {
                var book = library.BooksModel[index];
                library.SwitchToBookView (book as DatabaseAlbumInfo);
            }
        }

        private void OnEdit (object o, EventArgs a)
        {
            library.TrackModel.Selection.SelectAll ();
            Actions.TrackActions["SelectedTracksEditorAction"].Activate ();
        }

        private void OnMerge (object o, EventArgs a)
        {
            var discs = library.BooksModel.SelectedItems.OrderBy (d => d.Title).ToList ();
            var author = DatabaseArtistInfo.Provider.FetchSingle ((discs[0] as DatabaseAlbumInfo).ArtistId);

            var dialog = new HigMessageDialog (
                ServiceManager.Get<GtkElementsService> ().PrimaryWindow,
                DialogFlags.DestroyWithParent, MessageType.Question, ButtonsType.OkCancel,

                String.Format (Catalog.GetPluralString (
                    "Merge the {0} selected discs into one book?",
                    "Merge the {0} selected discs into one book?",
                    discs.Count), discs.Count),

                Catalog.GetString (
                    "This will ensure the disc numbers are all " + 
                    "set properly, and then set the author and book title for all tracks " +
                    "on all these discs to the values below")
            );

            var table = new SimpleTable<int> ();

            var author_entry = new Entry () { Text = discs[0].ArtistName };
            table.AddRow (0,
                new Label (Catalog.GetString ("Author:")) { Xalign = 0 },
                author_entry
            );

            var trimmings = new char [] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ' ', '-' };
            var title_entry  = new Entry () { Text = discs[0].Title.Trim (trimmings) };
            table.AddRow (1,
                new Label (Catalog.GetString ("Book Title:")) { Xalign = 0 },
                title_entry
            );

            dialog.LabelVBox.PackStart (table, false, false, 0);

            dialog.ShowAll ();
            var response = dialog.Run ();
            string title = title_entry.Text;
            string author_name = author_entry.Text;
            dialog.Destroy ();

            if (response == (int)Gtk.ResponseType.Ok && !String.IsNullOrEmpty (title)) {
                if (author_name != author.Name) {
                    author = DatabaseArtistInfo.FindOrCreate (author_name, null);
                }
                var book = DatabaseAlbumInfo.FindOrCreate (author, title, null, false);

                int disc_num = 1;
                foreach (DatabaseAlbumInfo disc in discs) {
                    // Update the disc num/count field for all tracks on this 'book' (actually just one disc of a book)
                    ServiceManager.DbConnection.Execute (
                        @"UPDATE CoreTracks SET AlbumID = ?, Disc = ?, DiscCount = ?, DateUpdatedStamp = ?
                            WHERE PrimarySourceID = ? AND AlbumID = ?",
                        book.DbId, disc_num++, discs.Count, DateTime.Now,
                        library.DbId, disc.DbId
                    );
                }

                // Update the MetadataHash for all those tracks
                DatabaseTrackInfo.UpdateMetadataHash (
                    book.Title, author.Name,
                    String.Format ("PrimarySourceId = {0} AND AlbumID = {1}", library.DbId, book.DbId)
                );

                library.NotifyTracksChanged ();
            }
        }
    }
}
