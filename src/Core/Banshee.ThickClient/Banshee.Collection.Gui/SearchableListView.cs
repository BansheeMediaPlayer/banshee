//
// SearchableListView.cs
//
// Author:
//   Neil Loknath <neil.loknath@gmail.com>
//
// Copyright (C) 2009 Neil Loknath
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
using Mono.Unix;
using Gtk;

using Hyena.Data;
using Hyena.Data.Gui;
using Hyena.Gui;
using Hyena.Query;
using Hyena.Widgets;

using Banshee.Gui;
using Banshee.Collection;
using Banshee.Collection.Database;

namespace Banshee.Collection.Gui
{
    public class SearchableListView<T> : ListView<T>
    {
        protected SearchableListView () : base ()
        {
        }

        protected SearchableListView (IntPtr raw) : base (raw)
        {
        }

        private EntryPopup search_popup;

        private long previous_search_offset;
        private long search_offset = 0;

        private QueryFieldSet last_query_fields = null;

        private QueryNode last_query = null;
        public QueryNode LastQuery {
            get { return last_query; }
        }

        public virtual bool SelectOnRowFound {
            get { return true; }
        }

        private QueryTermNode CreateNode (QueryField field, Operator op, string target)
        {
            QueryTermNode node = new QueryTermNode ();
            if (field == null || op == null || String.IsNullOrEmpty (target)) {
                return node;
            }

            node.Field = field;
            node.Operator = op;
            node.Value = QueryValue.CreateFromStringValue (target, field);

            return node;
        }

        private QueryNode BuildQueryTree (QueryFieldSet fields, Operator op, string target)
        {
            return BuildQueryTree (fields, op, target, false);
        }

        private QueryNode BuildQueryTree (QueryFieldSet fields, Operator op, string target, bool force)
        {
            if (fields == null || op == null || String.IsNullOrEmpty (target)) {
                return null;
            }

            QueryListNode root = new QueryListNode (Keyword.Or);

            foreach (QueryField field in fields) {
                if (force || field.IsDefault) {
                    root.AddChild (CreateNode (field, op, target));
                }
            }

            // force the query to build if no default fields in QueryFieldSet
            if (!force && root.ChildCount == 0) {
                return BuildQueryTree (fields, op, target, true);
            }

            return root.Trim ();
        }

        private void UpdateQueryTree (QueryNode query, string target)
        {
            if (query == null || String.IsNullOrEmpty (target)) {
                return;
            }

            foreach (QueryTermNode node in query.GetTerms ()) {
                node.Value = QueryValue.CreateFromStringValue (target, node.Field);
            }
        }

        private bool PerformSearch (string target)
        {
            if (String.IsNullOrEmpty (target)) {
                return false;
            }

            ISearchable model = Model as ISearchable;
            if (model == null) {
                return false;
            }

            if (last_query == null || !last_query_fields.Equals (model.QueryFields)) {
                last_query_fields = model.QueryFields;
                last_query = BuildQueryTree (last_query_fields, StringQueryValue.StartsWith, target);
            } else {
                UpdateQueryTree (last_query, target);
            }

            int i = model.IndexOf (last_query, search_offset);
            if (i >= 0) {
                SelectRow (i);
                return true;
            }

            return false;
        }

        private void SelectRow (int i)
        {
            CenterOn (i);

            Selection.FocusedIndex = i;
            if (SelectOnRowFound) {
                Selection.Clear (false);
                Selection.Select (i);
            }

            InvalidateList ();
        }


        /*private bool IsCharValid (char c)
        {
            return Char.IsLetterOrDigit (c) ||
                Char.IsPunctuation (c) ||
                Char.IsSymbol (c);
        }*/

        protected override bool OnKeyPressEvent (Gdk.EventKey press)
        {
            // TODO this pops it up whenever any key is pressed, like a normal TreeView.
            // But, Banshee has single-char keybindings, so for now at least, require
            // ? (== shift / on US keyboards, at least) to be pressed.
            /*char input = Convert.ToChar (Gdk.Keyval.ToUnicode (press.KeyValue));
            if (!IsCharValid (input) || Model as ISelectable == null) {
                return base.OnKeyPressEvent (press);
            }*/

            if (press.Key != Gdk.Key.question) {
                return base.OnKeyPressEvent (press);
            }

            if (search_popup == null) {
                search_popup = new EntryPopup ();
                search_popup.Changed += (o, a) => {
                    search_offset = 0;
                    PerformSearch (search_popup.Text);
                };

                search_popup.KeyPressed += OnPopupKeyPressed;
            }

            search_popup.Position (EventWindow);
            search_popup.HasFocus = true;
            search_popup.Show ();
            search_popup.Text = String.Format ("{0}{1}", search_popup.Text, "");//input);
            search_popup.Entry.Position = search_popup.Text.Length;
            return true;
        }

        private void OnPopupKeyPressed (object sender, KeyPressEventArgs args)
        {
            bool search_forward = false;
            bool search_backward = false;

            Gdk.EventKey press = args.Event;
            Gdk.Key key = press.Key;

            switch (key) {
                case Gdk.Key.Up:
                case Gdk.Key.KP_Up:
                    search_backward = true;
                    break;
                case Gdk.Key.g:
                case Gdk.Key.G:
                    if ((press.State & Gdk.ModifierType.ControlMask) != 0) {
                        if ((press.State & Gdk.ModifierType.ShiftMask) != 0) {
                            search_backward = true;
                        } else {
                            search_forward = true;
                        }
                    }
                    break;
                case Gdk.Key.F3:
                case Gdk.Key.KP_F3:
                    if ((press.State & Gdk.ModifierType.ShiftMask) != 0) {
                        search_backward = true;
                    } else {
                        search_forward = true;
                    }
                    break;
                case Gdk.Key.Down:
                case Gdk.Key.KP_Down:
                    search_forward = true;
                    break;
            }

            if (search_forward) {
                previous_search_offset = search_offset++;
                if (!PerformSearch (search_popup.Text)) {
                   search_offset = previous_search_offset;
               }
            } else if (search_backward) {
                search_offset = search_offset == 0 ? 0 : search_offset - 1;
                PerformSearch (search_popup.Text);
            }

            args.RetVal = search_forward || search_backward;
        }
    }
}
