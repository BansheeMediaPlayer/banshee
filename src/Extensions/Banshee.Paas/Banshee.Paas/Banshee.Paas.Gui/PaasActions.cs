// 
// PaasActions.cs
//  
// Author:
//    Mike Urbanski <michael.c.urbanski@gmail.com>
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
using System.Linq;
using System.Collections.Generic;

using Mono.Unix;

using Gtk;

using Migo2.Async;
using Hyena.Collections;

using Banshee.Gui;
using Banshee.Widgets;

using Banshee.Sources;
using Banshee.ServiceStack;

using Banshee.Collection;
using Banshee.Collection.Database;

using Banshee.Paas.Data;
using Banshee.Paas.Aether;

namespace Banshee.Paas.Gui
{
    enum SelectionInfo {
        None,
        One,
        Multiple
    }

    [Flags]
    enum SelectionOldNewInfo {
        Zero    = 0x00,
        ShowNew = 0x01,
        ShowOld = 0x02
    }

    public class PaasActions : BansheeActionGroup
    {
        private uint actions_id;
        private PaasService service;
        
        private DatabaseSource last_source;
        
        public PaasActions (PaasService service) : base (ServiceManager.Get<InterfaceActionService> (), "Paas")
        {
            this.service = service;
            
            AddImportant (
                new ActionEntry (
                    "PaasUpdateAllAction", Stock.Refresh,
                     Catalog.GetString ("Update Channels"), "<control><shift>U", 
                     null, OnPaasUpdateHandler
                ),
                new ActionEntry (
                    "PaasSubscribeAction", Stock.Add,
                     Catalog.GetString ("Subscribe to Channel"), null,
                     null, OnPaasSubscribeHandler
                )
            );

            Add (new ActionEntry [] {
                new ActionEntry (
                    "PaasImportOpmlAction", null,
                     Catalog.GetString ("Import Channels from OPML"), null,
                     null, OnPaasImportOpmlHandler
                ),
                new ActionEntry (
                    "PaasExportAllOpmlAction", null,
                     Catalog.GetString ("Export Channels as OPML"), null,
                     null, OnPaasExportAllOpmlHandler
                ),
                new ActionEntry (
                    "PaasExportOpmlAction", null,
                     Catalog.GetString ("Export Selected as OPML"), null,
                     null, OnPaasExportOpmlHandler
                ),                 
                new ActionEntry (
                    "PaasItemDownloadAction", Stock.SaveAs,
                     Catalog.GetString ("Download"), null,
                     null, OnPaasItemDownloadHandler
                ),
                new ActionEntry (
                    "PaasItemCancelAction", Stock.Cancel,
                     Catalog.GetString ("Cancel"), null,
                     null, OnPaasItemCancelHandler
                ),
                new ActionEntry (
                    "PaasItemPauseAction", Stock.MediaPause,
                     Catalog.GetString ("Pause"), null,
                     null, OnPaasItemPauseHandler
                ),
                new ActionEntry (
                    "PaasItemResumeAction", Stock.Redo,
                     Catalog.GetString ("Resume"), null,
                     Catalog.GetString ("Resume"), OnPaasItemResumeHandler
                ),
                new ActionEntry (
                    "PaasItemMarkNewAction", null,
                     Catalog.GetString ("Mark as New"), null,
                     null, OnPaasItemMarkedNewHandler
                ),                    
                new ActionEntry (
                    "PaasItemMarkOldAction", null,
                     Catalog.GetString ("Mark as Old"), null,
                     null, OnPaasItemMarkedOldHandler
                ),         
                new ActionEntry (
                    "PaasItemRemoveAction", Stock.Remove,
                     Catalog.GetString ("Remove From Library"), null,
                     null, OnPaasItemRemovedHandler
                ),                
                new ActionEntry (
                    "PaasItemDeleteAction", null,
                     Catalog.GetString ("Delete From Drive"), null,
                     null, OnPaasItemDeletedHandler
                ),
                new ActionEntry (
                    "PaasItemLinkAction", Stock.JumpTo,
                     Catalog.GetString ("Visit Homepage"), null,
                     null, OnPaasItemHomepageHandler
                ),                
                new ActionEntry (
                    "PaasChannelPopupAction", null, null, null, null, OnChannelPopup
                ),
                new ActionEntry (
                    "PaasChannelUpdateAction", Stock.Refresh,
                     Catalog.GetString ("Update"), null,
                     null, OnPaasChannelUpdateHandler
                ),                  
                new ActionEntry (
                    "PaasChannelDeleteAction", Stock.Delete,
                     Catalog.GetString ("Unsubscribe and Delete"), null,
                     null, OnPaasChannelDeleteHandler
                ),                   
                new ActionEntry (
                    "PaasChannelHomepageAction", Stock.JumpTo,
                     Catalog.GetString ("Visit Homepage"), null,
                     null, OnPaasChannelHomepageHandler
                ),
                new ActionEntry (
                    "PaasDownloadAllAction", Stock.SaveAs,
                     Catalog.GetString ("Download All Episodes"), null,
                     null, OnPaasDownloadAllHandler
                ),                
                new ActionEntry (
                    "PaasChannelDownloadAllAction", Stock.SaveAs,
                     Catalog.GetString ("Download All Episodes"), null,
                     null, OnPaasChannelDownloadAllHandler
                ), new ActionEntry (
                    "PaasChannelPropertiesAction", Stock.Preferences,
                     Catalog.GetString ("Properties"), null,
                     null, OnPaasChannelPropertiesHandler
                )
            });
            
            actions_id = Actions.UIManager.AddUiFromResource ("GlobalUI.xml");
            Actions.AddActionGroup (this);

            ServiceManager.SourceManager.ActiveSourceChanged += HandleActiveSourceChanged;
        }
        
