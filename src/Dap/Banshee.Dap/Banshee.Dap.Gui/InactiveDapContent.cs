//
// InactiveContent.cs
//
// Author:
//   Nicholas Little <arealityfarbetween@googlemail.com>
//
// Copyright 2014 Nicholas Little
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
    public class InactiveDapContent : DapPropertiesDisplay
    {
        Label title;

        // To avoid the GLib.MissingIntPtrCtorException seen by some; BGO #552169
        protected InactiveDapContent (IntPtr ptr) : base (ptr)
        {
        }

        public InactiveDapContent (DapSource dapSource) : base(dapSource)
        {
            dapSource.Properties.PropertyChanged += OnPropertyChanged;
            BuildWidgets ();
        }

        private void BuildWidgets ()
        {
            var outer = new HBox();
            var device = new Image (LargeIcon) { Yalign = 0.0f };
            outer.PackStart (device, false, false, 0);

            var inner = new VBox { Spacing = 5, BorderWidth = 5 };
            title = new Label { UseMarkup = true, Xalign = 0.0f };
            SetTitleText (Source.Name);
            inner.PackStart (title, false, false, 0);

            var box = new HBox { Spacing = 5 };
            box.PackStart (new Image { IconName = "dialog-warning" }, false, false, 0);
            box.PackStart (new Label { Markup = ErrorString, UseMarkup = true }, false, false, 0);
            inner.PackStart (box, false, false, 0);

            outer.PackEnd (inner, false, false, 0);
            Add (outer);
            ShowAll ();
        }

        private void SetTitleText (string name)
        {
            title.Markup = String.Format (@"<span size=""x-large"" weight=""bold"">{0}</span>", name);
        }

        private void OnPropertyChanged (object o, PropertyChangeEventArgs args)
        {
            if (args.PropertyName == "Name") {
                SetTitleText (args.NewValue.ToString ());
            }
        }

        protected virtual string ErrorString {
            get { return DefaultErrorString; }
        }

        static InactiveDapContent ()
        {
            var generic = Catalog.GetString ("Your device appears to be in use by another program");
            var claimit = String.Format (
                @"<span weight=""bold"">{0}</span>",
                Catalog.GetString ("Claim")
            );
            var pressit = String.Format (
                Catalog.GetString ("Press the {0} button above to use it in Banshee"),
                claimit
            );
            DefaultErrorString = string.Format (
                @"<span size=""large"">{0}." + "\n" + "{1}…</span>", generic, pressit
            );
            DapContent.BuildActions ();
        }

        private static readonly string DefaultErrorString;
    }
}
