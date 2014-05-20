/***************************************************************************
 *  PodcastManagerSource.cs
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
using System.Linq;
using System.Collections.Generic;

using Gtk;
using Gdk;

using Mono.Unix;

using Hyena;
using Hyena.Data;
using Hyena.Data.Gui;
using Hyena.Data.Sqlite;

using Banshee.Gui;
using Banshee.Base;
using Banshee.Query;
using Banshee.Database;
using Banshee.Collection;
using Banshee.ServiceStack;
using Banshee.Collection.Database;

using Banshee.Sources;
using Banshee.Sources.Gui;

using Banshee.Podcasting.Data;

using Migo.Syndication;

namespace Banshee.Podcasting.Gui
{
    public class PodcastSource : Banshee.Library.LibrarySource
    {
        private PodcastFeedModel feed_model;
        private PodcastUnheardFilterModel new_filter;

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

        public override bool CanShuffle {
            get { return false; }
        }

        public PodcastFeedModel FeedModel {
            get { return feed_model; }
        }

        public PodcastUnheardFilterModel NewFilter { get { return new_filter; } }

        public override string PreferencesPageId {
            get { return UniqueId; }
        }

        protected override string SectionName {
            get { return Catalog.GetString ("Podcasts Folder"); }
        }

        class FeedMessage : SourceMessage
        {
            public Feed Feed { get; set; }

            public bool Valid { get; private set; }

            public FeedMessage (Source src, Feed feed) : base (src)
            {
                Feed = feed;
                Update ();
            }

            public void Update ()
            {
                ClearActions ();
                CanClose = Feed.LastDownloadError != FeedDownloadError.None;
                IsSpinning = !CanClose;

                var title = Feed.Title == Feed.UnknownPodcastTitle ? Feed.Url : Feed.Title;

                if (CanClose) {
                    Text = String.Format (GetErrorText (), title);
                    SetIconName ("dialog-error");

                    AddAction (new MessageAction (Catalog.GetString ("Remove Podcast"), delegate {
                        Feed.Delete (true);
                        IsHidden = true;
                    }));

                    AddAction (new MessageAction (Catalog.GetString ("Disable Auto Updates"), delegate {
                        Feed.IsSubscribed = false;
                        Feed.Save ();
                        IsHidden = true;
                    }));
                } else {
                    Text = String.Format (Catalog.GetString ("Loading {0}"), title);
                }

                // TODO Avoid nagging about an error more than once
                Valid = true;//Feed.LastDownloadTime == DateTime.MinValue || Feed.LastDownloadTime > last_feed_nag;
            }

            private string GetErrorText ()
            {
                switch (Feed.LastDownloadError) {
                    case FeedDownloadError.DoesNotExist:
                    case FeedDownloadError.DownloadFailed:
                        return Catalog.GetString ("Network error updating {0}");

                    case FeedDownloadError.InvalidFeedFormat:
                    case FeedDownloadError.NormalizationFailed:
                    case FeedDownloadError.UnsupportedMsXml:
                    case FeedDownloadError.UnsupportedDtd:
                        return Catalog.GetString ("Parsing error updating {0}");

                    case FeedDownloadError.UnsupportedAuth:
                        return Catalog.GetString ("Authentication error updating {0}");

                    default:
                        return Catalog.GetString ("Error updating {0}");
                }
            }
        }

        //private static DateTime last_feed_nag = DateTime.MinValue;
        private List<FeedMessage> feed_messages = new List<FeedMessage> ();
        public void UpdateFeedMessages ()
        {
            var feeds = Feed.Provider.FetchAllMatching (
                "IsSubscribed = 1 AND (LastDownloadTime = 0 OR LastDownloadError != 0) ORDER BY LastDownloadTime ASC").ToList ();

            lock (feed_messages) {
                var msgs = new List<FeedMessage> ();

                var cur = CurrentMessage as FeedMessage;
                if (cur != null && feeds.Contains (cur.Feed)) {
                    cur.Update ();
                    feeds.Remove (cur.Feed);
                    feed_messages.Remove (cur);
                    msgs.Add (cur);
                }

                feed_messages.ForEach (RemoveMessage);
                feed_messages.Clear ();

                foreach (var feed in feeds) {
                    var msg = new FeedMessage (this, feed);
                    if (msg.Valid) {
                        msgs.Add (msg);
                        PushMessage (msg);
                    }
                }

                feed_messages = msgs;
                //last_feed_nag = DateTime.Now;

                // If there's at least one new message, notify the user
                if (msgs.Count > ((cur != null) ? 1 : 0)) {
                    NotifyUser ();
                }
            }
        }

#region Constructors

        public PodcastSource () : base (Catalog.GetString ("Podcasts"), "PodcastLibrary", 51)
        {
            TrackExternalObjectHandler = GetPodcastInfoObject;
            TrackArtworkIdHandler = GetTrackArtworkId;
            MediaTypes = TrackMediaAttributes.Podcast;
            NotMediaTypes = TrackMediaAttributes.AudioBook;
            SyncCondition = String.Format ("(substr({0}, 0, 4) != 'http' AND {1} = 0)",
                                           BansheeQuery.UriField.Column, BansheeQuery.PlayCountField.Column);
            TrackModel.Reloaded += OnReloaded;

            Properties.SetString ("Icon.Name", "podcast");
            Properties.Set<string> ("SearchEntryDescription", Catalog.GetString ("Search your podcasts"));

            Properties.Set<string> ("SelectedTracksPropertiesActionLabel", Catalog.GetString ("Episode Properties"));

            Properties.SetString ("ActiveSourceUIResource", "ActiveSourceUI.xml");
            Properties.Set<bool> ("ActiveSourceUIResourcePropagate", true);
            Properties.Set<System.Reflection.Assembly> ("ActiveSourceUIResource.Assembly", typeof(PodcastSource).Assembly);

            Properties.SetString ("GtkActionPath", "/PodcastSourcePopup");

            Properties.Set<ISourceContents> ("Nereid.SourceContents", new LazyLoadSourceContents<PodcastSourceContents> ());
            Properties.Set<bool> ("Nereid.SourceContentsPropagate", true);

            Properties.SetString ("TrackView.ColumnControllerXml", String.Format (@"
                    <column-controller>
                      <add-all-defaults />
                      <column modify-default=""IndicatorColumn"">
                          <renderer type=""Banshee.Podcasting.Gui.ColumnCellPodcastStatusIndicator"" />
                      </column>
                      <remove-default column=""TrackColumn"" />
                      <remove-default column=""DiscColumn"" />
                      <remove-default column=""ComposerColumn"" />
                      <remove-default column=""ArtistColumn"" />
                      <column modify-default=""AlbumColumn"">
                        <title>{0}</title>
                        <long-title>{0}</long-title>
                        <sort-key>PodcastTitle</sort-key>
                        <renderer property=""ExternalObject.PodcastTitle""/>
                      </column>
                      <column modify-default=""DurationColumn"">
                          <visible>false</visible>
                      </column>
                      <column>
                          <visible>false</visible>
                          <title>{4}</title>
                          <renderer type=""Hyena.Data.Gui.ColumnCellText"" property=""ExternalObject.Description"" />
                          <sort-key>Description</sort-key>
                      </column>
                      <column>
                          <visible>false</visible>
                          <title>{2}</title>
                          <renderer type=""Banshee.Podcasting.Gui.ColumnCellYesNo"" property=""ExternalObject.IsNew"" />
                          <sort-key>IsNew</sort-key>
                      </column>
                      <column>
                          <visible>false</visible>
                          <title>{3}</title>
                          <renderer type=""Banshee.Podcasting.Gui.ColumnCellYesNo"" property=""ExternalObject.IsDownloaded"" />
                          <sort-key>IsDownloaded</sort-key>
                      </column>
                      <column>
                          <visible>true</visible>
                          <title>{1}</title>
                          <renderer type=""Banshee.Podcasting.Gui.ColumnCellPublished"" property=""ExternalObject.PublishedDate"" />
                          <sort-key>PublishedDate</sort-key>
                      </column>
                      <sort-column direction=""desc"">published_date</sort-column>
                    </column-controller>
                ",
                Catalog.GetString ("Podcast"), Catalog.GetString ("Published"), Catalog.GetString ("New"),
                Catalog.GetString ("Downloaded"), Catalog.GetString ("Description")
            ));
        }

#endregion

        private object GetPodcastInfoObject (DatabaseTrackInfo track)
        {
            return new PodcastTrackInfo (track);
        }

        private string GetTrackArtworkId (DatabaseTrackInfo track)
        {
            return PodcastService.ArtworkIdFor (PodcastTrackInfo.From (track).Feed);
        }

        protected override bool HasArtistAlbum {
            get { return false; }
        }

        public override bool HasEditableTrackProperties {
            get { return false; }
        }

        public override string GetPluralItemCountString (int count)
        {
            return Catalog.GetPluralString ("{0} episode", "{0} episodes", count);
        }

        public override bool AcceptsInputFromSource (Source source)
        {
            return false;
        }

        public PodcastTrackListModel PodcastTrackModel { get; private set; }

        protected override DatabaseTrackListModel CreateTrackModelFor (DatabaseSource src)
        {
            var model = new PodcastTrackListModel (ServiceManager.DbConnection, DatabaseTrackInfo.Provider, src);

            if (PodcastTrackModel == null) {
                PodcastTrackModel = model;
            }

            return model;
        }

        protected override IEnumerable<IFilterListModel> CreateFiltersFor (DatabaseSource src)
        {
            PodcastFeedModel feed_model;
            yield return new_filter = new PodcastUnheardFilterModel (src.DatabaseTrackModel);
            yield return feed_model = new PodcastFeedModel (src, src.DatabaseTrackModel, ServiceManager.DbConnection, String.Format ("PodcastFeeds-{0}", src.UniqueId));

            if (src == this) {
                this.feed_model = feed_model;
                AfterInitialized ();
            }
        }

        void OnReloaded(object sender, EventArgs e)
        {
            for (int i=0; i < TrackModel.Count; i++) {
                PodcastTrackInfo.From (TrackModel[i]);
            }
        }

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

        /*protected override void AddTrack (DatabaseTrackInfo track)
        {
            // TODO
            // Need to create a Feed, FeedItem, and FeedEnclosure for this track for it to be
            // considered a Podcast item
            base.AddTrack (track);
        }*/

        public override bool ShowBrowser {
            get { return true; }
        }

        /*public override IEnumerable<SmartPlaylistDefinition> DefaultSmartPlaylists {
            get { return default_smart_playlists; }
        }

        private static SmartPlaylistDefinition [] default_smart_playlists = new SmartPlaylistDefinition [] {
            new SmartPlaylistDefinition (
                Catalog.GetString ("Favorites"),
                Catalog.GetString ("Videos rated four and five stars"),
                "rating>=4"),

            new SmartPlaylistDefinition (
                Catalog.GetString ("Unwatched"),
                Catalog.GetString ("Videos that haven't been played yet"),
                "plays=0"),
        };*/
    }
}
