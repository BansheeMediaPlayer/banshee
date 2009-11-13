//
// PodcastActions.cs
//
// Authors:
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
using System.Collections.Generic;

using Mono.Unix;
using Gtk;

using Migo.Syndication;

using Banshee.Base;
using Banshee.Query;
using Banshee.Sources;
using Banshee.Library;
using Banshee.Playlist;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.ServiceStack;

using Banshee.Widgets;
using Banshee.Gui.Dialogs;
using Banshee.Gui.Widgets;
using Banshee.Gui;

using Banshee.Podcasting;
using Banshee.Podcasting.Data;

namespace Banshee.Podcasting.Gui
{
    public class PodcastActions : BansheeActionGroup
    {
        private uint actions_id;
        private DatabaseSource last_source;
        
        public PodcastActions (PodcastSource source) : base (ServiceManager.Get<InterfaceActionService> (), "Podcast")
        {
            AddImportant (
                new ActionEntry (
                    "PodcastUpdateAllAction", Stock.Refresh,
                     Catalog.GetString ("Check for New Episodes"), null,//"<control><shift>U",
                     Catalog.GetString ("Refresh All Podcasts"), 
                     OnPodcastUpdateAll
                ),
                new ActionEntry (
                    "PodcastAddAction", Stock.Add,
                     Catalog.GetString ("Subscribe to Podcast..."),"<control><shift>F", 
                     Catalog.GetString ("Subscribe to a new podcast"),
                     OnPodcastAdd
                )         
            );
            
            Add (
                new ActionEntry("PodcastFeedPopupAction", null, 
                    String.Empty, null, null, OnFeedPopup),
                    
                new ActionEntry (
                    "PodcastDeleteAction", Stock.Delete,
                     Catalog.GetString ("Unsubscribe and Delete"),
                     null, String.Empty, 
                     OnPodcastDelete
                ),
                new ActionEntry (
                    "PodcastUpdateFeedAction", Stock.Refresh,
                     Catalog.GetString ("Check for New Episodes"),
                     null, String.Empty, 
                     OnPodcastUpdate
                ),
                new ActionEntry (
                    "PodcastDownloadAllAction", Stock.Save,
                     Catalog.GetString ("Download All Episodes"),
                     null, String.Empty, 
                     OnPodcastDownloadAllEpisodes
                ),
                new ActionEntry (
                    "PodcastHomepageAction", Stock.JumpTo,
                     Catalog.GetString ("Visit Podcast Homepage"),
                     null, String.Empty, 
                     OnPodcastHomepage
                ),
                new ActionEntry (
                    "PodcastPropertiesAction", Stock.Properties,
                     Catalog.GetString ("Properties"),
                     null, String.Empty, 
                     OnPodcastProperties
                ),
                new ActionEntry (
                    "PodcastItemMarkNewAction", null,
                     Catalog.GetString ("Mark as New"), 
                     null, String.Empty,
                     OnPodcastItemMarkNew
                ),
                new ActionEntry (
                    "PodcastItemMarkOldAction", null,
                     Catalog.GetString ("Mark as Old"), "y", String.Empty,
                     OnPodcastItemMarkOld
                ),
                new ActionEntry (
                    "PodcastItemDownloadAction", Stock.Save,
                     /* Translators: this is a verb used as a button name, not a noun*/
                     Catalog.GetString ("Download Podcast(s)"),
                     "<control><shift>D", String.Empty, 
                     OnPodcastItemDownload
                ),
                new ActionEntry (
                    "PodcastItemCancelAction", Stock.Cancel,
                     Catalog.GetString ("Cancel Download"),
                     "<control><shift>C", String.Empty, 
                     OnPodcastItemCancel
                ),
                new ActionEntry (
                    "PodcastItemDeleteFileAction", Stock.Remove,
                     Catalog.GetString ("Remove Downloaded File(s)"),
                     null, String.Empty, 
                     OnPodcastItemDeleteFile
                ),
                new ActionEntry (
                    "PodcastItemLinkAction", Stock.JumpTo,
                     Catalog.GetString ("Visit Website"),
                     null, String.Empty, 
                     OnPodcastItemLink
                ),
                new ActionEntry (
                    "PodcastItemPropertiesAction", Stock.Properties,
                     Catalog.GetString ("Properties"),
                     null, String.Empty, 
                     OnPodcastItemProperties
                )
            );

            this["PodcastAddAction"].ShortLabel = Catalog.GetString ("Subscribe to Podcast");
            
            actions_id = Actions.UIManager.AddUiFromResource ("GlobalUI.xml");
            Actions.AddActionGroup (this);

            ServiceManager.SourceManager.ActiveSourceChanged += HandleActiveSourceChanged;
            OnSelectionChanged (null, null);
        }

