/***************************************************************************
 *  Feed.cs
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
using System.IO;
using System.Collections.Generic;

using Mono.Unix;

using Hyena.Data.Sqlite;

namespace Migo.Syndication
{
    public enum FeedAutoDownload : int
    {
        All = 0,
        One = 1,
        None = 2
    }

    // TODO remove this, way too redundant with DownloadStatus
    public enum PodcastFeedActivity : int {
        Updating = 0,
        UpdatePending = 1,
        UpdateFailed = 2,
        ItemsDownloading = 4,
        ItemsQueued = 5,
        None = 6
    }

    public class FeedProvider : CacheableSqliteModelProvider<Feed>
    {
        public FeedProvider (HyenaSqliteConnection connection) : base (connection, "PodcastSyndications")
        {
        }

        protected override void CreateTable ()
        {
            base.CreateTable ();

            CreateIndex ("PodcastSyndicationsIndex", "IsSubscribed, Title");
        }

        protected override int ModelVersion {
            get { return 4; }
        }

        protected override void MigrateTable (int old_version)
        {
            CheckTable ();

            if (old_version < 2) {
                Connection.Execute (String.Format ("UPDATE {0} SET IsSubscribed=1", TableName));
            }

            if (old_version < 3) {
                CreateIndex ("PodcastSyndicationsIndex", "IsSubscribed, Title");
            }

            if (old_version < 4) {
                Connection.Execute (String.Format ("UPDATE {0} SET MaxItemCount=0 WHERE MaxItemCount=200", TableName));
            }
        }
    }

    public class Feed : CacheableItem<Feed>
    {
        private static FeedProvider provider;
        public static FeedProvider Provider {
            get { return provider; }
        }

        public static void Init () {
            provider = new FeedProvider (FeedsManager.Instance.Connection);
        }

        public static bool Exists (string url)
        {
            return Provider.Connection.Query<int> (String.Format ("select count(*) from {0} where url = ?", Provider.TableName), url) != 0;
        }

        //private bool canceled;
        //private bool deleted;
        //private bool updating;

        //private ManualResetEvent updatingHandle = new ManualResetEvent (true);

        private readonly object sync = new object ();

        private string copyright;
        private string description;
        private string image_url;
        private int update_period_minutes = 24 * 60;
        private string language;
        private DateTime last_build_date = DateTime.MinValue;
        private FeedDownloadError lastDownloadError;
        private DateTime last_download_time = DateTime.MinValue;
        private string link;
        //private string local_enclosure_path;
        private long dbid = -1;
        private long maxItemCount = 0;
        private DateTime pubDate;
        private FeedSyncSetting syncSetting;
        private string title;
        private string url;
        private string keywords, category;

#region Database-bound Properties

        [DatabaseColumn ("FeedID", Constraints = DatabaseColumnConstraints.PrimaryKey)]
        public override long DbId {
            get { return dbid; }
            protected set { dbid = value; }
        }

        public static string UnknownPodcastTitle = Catalog.GetString ("Unknown Podcast");

        [DatabaseColumn]
        public string Title {
            get { return title ?? UnknownPodcastTitle; }
            set { title = value; }
        }

        [DatabaseColumn]
        public string Description {
            get { return description; }
            set { description = value; }
        }

        [DatabaseColumn]
        public string Url {
            get { return url; }
            set { url = value; }
        }

        [DatabaseColumn]
        public string Keywords {
            get { return keywords; }
            set { keywords = value; }
        }

        [DatabaseColumn]
        public string Category {
            get { return category; }
            set { category = value; }
        }

        [DatabaseColumn]
        public string Copyright {
            get { return copyright; }
            set { copyright = value; }
        }

        [DatabaseColumn]
        public string ImageUrl {
            get { return image_url; }
            set { image_url = value; }
        }

        [DatabaseColumn]
        public int UpdatePeriodMinutes {
            get { return update_period_minutes; }
            set { update_period_minutes = value; }
        }

        [DatabaseColumn]
        public string Language {
            get { return language; }
            set { language = value; }
        }

        [DatabaseColumn]
        public FeedDownloadError LastDownloadError {
            get { return lastDownloadError; }
            set { lastDownloadError = value; }
        }

        [DatabaseColumn]
        public DateTime LastDownloadTime {
            get { return last_download_time; }
            set { last_download_time = value; }
        }

        [DatabaseColumn]
        public string Link {
            get { return link; }
            set { link = value; }
        }

        //[DatabaseColumn]
        public string LocalEnclosurePath {
            get {
                string escaped = Hyena.StringUtil.EscapeFilename (Title);
                return Path.Combine (FeedsManager.Instance.PodcastStorageDirectory, escaped);
            }
            //set { local_enclosure_path = value; }
        }

        [DatabaseColumn]
        public long MaxItemCount {
            get { return maxItemCount; }
            set { maxItemCount = value; }
        }

        [DatabaseColumn]
        public DateTime PubDate {
            get { return pubDate; }
            set { pubDate = value; }
        }

        [DatabaseColumn]
        public DateTime LastBuildDate {
            get { return last_build_date; }
            set { last_build_date = value; }
        }

        /*private DateTime last_downloaded;
        [DatabaseColumn]
        public DateTime LastDownloaded {
            get { return last_downloaded; }
            set { last_downloaded = value; }
        }*/

        [DatabaseColumn]
		public FeedSyncSetting SyncSetting {
            get { return syncSetting; }
            set { syncSetting = value; }
        }

        [DatabaseColumn]
        protected DateTime last_auto_download = DateTime.MinValue;
        public DateTime LastAutoDownload {
            get { return last_auto_download; }
            set { last_auto_download = value; }
        }

        [DatabaseColumn("AutoDownload")]
        protected FeedAutoDownload auto_download = FeedAutoDownload.None;
        public FeedAutoDownload AutoDownload {
            get { return auto_download; }
            set {
                if (value == auto_download)
                    return;

                auto_download = value;
                CheckForItemsToDownload ();
            }
        }

        [DatabaseColumn("DownloadStatus")]
        private FeedDownloadStatus download_status;
        public FeedDownloadStatus DownloadStatus {
            get { return download_status; }
            set { download_status = value; }
        }

        [DatabaseColumn("IsSubscribed")]
        private bool is_subscribed;
        public bool IsSubscribed {
            get { return is_subscribed; }
            set { is_subscribed = value; }
        }

