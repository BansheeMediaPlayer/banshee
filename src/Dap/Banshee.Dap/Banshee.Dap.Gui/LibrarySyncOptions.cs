//
// LibrarySyncOptions.cs
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
using System.Linq;

using Gtk;

using Hyena;
using Hyena.Data;
using Hyena.Widgets;

using Mono.Unix;

using Banshee.Dap;
using Banshee.Sources.Gui;
using Banshee.ServiceStack;
using Banshee.Preferences;
using Banshee.Sources;
using Banshee.Preferences.Gui;
using Banshee.Widgets;

namespace Banshee.Dap.Gui
{
    public class LibrarySyncOptions : IDisposable
    {
        private DapLibrarySync library_sync;
        private DictionaryComboBox<DatabaseSource> combo;
        private TreeIter sep_iter;
        private int playlist_count = 0;

        public Widget [] RowCells { get; private set; }

        public LibrarySyncOptions (DapLibrarySync library_sync)
        {
            this.library_sync = library_sync;
            var library = library_sync.Library;

            // Translators: {0} is the name of a library, eg 'Music' or 'Podcasts'
            var label = new Label (String.Format (Catalog.GetString ("{0}:"), library.Name)) { Xalign = 1f };

            // Create the combo for selecting what type of sync to do for this library
            combo = new DictionaryComboBox<DatabaseSource> ();
            combo.RowSeparatorFunc = (model, iter) => { return (string)model.GetValue (iter, 0) == "---"; };
            combo.Add (null, Catalog.GetString ("Manage manually"), -10);
            combo.Add (null, Catalog.GetString ("Sync entire library"), -9);

            foreach (var child in library.Children) {
                AddPlaylist (child);
            }

            library.ChildSourceAdded   += OnChildSourceAdded;
            library.ChildSourceRemoved += OnChildSourceRemoved;

            if (!library_sync.Enabled)
                combo.Active = 0;
            else if (library_sync.SyncEntireLibrary)
                combo.Active = 1;
            else if (library_sync.SyncSource != null)
                combo.ActiveValue = library_sync.SyncSource;

            combo.Changed += (o, a) => {
                library_sync.Enabled = combo.Active != 0;
                library_sync.SyncEntireLibrary = combo.Active == 1;

                if (combo.Active > 1) {
                    library_sync.SyncSource = combo.ActiveValue;
                }

                library_sync.MaybeTriggerAutoSync ();
            };

            RowCells = new Widget [] { label, combo };
        }

        public void Dispose ()
        {
            library_sync.Library.ChildSourceAdded   -= OnChildSourceAdded;
            library_sync.Library.ChildSourceRemoved -= OnChildSourceRemoved;

            foreach (var child in library_sync.Library.Children) {
                RemovePlaylist (child, true);
            }

            combo.Dispose ();
        }

        private void AddPlaylist (Source source)
        {
            var db_src = source as DatabaseSource;
            if (db_src == null)
                return;

            if (playlist_count == 0) {
                sep_iter = combo.Add (null, "---", -8);
            }

            // Translators: {0} is the name of a playlist
            combo.Add (db_src, String.Format (Catalog.GetString ("Sync from \u201c{0}\u201d"), db_src.Name), db_src.Order);
            db_src.Updated += OnPlaylistChanged;
            playlist_count++;
        }

        private void RemovePlaylist (Source source, bool disposing)
        {
            var db_src = source as DatabaseSource;
            if (db_src == null)
                return;

            // If this was the selected playlist, change to manually manage
            if (!disposing && db_src == combo.ActiveValue) {
                combo.Active = 0;
            }

            if (combo.Remove (db_src)) {
                db_src.Updated -= OnPlaylistChanged;
                playlist_count--;

                if (playlist_count == 0) {
                    combo.Remove (ref sep_iter);
                    sep_iter = TreeIter.Zero;
                }
            }
        }

        private void OnPlaylistChanged (object o, EventArgs args)
        {
            var db_src = o as DatabaseSource;
            combo.Update (db_src, String.Format (Catalog.GetString ("Sync from \u201c{0}\u201d"), db_src.Name), db_src.Order);
        }

        private void OnChildSourceAdded (SourceEventArgs args)
        {
            AddPlaylist (args.Source);
        }

        private void OnChildSourceRemoved (SourceEventArgs args)
        {
            RemovePlaylist (args.Source, false);
        }
    }
}