        public override void Dispose ()
        {
            Actions.UIManager.RemoveUi (actions_id);
            Actions.RemoveActionGroup (this);
            base.Dispose ();
        }

#region State Event Handlers
        
        private void HandleActiveSourceChanged (SourceEventArgs args)
        {
            if (last_source != null) {
                foreach (IFilterListModel filter in last_source.AvailableFilters) {
                    filter.Selection.Changed -= OnSelectionChanged;
                }
                last_source.TrackModel.Selection.Changed -= OnSelectionChanged;
                last_source = null;
            }

            last_source = args.Source as DatabaseSource;
            if (IsPodcastSource) {
                if (last_source != null) {
                    foreach (IFilterListModel filter in last_source.AvailableFilters) {
                        filter.Selection.Changed += OnSelectionChanged;
                    }
                    last_source.TrackModel.Selection.Changed += OnSelectionChanged;
                }
            } else {
                last_source = null;
            }

            OnSelectionChanged (null, null);
        }

        private void OnSelectionChanged (object o, EventArgs args)
        {
            Banshee.Base.ThreadAssist.ProxyToMain (delegate {
                UpdateFeedActions ();
                UpdateItemActions ();
            });
        }

#endregion

#region Utility Methods

        private DatabaseSource ActiveDbSource {
            get { return last_source; }
        }

        private bool IsPodcastSource {
            get {
                return ActiveDbSource != null && (ActiveDbSource is PodcastSource || ActiveDbSource.Parent is PodcastSource);
            }
        }

        public PodcastFeedModel ActiveFeedModel {
            get {
                if (ActiveDbSource == null) {
                    return null;
                } else if (ActiveDbSource is PodcastSource) {
                    return (ActiveDbSource as PodcastSource).FeedModel;
                } else {
                    foreach (IFilterListModel filter in ActiveDbSource.AvailableFilters) {
                        if (filter is PodcastFeedModel) {
                            return filter as PodcastFeedModel;
                        }
                    }
                    return null;
                }
            }
        }

        private void UpdateItemActions ()
        {
            if (IsPodcastSource) {
                bool has_single_selection = ActiveDbSource.TrackModel.Selection.Count == 1;
                UpdateActions (true, has_single_selection,
                   "PodcastItemLinkAction"
                );
            }
        }
        
        private void UpdateFeedActions ()
        {
            if (IsPodcastSource) {
                bool has_single_selection = ActiveFeedModel.Selection.Count == 1;
                bool all_selected = ActiveFeedModel.Selection.AllSelected;

                UpdateActions (true, has_single_selection && !all_selected,
                    "PodcastHomepageAction",
                    "PodcastPropertiesAction"
                );

                UpdateActions (true, ActiveFeedModel.Selection.Count > 0 && !all_selected,
                    "PodcastDeleteAction",
                    "PodcastUpdateFeedAction",
                    "PodcastDownloadAllAction"
                );
            }
        }
        
        private void SubscribeToPodcast (Uri uri, FeedAutoDownload syncPreference)
        {
            FeedsManager.Instance.FeedManager.CreateFeed (uri.ToString (), syncPreference);
        }

        private IEnumerable<TrackInfo> GetSelectedItems ()
        {
            return new List<TrackInfo> (ActiveDbSource.TrackModel.SelectedItems);
        }

#endregion

                
#region Action Handlers

        private void OnFeedPopup (object o, EventArgs args)
        {
            if (ActiveFeedModel.Selection.AllSelected) {
                ShowContextMenu ("/PodcastAllFeedsContextMenu");
            } else {
                ShowContextMenu ("/PodcastFeedPopup");
            }
        }

