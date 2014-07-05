//
// DapContent.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
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
    public class DapContent : DapPropertiesDisplay
    {
        private Label title;
        private SimpleTable<DapLibrarySync> table;
        private Dictionary<DapLibrarySync, LibrarySyncOptions> library_opts = new Dictionary<DapLibrarySync, LibrarySyncOptions> ();
        private DapSource dap;

        // Ugh, this is to avoid the GLib.MissingIntPtrCtorException seen by some; BGO #552169
        protected DapContent (IntPtr ptr) : base (ptr)
        {
        }

        public DapContent (DapSource source) : base (source)
        {
            dap = source;

            BuildWidgets ();
            BuildActions ();
            dap.Properties.PropertyChanged += OnPropertyChanged;
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                foreach (var opt in library_opts.Values) {
                    opt.Dispose ();
                }
                library_opts.Clear ();
            }
            
            base.Dispose (disposing);
        }

        private void BuildWidgets ()
        {
            HBox split_box = new HBox ();
            VBox content_box = new VBox ();

            content_box.BorderWidth = 5;

            title = new Label ();
            SetTitleText (dap.Name);
            title.Xalign = 0.0f;

            // Define custom preference widgetry
            var hbox = new HBox ();
            table = new SimpleTable<DapLibrarySync> ();

            dap.Sync.LibraryAdded += l => AddLibrary (l);
            dap.Sync.LibraryRemoved += l => RemoveLibrary (l);

            foreach (var sync in dap.Sync.Libraries) {
                AddLibrary (sync);
            }

            hbox.PackStart (table, false, false, 0);
            hbox.ShowAll ();
            dap.Preferences["sync"]["library-options"].DisplayWidget = hbox;

            var properties = new Banshee.Preferences.Gui.NotebookPage (dap.Preferences) {
                BorderWidth = 0
            };

            content_box.PackStart (title, false, false, 0);
            content_box.PackStart (properties, false, false, 0);

            var image = new Image (LargeIcon) { Yalign = 0.0f };

            split_box.PackStart (image, false, true, 0);
            split_box.PackEnd (content_box, true, true, 0);

            Add (split_box);
            ShowAll ();
        }

        private void AddLibrary (DapLibrarySync library_sync)
        {
            var opts = new LibrarySyncOptions (library_sync);
            table.AddRow (library_sync, opts.RowCells);
            table.ShowAll ();
            library_opts.Add (library_sync, opts);
        }

        private void RemoveLibrary (DapLibrarySync library_sync)
        {
            table.RemoveRow (library_sync);
            var opts = library_opts[library_sync];
            library_opts.Remove (library_sync);
            opts.Dispose ();
        }

        internal static void BuildActions()
        {
            if (actions == null) {
                actions = new DapActions ();
            }
        }

        private void SetTitleText (string name)
        {
            title.Markup = String.Format ("<span size=\"x-large\" weight=\"bold\">{0}</span>", name);
        }

        private void OnPropertyChanged (object o, PropertyChangeEventArgs args)
        {
            if (args.PropertyName == "Name")
                SetTitleText (args.NewValue.ToString ());
        }

        private static Banshee.Gui.BansheeActionGroup actions;
    }
}
