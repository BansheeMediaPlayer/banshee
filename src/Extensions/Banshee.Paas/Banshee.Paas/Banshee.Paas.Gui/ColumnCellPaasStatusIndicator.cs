// 
// ColumnCellPaasStatusIndicator.cs
//  
// Author:
//       Mike Urbanski <michael.c.urbanski@gmail.com>
// 
// Copyright (c) 2009 Michael C. Urbanski
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
using Banshee.Collection;
using Banshee.Collection.Gui;

using Migo2.Async;

using Banshee.Paas;
using Banshee.Paas.Data;

namespace Banshee.Paas.Gui
{
    public class ColumnCellPaasStatusIndicator : ColumnCellStatusIndicator, IColumnCellDataHelper
    {
        protected enum Offset : int {
            New         = 0,
            Downloading = 1,
            Video       = 2,
            Count       = 3
        }

        private int  icon_index = -1;
        private bool sensitive = true;

        public ColumnCellDataHelper DataHelper { get; set; }

        protected override int PixbufCount {
            get { return base.PixbufCount + (int)Offset.Count; }
        }
        
        public ColumnCellPaasStatusIndicator (string property) : base (property)
        {
        }
        
        public ColumnCellPaasStatusIndicator (string property, bool expand) : base (property, expand)
        {
        }        
        
        protected override void LoadPixbufs ()
        {
            base.LoadPixbufs ();
            
            Pixbufs[base.PixbufCount + (int)Offset.New]         = IconThemeUtils.LoadIcon (PixbufSize, "podcast-new");
            Pixbufs[base.PixbufCount + (int)Offset.Downloading] = IconThemeUtils.LoadIcon (PixbufSize, "document-save");
            Pixbufs[base.PixbufCount + (int)Offset.Video]       = IconThemeUtils.LoadIcon (PixbufSize, "video-x-generic");                        
        }
        
        protected override int GetIconIndex (TrackInfo track)
        {
            return icon_index;
        }
        
        public override void Render (CellContext context, StateType state, double cellWidth, double cellHeight)
        {
            sensitive = true;
            PaasTrackInfo pti = PaasTrackInfo.From (BoundTrack);
            
            if (pti == null || pti.Item == null) {
                icon_index = -1;
            } else {
                TaskState task_state = (DataHelper != null) ? (TaskState)DataHelper (this, pti.Item) : TaskState.None;
                
                switch (task_state) {                
                    case TaskState.Ready:
                        sensitive = pti.Track.IsPlaying;
                        goto case TaskState.Running;
                    case TaskState.Running:
                        icon_index = base.PixbufCount + (int)Offset.Downloading;
                        break;
                    case TaskState.Paused:
                        icon_index = (int)Icon.Paused;
                        break;
                    case TaskState.Failed:
                        icon_index = (int)Icon.Error;
                        break;                        
                    default:
                        icon_index = pti.IsNew ? base.PixbufCount + (int) Offset.New : -1;
                        break;
                }

//                if (icon_index < 0) {
//                    if (pti.Track.MimeType.Contains ("video")) {
//                        icon_index = base.PixbufCount + (int) Offset.Video;
//                    }
//                }
            }
            
            context.Opaque = sensitive;            
            base.Render (context, state, cellWidth, cellHeight);
        }
    }
}
