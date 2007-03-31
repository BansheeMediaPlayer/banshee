/***************************************************************************
 *  PodcastDBManager.cs
 *
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
using System.Data;
using System.Text;
using System.Collections;

using Banshee.Base;
using Banshee.Database;

namespace Banshee.Plugins.Podcast
{
    internal static class PodcastDBManager
    {
        public static void InitPodcastDatabase ()
        {
            if(!Globals.Library.Db.TableExists("PodcastFeeds"))
            {

                Globals.Library.Db.Query (@"
                                          CREATE TABLE PodcastFeeds (
                                          PodcastFeedID INTEGER PRIMARY KEY,
                                          Title TEXT NOT NULL,
                                          FeedUrl TEXT NOT NULL,
                                          Link TEXT DEFAULT '',
                                          Description TEXT DEFAULT '',
                                          Image TEXT DEFAULT '',
                                          LastUpdated TIMESTAMP,
                                          Subscribed INTERGER DEFAULT 1,
                                          SyncPreference INTEGER DEFAULT 1
                                          )"
                                         );
            }

            if(!Globals.Library.Db.TableExists("Podcasts"))
            {
                // TODO Add downloaded timestamp
                Globals.Library.Db.Query (@"
                                          CREATE TABLE Podcasts (
                                          PodcastID INTEGER PRIMARY KEY,
                                          PodcastFeedID INTEGER NOT NULL,

                                          Title TEXT DEFAULT '',
                                          Link TEXT DEFAULT '',
                                          PubDate DATE NOT NULL,
                                          Description TEXT DEFAULT '',
                                          Author TEXT DEFAULT '',
                                          LocalPath TEXT DEFAULT '',
                                          Url TEXT NOT NULL,
                                          MimeType TEXT NOT NULL,
                                          Length INTEGER NOT NULL,
                                          Downloaded INTEGER DEFAULT 0,
                                          Active INTEGER DEFAULT 1
                                          )"
                                         );
            }
        }

        public static PodcastFeedInfo[] LoadPodcastFeeds ()
        {
            ArrayList podcastFeeds = new ArrayList ();

            IDataReader feed_reader = Globals.Library.Db.Query (@"
                                      SELECT * FROM PodcastFeeds ORDER BY Title
                                      ");

            while(feed_reader.Read())
            {
                PodcastFeedInfo feed = null;
                
                feed = new PodcastFeedInfo (
                    feed_reader.GetInt32 (0), feed_reader.GetString (1),
                    feed_reader.GetString (2), GetStringSafe (feed_reader, 3),
                    GetStringSafe (feed_reader, 4), GetStringSafe (feed_reader, 5),
                    GetDateTimeSafe (feed_reader, 6), feed_reader.GetBoolean (7),
                    (SyncPreference)feed_reader.GetInt32(8)
                );
                
                podcastFeeds.Add (feed);
                feed.Add (LoadPodcasts (feed));
            }

            feed_reader.Close ();

            return podcastFeeds.ToArray (typeof (PodcastFeedInfo)) as PodcastFeedInfo[];
        }

        private static PodcastInfo[] LoadPodcasts (PodcastFeedInfo feed)
        {
            ArrayList podcasts = new ArrayList ();

            IDataReader podcast_reader = Globals.Library.Db.Query (
                                             new DbCommand(
                                                 @"SELECT * FROM Podcasts
                                                 WHERE PodcastFeedID = :feed_id",
                                                 "feed_id", feed.ID
                                             )
                                         );

            while (podcast_reader.Read())
            {   
               podcasts.Add(
                   new PodcastInfo (
                       feed, podcast_reader.GetInt32 (0), GetStringSafe (podcast_reader, 2),
                       GetStringSafe (podcast_reader, 3), podcast_reader.GetDateTime (4),
                       GetStringSafe (podcast_reader, 5), GetStringSafe (podcast_reader, 6),
                       GetStringSafe (podcast_reader, 7), podcast_reader.GetString (8),
                       podcast_reader.GetString (9), podcast_reader.GetInt64 (10),
                       podcast_reader.GetBoolean (11), podcast_reader.GetBoolean (12)
                   )
               );
            }

            podcast_reader.Close ();

            return podcasts.ToArray (typeof (PodcastInfo)) as PodcastInfo[];
        }

        public static int LocalPodcastCount ()
        {
            string query =
                @"SELECT COUNT (*)
                FROM Podcasts
                WHERE Downloaded > 0 AND Active > 0
                LIMIT 1";

            try
            {
                return Convert.ToInt32(Globals.Library.Db.QuerySingle(query));
            }
            catch(Exception)
            {
                return -1;
            }
        }

        public static int Commit (PodcastFeedInfo feed)
        {
            int ret = 0;

            if (feed.ID != 0)
            {
                ret = Globals.Library.Db.Execute(new DbCommand(
                                                      @"UPDATE PodcastFeeds
                                                      SET Title=:title, FeedUrl=:feed_url, Link=:link,
                                                      Description=:description, Image=:image, LastUpdated=:last_updated,
                                                      Subscribed=:subscribed, SyncPreference=:sync_preference WHERE PodcastFeedID=:feed_id",
                                                      "title", feed.Title,
                                                      "feed_url", feed.Url.ToString(),
                                                      "link", feed.Link,
                                                      "description", feed.Description,
                                                      "image", feed.Image,
                                                      "last_updated", feed.LastUpdated.ToString(),
                                                      "subscribed", Convert.ToInt32(feed.IsSubscribed),
                                                      "sync_preference", (int)feed.SyncPreference,
                                                      "feed_id", feed.ID
                                                  ));
            }
            else
            {
                ret = Globals.Library.Db.Execute(new DbCommand(
                                                      @"INSERT INTO PodcastFeeds
                                                      VALUES (NULL, :title, :feed_url, :link,
                                                      :description, :image, :last_updated, :subscribed, :sync_preference)",
                                                      "title", feed.Title,
                                                      "feed_url", feed.Url.ToString(),
                                                      "link", feed.Link,
                                                      "description", feed.Description,
                                                      "image", feed.Image,
                                                      "last_updated", feed.LastUpdated.ToString(),
                                                      "subscribed", Convert.ToInt32(feed.IsSubscribed),
                                                      "sync_preference", (int)feed.SyncPreference
                                                  ));
            }

            return ret;
        }

        public static int Commit (PodcastInfo pi)
        {
            int ret = 0;

            if (pi.ID != 0)
            {

                ret = Globals.Library.Db.Execute(new DbCommand(
                                                     @"UPDATE Podcasts
                                                     SET PodcastFeedID=:feed_id, Title=:title, Link=:link, PubDate=:pubdate,
                                                     Description=:description, Author=:author, LocalPath=:local_path, Url=:url,
                                                     MimeType=:mimetype, Length=:length, Downloaded=:downloaded, Active=:active
                                                     WHERE PodcastID=:podcast_id",
                                                     "feed_id", pi.Feed.ID, 
                                                     "title", pi.Title,
                                                     "link", pi.Link, 
                                                     "pubdate", pi.PubDate.ToString (),
                                                     "description", pi.Description,
                                                     "author", pi.Author,
                                                     "local_path", pi.LocalPath,
                                                     "url", pi.Url.ToString (), 
                                                     "mimetype", pi.MimeType, 
                                                     "length", pi.Length,
                                                     "downloaded", Convert.ToInt32(pi.IsDownloaded),  
                                                     "active", Convert.ToInt32(pi.IsActive),
                                                     "podcast_id", pi.ID
                                                 ));
            }
            else
            {
                ret = Globals.Library.Db.Execute(new DbCommand(
                                                     @"INSERT INTO Podcasts
                                                     VALUES (NULL, :feed_id, :title, :link,
                                                     :pubdate, :description, :author, :local_path, :url,
                                                     :mimetype, :length, :downloaded, :active)",
                                                     "feed_id", pi.Feed.ID, 
                                                     "title", pi.Title,
                                                     "link", pi.Link, 
                                                     "pubdate", pi.PubDate.ToString (),
                                                     "description", pi.Description,
                                                     "author", pi.Author,
                                                     "local_path", pi.LocalPath,
                                                     "url", pi.Url.ToString (), 
                                                     "mimetype", pi.MimeType, 
                                                     "length", pi.Length,
                                                     "downloaded", Convert.ToInt32(pi.IsDownloaded),  
                                                     "active", Convert.ToInt32(pi.IsActive)
                                                 ));
            }

            return ret;
        }

        public static void Delete (PodcastFeedInfo pfi)
        {
            if (pfi == null)
            {
                throw new ArgumentNullException ("pfi");
            }

            Globals.Library.Db.Execute(new DbCommand(
                                           @"DELETE FROM PodcastFeeds
                                           WHERE PodcastFeedID = :id",
                                           "id", pfi.ID
                                       ));

            Globals.Library.Db.Execute(new DbCommand(
                                           @"DELETE FROM Podcasts
                                           WHERE PodcastFeedID = :id",
                                           "id", pfi.ID
                                       ));
        }

        private static readonly string base_podcast_remove_query =
            @"DELETE FROM Podcasts WHERE";

        public static void Delete (PodcastInfo pi)
        {
            if (pi == null)
            {
                throw new ArgumentNullException ("pi");
            }

            DbCommand query = new DbCommand(
                               String.Format(@"{0} PodcastID = :id",
                               base_podcast_remove_query),
                               "id", pi.ID
                           );

            Globals.Library.Db.Execute(query);
        }

        public static void Delete (ICollection podcasts)
        {
            QueryOnID (base_podcast_remove_query, podcasts);
        }

        private static readonly string base_podcast_deactivate_query =
            @"Update Podcasts SET Active=0 WHERE";

        public static void Deactivate (PodcastInfo pi)
        {
            if (pi == null)
            {
                throw new ArgumentNullException ("pi");
            }

            DbCommand query = new DbCommand(
                               String.Format(@"{0} PodcastID = :id",
                               base_podcast_deactivate_query),
                               "id", pi.ID
                           );

            Globals.Library.Db.Execute(query);
        }

        public static void Deactivate (ICollection podcasts)
        {
            QueryOnID (base_podcast_deactivate_query, podcasts);
        }

        private static void QueryOnID (string base_query, ICollection podcasts)
        {
            if (podcasts == null)
            {
                throw new ArgumentNullException ("podcasts");
            }

            if (podcasts.Count == 0)
            {
                return;
            }

            StringBuilder query_builder = new StringBuilder (base_query);

            bool first = true;

            foreach (PodcastInfo pi in podcasts)
            {
                if (first)
                {
                    query_builder.AppendFormat (@" PodcastID={0}", pi.ID);
                    first = false;
                }
                else
                {
                    query_builder.AppendFormat (@" OR PodcastID={0}", pi.ID);
                }
            }

            Globals.Library.Db.Execute(query_builder.ToString ());
        }
        
        private static string GetStringSafe (IDataReader reader, int index)
        {
            return reader.IsDBNull (index) ? 
                String.Empty : reader.GetString (index);
        }

		private static DateTime GetDateTimeSafe(IDataReader reader, int index)
		{
			try {
				return reader.GetDateTime(index);
			} catch {
				return DateTime.MinValue;
			}
		}
    }
}
