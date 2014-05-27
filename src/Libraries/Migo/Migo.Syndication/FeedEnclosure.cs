//
// FeedEnclosure.cs
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
using System.IO;

using Hyena.Data.Sqlite;

namespace Migo.Syndication
{
    public class FeedEnclosure : CacheableItem<FeedEnclosure>
    {
        private static SqliteModelProvider<FeedEnclosure> provider;
        public static SqliteModelProvider<FeedEnclosure> Provider {
            get { return provider; }
        }

        public static void Init () {
            provider = new CacheableSqliteModelProvider<FeedEnclosure> (FeedsManager.Instance.Connection, "PodcastEnclosures");
        }

        private string mimetype;
        private FeedDownloadStatus download_status;
        private FeedDownloadError last_download_error;
        private long file_size;
        private TimeSpan duration;
        private string keywords;

        private string local_path;
        private FeedItem item;
        private string url;

        private readonly object sync = new object ();

#region Constructors

        public FeedEnclosure ()
        {
        }

#endregion

#region Public Properties

        public FeedItem Item {
            get { return item ?? (item = FeedItem.Provider.FetchSingle (item_id)); }
            internal set {
                item = value;
                if (item != null && item.DbId > 0) {
                    item_id = item.DbId;
                }
            }
        }

#endregion

        public static EnclosureManager Manager {
            get { return FeedsManager.Instance.EnclosureManager; }
        }

        public FeedDownloadError LastDownloadError {
            get { return last_download_error; }
            internal set { last_download_error = value; }
        }

#region Public Methods

        public void Save (bool save_item)
        {
            Provider.Save (this);

            if (save_item) {
                Item.Save ();
            }
        }

        public void Save ()
        {
            Save (true);
        }

        public void Delete (bool and_delete_file)
        {
            if (and_delete_file) {
                DeleteFile ();
            }
            Provider.Delete (this);
        }

        public void AsyncDownload ()
        {
            if (DownloadedAt == DateTime.MinValue && DownloadStatus == FeedDownloadStatus.None) {
                Manager.QueueDownload (this);
            }
        }

        public void CancelAsyncDownload ()
        {
            Manager.CancelDownload (this);
        }

        public void StopAsyncDownload ()
        {
            Manager.StopDownload (this);
        }

        // Deletes file associated with enclosure.
        // Does not cancel an active download like the WRP.
        public void DeleteFile ()
        {
            lock (sync) {
                if (!String.IsNullOrEmpty (local_path) && File.Exists (local_path)) {
                    try {
                        File.SetAttributes (local_path, FileAttributes.Normal);
                        File.Delete (local_path);
                        Directory.Delete (Path.GetDirectoryName (local_path));
                    } catch {}
                }

                LocalPath = null;
                DownloadStatus = FeedDownloadStatus.None;
                LastDownloadError = FeedDownloadError.None;
                Save ();
            }
        }


        public void SetFileImpl (string url, string path, string mimeType, string filename)
        {
            if (filename.EndsWith (".torrent", StringComparison.OrdinalIgnoreCase)) {
                filename = filename.Substring (0, filename.Length - 8);
            }
            string local_enclosure_path = Item.Feed.LocalEnclosurePath;

            string full_path = Path.Combine (path, filename);
            string new_local_path = Path.Combine (local_enclosure_path, filename);

            try {
                if (!Directory.Exists (path)) {
                    throw new InvalidOperationException ("Directory specified by path does not exist");
                } else if (!File.Exists (full_path)) {
                    throw new InvalidOperationException (String.Format ("File: {0}, does not exist", full_path));
                }

                if (!Directory.Exists (local_enclosure_path)) {
                    Directory.CreateDirectory (local_enclosure_path);
                }

                if (File.Exists (new_local_path)) {
                    int last_dot = new_local_path.LastIndexOf (".");

                    if (last_dot == -1) {
                        last_dot = new_local_path.Length-1;
                    }

                    string rep = String.Format ("-{0}",
                        Guid.NewGuid ().ToString ()
                                       .Replace ("-", String.Empty)
                                       .ToLower ()
                    );

                    new_local_path = new_local_path.Insert (last_dot, rep);
                }

                File.Move (full_path, new_local_path);
                File.SetAttributes (new_local_path, FileAttributes.ReadOnly);

                try {
                    Directory.Delete (path);
                } catch {}
            } catch {
                LastDownloadError = FeedDownloadError.DownloadFailed;
                DownloadStatus = FeedDownloadStatus.DownloadFailed;
                Save ();
                throw;
            }

            LocalPath = new_local_path;
            Url = url;
            MimeType = mimeType;

            DownloadStatus = FeedDownloadStatus.Downloaded;
            LastDownloadError = FeedDownloadError.None;
            Save ();
        }

#endregion

#region Database Columns

        private long dbid;
        [DatabaseColumn ("EnclosureID", Constraints = DatabaseColumnConstraints.PrimaryKey)]
        public override long DbId {
            get { return dbid; }
            protected set { dbid = value; }
        }

        [DatabaseColumn ("ItemID", Index = "PodcastEnclosuresItemIDIndex")]
        protected long item_id;
        public long ItemId {
            get { return Item.DbId; }
        }

        [DatabaseColumn]
        public string LocalPath {
            get { return local_path; }
            set { local_path = value; }
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
        public TimeSpan Duration {
            get { return duration; }
            set { duration = value; }
        }

        [DatabaseColumn]
        public long FileSize {
            get { return file_size; }
            set { file_size = value; }
        }

        [DatabaseColumn]
        public string MimeType {
            get { return mimetype; }
            set {
                mimetype = value;
                if (String.IsNullOrEmpty (mimetype)) {
                    mimetype = "application/octet-stream";
                }
            }
        }

        private DateTime downloaded_at;
        [DatabaseColumn]
        public DateTime DownloadedAt {
            get { return downloaded_at; }
            internal set { downloaded_at = value; }
        }

        [DatabaseColumn]
        public FeedDownloadStatus DownloadStatus {
            get {
                lock (sync) {
                    return download_status;
                }
            }

            internal set {
                lock (sync) {
                    download_status = value;
                }
            }
        }

#endregion

        public override string ToString ()
        {
            return String.Format ("FeedEnclosure<DbId: {0}, Url: {1}>", DbId, Url);
        }
    }
}