        private void RunSubscribeDialog ()
        {        
            Uri feedUri = null;
            FeedAutoDownload syncPreference;
            
            PodcastSubscribeDialog subscribeDialog = new PodcastSubscribeDialog ();
            
            ResponseType response = (ResponseType) subscribeDialog.Run ();
            syncPreference = subscribeDialog.SyncPreference;
            
            subscribeDialog.Destroy ();

            if (response == ResponseType.Ok) {
                string url = subscribeDialog.Url.Trim ().Trim ('/');
                
                if (String.IsNullOrEmpty (subscribeDialog.Url)) {
                    return;
                }
                
				if (!TryParseUrl (url, out feedUri)) {
                    HigMessageDialog.RunHigMessageDialog (
                        null,
                        DialogFlags.Modal,
                        MessageType.Warning,
                        ButtonsType.Ok,
                        Catalog.GetString ("Invalid URL"),
                        Catalog.GetString ("Podcast URL is invalid.")
                    );
				} else {
				    SubscribeToPodcast (feedUri, syncPreference); 
				}
            }        
        }
        
        /*private void RunConfirmDeleteDialog (bool feed, 
                                             int selCount, 
                                             out bool delete, 
                                             out bool deleteFiles)
        {
            
            delete = false;
            deleteFiles = false;        
            string header = null;
            int plural = (feed | (selCount > 1)) ? 2 : 1;

            if (feed) {
                header = Catalog.GetPluralString ("Delete Podcast?","Delete Podcasts?", selCount);
            } else {
                header = Catalog.GetPluralString ("Delete episode?", "Delete episodes?", selCount);
            }
                
            HigMessageDialog md = new HigMessageDialog (
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
            md.AddButton (
                Catalog.GetPluralString (
                    "Keep File", "Keep Files", plural
                ), ResponseType.No, false
            );
            
            md.AddButton (Stock.Delete, ResponseType.Yes, false);
            
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
        }*/
        
		private bool TryParseUrl (string url, out Uri uri)
		{
			uri = null;
			bool ret = false;
			
            try {
                uri = new Uri (url);
				
				if (uri.Scheme == Uri.UriSchemeHttp || 
				    uri.Scheme == Uri.UriSchemeHttps) {
					ret = true;
				}
            } catch {}
            
            return ret;			
		}

        /*private void OnFeedSelectionChangedHandler (object sender, EventArgs e)
        {
            lock (sync) {
                if (!disposed || disposing) {              
                    if (source.FeedModel.SelectedItems.Count == 0) {
                        source.FeedModel.Selection.Select (0);
                    }
                    
                    if (source.FeedModel.Selection.Contains (0)) {
                        itemModel.FilterOnFeed (Feed.All);
                    } else {
                        itemModel.FilterOnFeeds (source.FeedModel.CopySelectedItems ());
                    }
                    
                    itemModel.Selection.Clear ();
                }
            }
        }
        
        private void OnPodcastItemRowActivatedHandler (object sender, 
                                                       RowActivatedArgs<PodcastItem> e)
        {
            lock (sync) {
                if (!disposed || disposing) {
                    if (e.RowValue.Enclosure != null) {
                        e.RowValue.New = false;
                        ServiceManager.PlayerEngine.OpenPlay (e.RowValue);
                    }
                }
            }
        }*/

        private void OnPodcastAdd (object sender, EventArgs e)
        {
            RunSubscribeDialog ();
        }
        
        private void OnPodcastUpdate (object sender, EventArgs e)
        {
            foreach (Feed feed in ActiveFeedModel.SelectedItems) {
                feed.Update ();
            }
        }        
        
        private void OnPodcastUpdateAll (object sender, EventArgs e)
        {
            foreach (Feed feed in Feed.Provider.FetchAll ()) {
                feed.Update ();
            }
        }      
        
        private void OnPodcastDelete (object sender, EventArgs e)
        {
            foreach (Feed feed in ActiveFeedModel.SelectedItems) {
                if (feed != null) {
                    feed.Delete (true);
                }
            }
        }
        
