//
// SourceMessage.cs
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
using System.Collections.Generic;

namespace Banshee.Sources
{
    public class SourceMessage
    {
        private bool updated_when_frozen;
        private int freeze_count;

        private List<MessageAction> actions = new List<MessageAction> ();

        private Source source;
        private bool is_spinning;
        private bool is_hidden;
        private bool can_close;
        private string text;
        private string [] icon_names;

        public event EventHandler Updated;

        public SourceMessage (Source source)
        {
            this.source = source;
        }

        public void AddAction (MessageAction action)
        {
            lock (this) {
                actions.Add (action);
                OnUpdated ();
            }
        }

        public void ClearActions ()
        {
            lock (this) {
                actions.Clear ();
                OnUpdated ();
            }
        }

        public void SetIconName (params string [] name)
        {
            icon_names = name;
        }

        public string [] IconNames {
            get { return icon_names; }
        }

        public void FreezeNotify ()
        {
            lock (this) {
                freeze_count++;
            }
        }

        public void ThawNotify ()
        {
            lock (this) {
                if (freeze_count > 0) {
                    freeze_count--;
                }

                if (freeze_count == 0 && updated_when_frozen) {
                    OnUpdated ();
                }
            }
        }

        protected virtual void OnUpdated ()
        {
            if (freeze_count != 0) {
                updated_when_frozen = true;
                return;
            }

            updated_when_frozen = false;

            EventHandler handler = Updated;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }

        public bool IsSpinning {
            get { return is_spinning; }
            set { lock (this) { is_spinning = value; OnUpdated (); } }
        }

        public bool IsHidden {
            get { return is_hidden; }
            set { lock (this) { is_hidden = value; OnUpdated (); } }
        }

        public bool CanClose {
            get { return can_close; }
            set { lock (this) { can_close = value; OnUpdated (); } }
        }

        public Source Source {
            get { return source; }
            set { lock (this) { source = value; OnUpdated (); } }
        }

        public string Text {
            get { return text; }
            set { lock (this) { text = value; OnUpdated (); } }
        }

        public IEnumerable<MessageAction> Actions {
            get {
                lock (this) {
                    return actions.ToArray ();
                }
            }
        }
    }
}
