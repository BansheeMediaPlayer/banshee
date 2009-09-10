/***************************************************************************
 *  PodcastItemView.cs
 *
 *  Copyright (C) 2007 Michael C. Urbanski
 *  Written by Mike Urbanski <michael.c.urbanski@gmail.com>
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */
 
using System;
using System.Collections.ObjectModel;

using Gtk;
using Mono.Unix;
 
using Hyena.Data;
using Hyena.Data.Gui;

using Banshee.Gui;
using Banshee.ServiceStack;
using Banshee.Collection;
using Banshee.Collection.Gui;
using Banshee.Collection.Database;

using Migo.Syndication;

using Banshee.Podcasting.Data;

namespace Banshee.Podcasting.Gui
{
    /*public class PodcastItemView : ListView<TrackInfo>
    {
        private PersistentColumnController columnController;
        
        public PodcastItemView () : base ()
        {
            columnController = new PersistentColumnController ("plugins.podcasting.item_view_columns");
            
            SortableColumn podcastTitleSortColumn = new SortableColumn (
                Catalog.GetString ("Podcast"), 
                new ColumnCellText ("AlbumTitle", true), 0.35, 
                PodcastItemSortKeys.PodcastTitle, true
            );
            
            columnController.AddRange (
                new Column (null, Catalog.GetString ("Activity"), new PodcastItemActivityColumn ("Activity"), 0.00, true, 26, 26),            
                new SortableColumn (Catalog.GetString ("Title"), new ColumnCellText ("TrackTitle", true), 0.30, PodcastItemSortKeys.Title, true),
                podcastTitleSortColumn,
                new SortableColumn (Catalog.GetString ("Date"), new ColumnCellDateTime ("ReleaseDate", false), 0.5, PodcastItemSortKeys.PubDate, true)
            );
            
            podcastTitleSortColumn.SortType = Hyena.Data.SortType.Descending;
            columnController.DefaultSortColumn = podcastTitleSortColumn;
            
            ColumnController = columnController;
            columnController.Load ();
            
            RulesHint = true;
        }  
        
        private Menu popupMenu;
        private MenuItem cancelItem;       
        private MenuItem downloadItem;       
        private MenuItem linkItem;            
        private MenuItem propertiesItem;
        
        private MenuItem markNewItem;
        private MenuItem markOldItem;
        
        protected override bool OnPopupMenu ()
        {
            if (popupMenu == null) {
                UIManager uiManager = ServiceManager.Get<InterfaceActionService> ().UIManager;
                
                popupMenu = uiManager.GetWidget ("/PodcastItemViewPopup") as Menu;
                
                cancelItem = uiManager.GetWidget ("/PodcastItemViewPopup/PodcastItemCancel") as MenuItem;
                downloadItem = uiManager.GetWidget ("/PodcastItemViewPopup/PodcastItemDownload") as MenuItem; 
                
                propertiesItem = uiManager.GetWidget ("/PodcastItemViewPopup/PodcastItemProperties") as MenuItem;
                linkItem = uiManager.GetWidget ("/PodcastItemViewPopup/PodcastItemLink") as MenuItem;                
            
                markNewItem = uiManager.GetWidget ("/PodcastItemViewPopup/PodcastItemMarkNew") as MenuItem;
                markOldItem = uiManager.GetWidget ("/PodcastItemViewPopup/PodcastItemMarkOld") as MenuItem;              
            }
            
            DatabaseTrackListModel model = Model as DatabaseTrackListModel;
            
            bool showCancel = false;            
            bool showDownload = false;
            
            bool showMarkNew = false;
            bool showMarkOld = false;
            
            ModelSelection<Banshee.Collection.TrackInfo> items = model.SelectedItems;
            
            foreach (PodcastTrackInfo i in items) {
                if (showCancel && showDownload && showMarkNew && showMarkOld) {
                    break;
                } else if (!showDownload &&
                    i.Activity == PodcastItemActivity.None ||
                    i.Activity == PodcastItemActivity.DownloadFailed                     
                ) {
                    showDownload = true;
                } else if (!showCancel &&
                    i.Activity == PodcastItemActivity.Downloading ||
                    i.Activity == PodcastItemActivity.DownloadPending ||
                    i.Activity == PodcastItemActivity.DownloadPaused                     
                ) {
                    showCancel = true;
                }/* else if ((!showMarkNew || !showMarkOld)) {
                    if (i.New) {
                        showMarkOld = true;
                    } else { 
                        showMarkNew = true;
                    }
                }*/
            /*}
            
            if (items.Count > 1) {
                linkItem.Hide ();
                propertiesItem.Hide ();
            } else {
                linkItem.Show ();
                propertiesItem.Show ();
            }
            
            if (showCancel) {
                cancelItem.Show ();
            } else {
                cancelItem.Hide ();
            }            
            
            if (showDownload) {
                downloadItem.Show ();   
            } else {
                downloadItem.Hide ();
            }

            if (showMarkNew) {
                markNewItem.Show ();
            } else {
                markNewItem.Hide ();            
            }

            if (showMarkOld) {
                markOldItem.Show ();
            } else {
                markOldItem.Hide ();            
            }

            popupMenu.Popup (null, null, null, 0, Gtk.Global.CurrentEventTime);
            
            return true;
        }
    }*/
}