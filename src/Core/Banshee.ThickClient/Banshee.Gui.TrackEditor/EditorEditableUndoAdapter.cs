//
// EditorEditableUndoAdapter.cs
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

using Gtk;
using Hyena.Gui;

namespace Banshee.Gui.TrackEditor
{
    public class EditorEditableUndoAdapter<T> where T : Widget, IEditable
    {
        private Dictionary<EditorTrackInfo, EditableUndoAdapter<T>> undo_adapters
            = new Dictionary<EditorTrackInfo, EditableUndoAdapter<T>> ();
        private EditableUndoAdapter<T> current_adapter;

        public void DisconnectUndo ()
        {
            if (current_adapter != null) {
                current_adapter.Disconnect ();
                current_adapter = null;
            }
        }

        public void ConnectUndo (T entry, EditorTrackInfo track)
        {
            DisconnectUndo ();

            if (undo_adapters.ContainsKey (track)) {
                current_adapter = undo_adapters[track];
            } else {
                current_adapter = new EditableUndoAdapter<T> (entry);
                undo_adapters.Add (track, current_adapter);
            }

            current_adapter.Connect ();
        }
    }
}
