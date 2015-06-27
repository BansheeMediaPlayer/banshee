//
// ConnectedMessageBar.cs
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

using Hyena;
using Hyena.Data;
using Hyena.Widgets;

using Banshee.Sources;
using Banshee.ServiceStack;
using Banshee.Sources.Gui;
using Banshee.Base;

namespace Banshee.Gui.Widgets
{
    public class ConnectedMessageBar : MessageBar
    {
        private Source source;

        private class ActionButton : Button
        {
            private MessageAction action;

            public ActionButton (MessageAction action) : base (action.Label)
            {
                this.action = action;
            }

            protected override void OnClicked ()
            {
                action.Activate ();
            }
        }

        public ConnectedMessageBar ()
        {
            CloseClicked += OnCloseClicked;
            ServiceManager.SourceManager.ActiveSourceChanged += OnActiveSourceChanged;
            ConnectSource (ServiceManager.SourceManager.ActiveSource);

            LeftPadding = 15;
        }

        protected ConnectedMessageBar (IntPtr raw) : base (raw)
        {
        }

        private void ConnectSource (Source source)
        {
            if (source == this.source)
                return;

            this.source = source;

            if (this.source != null) {
                this.source.MessageNotify += OnSourceMessageNotify;
                Update ();
            }
        }

        private void Update ()
        {
            ThreadAssist.ProxyToMain (InnerUpdate);
        }

        private void InnerUpdate ()
        {
            if (source == null || source.CurrentMessage == null || source.CurrentMessage.IsHidden) {
                Hide ();
                return;
            }

            Message = source.CurrentMessage.Text;
            Pixbuf = null;
            ShowCloseButton = source.CurrentMessage.CanClose;
            Spinning = source.CurrentMessage.IsSpinning;

            Pixbuf = source.CurrentMessage.IconNames == null ? null :
                IconThemeUtils.LoadIcon (22, source.CurrentMessage.IconNames);

            ClearButtons ();

            foreach (MessageAction action in source.CurrentMessage.Actions) {
                Button button = new ActionButton (action);
                button.UseStock = action.IsStock;
                AddButton (button);
            }

            Show ();
        }

        private void OnCloseClicked (object o, EventArgs args)
        {
            source.CurrentMessage.IsHidden = true;
        }

        private void OnActiveSourceChanged (SourceEventArgs args)
        {
            if (source != null && source != args.Source) {
                source.MessageNotify -= OnSourceMessageNotify;
            }

            ConnectSource (args.Source);
        }

        private void OnSourceMessageNotify (object o, EventArgs args)
        {
            Update ();
        }
    }
}
