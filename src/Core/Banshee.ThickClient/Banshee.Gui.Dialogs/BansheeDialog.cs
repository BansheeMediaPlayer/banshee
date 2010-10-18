//
// BansheeDialog.cs
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

using Banshee.ServiceStack;
using Banshee.Gui;

namespace Banshee.Gui.Dialogs
{
    public class BansheeDialog : Gtk.Dialog
    {
        private AccelGroup accel_group;
        protected AccelGroup AccelGroup {
            get { return accel_group; }
        }

        private VBox inner_vbox;

        public BansheeDialog () : this (null, null)
        {
        }

        public BansheeDialog (string title) : this (title, null)
        {
        }

        public BansheeDialog (string title, Window parent) : base ()
        {
            Title = title ?? String.Empty;

            // The BorderWidth situation here is a bit nuts b/c the
            // ActionArea's is set to 5.  So we work everything else out
            // so it all totals to 12.
            //
            // WIDGET           BorderWidth
            // Dialog           5
            //   VBox           2
            //     inner_vbox   5 => total = 12
            //     ActionArea   5 => total = 12
            BorderWidth = 5;
            base.VBox.BorderWidth = 0;

            // This spacing is 2 b/c the inner_vbox and ActionArea should be
            // 12 apart, and they already have BorderWidth 5 each
            base.VBox.Spacing = 2;

            inner_vbox = new VBox () { Spacing = 12, BorderWidth = 5, Visible = true };
            base.VBox.PackStart (inner_vbox, true, true, 0);

            Visible = false;
            HasSeparator = false;

            if (parent == null) {
                GtkElementsService service = ServiceManager.Get<GtkElementsService> ();
                if (service != null) {
                    TransientFor = service.PrimaryWindow;
                }
            } else {
                TransientFor = parent;
            }

            WindowPosition = TransientFor == null || !TransientFor.Visible
                ? WindowPosition.Center
                : WindowPosition.CenterOnParent;
            DestroyWithParent = true;

            accel_group = new AccelGroup ();
            AddAccelGroup (accel_group);
        }

        public new VBox VBox { get { return inner_vbox; } }

        public new ResponseType Run ()
        {
            return (ResponseType)base.Run ();
        }

        public Button AddDefaultCloseButton ()
        {
            return AddStockButton (Stock.Close, ResponseType.Close);
        }

        public new Button AddButton (string message, ResponseType response)
        {
            return AddButton (message, response, false);
        }

        public Button AddStockButton (string stock, ResponseType response)
        {
            return AddStockButton (stock, response, false);
        }

        public Button AddButton (string message, ResponseType response, bool isDefault)
        {
            return AddButton (message, response, isDefault, false);
        }

        public Button AddStockButton (string stock, ResponseType response, bool isDefault)
        {
            return AddButton (stock, response, isDefault, true);
        }

        public Button AddButton (string message, ResponseType response, bool isDefault, bool isStock)
        {
            Button button = new Button (message);
            button.CanDefault = true;
            button.UseStock = isStock;
            button.Show ();
            AddButton (button, response, isDefault);
            return button;
        }

        public Button AddButton (Button button, ResponseType response, bool isDefault)
        {
            AddActionWidget (button, response);

            if (isDefault) {
                DefaultResponse = response;
                button.AddAccelerator ("activate", accel_group, (uint)Gdk.Key.Return, 0, AccelFlags.Visible);
            }

            return button;
        }
    }
}
