//
// FeedItem.cs
//
// Authors:
//   Mike Urbanski  <michael.c.urbanski@gmail.com>
//   Gabriel Burt  <gburt@novell.com>
//
// Copyright (C) 2007 Michael C. Urbanski
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

using Hyena.Data.Sqlite;

namespace Migo.Syndication
{
    public class FeedItemProvider : CacheableSqliteModelProvider<FeedItem>
    {
        public FeedItemProvider (HyenaSqliteConnection connection) : base (connection, "PodcastItems")
        {
        }
    }

    public class FeedItem : CacheableItem<FeedItem>
    {
        private static SqliteModelProvider<FeedItem> provider;
        public static SqliteModelProvider<FeedItem> Provider {
            get { return provider; }
        }

        public static bool Exists (long feed_id, string guid)
        {
            return Provider.Connection.Query<int> (
                String.Format ("SELECT count(*) FROM {0} WHERE FeedID = ? AND Guid = ?", Provider.TableName),
                feed_id, guid
            ) != 0;
        }

        public static void Init () {
            provider = new FeedItemProvider (FeedsManager.Instance.Connection);
        }

        private bool active = true;
        private string author;
        private string comments;
        private string description;
        private string stripped_description;
        private FeedEnclosure enclosure;
        private string guid;
        private bool isRead;
        private string link;
        private long dbid;
        private DateTime modified;
        private Feed feed;
        private DateTime pubDate;
        private string title;

#region Database-backed Properties

        [DatabaseColumn ("ItemID", Constraints = DatabaseColumnConstraints.PrimaryKey)]
        public override long DbId {
            get { return dbid; }
            protected set { dbid = value; }
        }

        [DatabaseColumn("FeedID", Index = "PodcastItemsFeedIDIndex")]
        protected long feed_id;
        public long FeedId {
            get { return feed_id; }
        }

        [DatabaseColumn]
        internal bool Active {
            get { return active;}
            set {
                if (value != active) {
                    active = value;
                }
            }
        }

        [DatabaseColumn]
        public string Author {
            get { return author; }
            set { author = value; }
        }

        [DatabaseColumn]
        public string Comments {
            get { return comments; }
            set { comments = value; }
        }

        [DatabaseColumn]
        public string Description {
            get { return description; }
            set {
                description = value;
            }
        }

        [DatabaseColumn]
        public string StrippedDescription {
            get { return stripped_description; }
            set { stripped_description = value; }
        }

        [DatabaseColumn("Guid", Index = "PodcastItemsGuidIndex")]
        public string Guid {
            get {
                if (String.IsNullOrEmpty (guid)) {
                    guid = String.Format ("{0}-{1}", Title,
                        PubDate.ToUniversalTime ().ToString (System.Globalization.DateTimeFormatInfo.InvariantInfo)
                    );
                }
                return guid;
            }
            set { guid = value; }
        }

        [DatabaseColumn("IsRead", Index = "PodcastItemIsReadIndex")]
        public bool IsRead {
            get { return isRead; }
            set { isRead = value; }
        }

        [DatabaseColumn]
        public string Link {
            get { return link; }
            set { link = value; }
        }

        [DatabaseColumn]
        public DateTime Modified {
            get { return modified; }
            set { modified = value; }
        }

        [DatabaseColumn]
        public DateTime PubDate {
            get { return pubDate; }
            set { pubDate = value; }
        }

        [DatabaseColumn]
        public string Title {
            get { return title; }
            set { title = value; }
        }

        [DatabaseColumn("LicenseUri")]
        protected string license_uri;
        public string LicenseUri {
            get { return license_uri; }
            set { license_uri = value; }
        }

#endregion

#region Properties

        public Feed Feed {
            get {
                if (feed == null && feed_id > 0) {
                    feed = Feed.Provider.FetchSingle (feed_id);
                }
                return feed;
            }
            internal set { feed = value; feed_id = value.DbId; }
        }

        public FeedEnclosure Enclosure {
            get {
                if (enclosure == null) {
                    LoadEnclosure ();
                }
                return enclosure;
            }
            internal set {
                enclosure = value;
                enclosure_loaded = true;
                if (enclosure != null)
                    enclosure.Item = this;
            }
        }

#endregion

#region Constructor

        public FeedItem ()
        {
        }

#endregion

        private static FeedManager Manager {
            get { return FeedsManager.Instance.FeedManager; }
        }

#region Public Methods

        public void UpdateStrippedDescription ()
        {
            StrippedDescription = Hyena.StringUtil.RemoveHtml (Description);
            if (StrippedDescription != null) {
                StrippedDescription = System.Web.HttpUtility.HtmlDecode (StrippedDescription);
            }
        }

        public void Save ()
        {
            Save (true);
        }

        internal void Save (bool notify)
        {
            bool is_new = DbId < 1;
            Provider.Save (this);
            if (enclosure != null) {
                enclosure.Item = this;
                enclosure.Save (false);
            }

            if (is_new)
                Manager.OnItemAdded (this);
            else
                Manager.OnItemChanged (this);
        }

        public void Delete (bool delete_file)
        {
            if (enclosure != null) {
                enclosure.Delete (delete_file);
            }

            Provider.Delete (this);
            Manager.OnItemRemoved (this);
        }

#endregion

        private bool enclosure_loaded;
        private void LoadEnclosure ()
        {
            if (!enclosure_loaded && DbId > 0) {
                FeedEnclosure enclosure = FeedEnclosure.Provider.FetchFirstMatching (String.Format (
                    "{0}.ItemID = {1}", FeedEnclosure.Provider.TableName, DbId
                ));

                if (enclosure != null) {
                    enclosure.Item = this;
                    this.enclosure = enclosure;
                }
                enclosure_loaded = true;
            }
        }
    }
}
