// 
// PaasSource.cs
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
using System.Collections.Generic;

using Mono.Unix;

using Hyena.Collections;

using Banshee.Base;
using Banshee.Sources;
using Banshee.Sources.Gui;
using Banshee.ServiceStack;

using Banshee.Collection;
using Banshee.Collection.Gui;
using Banshee.Collection.Database;

using Banshee.Paas.Gui;
using Banshee.Paas.Data;

// remove
using Hyena.Data;
// remove

namespace Banshee.Paas
{ 
    public class PaasSource : Banshee.Library.LibrarySource
    {
        private PaasActions actions = null;    
        private PaasSourceContents contents;
        private PaasChannelModel channel_model;
        
        protected override bool HasArtistAlbum {
            get { return false; }
        }

        public override bool AcceptsInputFromSource (Source source)
        {
            return false;
        }

        public override string DefaultBaseDirectory {
            get {
                // HACK there isn't an XDG_PODCASTS_DIR; propose it?
                return XdgBaseDirectorySpec.GetUserDirectory ("XDG_PODCASTS_DIR", "Podcasts");
            }
        }

        public override bool CanRename {
            get { return false; }
        }

        public override bool CanAddTracks {
            get { return true; }
        }

        public override bool CanRemoveTracks {
            get { return false; }
        }

        public override bool CanDeleteTracks {
            get { return false; }
        }
        
        public PaasChannelModel ChannelModel {
            get { return channel_model; }
        }

        public override string PreferencesPageId {
            get { return UniqueId; }
        }

        public override bool ShowBrowser {
            get { return true; }
        }

        public override SourceMergeType SupportedMergeTypes {
            get { return SourceMergeType.None; }
        }

#region Constructors

        public PaasSource (PaasService service) : base (Catalog.GetString ("Paas"), "PaasLibrary", 200)
        {
            actions = new PaasActions (service);

            SupportsPlaylists = false;
            TrackExternalObjectHandler = GetPaasTrackInfo;
            //TrackArtworkIdHandler = GetTrackArtworkId;
            MediaTypes = TrackMediaAttributes.Podcast;
            NotMediaTypes = TrackMediaAttributes.AudioBook;
            //SyncCondition = "(substr(CoreTracks.Uri, 0, 4) != 'http' AND CoreTracks.PlayCount = 0)";
            
            Properties.SetString ("Icon.Name", "weather-storm");
            
            Properties.SetString ("ActiveSourceUIResource", "ActiveSourceUI.xml");
            Properties.Set<bool> ("ActiveSourceUIResourcePropagate", true);
            Properties.Set<System.Reflection.Assembly> ("ActiveSourceUIResource.Assembly", typeof(PaasSource).Assembly);

            Properties.SetString ("GtkActionPath", "/PaasSourcePopup");
            
            contents = new PaasSourceContents ();
            (contents.TrackView as PaasItemView).PopupMenu += OnPopupMenuHandler;
            (contents.TrackView as PaasItemView).FuckedPopupMenu += OnFuckedPopupMenuHandler;            
            
            Properties.Set<ISourceContents> ("Nereid.SourceContents", contents);
            Properties.Set<bool> ("Nereid.SourceContentsPropagate", false);

            PaasColumnController column_controller = new PaasColumnController ();
            
            column_controller.SetIndicatorColumnDataHelper (
                (cell, item) => 
                    service.DownloadManager.CheckActiveDownloadStatus ((item as PaasItem).DbId
                )
            );

            Properties.Set<PaasColumnController> ("TrackView.ColumnController", column_controller);
        }
        
#endregion

        public void AddItem (PaasItem item)
        {
            if (item != null) {
                DatabaseTrackInfo track = new DatabaseTrackInfo ();
                track.ExternalId = item.DbId;
                item.TrackID = item.DbId;
                track.PrimarySource = this;
                (track.ExternalObject as PaasTrackInfo).SyncWithItem ();
                track.Save (false);
                item.Save ();            

                //RefreshArtworkFor (item.Feed);
            } 
        }

        public void AddItems (IEnumerable<PaasItem> items)
        {
            if (items != null) {
                foreach (PaasItem item in items) {
                    AddItem (item);
                }
            } 
        }

