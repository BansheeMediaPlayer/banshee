//
// AudiobookLibrarySource.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2008-2009 Novell, Inc.
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
using System.Collections.Generic;

using Mono.Unix;

using Hyena;

using Banshee.Library;
using Banshee.Collection;
using Banshee.SmartPlaylist;
using Banshee.Collection.Database;
using Banshee.MediaEngine;
using Banshee.Sources;
using Banshee.Database;
using Banshee.ServiceStack;

using Banshee.Sources.Gui;
using Banshee.PlaybackController;
using Banshee.Query;

namespace Banshee.Audiobook
{
    public class AudiobookLibrarySource : LibrarySource, IBasicPlaybackController
    {
        internal const string LAST_PLAYED_BOOKMARK = "audiobook-lastplayed";

        AudiobookModel books_model;

        LazyLoadSourceContents<AudiobookContent> grid_view;
        LazyLoadSourceContents<BookView> book_view;

        public BookPlaylist PlaybackSource { get; private set; }

        Gtk.HBox title_switcher;
        Gtk.Label book_label;

        public Actions Actions { get; private set; }

        public AudiobookLibrarySource () : base (Catalog.GetString ("Audiobooks"), "AudiobookLibrary", 49)
        {
            MediaTypes = TrackMediaAttributes.AudioBook;
            NotMediaTypes = TrackMediaAttributes.Podcast | TrackMediaAttributes.VideoStream | TrackMediaAttributes.Music;
            SupportsPlaylists = false;

            Properties.SetStringList ("Icon.Name", "audiobook", "source-library");
            Properties.Set<string> ("SearchEntryDescription", Catalog.GetString ("Search your audiobooks"));
            Properties.SetString ("TrackView.ColumnControllerXml", String.Format (@"
                <column-controller>
                  <add-all-defaults />
                  <remove-default column=""DiscColumn"" />
                  <remove-default column=""AlbumColumn"" />
                  <remove-default column=""ComposerColumn"" />
                  <remove-default column=""AlbumArtistColumn"" />
                  <remove-default column=""ConductorColumn"" />
                  <remove-default column=""ComposerColumn"" />
                  <remove-default column=""BpmColumn"" />
                  <sort-column direction=""asc"">track_title</sort-column>
                  <column modify-default=""ArtistColumn"">
                    <title>{0}</title>
                    <long-title>{0}</long-title>
                  </column>
                </column-controller>
            ", Catalog.GetString ("Author")));

            var pattern = new AudiobookFileNamePattern ();
            pattern.FolderSchema = CreateSchema<string> ("folder_pattern", pattern.DefaultFolder, "", "");
            pattern.FileSchema   = CreateSchema<string> ("file_pattern",   pattern.DefaultFile, "", "");
            SetFileNamePattern (pattern);

            Actions = new Actions (this);

            grid_view = new LazyLoadSourceContents<AudiobookContent> ();
            book_view = new LazyLoadSourceContents<BookView> ();
            Properties.Set<ISourceContents> ("Nereid.SourceContents", grid_view);

            Properties.Set<System.Reflection.Assembly> ("ActiveSourceUIResource.Assembly", System.Reflection.Assembly.GetExecutingAssembly ());
            Properties.SetString ("ActiveSourceUIResource", "ActiveSourceUI.xml");
            Properties.Set<bool> ("ActiveSourceUIResourcePropagate", true);
            Properties.Set<System.Action> ("ActivationAction", delegate { SwitchToGridView (); });

            title_switcher = new Gtk.HBox () { Spacing = 0 };
            var title_label = new Gtk.Label () { Markup = String.Format ("<b>{0}</b>", GLib.Markup.EscapeText (this.Name)) };
            var b = new Gtk.Button (title_label) { Relief = Gtk.ReliefStyle.None };
            b.Clicked += delegate { SwitchToGridView (); };
            title_switcher.PackStart (b);

            title_switcher.ShowAll ();
            book_label = new Gtk.Label () { Visible = false };
            title_switcher.PackStart (book_label);
            Properties.Set<Gtk.Widget> ("Nereid.SourceContents.TitleWidget", title_switcher);

            TracksAdded += (o, a) => {
                if (!IsAdding) {
                    MergeBooksAddedSince (DateTime.Now - TimeSpan.FromHours (2));

                    ServiceManager.DbConnection.Execute (
                        "UPDATE CoreTracks SET Attributes = Attributes | ? WHERE PrimarySourceID = ?",
                        (int)TrackMediaAttributes.AudioBook, this.DbId);
                }
            };

            TrackIsPlayingHandler = ServiceManager.PlayerEngine.IsPlaying;

            PlaybackSource = new BookPlaylist ("audiobook-playback-source", this);
            PlaybackSource.DatabaseTrackModel.ForcedSortQuery = BansheeQuery.GetSort ("track", true);

            ServiceManager.PlaybackController.SourceChanged += OnPlaybackSourceChanged;

            // Listen for playback changes and auto-set the last-played bookmark
            ServiceManager.PlayerEngine.ConnectEvent (OnPlayerEvent,
                PlayerEvent.StartOfStream | PlayerEvent.EndOfStream | PlayerEvent.Seek,
                true);
        }

        public override string GetPluralItemCountString (int count)
        {
            return Catalog.GetPluralString ("{0} book", "{0} books", count);
        }

        protected override string SectionName {
            get { return Catalog.GetString ("Audiobooks Folder"); }
        }

        private void OnPlaybackSourceChanged (object o, EventArgs args)
        {
            if (ServiceManager.PlaybackController.Source == this) {
                PlaybackSource.Book = PlaybackSource.Book ?? ActiveBook;
            } else {
                PlaybackSource.Book = null;
            }

            Actions.UpdateActions ();
        }

        public override bool CanSearch {
            get { return false; }
        }

        internal void SwitchToGridView ()
        {
            var last_book = CurrentViewBook;
            if (last_book != null) {
                CurrentViewBook = null;
                book_label.Visible = false;
                Properties.Set<ISourceContents> ("Nereid.SourceContents", grid_view);
                Actions.UpdateActions ();
            }
        }

        public void SwitchToBookView (DatabaseAlbumInfo book)
        {
            if (!book_label.Visible) {
                CurrentViewBook = book;
                book_label.Text = String.Format (" »  {0}", book.DisplayTitle);
                book_label.Visible = true;
                book_view.SetSource (this);
                book_view.Contents.SetBook (book);
                Properties.Set<ISourceContents> ("Nereid.SourceContents", book_view);

                if (BooksModel.Selection.Count != 1) {
                    var index = BooksModel.Selection.FocusedIndex;
                    BooksModel.Selection.Clear (false);
                    BooksModel.Selection.Select (index);
                }

                Actions.UpdateActions ();
            }
        }

        public DatabaseAlbumInfo CurrentViewBook { get; private set; }

        public DatabaseAlbumInfo ActiveBook {
            get {
                if (CurrentViewBook != null) {
                    return CurrentViewBook;
                }

                if (BooksModel.Selection.FocusedIndex != -1) {
                    return BooksModel [BooksModel.Selection.FocusedIndex] as DatabaseAlbumInfo;
                }

                if (BooksModel.Selection.Count > 0) {
                    return BooksModel.SelectedItems.First () as DatabaseAlbumInfo;
                }

                if (BooksModel.Count > 0) {
                    return BooksModel[0] as DatabaseAlbumInfo;
                }

                return null;
            }
        }

        private void MergeBooksAddedSince (DateTime since)
        {
            // TODO after import of files or move to audiobook:
            // If for a given author, there are a set of 'albums' (books)
            // whose names stripped of numbers are equal, merge them
            // into one book:
            //    1) If they already have sequential disc info, good to go
            //    2) If they do not, need to extract that from album title
            //        -- or just generate it by incrementing a counter, assuming
            //           that as-is they sort lexically

            //foreach (var book in BookModel.FetchMatching ("DateAdded > ? ORDER BY Title", since)) {
            //}
        }

        private DatabaseTrackInfo book_track;
        private void OnPlayerEvent (PlayerEventArgs args)
        {
            book_track = ServiceManager.PlayerEngine.CurrentTrack as DatabaseTrackInfo;
            if (book_track == null || book_track.PrimarySourceId != this.DbId) {
                book_track = null;
            }

            switch (args.Event) {
                case PlayerEvent.StartOfStream:
                    if (book_track != null) {
                        StartTimeout ();

                        if (PlaybackSource.Book == null || PlaybackSource.Book.DbId != book_track.AlbumId) {
                            PlaybackSource.Book = DatabaseAlbumInfo.Provider.FetchSingle (book_track.AlbumId);
                        }

                        if (book_track.CacheModelId != PlaybackSource.DatabaseTrackModel.CacheId) {
                            var index = PlaybackSource.DatabaseTrackModel.IndexOfFirst (book_track);
                            if (index >= 0) {
                                ServiceManager.PlaybackController.PriorTrack = PlaybackSource.TrackModel [index];
                            } else {
                                Log.Error ("Audiobook track started, but couldn't find in the Audiobook.PlaybackSource");
                            }
                        }
                    }
                    break;
                case PlayerEvent.EndOfStream:
                    StopTimeout ();
                    break;
                case PlayerEvent.Seek:
                    UpdateLastPlayed ();
                    break;
            }
        }

        private uint timeout_id;
        private void StartTimeout ()
        {
            if (timeout_id == 0) {
                timeout_id = Application.RunTimeout (3000, delegate { UpdateLastPlayed (); return true; });
            }
        }

        private void StopTimeout ()
        {
            if (timeout_id != 0) {
                Application.IdleTimeoutRemove (timeout_id);
                timeout_id = 0;
            }
        }

        private Bookmark bookmark;
        private void UpdateLastPlayed ()
        {
            if (book_track != null && book_track.IsPlaying &&
                ServiceManager.PlayerEngine.CurrentState == PlayerState.Playing)
            {
                bookmark = GetLastPlayedBookmark (book_track.AlbumId) ?? new Bookmark ();

                bookmark.Type = LAST_PLAYED_BOOKMARK;
                bookmark.CreatedAt = DateTime.Now;
                bookmark.Track = book_track;
                bookmark.Position = TimeSpan.FromMilliseconds ((int)ServiceManager.PlayerEngine.Position);
                bookmark.Save ();

                if (CurrentViewBook != null && book_track.AlbumId == CurrentViewBook.DbId) {
                    book_view.Contents.UpdateResumeButton (bookmark);
                }
            }
        }

        public Bookmark GetLastPlayedBookmark (int book_id)
        {
            return Bookmark.Provider.FetchFirstMatching (
                "Type = ? AND TrackID IN (SELECT TrackID FROM CoreTracks WHERE PrimarySourceID = ? AND AlbumID = ?)",
                LAST_PLAYED_BOOKMARK, this.DbId, book_id
            );
        }

        public override void Dispose ()
        {
            ServiceManager.PlaybackController.SourceChanged -= OnPlaybackSourceChanged;
            ServiceManager.PlayerEngine.DisconnectEvent (OnPlayerEvent);

            if (Actions != null) {
                Actions.Dispose ();
                Actions = null;
            }

            base.Dispose ();
        }

        protected override IEnumerable<IFilterListModel> CreateFiltersFor (DatabaseSource src)
        {
            var books_model = new AudiobookModel (this, this.DatabaseTrackModel, ServiceManager.DbConnection, this.UniqueId);
            if (src == this) {
                this.books_model = books_model;
            }

            yield return books_model;
        }

        #region IBasicPlaybackController implementation

        public bool First ()
        {
            return DoPlaybackAction ();
        }


        public bool Next (bool restart, bool changeImmediately)
        {
            return DoPlaybackAction ();
        }

        public bool Previous (bool restart)
        {
            return DoPlaybackAction ();
        }

        #endregion

        private bool DoPlaybackAction ()
        {
            PlaybackSource.Book = PlaybackSource.Book ?? ActiveBook;
            ServiceManager.PlaybackController.Source = PlaybackSource;
            ServiceManager.PlaybackController.NextSource = this;
            return false;
        }

        public DatabaseAlbumListModel BooksModel {
            get { return books_model; }
        }

        public override string DefaultBaseDirectory {
            // FIXME should probably translate this fallback directory
            get { return XdgBaseDirectorySpec.GetXdgDirectoryUnderHome ("XDG_AUDIOBOOKS_DIR", "Audiobooks"); }
        }

        public override int Count {
            get { return 0; }
        }

        public override int FilteredCount {
            get { return books_model.Count; }
        }

        public override TimeSpan Duration {
            get { return DatabaseTrackModel.UnfilteredDuration; }
        }

        public override long FileSize {
            get { return DatabaseTrackModel.UnfilteredFileSize; }
        }

        public override bool ShowBrowser {
            get { return false; }
        }

        protected override bool HasArtistAlbum {
            get { return false; }
        }

        public override bool CanShuffle {
            get { return false; }
        }

        public override IEnumerable<SmartPlaylistDefinition> DefaultSmartPlaylists {
            get { yield break; }
        }
    }
}
