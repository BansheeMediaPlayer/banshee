//
// ArtistListView.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//   Frank Ziegler <funtastix@googlemail.com>
//
// Copyright (C) 2007 Novell, Inc.
// Copyright (C) 2013 Frank Ziegler
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

using Banshee.Collection;
using Banshee.ServiceStack;
using Banshee.Gui;
using Banshee.MediaEngine;

using Gtk;

namespace Banshee.Collection.Gui
{
    public class ArtistListView : TrackFilterListView<ArtistInfo>
    {
        protected ArtistListView (IntPtr ptr) : base () {}
        private IArtistListRenderer renderer = null;
        private readonly InterfaceActionService action_service = null;

        public ArtistListView () : base ()
        {
            action_service = ServiceManager.Get<InterfaceActionService> ();

            renderer = action_service.ArtistListActions.ArtistListRenderer;

            if (renderer == null) {
                renderer = new ColumnCellArtistText ();
            }
            UpdateRenderer ();

            ServiceManager.PlayerEngine.ConnectEvent (OnPlayerEvent, PlayerEvent.TrackInfoUpdated);
            Banshee.Metadata.MetadataService.Instance.ArtworkUpdated += OnArtworkUpdated;

            action_service.ArtistListActions.ArtistListModeChanged += HandleArtistListModeChanged;
        }

        private void HandleArtistListModeChanged (object sender, ArtistListModeChangedEventArgs args)
        {
            this.renderer = args.Renderer;
            UpdateRenderer ();
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing) {
                ServiceManager.PlayerEngine.DisconnectEvent (OnPlayerEvent);
                Banshee.Metadata.MetadataService.Instance.ArtworkUpdated -= OnArtworkUpdated;
                action_service.ArtistListActions.ArtistListModeChanged -= HandleArtistListModeChanged;
            }
            base.Dispose (disposing);
        }

        protected override Gdk.Size OnMeasureChild ()
        {
            return new Gdk.Size (0, renderer.ComputeRowHeight (this));
        }

        private void UpdateRenderer ()
        {
            column_controller.Clear ();
            ColumnController = renderer.ColumnController;
            QueueResize ();
        }

        private void OnPlayerEvent (PlayerEventArgs args)
        {
            QueueDraw ();
        }

        private void OnArtworkUpdated (IBasicTrackInfo track)
        {
            QueueDraw ();
        }

        protected override bool OnFocusInEvent (Gdk.EventFocus evnt)
        {
            action_service.ArtistListActions ["ArtistListMenuAction"].Visible =
                action_service.ArtistListActions.ListActions ().Length > 2;
            return base.OnFocusInEvent (evnt);
        }

        protected override bool OnFocusOutEvent (Gdk.EventFocus evnt)
        {
            var focus = ServiceManager.Get<GtkElementsService> ().PrimaryWindow.Focus;
            if (focus != this) {
                action_service.ArtistListActions ["ArtistListMenuAction"].Visible = false;
            }
            return base.OnFocusOutEvent (evnt);
        }

        // TODO add context menu for artists/albums...probably need a Banshee.Gui/ArtistActions.cs file.  Should
        // make TrackActions.cs more generic with regards to the TrackSelection stuff, using the new properties
        // set on the sources themselves that give us access to the IListView<T>.
        /*protected override bool OnPopupMenu ()
        {
            ServiceManager.Get<InterfaceActionService> ().TrackActions["TrackContextMenuAction"].Activate ();
            return true;
        }*/
    }
}