        public override void Dispose ()
        {
            Actions.UIManager.RemoveUi (actions_id);
            Actions.RemoveActionGroup (this);
            base.Dispose ();
        }
        
        private void HandleActiveSourceChanged (SourceEventArgs args)
        {
            last_source = args.Source as DatabaseSource;
        }

        private DatabaseSource ActiveDbSource {
            get { return last_source; }
        }

        private bool IsPaasSource {
            get {
                return ActiveDbSource != null && (ActiveDbSource is PaasSource || ActiveDbSource.Parent is PaasSource);
            }
        }

        public PaasChannelModel ActiveChannelModel {
            get {
                if (ActiveDbSource == null) {
                    return null;
                } else if (ActiveDbSource is PaasSource) {
                    return (ActiveDbSource as PaasSource).ChannelModel;
                } else {
                    PaasChannelModel model = null;

                    foreach (IFilterListModel filter in ActiveDbSource.AvailableFilters) {
                        model = filter as PaasChannelModel;
                    
                        if (model != null) {
                            break;
                        }
                    }
                    
                    return model;
                }
            }
        }

        private bool GetItemDownloadSelectionStatus (IEnumerable<PaasTrackInfo> items)
        {
            return GetItemDownloadSelectionStatus (items.Where (i => !i.IsDownloaded), TaskState.None) != SelectionInfo.None;
        }

        private SelectionInfo GetItemDownloadSelectionStatus (IEnumerable<PaasTrackInfo> items, TaskState state)
        {
            int cnt = -1;
            
            foreach (PaasTrackInfo ti in items) {
                if (CheckStatus (ti.Item, state)) {
                    if (++cnt == 1) {
                        break;
                    }
                }
            }

            switch (cnt) {
            case 0:
                return SelectionInfo.One;     
            case 1:
                return SelectionInfo.Multiple;
            default:
                return SelectionInfo.None;
            }
        }

        private SelectionOldNewInfo GetItemOldNewSelectionStatus (IEnumerable<PaasTrackInfo> items)
        {
            // C# needs multiple returns
            bool show_new = false, show_old = false;
            SelectionOldNewInfo info = SelectionOldNewInfo.Zero;
            
            foreach (PaasItem i in items.Select (ti => ti.Item)) {
                if (!i.IsDownloaded) {
                    continue;
                }

                if (!show_old && i.IsNew) {
                    show_old = true;
                    info |= SelectionOldNewInfo.ShowOld;
                } else if (!show_new && !i.IsNew) {
                    show_new = true;
                    info |= SelectionOldNewInfo.ShowNew;
                }

                if (show_new && show_old) {
                    break;
                }
            }

            return info;
        }

