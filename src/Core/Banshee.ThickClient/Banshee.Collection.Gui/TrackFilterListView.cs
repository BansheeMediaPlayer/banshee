//
// TrackFilterListView.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
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

using Hyena.Data;
using Hyena.Data.Gui;

using Banshee.Collection;
using Banshee.ServiceStack;
using Banshee.Gui;

namespace Banshee.Collection.Gui
{
    public class TrackFilterListView<T> : ListView<T>
    {
        protected ColumnController column_controller;
        
        public TrackFilterListView () : base ()
        {
            column_controller = new ColumnController ();

            ForceDragSourceSet = true;
            HeaderVisible = false;
            
            RowActivated += OnRowActivated;
        }

        protected override void OnModelReloaded ()
        {
            base.OnModelReloaded ();
            CenterOnSelection ();
        }

        protected virtual void OnRowActivated (object o, EventArgs args)
        {
            ServiceManager.PlaybackController.NextSource = (ServiceManager.SourceManager.ActiveSource as Banshee.Sources.ITrackModelSource);
            ServiceManager.PlaybackController.Next ();
        }

        // TODO add context menu for artists/albums...probably need a Banshee.Gui/ArtistActions.cs file.  Should
        // make TrackActions.cs more generic with regards to the TrackSelection stuff, using the new properties
        // set on the sources themselves that give us access to the IListView<T>.
        /*protected override bool OnPopupMenu ()
        {
            ServiceManager.Get<InterfaceActionService> ().TrackActions["TrackContextMenuAction"].Activate ();
            return true;
        }*/

        protected override bool OnFocusInEvent(Gdk.EventFocus evnt)
        {
            ServiceManager.Get<InterfaceActionService> ().TrackActions.SuppressSelectActions ();
            return base.OnFocusInEvent(evnt);
        }
        
        protected override bool OnFocusOutEvent(Gdk.EventFocus evnt)
        {
            ServiceManager.Get<InterfaceActionService> ().TrackActions.UnsuppressSelectActions ();
            return base.OnFocusOutEvent(evnt);
        }

#region Drag and Drop

        protected override void OnDragSourceSet ()
        {
            base.OnDragSourceSet ();
            Drag.SourceSetIconName (this, "audio-x-generic");
        }

#endregion

    }
}
