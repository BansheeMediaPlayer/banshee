// 
// PaasItemView.cs
//  
// Authors:
//   Mike Urbanski <michael.c.urbanski@gmail.com>
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2009 Michael C. Urbanski
// Copyright (C) 2008 Novell, Inc.
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

using Gtk;

using Hyena.Data.Gui;

using Banshee.Gui;
using Banshee.ServiceStack;

using Banshee.Collection;
using Banshee.Collection.Gui;
using Banshee.Collection.Database;

using Banshee.Paas.Data;

namespace Banshee.Paas.Gui
{
    public class PaasItemView : TrackListView
    {
        // Awful, dirty, filthy hack.
        // I'm having a similar problem, probably just need to tinker with the event flags... Need to move on for now...
        // http://lists.ximian.com/archives/public/gtk-sharp-list/2006-June/007247.html
        public EventHandler<EventArgs> FuckedPopupMenu;

        public PaasItemView ()
        {
        }

        protected override bool OnPopupMenu ()
        {
            EventHandler<EventArgs> handler = FuckedPopupMenu;

            if (handler != null) {
                handler (this, EventArgs.Empty);
            }

            return base.OnPopupMenu ();
        }

        protected override void ColumnCellDataProvider (ColumnCell cell, object boundItem)
        {
            ColumnCellText text_cell = cell as ColumnCellText;
            
            if (text_cell == null) {
                return;
            }

            DatabaseTrackInfo track = boundItem as DatabaseTrackInfo;
            PaasTrackInfo pti = PaasTrackInfo.From (track);
            
            if (pti == null) {
                return;
            }

            if (track.IsPlaying || pti.IsDownloaded) {
                text_cell.Sensitive = true;
            } else {
                text_cell.Sensitive = false;                                
            }
        }
    }
}