        private void OnPodcastDownloadAllEpisodes (object sender, EventArgs e)
        {
            foreach (Feed feed in ActiveFeedModel.SelectedItems) {
                if (feed != null) {
                    foreach (FeedItem item in feed.Items) {
                        item.Enclosure.AsyncDownload ();
                    }
                }
            }
        }

        private void OnPodcastItemDeleteFile (object sender, EventArgs e)
        {
            foreach (PodcastTrackInfo pi in PodcastTrackInfo.From (GetSelectedItems ())) {
                if (pi.Enclosure.LocalPath != null)
                    pi.Enclosure.DeleteFile ();
            }
        }

        private void OnPodcastHomepage (object sender, EventArgs e)
        {
            Feed feed = ActiveFeedModel.FocusedItem;
            if (feed != null && !String.IsNullOrEmpty (feed.Link)) {
                Banshee.Web.Browser.Open (feed.Link);
            }   
        }   

        private void OnPodcastProperties (object sender, EventArgs e)
        {
            Feed feed = ActiveFeedModel.FocusedItem;
            if (feed != null) {
                new PodcastFeedPropertiesDialog (feed).Run ();
            }
        }  

        private void OnPodcastItemProperties (object sender, EventArgs e)
        {
                /*ReadOnlyCollection<PodcastItem> items = itemModel.CopySelectedItems ();
                
                if (items != null && items.Count == 1) {
                    new PodcastPropertiesDialog (items[0]).Run ();
                } */                
        } 

        private void OnPodcastItemMarkNew (object sender, EventArgs e)
        {
            MarkPodcastItemSelection (false);
        }
        
        private void OnPodcastItemMarkOld (object sender, EventArgs e)
        {
            MarkPodcastItemSelection (true);
        }     
        
        private void MarkPodcastItemSelection (bool markRead) 
        {
            TrackInfo new_selection_track = ActiveDbSource.TrackModel [ActiveDbSource.TrackModel.Selection.LastIndex + 1];
            
            PodcastService.IgnoreItemChanges = true;
            
            bool any = false;
            foreach (PodcastTrackInfo track in PodcastTrackInfo.From (GetSelectedItems ())) {
                if (track.Item.IsRead != markRead) {
                    track.Item.IsRead = markRead;
                    track.Item.Save ();

                    if (track.Item.IsRead ^ track.Track.PlayCount > 0) {
                        track.Track.PlayCount = track.Item.IsRead ? 1 : 0;
                        track.Track.Save (false);
                    }
                    any = true;
                }
            }
            
            PodcastService.IgnoreItemChanges = false;
            
            if (any) {
                ActiveDbSource.Reload ();
                
                // If we just removed all of the selected items from our view, we should select the
                // item after the last removed item
                if (ActiveDbSource.TrackModel.Selection.Count == 0 && new_selection_track != null) {
                    int new_i = ActiveDbSource.TrackModel.IndexOf (new_selection_track);
                    if (new_i != -1) {
                        ActiveDbSource.TrackModel.Selection.Clear (false);
                        ActiveDbSource.TrackModel.Selection.FocusedIndex = new_i;
                        ActiveDbSource.TrackModel.Selection.Select (new_i);
                    }
                }
            }
        }
        
        private void OnPodcastItemCancel (object sender, EventArgs e)
        {
            /*
                if (!disposed || disposing) {                    
                    ReadOnlyCollection<PodcastItem> items = itemModel.CopySelectedItems ();

                    if (items != null) {
                        foreach (PodcastItem pi in items) {
                            pi.Enclosure.CancelAsyncDownload ();
                        }
                    }                
                }*/
        }        
        
        private void OnPodcastItemDownload (object sender, EventArgs e)
        {
            foreach (PodcastTrackInfo pi in PodcastTrackInfo.From (GetSelectedItems ())) {
                pi.Enclosure.AsyncDownload ();
            }
        }
        
        private void OnPodcastItemLink (object sender, EventArgs e)
        {
            PodcastTrackInfo track = PodcastTrackInfo.From (ActiveDbSource.TrackModel.FocusedItem);
            if (track != null && !String.IsNullOrEmpty (track.Item.Link)) {
                Banshee.Web.Browser.Open (track.Item.Link);
            }
        }

#endregion

    }
}