        private bool CheckStatus (PaasItem item, TaskState flags)
        {
            return (service.DownloadManager.CheckActiveDownloadStatus (item.DbId) & flags) != TaskState.Zero;
        }

        public void UpdateItemActions ()
        {
            UpdateItemActions (GetSelectedItems ());
        }

        public void UpdateItemActions (IEnumerable<PaasTrackInfo> items)
        {
            if (!IsPaasSource) {
                return;
            }
    
            bool show_download = GetItemDownloadSelectionStatus (items);            
            bool show_cancel   = GetItemDownloadSelectionStatus (items, TaskState.CanCancel) != SelectionInfo.None;
            bool show_resume   = GetItemDownloadSelectionStatus (items, TaskState.Paused)    != SelectionInfo.None;
            bool show_pause    = GetItemDownloadSelectionStatus (items, TaskState.CanPause)  != SelectionInfo.None;

            SelectionOldNewInfo selection = GetItemOldNewSelectionStatus (items);
            bool show_mark_new = ((selection & SelectionOldNewInfo.ShowNew) != SelectionOldNewInfo.Zero);
            bool show_mark_old = ((selection & SelectionOldNewInfo.ShowOld) != SelectionOldNewInfo.Zero);
            
            UpdateAction ("PaasItemDownloadAction", show_download);              
            UpdateAction ("PaasItemCancelAction", show_cancel);
            UpdateAction ("PaasItemPauseAction", show_pause);
            UpdateAction ("PaasItemResumeAction", show_resume);

            UpdateAction ("PaasItemMarkNewAction", show_mark_new);
            UpdateAction ("PaasItemMarkOldAction", show_mark_old);
            
            UpdateAction ("PaasItemLinkAction", (ActiveDbSource.TrackModel.Selection.Count == 1));                
        }

        public void UpdateChannelActions ()
        {
            if (!IsPaasSource) {
                return;
            }

            UpdateActions (
                true,
                (ActiveChannelModel.Selection.Count == 1 && 
                !ActiveChannelModel.Selection.AllSelected),
                "PaasChannelHomepageAction",
                "PaasChannelPropertiesAction"
            );
        }

        private IEnumerable<PaasChannel> GetSelectedChannels ()
        {
            return new List<PaasChannel> (ActiveChannelModel.SelectedItems);
        }

        private IEnumerable<PaasTrackInfo> GetSelectedItems ()
        {
            return new List<PaasTrackInfo> (
                PaasTrackInfo.From (ActiveDbSource.TrackModel.SelectedItems)
            );
        }

        private void RunSubscribeDialog ()
        {        
            SubscribeDialog dialog = new SubscribeDialog ();            
            ResponseType response = (ResponseType) dialog.Run ();
            dialog.Destroy ();

            if (response == ResponseType.Ok) {
                if (String.IsNullOrEmpty (dialog.Url)) {
                    return;
                }

                string url = dialog.Url.Trim ().Trim ('/');
                DownloadPreference download_pref = dialog.DownloadPreference;;
                
                try {
                    service.SyndicationClient.SubscribeToChannel (url, download_pref);
                } catch (Exception e) {
                    Hyena.Log.Exception (e);
                    
                    HigMessageDialog.RunHigMessageDialog (
                        null,
                        DialogFlags.Modal,
                        MessageType.Warning,
                        ButtonsType.Ok,
                        Catalog.GetString ("Invalid URL"),
                        Catalog.GetString ("Podcast URL is invalid.")
                    );
                }
            }        
        }