#endregion

#region Other Properties

        // TODO remove this, way too redundant with DownloadStatus
        /*public PodcastFeedActivity Activity {
            get { return activity; }

                PodcastFeedActivity ret = PodcastFeedActivity.None;

                if (this == All) {
                    return ret;
                }

                switch (DownloadStatus) {
                case FeedDownloadStatus.Pending:
                    ret = PodcastFeedActivity.UpdatePending;
                    break;
                case FeedDownloadStatus.Downloading:
                    ret = PodcastFeedActivity.Updating;
                    break;
                case FeedDownloadStatus.DownloadFailed:
                    ret = PodcastFeedActivity.UpdateFailed;
                    break;
                }

                if (ret != PodcastFeedActivity.Updating) {
                    if (ActiveDownloadCount > 0) {
                        ret = PodcastFeedActivity.ItemsDownloading;
                    } else if (QueuedDownloadCount > 0) {
                        ret = PodcastFeedActivity.ItemsQueued;
                    }
                }

                return ret;
            }
        }*/

        public IEnumerable<FeedItem> Items {
            get {
                if (DbId > 0) {
                    foreach (FeedItem item in
                        FeedItem.Provider.FetchAllMatching (String.Format ("{0}.FeedID = {1} ORDER BY {0}.PubDate DESC", FeedItem.Provider.TableName, DbId)))
                    {
                        yield return item;
                    }
                }
            }
        }

#endregion

        private static FeedManager Manager {
            get { return FeedsManager.Instance.FeedManager; }
        }

#region Constructors

        public Feed (string url, FeedAutoDownload auto_download) : this ()
        {
            Url = url;
            this.auto_download = auto_download;
        }

        public Feed ()
        {
        }

#endregion

#region Internal Methods

        // Removing a FeedItem means removing the downloaded file.
        /*public void Remove (FeedItem item)
        {
            if (item == null) {
                throw new ArgumentNullException ("item");
            }


                if (items.Remove (item)) {
                    inactive_items.Add (item);
                    OnFeedItemRemoved (item);
                }
            }
        }*/

        /*public void Remove (IEnumerable<FeedItem> itms)
        {
                if (removedItems.Count > 0) {
                    OnItemsChanged ();
                }
            }
        }*/

#endregion

