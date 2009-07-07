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
                    "PaasUpdateAction", Stock.Refresh,
                     Catalog.GetString ("Update Channels"), "<control><shift>U",
                     Catalog.GetString ("Recieve updates from Miro Guide"), OnPaasUpdateHandler
                ),
                new ActionEntry (
                    "PaasSubscribeAction", Stock.Add,
                     Catalog.GetString ("Subscribe to Channel"), null,
                     Catalog.GetString ("Subscribe to Channel"), OnPaasSubscribeHandler
                )
            );

            Add (new ActionEntry [] {
                new ActionEntry (
                    "PaasItemDownloadAction", Stock.SaveAs,
                     Catalog.GetString ("Download"), null,
                     Catalog.GetString ("Download"), OnPaasItemDownloadHandler
                ),
                new ActionEntry (
                    "PaasItemCancelAction", Stock.Cancel,
                     Catalog.GetString ("Cancel"), null,
                     Catalog.GetString ("Cancel"), OnPaasItemCancelHandler
                ),
                new ActionEntry (
                    "PaasItemPauseAction", Stock.MediaPause,
                     Catalog.GetString ("Pause"), null,
                     Catalog.GetString ("Pause"), OnPaasItemPauseHandler
                ),
                new ActionEntry (
                    "PaasItemResumeAction", Stock.Redo,
                     Catalog.GetString ("Resume"), null,
                     Catalog.GetString ("Resume"), OnPaasItemResumeHandler
                ),
                new ActionEntry (
                    "PaasItemRemoveAction", Stock.Remove,
                     Catalog.GetString ("Remove From Library"), null,
                     Catalog.GetString ("Remove From Library"), OnPaasItemRemovedHandler
                ),                
                new ActionEntry (
                    "PaasChannelPopupAction", null, null, null, null, OnChannelPopup
                ),                    
                new ActionEntry (
                    "PaasDeleteChannelAction", Stock.Delete,
                     Catalog.GetString ("Delete"), null,
                     Catalog.GetString ("Delete Channel"), OnPaasChannelDeleteHandler
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

        private SelectionInfo GetItemSelectionStatus (IEnumerable<PaasTrackInfo> items, TaskState state)
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
    
            bool show_download = GetItemSelectionStatus (items, TaskState.None)      != SelectionInfo.None;            
            bool show_cancel   = GetItemSelectionStatus (items, TaskState.CanCancel) != SelectionInfo.None;
            bool show_resume   = GetItemSelectionStatus (items, TaskState.Paused)    != SelectionInfo.None;
            bool show_pause    = GetItemSelectionStatus (items, TaskState.CanPause)  != SelectionInfo.None;

            UpdateAction ("PaasItemDownloadAction", show_download);              
            UpdateAction ("PaasItemCancelAction", show_cancel);
            UpdateAction ("PaasItemPauseAction", show_pause);
            UpdateAction ("PaasItemResumeAction", show_resume);
            
            if (ActiveDbSource.TrackModel.Selection.Count == 1) {
                //UpdateAction ("PodcastItemLinkAction", true);                
            }
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

        private void OnPaasSubscribeHandler (object sender, EventArgs args)
        {
            RunSubscribeDialog ();
        }

        private void OnPaasUpdateHandler (object sender, EventArgs args)
        {
            service.UpdateAsync ();
        }

        private void OnPaasItemDownloadHandler (object sender, EventArgs args)
        {
            var items = GetSelectedItems ();
            service.DownloadManager.QueueDownload (items.Select (ti => ti.Item));
        }

        private void OnPaasItemCancelHandler (object sender, EventArgs args)
        {
            if (ActiveDbSource.TrackModel.Selection.AllSelected) {
                service.DownloadManager.CancelAsync ();
                return;
            }
            
            var items = GetSelectedItems ();
                        
            service.DownloadManager.CancelDownload (
                items.Select (ti => ti.Item).Where  (i => CheckStatus (i, TaskState.CanCancel))
            );
        }

        private void OnPaasItemResumeHandler (object sender, EventArgs args)
        {
            var items = GetSelectedItems ();

            service.DownloadManager.ResumeDownload (
                items.Select (ti => ti.Item).Where  (i => CheckStatus (i, TaskState.Paused))
            );
        }
        
        private void OnPaasItemPauseHandler (object sender, EventArgs args)
        {
            var items = GetSelectedItems ();

            service.DownloadManager.PauseDownload (
                items.Select (ti => ti.Item).Where  (i => CheckStatus (i, TaskState.CanPause))
            );
        }

        private void OnPaasItemRemovedHandler (object sender, EventArgs args)
        {
            // Run dialog to determine the fate of downloaded files.
            var items = GetSelectedItems ();
            service.SyndicationClient.RemoveItems (items.Select (ti => ti.Item), true);
        }

        private void OnChannelPopup (object o, EventArgs args)
        {
            if (ActiveChannelModel.Selection.AllSelected) {
                ShowContextMenu ("/PaasAllChannelsContextMenu");
            } else {
                ShowContextMenu ("/PaasChannelPopup");
            }
        }

        private void OnPaasChannelDeleteHandler (object sender, EventArgs e)
        {
            service.SyndicationClient.DeleteChannels (ActiveChannelModel.SelectedItems, true);
        }      
    }
}