        private void MarkItems (IEnumerable<PaasTrackInfo> items, bool _new)
        {
            PaasSource s = ActiveDbSource as PaasSource;
            
            if (s == null) {
                return;
            }
            
            RangeCollection rc = new RangeCollection ();
            
            foreach (var i in items.Select (i => i.Item)) {
                if (!i.IsDownloaded) {
                    continue;
                }
                
                if (_new && !i.IsNew) {
                    rc.Add ((int)i.DbId);
                } else if (i.IsNew) {
                    rc.Add ((int)i.DbId);
                }
            }

            foreach (var range in rc.Ranges) {
                ServiceManager.DbConnection.Execute (
                    String.Format ("UPDATE PaasItems SET IsNew = ? WHERE ID >= ? AND ID <= ?"),
                    (_new ? 1 : 0), range.Start, range.End
                );
            }

            s.Reload ();
        }

        private void RunConfirmDeleteDialog (bool channel, int selCount, out bool delete, out bool deleteFiles)
        {
            
            delete = false;
            deleteFiles = false;        
            string header = null;
            int plural = (channel | (selCount > 1)) ? 2 : 1;
            int channel_count = 0;
            
            if (channel) {
                channel_count = GetSelectedChannels ().Count ();
                header = Catalog.GetPluralString ("Delete Channel?", "Delete Channels?", channel_count);
            } else {
                header = Catalog.GetPluralString ("Delete Item?", "Delete Items?", selCount);
            }
            
            HigMessageDialog md = null;
            
            if (selCount > 0) {
                md = new HigMessageDialog (
                    ServiceManager.Get<GtkElementsService> ("GtkElementsService").PrimaryWindow,
                    DialogFlags.DestroyWithParent, 
                    MessageType.Question,
                    ButtonsType.None, header, 
                    Catalog.GetPluralString (
                        "Would you like to delete the associated file?",
                        "Would you like to delete the associated files?", plural                
                    )
                );
    
                md.AddButton (Stock.Cancel, ResponseType.Cancel, true);
                md.AddButton (Catalog.GetPluralString ("Keep File", "Keep Files", plural), ResponseType.No, false);
                md.AddButton (Stock.Delete, ResponseType.Yes, false);
            } else {
                md = new HigMessageDialog (
                    ServiceManager.Get<GtkElementsService> ("GtkElementsService").PrimaryWindow,
                    DialogFlags.DestroyWithParent, 
                    MessageType.Question,
                    ButtonsType.None, header, 
                    Catalog.GetPluralString (
                        "Would you like to delete the selected channel?",
                        "Would you like to delete the selected channels?", channel_count               
                    )
                );

                md.AddButton (Stock.Cancel, ResponseType.Cancel, true);
                md.AddButton (Stock.Delete, ResponseType.Yes, false);
            }
            
            try {
                switch ((ResponseType)md.Run ()) {
                case ResponseType.Yes:
                    deleteFiles = true;
                    goto case ResponseType.No;
                case ResponseType.No:
                    delete = true;
                    break;
                }                
            } finally {
                md.Destroy ();
            }       
        }

        private ResponseType RunConfirmRemoveDeleteDialog (bool delete, int itemCount)
        {
            ResponseType response;
            
            HigMessageDialog md = new HigMessageDialog (
                ServiceManager.Get<GtkElementsService> ("GtkElementsService").PrimaryWindow,
                DialogFlags.DestroyWithParent, 
                MessageType.Question,
                ButtonsType.None,
                String.Format (
                    "{0} {1}", 
                    delete ? Catalog.GetString ("Delete") : Catalog.GetString ("Remove"), 
                    Catalog.GetPluralString ("Item?", "Items?", itemCount)
                ),
                String.Format (
                    Catalog.GetString ("Are you sure that you want to {0} the selected {1}?"),
                    delete ? Catalog.GetString ("delete") : Catalog.GetString ("remove"),                     
                    Catalog.GetPluralString ("item", "items", itemCount)
                )
            );

            md.AddButton (Stock.Cancel, ResponseType.Cancel, true);
            md.AddButton ((delete ? Stock.Delete : Stock.Remove), ResponseType.Ok, false);

            response = (ResponseType) md.Run ();
            md.Destroy ();
            
            return response;
        }

