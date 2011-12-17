//
// RangeEntry.cs
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
using Hyena.Gui;

namespace Banshee.Gui.TrackEditor
{
    public class RangeEntry : HBox, IEditorField
    {
        public delegate void RangeOrderClosure (RangeEntry entry);

        public event EventHandler Changed;

        private SpinButtonEntry from_entry;
        public SpinButtonEntry From {
            get { return from_entry; }
        }

        private SpinButtonEntry to_entry;
        public SpinButtonEntry To {
            get { return to_entry; }
        }

        public RangeEntry (string rangeLabel) : this (rangeLabel, null, null)
        {
        }

        public RangeEntry (string rangeLabel, RangeOrderClosure orderClosure, string orderTooltip)
        {
            AutoOrderButton auto_order_button;

            PackStart (from_entry = new SpinButtonEntry (0, 999, 1), true, true, 0);
            PackStart (new Label (rangeLabel), false, false, 6);
            PackStart (to_entry = new SpinButtonEntry (0, 999, 1), true, true, 0);
            if (orderClosure != null) {
                PackStart (auto_order_button = new AutoOrderButton (), false, false, 1);
                auto_order_button.Clicked += delegate { orderClosure (this); };
                if (orderTooltip != null) {
                    auto_order_button.TooltipText = orderTooltip;
                }
            }

            ShowAll ();

            from_entry.WidthChars = 2;
            to_entry.WidthChars = 2;

            from_entry.ValueChanged += OnChanged;
            to_entry.ValueChanged += OnChanged;
            from_entry.Output += OnOutput;
            to_entry.Output += OnOutput;
        }

        public void SetAsReadOnly ()
        {
            from_entry.IsEditable = false;
            to_entry.IsEditable = false;
        }

        protected override bool OnMnemonicActivated (bool group_cycling)
        {
            return from_entry.MnemonicActivate (group_cycling);
        }

        protected void OnOutput (object o, OutputArgs args)
        {
            SpinButtonEntry entry = (SpinButtonEntry) o;

            if (0 == entry.ValueAsInt) {
                entry.Text = "";
            } else {
                entry.Text = entry.ValueAsInt.ToString ();
            }
            args.RetVal = 1;
        }

        private class AutoOrderButton : Button
        {
            public AutoOrderButton ()
            {
                Image image = new Image (Gtk.Stock.SortAscending, IconSize.Menu);
                Add (image);
                Relief = ReliefStyle.None;
                image.Show ();
            }
        }

        private void OnChanged (object o, EventArgs args)
        {
            EventHandler handler = Changed;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }
    }
}
