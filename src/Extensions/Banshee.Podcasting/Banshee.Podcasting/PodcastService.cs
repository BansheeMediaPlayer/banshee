//
// PodcastService.cs
//
// Authors:
//   Mike Urbanski <michael.c.urbanski@gmail.com>
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2008 Michael C. Urbanski
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
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Mono.Unix;

using Hyena;

using Banshee.Base;
using Banshee.ServiceStack;

using Migo.TaskCore;
using Migo.Syndication;
using Migo.DownloadCore;

using Banshee.Networking;
using Banshee.MediaEngine;
using Banshee.Podcasting.Gui;
using Banshee.Podcasting.Data;
using Banshee.Collection.Database;
using Banshee.Configuration;
using Banshee.Preferences;

namespace Banshee.Podcasting
{
    public partial class PodcastService : IExtensionService, IDisposable, IDelayedInitializeService
    {
        private readonly string tmp_download_path = Paths.Combine (Paths.ExtensionCacheRoot, "podcasting", "partial-downloads");
        private uint refresh_timeout_id = 0;

        private bool disposed;

        private DownloadManager download_manager;
        private DownloadManagerInterface download_manager_iface;

        private FeedsManager feeds_manager;

        private PodcastSource source;
        //private PodcastImportManager import_manager;

        private readonly object sync = new object ();

        public PodcastService ()
        {
            Migo.Net.AsyncWebClient.DefaultUserAgent = Banshee.Web.Browser.UserAgent;
        }

