//
// View.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright 2010 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;

using Mono.Unix;

using Gtk;

using Hyena;
using Hyena.Data;
using Hyena.Data.Gui;
using Hyena.Data.Sqlite;

using Hyena.Widgets;

using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Library;

using Banshee.Gui;
using Banshee.Sources.Gui;
using Banshee.Preferences.Gui;

namespace Banshee.Fixup
{
    public class View : RoundedFrame, ISourceContents
    {
        public View (ProblemModel model)
        {
            var view = new ProblemListView (model);
            var sw = new Gtk.ScrolledWindow () {
                ShadowType = ShadowType.None,
                BorderWidth = 0
            };
            sw.Add (view);

            Add (sw);
            ShowAll ();
        }

        protected View (IntPtr ptr) : base (ptr)
        {
        }

        private class ProblemListView : ListView<Problem>
        {
            ProblemModel model;
            public ProblemListView (ProblemModel model)
            {
                this.model = model;
                SetModel (model);
                ColumnController = new ColumnController ();

                var selected = new ColumnCellCheckBox ("SavedSelected", true);
                selected.Toggled += (o, a) => model.Reload ();
                ColumnController.Add (new Column (Catalog.GetString ("Fix?"), selected, 0));

                var summary = new ColumnCellSolutionOptions ();
                var summary_col = new Column ("", summary, 1.0);
                ColumnController.Add (summary_col);
                model.Reloaded += (o, a) => summary_col.Title = model.Solver.Name;

                RowOpaquePropertyName = "Selected";
                RulesHint = true;
                RowActivated += (o, e) => model.ToggleSelection ();
            }

            protected override bool OnKeyPressEvent (Gdk.EventKey press)
            {
                switch (press.Key) {
                    case Gdk.Key.space:
                    case Gdk.Key.Return:
                    case Gdk.Key.KP_Enter:
                        model.ToggleSelection ();
                        return true;
                }

                return base.OnKeyPressEvent (press);
            }

            protected override bool OnPopupMenu ()
            {
                // TODO add a context menu w/ Select and Unselect options
                //ServiceManager.Get<InterfaceActionService> ().TrackActions["TrackContextMenuAction"].Activate ();
                return true;
            }
        }

#region ISourceContents

        private MusicLibrarySource source;
        public bool SetSource (ISource source)
        {
            this.source = source as MusicLibrarySource;
            return this.source != null;
        }

        public ISource Source {
            get { return source; }
        }

        public void ResetSource ()
        {
            source = null;
        }

        public Widget Widget {
            get { return this; }
        }

#endregion
    }
}