        public void RemoveItem (PaasItem item)
        {
            if (item != null) {
                RemoveTrack ((int)item.TrackID);
            }
        }

        public void RemoveItems (IEnumerable<PaasItem> items)
        {
            if (items != null) {
                RangeCollection rc = new RangeCollection ();
                // This sucks, PaasItem needs to inherit from DatabaseTrackInfo                
                
                //Console.WriteLine (TrackModel.IndexOf (item.Track));
                
                foreach (PaasItem item in items) {
                    rc.Add ((int)item.TrackID);
                }

                foreach (RangeCollection.Range range in rc.Ranges) {
                    RemoveTrackRange (TrackModel as DatabaseTrackListModel, range);
                }
            }
        }

        public void QueueDraw ()
        {
            ThreadAssist.ProxyToMain (delegate {
                contents.QueueDraw ();
            });
        }

        protected override IEnumerable<IFilterListModel> CreateFiltersFor (DatabaseSource src)
        {
            PaasChannelModel channel_model = new PaasChannelModel (
                src, src.DatabaseTrackModel, ServiceManager.DbConnection, 
                String.Format ("PaasChannels-{0}", src.UniqueId)
            );
            
            yield return channel_model;
/*
            yield return new PodcastUnheardFilterModel (src.DatabaseTrackModel);
            yield return new DownloadStatusFilterModel (src.DatabaseTrackModel);
*/
            if (src == this) {
                this.channel_model = channel_model;
                AfterInitialized ();
            }
        }

        public override void Dispose ()
        {
            if (actions != null) {
                actions.Dispose ();
                actions = null;
            }
            
            base.Dispose ();
        }

        // Right now there's no way to get a track's CacheEntryID from the items removed during an update...
        // Meaning PrimarySource.RemoveTrackRange is useless because of its DatabaseTrackListModel.TrackIdsSql dep.  
        // Yell at me is this sucks.
        protected override void RemoveTrackRange (DatabaseTrackListModel model, RangeCollection.Range range)
        {
            ServiceManager.DbConnection.Execute (
                String.Format (remove_range_sql, "TrackID FROM CoreTracks WHERE TrackID >= ? AND TrackID <= ?"),
                DateTime.Now, range.Start, range.End, range.Start, range.End
            );
        }

        private object GetPaasTrackInfo (DatabaseTrackInfo track)
        {
            return new PaasTrackInfo (track);
        }

        protected override DatabaseTrackListModel CreateTrackModelFor (DatabaseSource src)
        {
            return new PaasTrackListModel (ServiceManager.DbConnection, DatabaseTrackInfo.Provider, src);
        }

        [GLib.ConnectBefore]
        private void OnPopupMenuHandler (object sender, Gtk.PopupMenuArgs e)
        {
            Console.WriteLine ("Popup");
            actions.UpdateItemActions ();
        }

        private void OnFuckedPopupMenuHandler (object sender, EventArgs e)
        {
            Console.WriteLine ("FuckedPopup");
            actions.UpdateItemActions ();
        }
/*
        private string GetTrackArtworkId (DatabaseTrackInfo track)
        {
            return PodcastService.ArtworkIdFor (PodcastTrackInfo.From (track).Feed);
        }

        protected override IEnumerable<IFilterListModel> CreateFiltersFor (DatabaseSource src)
        {
            PodcastFeedModel feed_model;
            yield return feed_model = new PodcastFeedModel (src, src.DatabaseTrackModel, ServiceManager.DbConnection, String.Format ("PodcastFeeds-{0}", src.UniqueId));
            yield return new PodcastUnheardFilterModel (src.DatabaseTrackModel);
            yield return new DownloadStatusFilterModel (src.DatabaseTrackModel);

            if (src == this) {
                this.feed_model = feed_model;
                AfterInitialized ();
            }
        }
*/
        
        // Probably don't want this -- do we want to allow actually removing the item?  It will be
        // repopulated the next time we update the podcast feed...
        /*protected override void DeleteTrack (DatabaseTrackInfo track)
        {
            PodcastTrackInfo episode = track as PodcastTrackInfo;
            if (episode != null) {
                if (episode.Uri.IsFile)
                    base.DeleteTrack (track);
                
                episode.Delete ();
                episode.Item.Delete (false);
            }
        }*/    
    }
}
