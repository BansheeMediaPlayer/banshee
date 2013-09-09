//
// PreferencePage.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
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
using Gtk;

using Banshee.Preferences;

namespace Banshee.Preferences.Gui
{
    public class NotebookPage : VBox
    {
        private Page page;
        public Page Page {
            get { return page; }
        }

        private Label tab_widget;
        public Widget TabWidget {
            get { return tab_widget; }
        }

        public NotebookPage (Page page)
        {
            this.page = page;

            BorderWidth = 5;
            Spacing = 10;

            tab_widget = new Label (page.Name);
            tab_widget.Show ();

            Widget page_widget = page.DisplayWidget as Widget;
            if (page_widget != null) {
                page_widget.Show ();
                PackStart (page_widget, true, true, 0);
            } else {
                foreach (Section section in page) {
                    AddSection (section);
                }

                if (page.ChildPages.Count > 0) {
                    Notebook notebook = new Notebook ();
                    notebook.ShowBorder = false;
                    notebook.ShowTabs = false;
                    notebook.Show ();

                    var hbox = new HBox () { Spacing = 6 };
                    // FIXME this shouldn't be hard-coded to 'Source:', but this is the only
                    // user of this code atm...
                    var page_label = new Label (Mono.Unix.Catalog.GetString ("Source:"));
                    var page_combo = new PageComboBox (page.ChildPages, notebook);
                    hbox.PackStart (page_label, false, false, 0);
                    hbox.PackStart (page_combo, true, true, 0);
                    hbox.ShowAll ();

                    PackStart (hbox, false, false, 0);

                    HSeparator sep = new HSeparator ();
                    sep.Show ();
                    PackStart (sep, false, false, 0);

                    foreach (Page child_page in page.ChildPages) {
                        NotebookPage page_ui = new NotebookPage (child_page);
                        page_ui.BorderWidth = 0;
                        page_ui.Show ();
                        notebook.AppendPage (page_ui, null);
                    }

                    PackStart (notebook, true, true, 0);
                }
            }
        }

        private void AddSection (Section section)
        {
            Frame frame = null;

            if (section.Count == 0) {
                return;
            }

            if (section.ShowLabel) {
                frame = new Frame ();
                Label label = new Label ();
                label.Markup = String.Format ("<b>{0}</b>", GLib.Markup.EscapeText (section.Name));
                label.UseUnderline = true;
                label.Show ();
                frame.LabelWidget = label;
                frame.LabelXalign = 0.0f;
                frame.LabelYalign = 0.5f;
                frame.ShadowType = ShadowType.None;
                frame.Show ();
                PackStart (frame, false, false, 0);
            }

            Alignment alignment = new Alignment (0.0f, 0.0f, 1.0f, 1.0f);
            alignment.TopPadding = (uint)(frame == null ? 0 : 5);
            alignment.LeftPadding = section.ShowLabel ? (uint)12 : (uint)0;
            alignment.Show ();

            if (frame != null) {
                frame.Add (alignment);
            } else {
                PackStart (alignment, false, false, 0);
            }

            SectionBox box = new SectionBox (section);
            box.Show ();

            alignment.Add (box);
        }
    }
}
