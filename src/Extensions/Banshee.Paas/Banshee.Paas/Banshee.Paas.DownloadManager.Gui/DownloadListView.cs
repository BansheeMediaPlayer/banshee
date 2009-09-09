// 
// DownloadListView.cs
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
using System.Text;

using Gtk;
using Mono.Unix;

using Hyena.Data.Gui;

using Migo2.DownloadService;

using Banshee.Gui;
using Banshee.ServiceStack;
using Banshee.Collection.Gui;

using Banshee.Paas.Data;

namespace Banshee.Paas.DownloadManager.Gui
{
    public class DownloadListView : ListView<HttpFileDownloadTask>
    {
        private PaasDownloadManager manager;
    
        public DownloadListView (PaasDownloadManager manager)            
        {
            this.manager = manager;

            IsReorderable = true;
            IsEverReorderable = true;

            ColumnCellText name_renderer = new ColumnCellText ("Name", true);
            ColumnCellText progress_renderer = new ColumnCellText ("Progress", true);

            ColumnController = new ColumnController ();

            ColumnController.Add (new Column (Catalog.GetString ("Downloads"), name_renderer, 0.7));
            ColumnController.Add (new Column (Catalog.GetString ("Progress"), progress_renderer, 0.3));            
            
            //ColumnController  = column_controller;
            //RowHeightProvider = renderer.ComputeRowHeight;
        }
/*        
        protected override bool OnPopupMenu ()
        {
            ServiceManager.Get<InterfaceActionService> ().FindAction ("Paas.PaasChannelPopupAction").Activate ();
            return true;
        }
*/

#region D&D
//    Dragon-Drop    

//                     _ _
//              _     //` `\
//          _,-"\%   // /``\`\
//     ~^~ >__^  |% // /  } `\`\
//            )  )%// / }  } }`\`\
//           /  (%/'/.\_/\_/\_/\`/
//          (    '         `-._`
//           \   ,     (  \   _`-.__.-;%>
//          /_`\ \      `\ \." `-..-'`
//         ``` /_/`"-=-'`/_/
//
        protected override void OnDragSourceSet ()
        {
            base.OnDragSourceSet ();
        }

        protected override bool OnDragDrop (Gdk.DragContext context, int x, int y, uint time_)
        {
            y = TranslateToListY (y);

            if (Gtk.Drag.GetSourceWidget (context) == this) {
                DownloadSource source = ServiceManager.SourceManager.ActiveSource as DownloadSource;
                
                if (source != null) {
                    int row = GetRowAtY (y);                
                    DownloadListModel model = source.DownloadListModel;
                    
                    if (row != GetRowAtY (y + RowHeight / 2)) {
                        row += 1;
                    }
                    
                    if (model.Selection.Contains (row)) {
                        return false;
                    }

                    try {
                        manager.Move (row, model.GetSelected ());
                    } catch {}
                    
                    return true;
                }
            }
            
            return false;
        }
#endregion
    }
}