#region Private Methods

        public void SetItems (IEnumerable<FeedItem> items)
        {
            bool added_any = false;
            foreach (FeedItem item in items) {
                added_any |= AddItem (item);
            }

            if (added_any) {
               CheckForItemsToArchive ();
               Manager.OnFeedsChanged ();
               CheckForItemsToDownload ();
            }
        }

        private bool AddItem (FeedItem item)
        {
            try {
            if (!FeedItem.Exists (this.DbId, item.Guid)) {
                item.Feed = this;
                item.Save ();
                return true;
            }
            } catch (Exception e) {
                Hyena.Log.Error (e);
            }
            return false;
        }

        /*private void UpdateItems (IEnumerable<FeedItem> new_items)
        {
            ICollection<FeedItem> tmpNew = null;
            List<FeedItem> zombies = new List<FeedItem> ();

            if (items.Count == 0 && inactive_items.Count == 0) {
                tmpNew = new List<FeedItem> (new_items);
            } else {
                // Get remote items that aren't in the items list
                tmpNew = Diff (items, new_items);

                // Of those, remove the ones that are in our inactive list
                tmpNew = Diff (inactive_items, tmpNew);

                // Get a list of inactive items that aren't in the remote list any longer
                ICollection<FeedItem> doubleKilledZombies = Diff (
                    new_items, inactive_items
                );

                foreach (FeedItem zombie in doubleKilledZombies) {
                    inactive_items.Remove (zombie);
                }

                zombies.AddRange (doubleKilledZombies);

                foreach (FeedItem fi in Diff (new_items, items)) {
                    if (fi.Enclosure != null &&
                        !String.IsNullOrEmpty (fi.Enclosure.LocalPath)) {
                        // A hack for the podcast plugin, keeps downloaded items
                        // from being deleted when they are no longer in the feed.
                        continue;
                    }

                    zombies.Add (fi);
                }
            }

            if (tmpNew.Count > 0) {
                Add (tmpNew);
            }

            // TODO merge...should we really be deleting these items?
            if (zombies.Count > 0) {
                foreach (FeedItem item in zombies) {
                    if (item.Active) {
                        zombie.Delete ();
                    }
                }

                // TODO merge
                //ItemsTableManager.Delete (zombies);
            }
        }

        // Written before LINQ, will update.
        private ICollection<FeedItem> Diff (IEnumerable<FeedItem> baseSet,
                                            IEnumerable<FeedItem> overlay) {
            bool found;
            List<FeedItem> diff = new List<FeedItem> ();

            foreach (FeedItem opi in overlay) {
                found = false;

                foreach (FeedItem bpi in baseSet) {
                    if (opi.Title == bpi.Title &&
                        opi.Description == bpi.Description) {
                        found = true;
                        break;
                    }
                }

                if (!found) {
                    diff.Add (opi);
                }
            }

            return diff;
        }*/

#endregion

#region Public Methods


        public void Update ()
        {
            Manager.QueueUpdate (this);
        }

        public void Delete ()
        {
            Delete (true);
            Manager.OnFeedsChanged ();
        }

        public void Delete (bool deleteEnclosures)
        {
            lock (sync) {
                //if (deleted)
                //    return;

                //if (updating) {
                //    Manager.CancelUpdate (this);
                //}

                foreach (FeedItem item in Items) {
                    item.Delete (deleteEnclosures);
                }

                Provider.Delete (this);
            }

            //updatingHandle.WaitOne ();
            Manager.OnFeedsChanged ();
        }

        public void MarkAllItemsRead ()
        {
            lock (sync) {
                foreach (FeedItem i in Items) {
                    i.IsRead = true;
                }
            }
        }

        public override string ToString ()
        {
            return String.Format ("Title:  {0} - Url:  {1}", Title, Url);
        }

        public void Save ()
        {
            Save (true);
        }

        public void Save (bool notify)
        {
            Save (notify, true);
        }

        public void Save (bool notify, bool download_items)
        {
            Provider.Save (this);
            CheckForItemsToArchive ();

            if (download_items && (LastBuildDate > LastAutoDownload)) {
                CheckForItemsToDownload ();
            }

            if (notify) {
                Manager.OnFeedsChanged ();
            }
        }

        private void CheckForItemsToArchive ()
        {
            if (MaxItemCount == 0)
                return;

            int i = 0;
            foreach (var item in Items) {
                if (!item.IsRead) {
                    if (i++ >= MaxItemCount) {
                        item.IsRead = true;
                        item.Save (false);
                    }
                }
            }
        }

        private void CheckForItemsToDownload ()
        {
            if (LastDownloadError != FeedDownloadError.None || AutoDownload == FeedAutoDownload.None)
                return;

            bool only_first = (AutoDownload == FeedAutoDownload.One);

            bool any = false;
            foreach (FeedItem item in Items) {
                if (item.Enclosure != null && item.Active &&
                    item.Enclosure.DownloadStatus != FeedDownloadStatus.Downloaded && item.PubDate > LastAutoDownload)
                {
                    item.Enclosure.AsyncDownload ();
                    any = true;
                    if (only_first)
                        break;
                }
            }

            if (any) {
                LastAutoDownload = DateTime.Now;
                // We don't want Save to call CheckForItemsToDownload again
                Save (true, false);
            }
        }

        /*private bool SetCanceled ()
        {
            bool ret = false;

            if (!canceled && updating) {
                ret = canceled = true;
            }

            return ret;
        }*/

#endregion

    }
}