        private void MigrateLegacyIfNeeded ()
        {
            if (DatabaseConfigurationClient.Client.Get<int> ("Podcast", "Version", 0) == 0) {
                if (ServiceManager.DbConnection.TableExists ("Podcasts") &&
                        ServiceManager.DbConnection.Query<int> ("select count(*) from podcastsyndications") == 0) {
                    Hyena.Log.Information ("Migrating Podcast Feeds and Items");
                    ServiceManager.DbConnection.Execute(@"
                        INSERT INTO PodcastSyndications (FeedID, Title, Url, Link,
                            Description, ImageUrl, LastBuildDate, AutoDownload, IsSubscribed)
                            SELECT
                                PodcastFeedID,
                                Title,
                                FeedUrl,
                                Link,
                                Description,
                                Image,
                                strftime(""%s"", LastUpdated),
                                SyncPreference,
                                1
                            FROM PodcastFeeds
                    ");

                    ServiceManager.DbConnection.Execute(@"
                        INSERT INTO PodcastItems (ItemID, FeedID, Title, Link, PubDate,
                            Description, Author, Active, Guid)
                            SELECT
                                PodcastID,
                                PodcastFeedID,
                                Title,
                                Link,
                                strftime(""%s"", PubDate),
                                Description,
                                Author,
                                Active,
                                Url
                            FROM Podcasts
                    ");

                    // Note: downloaded*3 is because the value was 0 or 1, but is now 0 or 3 (FeedDownloadStatus.None/Downloaded)
                    ServiceManager.DbConnection.Execute(@"
                        INSERT INTO PodcastEnclosures (ItemID, LocalPath, Url, MimeType, FileSize, DownloadStatus)
                            SELECT
                                PodcastID,
                                LocalPath,
                                Url,
                                MimeType,
                                Length,
                                Downloaded*3
                            FROM Podcasts
                    ");

                    // Finally, move podcast items from the Music Library to the Podcast source
                    int moved = 0;
                    foreach (FeedEnclosure enclosure in FeedEnclosure.Provider.FetchAllMatching ("LocalPath IS NOT NULL AND LocalPath != ''")) {
                        SafeUri uri = new SafeUri (enclosure.LocalPath);
                        long track_id = DatabaseTrackInfo.GetTrackIdForUri (uri, ServiceManager.SourceManager.MusicLibrary.DbId);

                        if (track_id > 0) {
                            PodcastTrackInfo pi = new PodcastTrackInfo (DatabaseTrackInfo.Provider.FetchSingle (track_id));
                            pi.Item = enclosure.Item;
                            pi.Track.PrimarySourceId = source.DbId;
                            pi.SyncWithFeedItem ();
                            pi.Track.Save (false);
                            moved++;
                        }
                    }

                    if (moved > 0) {
                        ServiceManager.SourceManager.MusicLibrary.Reload ();
                        source.Reload ();
                    }

                    Hyena.Log.Information ("Done Migrating Podcast Feeds and Items");
                }
                DatabaseConfigurationClient.Client.Set<int> ("Podcast", "Version", 1);
            }

            if (DatabaseConfigurationClient.Client.Get<int> ("Podcast", "Version", 0) < 3) {
                // We were using the Link as the fallback if the actual Guid was missing, but that was a poor choice
                // since it is not always unique.  We now use the title and pubdate combined.
                ServiceManager.DbConnection.Execute ("UPDATE PodcastItems SET Guid = NULL");
                foreach (FeedItem item in FeedItem.Provider.FetchAll ()) {
                    item.Guid = null;
                    if (item.Feed == null || FeedItem.Exists (item.Feed.DbId, item.Guid)) {
                        item.Delete (false);
                    } else {
                        item.Save ();
                    }
                }

                DatabaseConfigurationClient.Client.Set<int> ("Podcast", "Version", 3);
            }

            // Intentionally skpping 4 here because this needs to get run again for anybody who ran it
            // before it was fixed, but only once if you never ran it
            if (DatabaseConfigurationClient.Client.Get<int> ("Podcast", "Version", 0) < 5) {
                ReplaceNewlines ("CoreTracks", "Title");
                ReplaceNewlines ("CoreTracks", "TitleLowered");
                ReplaceNewlines ("PodcastItems", "Title");
                ReplaceNewlines ("PodcastItems", "Description");
                DatabaseConfigurationClient.Client.Set<int> ("Podcast", "Version", 5);
            }

            // Initialize the new StrippedDescription field
            if (DatabaseConfigurationClient.Client.Get<int> ("Podcast", "Version", 0) < 6) {
                foreach (FeedItem item in FeedItem.Provider.FetchAll ()) {
                    item.UpdateStrippedDescription ();
                    item.Save ();
                }
                DatabaseConfigurationClient.Client.Set<int> ("Podcast", "Version", 6);
            }
        }

        private void MigrateDownloadCache ()
        {
            string old_download_dir = Path.Combine (Paths.ApplicationData, "downloads");
            if (Directory.Exists (old_download_dir)) {
                foreach (string old_subdir in Directory.GetDirectories (old_download_dir)) {
                    string subdir_name = Path.GetFileName (old_subdir);
                    string new_subdir = Path.Combine (tmp_download_path, subdir_name);
                    Directory.Move (old_subdir, new_subdir);
                }
                Directory.Delete (old_download_dir);
            }
        }

        private void ReplaceNewlines (string table, string column)
        {
            string cmd = String.Format ("UPDATE {0} SET {1}=replace({1}, ?, ?)", table, column);
            ServiceManager.DbConnection.Execute (cmd, "\r\n", String.Empty);
            ServiceManager.DbConnection.Execute (cmd, "\n", String.Empty);
            ServiceManager.DbConnection.Execute (cmd, "\r", String.Empty);
        }

        public void Initialize ()
        {
        }

        public void DelayedInitialize ()
        {
            download_manager = new DownloadManager (2, tmp_download_path);
            download_manager_iface = new DownloadManagerInterface (download_manager);
            download_manager_iface.Initialize ();

            feeds_manager = new FeedsManager (ServiceManager.DbConnection, download_manager, null);

            // Migrate data from 0.13.2 podcast tables, if they exist
            MigrateLegacyIfNeeded ();

            // Move incomplete downloads to the new cache location
            try {
                MigrateDownloadCache ();
            } catch (Exception e) {
                Log.Error ("Couldn't migrate podcast download cache", e);
            }

            source = new PodcastSource ();
            ServiceManager.SourceManager.AddSource (source);

            InitializeInterface ();

            var preference = source.PreferencesPage["library-location"]["library-location"] as SchemaPreference<string>;
            preference.ValueChanged += delegate (Root obj) {
                feeds_manager.PodcastStorageDirectory = preference.Value;
            };

            ThreadAssist.SpawnFromMain (delegate {
                feeds_manager.PodcastStorageDirectory = source.BaseDirectory;
                feeds_manager.FeedManager.ItemAdded += OnItemAdded;
                feeds_manager.FeedManager.ItemChanged += OnItemChanged;
                feeds_manager.FeedManager.ItemRemoved += OnItemRemoved;
                feeds_manager.FeedManager.FeedsChanged += OnFeedsChanged;

                if (DatabaseConfigurationClient.Client.Get<int> ("Podcast", "Version", 0) < 7) {
                    Banshee.Library.LibrarySource music_lib = ServiceManager.SourceManager.MusicLibrary;
                    if (music_lib != null) {
                        string old_path = Path.Combine (music_lib.BaseDirectory, "Podcasts");
                        string new_path = source.BaseDirectory;
                        SafeUri old_uri = new SafeUri (old_path);
                        SafeUri new_uri = new SafeUri (new_path);
                        if (old_path != null && new_path != null && old_path != new_path &&
                            Banshee.IO.Directory.Exists (old_path) && !Banshee.IO.Directory.Exists (new_path)) {
                            Banshee.IO.Directory.Move (new SafeUri (old_path), new SafeUri (new_path));
                            ServiceManager.DbConnection.Execute (String.Format (
                                "UPDATE {0} SET LocalPath = REPLACE(LocalPath, ?, ?) WHERE LocalPath IS NOT NULL",
                                FeedEnclosure.Provider.TableName), old_path, new_path);
                            ServiceManager.DbConnection.Execute (String.Format (
                                    "UPDATE CoreTracks SET Uri = REPLACE ({0}, ?, ?)" +
                                    "WHERE {0} LIKE 'file://%' AND PrimarySourceId = ?",
                                    Banshee.Query.BansheeQuery.UriField.Column),
                                old_uri.AbsoluteUri, new_uri.AbsoluteUri, source.DbId);
                            Hyena.Log.DebugFormat ("Moved Podcasts from {0} to {1}", old_path, new_path);
                        }
                    }
                    DatabaseConfigurationClient.Client.Set<int> ("Podcast", "Version", 7);
                }

                ServiceManager.PlayerEngine.ConnectEvent (OnPlayerEvent, PlayerEvent.StateChange);
                ServiceManager.Get<DBusCommandService> ().ArgumentPushed += OnCommandLineArgument;

                RefreshFeeds ();

                // Every 10 minutes try to refresh again
                refresh_timeout_id = Application.RunTimeout (1000 * 60 * 10, RefreshFeeds);

                ServiceManager.Get<Network> ().StateChanged += OnNetworkStateChanged;
            });

            source.UpdateFeedMessages ();
        }

        private void OnNetworkStateChanged (object o, NetworkStateChangedArgs args)
        {
            RefreshFeeds ();
        }

        bool disposing;
        public void Dispose ()
        {
            lock (sync) {
                if (disposing | disposed) {
                    return;
                } else {
                    disposing = true;
                }
            }

            Application.IdleTimeoutRemove (refresh_timeout_id);
            refresh_timeout_id = 0;

            ServiceManager.PlayerEngine.DisconnectEvent (OnPlayerEvent);
            ServiceManager.Get<DBusCommandService> ().ArgumentPushed -= OnCommandLineArgument;
            ServiceManager.Get<Network> ().StateChanged -= OnNetworkStateChanged;

            if (download_manager_iface != null) {
                download_manager_iface.Dispose ();
                download_manager_iface = null;
            }

            if (feeds_manager != null) {
                feeds_manager.Dispose ();
                feeds_manager = null;
            }

            if (download_manager != null) {
                download_manager.Dispose ();
                download_manager = null;
            }

            DisposeInterface ();

            lock (sync) {
                disposing = false;
                disposed = true;
            }
        }

        private bool RefreshFeeds ()
        {
            if (!ServiceManager.Get<Network> ().Connected)
                return true;

            Hyena.Log.Debug ("Refreshing any podcasts that haven't been updated in over an hour");
            Banshee.Kernel.Scheduler.Schedule (new Banshee.Kernel.DelegateJob (delegate {
                DateTime now = DateTime.Now;
                foreach (Feed feed in Feed.Provider.FetchAll ()) {
                    if (feed.IsSubscribed && (now - feed.LastDownloadTime).TotalHours > 1) {
                        feed.Update ();
                        RefreshArtworkFor (feed);
                    }
                }
            }));
            return true;
        }

        private void OnCommandLineArgument (string argument, object value, bool isFile)
        {
            if (isFile) {
                ProcessFile (argument, null);
            } else if (argument == "podcast") {
                var podcast = Hyena.Json.JsonObject.FromString (value as string);
                if (podcast != null) {
                    ProcessFile ((string)podcast["uri"], (string)podcast["name"]);
                }
            }
        }

        private void ProcessFile (string uri, string title)
        {
            if (String.IsNullOrEmpty (uri)) {
                return;
            }

            if (uri.Contains ("opml") || uri.EndsWith (".miro") || uri.EndsWith (".democracy")) {
                // Handle OPML files
                try {
                    OpmlParser opml_parser = new OpmlParser (uri, true);
                    foreach (string feed in opml_parser.Feeds) {
                        ProcessFile (feed, title);
                    }
                } catch (Exception e) {
                    Log.Error (e);
                }
            } else if (uri.Contains ("xml") || uri.Contains ("rss") || uri.Contains ("feed") || uri.StartsWith ("itpc") || uri.StartsWith ("pcast")) {
                // Handle normal XML/RSS URLs
                if (uri.StartsWith ("feed://") || uri.StartsWith ("itpc://")) {
                    uri = String.Format ("http://{0}", uri.Substring (7));
                } else if (uri.StartsWith ("pcast://")) {
                    uri = String.Format ("http://{0}", uri.Substring (8));
                }

                AddFeed (uri, title);
            } else if (uri.StartsWith ("itms://")) {
                // Handle iTunes podcast URLs
                System.Threading.ThreadPool.QueueUserWorkItem (delegate {
                    try {
                        var feed = new ItmsPodcast (uri);
                        if (feed.FeedUrl != null) {
                            ThreadAssist.ProxyToMain (delegate {
                                AddFeed (feed.FeedUrl, feed.Title ?? title);
                            });
                        }
                    } catch (Exception e) {
                        Log.Error (e);
                    }
                });
            }
        }

        private void AddFeed (string uri, string title)
        {
            // TODO replace autodownload w/ actual default preference
            FeedsManager.Instance.FeedManager.CreateFeed (uri, title, FeedAutoDownload.None, 0);
            source.NotifyUser ();
            source.UpdateFeedMessages ();
        }

        private void RefreshArtworkFor (Feed feed)
        {
            if (!String.IsNullOrEmpty (feed.ImageUrl) && !CoverArtSpec.CoverExists (PodcastService.ArtworkIdFor (feed))) {
                Banshee.Kernel.Scheduler.Schedule (new PodcastImageFetchJob (feed), Banshee.Kernel.JobPriority.BelowNormal);
            }
        }

        private DatabaseTrackInfo GetTrackByItemId (long item_id)
        {
            return DatabaseTrackInfo.Provider.FetchFirstMatching ("PrimarySourceID = ? AND ExternalID = ?", source.DbId, item_id);
        }

        private void OnItemAdded (FeedItem item)
        {
            if (item.Enclosure != null) {
                DatabaseTrackInfo track = new DatabaseTrackInfo ();
                track.ExternalId = item.DbId;
                track.PrimarySource = source;
                (track.ExternalObject as PodcastTrackInfo).SyncWithFeedItem ();
                track.Save (false);
                RefreshArtworkFor (item.Feed);
            } else {
                // We're only interested in items that have enclosures
                item.Delete (false);
            }
        }

        private void OnItemRemoved (FeedItem item)
        {
            DatabaseTrackInfo track = GetTrackByItemId (item.DbId);
            if (track != null) {
                DatabaseTrackInfo.Provider.Delete (track);
            }
        }

        internal static bool IgnoreItemChanges = false;

        private void OnItemChanged (FeedItem item)
        {
            if (IgnoreItemChanges) {
                return;
            }

            DatabaseTrackInfo track = GetTrackByItemId (item.DbId);
            if (track != null) {
                PodcastTrackInfo pi = track.ExternalObject as PodcastTrackInfo;
                if (pi != null) {
                    pi.SyncWithFeedItem ();
                    track.Save (true);
                }
            }
        }

        private void OnFeedsChanged (object o, EventArgs args)
        {
            source.Reload ();
            source.NotifyTracksChanged ();
            source.UpdateFeedMessages ();
        }

        /*private void OnFeedAddedHandler (object sender, FeedEventArgs args)
        {
            lock (sync) {
                source.FeedModel.Add (args.Feed);
            }
        }

        private void OnFeedRemovedHandler (object sender, FeedEventArgs args)
        {
            lock (sync) {
                source.FeedModel.Remove (args.Feed);
                args.Feed.Delete ();
            }
        }

        private void OnFeedRenamedHandler (object sender, FeedEventArgs args)
        {
            lock (sync) {
                source.FeedModel.Sort ();
            }
        }

        private void OnFeedUpdatingHandler (object sender, FeedEventArgs args)
        {
            lock (sync) {
                source.FeedModel.Reload ();
            }
        }

        private void OnFeedDownloadCountChangedHandler (object sender, FeedDownloadCountChangedEventArgs args)
        {
            lock (sync) {
                source.FeedModel.Reload ();
            }
        }*/

        /*private void OnFeedItemAddedHandler (object sender, FeedItemEventArgs args)
        {
            lock (sync) {
                if (args.Item != null) {
                    AddFeedItem (args.Item);
                } else if (args.Items != null) {
                    foreach (FeedItem item in args.Items) {
                        AddFeedItem (item);
                    }
                }
            }
        }*/

        public void AddFeedItem (FeedItem item)
        {
            if (item.Enclosure != null) {
                PodcastTrackInfo pi = new PodcastTrackInfo (new DatabaseTrackInfo (), item);
                pi.Track.PrimarySource = source;
                pi.Track.Save (true);
                source.NotifyUser ();
            } else {
                item.Delete (false);
            }
        }

        /*private void OnFeedItemRemovedHandler (object sender, FeedItemEventArgs e)
        {
            lock (sync) {
                if (e.Item != null) {
                    PodcastItem.DeleteWithFeedId (e.Item.DbId);
                } else if (e.Items != null) {
                    foreach (FeedItem fi in e.Items) {
                        PodcastItem.DeleteWithFeedId (fi.DbId);
                    }
                }

                source.Reload ();
            }
        }

        private void OnFeedItemCountChanged (object sender,
                                             FeedItemCountChangedEventArgs e)
        {
            //UpdateCount ();
        }*/

        /*private void OnFeedDownloadCompletedHandler (object sender,
                                                     FeedDownloadCompletedEventArgs e)
        {
            lock (sync) {
                Feed f = feedDict[e.Feed.DbId];

                if (e.Error == FeedDownloadError.None) {
                    if (String.IsNullOrEmpty(e.Feed.LocalEnclosurePath)) {
                        e.Feed.LocalEnclosurePath = Path.Combine (
                            tmp_enclosure_path, SanitizeName (e.Feed.Name)
                        );
                    }

                    if (f.AutoDownload != FeedAutoDownload.None) {
                        ReadOnlyCollection<FeedItem> items = e.Feed.Items;

                        if (items != null) {
                            if (f.AutoDownload == FeedAutoDownload.One &&
                                items.Count > 0) {
                                items[0].Enclosure.AsyncDownload ();
                            } else {
                                foreach (FeedItem fi in items) {
                                    fi.Enclosure.AsyncDownload ();
                                }
                            }
                        }
                    }
                }

                source.Reload ();
            }
        }*/

        /*private void OnTaskAssociated (object sender, EventArgs e)
        {
            lock (sync) {
                source.Reload ();
            }
        }

        private void OnTaskStatusChanged (object sender,
        								  TaskStatusChangedEventArgs e)
        {
            lock (sync) {
                source.Reload ();
            }
        }

        private void TaskStartedHandler (object sender,
                                         TaskEventArgs<HttpFileDownloadTask> e)
        {
            lock (sync) {
                source.Reload ();
            }
        }

        private void OnTaskStoppedHandler (object sender,
                                           TaskEventArgs<HttpFileDownloadTask> e)
        {
            // TODO merge
            lock (sync) {
                if (e.Task != null && e.Task.Status == TaskStatus.Succeeded) {
                    FeedEnclosure enc = e.Task.UserState as FeedEnclosure;

                    if (enc != null) {
                        FeedItem item = enc.Item;
                        DatabaseTrackInfo track = null;



                        if (itemDict.ContainsKey (item.DbId)) {
                            PodcastItem pi = itemDict[item.DbId];
                            track = import_manager.ImportPodcast (enc.LocalPath);

                            if (track != null) {
                                pi.Track = track;
                                pi.New = true;
                                pi.Save ();
                            }

                            item.IsRead = true;
                        }
                    }
                }

                source.Reload ();
            }
        }*/

        private void OnPlayerEvent (PlayerEventArgs args)
        {
            lock (sync) {
                //source.Reload ();
            }
        }

        public static string ArtworkIdFor (Feed feed)
        {
            string digest = Banshee.Base.CoverArtSpec.Digest (feed.Title);
            return digest == null ? null : String.Format ("podcast-{0}", digest);
        }

        // Via Monopod
        /*private static string SanitizeName (string s)
        {
            // remove /, : and \ from names
            return s.Replace ('/', '_').Replace ('\\', '_').Replace (':', '_').Replace (' ', '_');
        }*/

        public string ServiceName {
            get { return "PodcastService"; }
        }
    }
}