        private void ExportToOpml (IEnumerable<PaasChannel> channels)
        {        
            if (channels.Count () == 0) {
                HigMessageDialog.RunHigMessageDialog (
                    null, DialogFlags.Modal, MessageType.Warning, ButtonsType.Ok,
                    Catalog.GetString ("Error Exporting Channels!"), 
                    Catalog.GetString ("No channels to export.")
                );                            
                
                return;
            }

            FileChooserDialog chooser = new FileChooserDialog (
                Catalog.GetString ("Save As..."), null, FileChooserAction.Save, 
                new object[] { 
                    Stock.Cancel, ResponseType.Cancel, 
                    Stock.Save, ResponseType.Ok
                }
            );
            
            chooser.Response += (o, ea) => {
                if (ea.ResponseId == Gtk.ResponseType.Ok) { 
                    try {
                        service.ExportChannelsToOpml (chooser.Filename, channels);
                    } catch (Exception ex) {
                       HigMessageDialog.RunHigMessageDialog (
                            null, DialogFlags.Modal, MessageType.Warning, ButtonsType.Ok,
                            Catalog.GetString ("Error Exporting Channels!"), ex.Message   
                        );
                    }
                }
                
                (o as Dialog).Destroy ();
            };
            
            chooser.Run ();        
        }
        
        private void OnPaasExportOpmlHandler (object sender, EventArgs e)
        {
            ExportToOpml (GetSelectedChannels ());
        }

        private void OnPaasExportAllOpmlHandler (object sender, EventArgs e)
        {
            ExportToOpml (PaasChannel.Provider.FetchAll ());
        }

        private void OnPaasImportOpmlHandler (object sender, EventArgs e)
        {
            FileChooserDialog chooser = new FileChooserDialog (
                Catalog.GetString ("Please Select a File to Import..."), null, FileChooserAction.Open, 
                new object[] { 
                    Stock.Cancel, ResponseType.Cancel, 
                    Catalog.GetString ("Import"), ResponseType.Ok
                }
            );
            
            FileFilter filter = new FileFilter () {
                Name = "OPML"
            };

            FileFilter unfiltered = new FileFilter () { 
                Name = Catalog.GetString ("All") 
            };
            
            unfiltered.AddPattern ("*");
            filter.AddPattern ("*.opml");
            filter.AddPattern ("*.miro");

            chooser.AddFilter (filter);
            chooser.AddFilter (unfiltered);

            chooser.Response += (o, ea) => {
                if (ea.ResponseId == Gtk.ResponseType.Ok) { 
                    try {
                        service.ImportOpml (chooser.Filename);
                    } catch (Exception ex) {
                       HigMessageDialog.RunHigMessageDialog (
                            null, DialogFlags.Modal, MessageType.Warning, ButtonsType.Ok,
                            Catalog.GetString ("Error Importing Channels!"), ex.Message   
                        );

                        chooser.Run ();
                    }
                }
                
                (o as Dialog).Destroy ();
            };

            chooser.Run ();
        }

        private void OnPaasSubscribeHandler (object sender, EventArgs e)
        {
            RunSubscribeDialog ();
        }

        private void OnPaasUpdateHandler (object sender, EventArgs e)
        {
            service.UpdateAsync ();
        }
        private void OnPaasItemDownloadHandler (object sender, EventArgs e)
        {
            var items = GetSelectedItems ();
            service.DownloadManager.QueueDownload (items.Select (ti => ti.Item).Where (i => !i.IsDownloaded));
        }

        private void OnPaasItemCancelHandler (object sender, EventArgs e)
        {
            var items = GetSelectedItems ();
            service.DownloadManager.CancelDownload (
                items.Select (t => t.Item).Where  (i => CheckStatus (i, TaskState.CanCancel))
            );
        }

        private void OnPaasItemResumeHandler (object sender, EventArgs e)
        {
            var items = GetSelectedItems ();
            service.DownloadManager.ResumeDownload (
                items.Select (t => t.Item).Where  (i => CheckStatus (i, TaskState.Paused))
            );
        }
        
        private void OnPaasItemPauseHandler (object sender, EventArgs e)
        {
            var items = GetSelectedItems ();
            service.DownloadManager.PauseDownload (
                items.Select (t => t.Item).Where  (i => CheckStatus (i, TaskState.CanPause))
            );
        }

