//
// FixSource.cs
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
using Hyena.Data.Sqlite;

using Banshee.ServiceStack;
using Banshee.Gui;
using Banshee.Sources;
using Banshee.Preferences;

namespace Banshee.Fixup
{
    public class FixSource : Source, IUnmapableSource
    {
        ProblemModel problem_model = new ProblemModel ();
        private SourcePage page = null;
        private HashSet<Section> sections = new HashSet<Section> ();

        public FixSource () : base (Catalog.GetString ("Metadata Fixer"), Catalog.GetString ("Metadata Fixer"), -1)
        {
            TypeUniqueId = "fixes";

            var header_widget = new HBox () { Spacing = 6 };

            header_widget.PackStart (new Label (Catalog.GetString ("Problem Type:")), false, false, 0);

            var combo = new Banshee.Widgets.DictionaryComboBox<Solver> ();
            foreach (var solver in problem_model.Solvers) {
                if (solver.PreferencesSection != null) {
                    sections.Add (solver.PreferencesSection);
                }
                combo.Add (solver.Name, solver);
            }

            if (sections.Count > 0) {
                page = new SourcePage (this);
                foreach (var section in sections) {
                    page.Add (section);
                }
            }

            combo.Changed += (o, a) => {
                problem_model.Solver = combo.ActiveValue;
                SetStatus (problem_model.Solver.Description, false, false, "gtk-info");
            };
            combo.Active = 0;

            var apply_button = new Hyena.Widgets.ImageButton (Catalog.GetString ("Apply Selected Fixes"), "gtk-apply");
            apply_button.Clicked += (o, a) => problem_model.Fix ();
            problem_model.Reloaded += (o, a) => apply_button.Sensitive = problem_model.SelectedCount > 0;

            header_widget.PackStart (combo, false, false, 0);
            header_widget.PackStart (apply_button, false, false, 0);
            header_widget.ShowAll ();

            Properties.SetStringList ("Icon.Name", "search", "gtk-search");
            Properties.SetString ("ActiveSourceUIResource", "ActiveUI.xml");
            Properties.SetString ("GtkActionPath", "/FixSourcePopup");
            Properties.Set<Gtk.Widget> ("Nereid.SourceContents.HeaderWidget", header_widget);
            Properties.Set<Banshee.Sources.Gui.ISourceContents> ("Nereid.SourceContents", new View (problem_model));
            Properties.SetString ("UnmapSourceActionLabel", Catalog.GetString ("Close"));
            Properties.SetString ("UnmapSourceActionIconName", "gtk-close");

            var actions = new BansheeActionGroup ("fix-source");
            actions.AddImportant (
                new ActionEntry ("RefreshProblems", Stock.Refresh, Catalog.GetString ("Refresh"), null, null, (o, a) => {
                    problem_model.Refresh ();
                })
            );
            actions.Register ();

            problem_model.Reload ();
        }

        public bool CanUnmap { get { return true; } }
        public bool ConfirmBeforeUnmap { get { return false; } }

        public bool Unmap ()
        {
            Parent.RemoveChildSource (this);
            Properties.Get<Banshee.Sources.Gui.ISourceContents> ("Nereid.SourceContents").Widget.Destroy ();
            problem_model.Dispose ();
            page.Dispose ();
            return true;
        }

        public override string PreferencesPageId {
            get { return page == null ? String.Empty : page.Id; }
        }
    }
}