        private void OnPaasItemHomepageHandler (object sender, EventArgs e)
        {
            PaasItem item = PaasTrackInfo.From (ActiveDbSource.TrackModel.FocusedItem).Item;
            if (item != null && !String.IsNullOrEmpty (item.Link)) {
                Banshee.Web.Browser.Open (item.Link);
            }    
        }

        private void OnPaasItemDeletedHandler (object sender, EventArgs e)
        {
            var items = GetSelectedItems ();
            if (RunConfirmRemoveDeleteDialog (true, items.Count ()) == ResponseType.Ok) {
                service.SyndicationClient.RemoveItems (items.Select (t => t.Item), true);
            }
        }

        private void OnPaasItemRemovedHandler (object sender, EventArgs e)
        {
            bool delete, delete_files;
            
            var items = GetSelectedItems ().Select (t => t.Item);
            int cnt = GetSelectedItems ().Select (t => t.Item).Where (i => i.IsDownloaded).Count ();
            
            if (cnt > 0) {
                RunConfirmDeleteDialog (false, cnt, out delete, out delete_files);
                
                if (delete) {
                    service.SyndicationClient.RemoveItems (items, delete_files);                
                }              
            } else {
                if (RunConfirmRemoveDeleteDialog (false, items.Count ()) == ResponseType.Ok) {
                    service.SyndicationClient.RemoveItems (items);
                }
            }
        }

        private void OnPaasItemMarkedNewHandler (object sender, EventArgs e)
        {
            var items = GetSelectedItems ();
            MarkItems (items, true);
        }

        private void OnPaasItemMarkedOldHandler (object sender, EventArgs e)
        {
            var items = GetSelectedItems ();
            MarkItems (items, false);
        }

        private void OnChannelPopup (object o, EventArgs e)
        {
            if (ActiveChannelModel.Selection.AllSelected) {
                ShowContextMenu ("/PaasAllChannelsContextMenu");
            } else {
                ShowContextMenu ("/PaasChannelPopup");
            }
        }

        private void OnPaasChannelUpdateHandler (object sender, EventArgs e)
        {
            var channels = GetSelectedChannels ();
            service.SyndicationClient.QueueUpdate (channels);
        }

        private void OnPaasChannelDeleteHandler (object sender, EventArgs e)
        {
            int cnt = 0;
            bool delete = true, delete_files = false;
            
            var channels = GetSelectedChannels ();

            foreach (var channel in channels) {
                foreach (var item in channel.Items) {
                    if (item.Active && item.IsDownloaded) {
                        if (++cnt == 2) {
                            break;
                        }
                    }
                }
                
                if (cnt > 0) {
                    RunConfirmDeleteDialog (true, cnt, out delete, out delete_files);
                    break;
                }
            }

            if (cnt == 0) {
                RunConfirmDeleteDialog (true, 0, out delete, out delete_files);
            }

            if (delete) {
                service.DeleteChannels (channels, delete_files);                
            }
        }

        private void OnPaasChannelHomepageHandler (object sender, EventArgs e)
        {
            PaasChannel channel = ActiveChannelModel.FocusedItem;
            if (channel != null && !String.IsNullOrEmpty (channel.Link)) {
                Banshee.Web.Browser.Open (channel.Link);
            }    
        }

        private void OnPaasDownloadAllHandler (object sender, EventArgs e)
        {
            var channels = new List<PaasChannel> (PaasChannel.Provider.FetchAll ().OrderBy (c => c.Name));
            foreach (var c in channels) {
                service.DownloadManager.QueueDownload (c.Items.Where (i => i.Active && !i.IsDownloaded));
            }
        }

        private void OnPaasChannelDownloadAllHandler (object sender, EventArgs e)
        {
            var channels = GetSelectedChannels ();
            foreach (var c in channels) {
                service.DownloadManager.QueueDownload (c.Items.Where (i => i.Active && !i.IsDownloaded));
            }
        }
        
        private void OnPaasChannelPropertiesHandler (object sender, EventArgs e)
        {
            PaasChannel channel = ActiveChannelModel.FocusedItem;
            
            if (channel != null) {
                new ChannelPropertiesDialog (channel).Run ();
            }
        }
    }
}
