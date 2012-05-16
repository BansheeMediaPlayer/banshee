//
// RssParser.cs
//
// Authors:
//   Mike Urbanski <michael.c.urbanski@gmail.com>
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2007 Mike Urbanski
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
using System.Xml;
using System.Text;
using System.Collections.Generic;

using Hyena;

namespace Migo.Syndication
{
    public class RssParser
    {
        private XmlDocument doc;
        private XmlNamespaceManager mgr;
        private string url;

        public RssParser (string url, string xml)
        {
            this.url = url;
            xml = xml.TrimStart ();
            doc = new XmlDocument ();
            // Don't resolve any external references, see bgo#601554
            doc.XmlResolver = null;
            
            try {
                doc.LoadXml (xml);
            } catch (XmlException e) {
                bool have_stripped_control = false;
                StringBuilder sb = new StringBuilder ();

                foreach (char c in xml) {
                    if (Char.IsControl (c) && c != '\n') {
                        have_stripped_control = true;
                    } else {
                        sb.Append (c);
                    }
                }

                bool loaded = false;
                if (have_stripped_control) {
                    try {
                        doc.LoadXml (sb.ToString ());
                        loaded = true;
                    } catch (Exception) {
                    }
                }

                if (!loaded) {
                    Hyena.Log.Exception (e);
                    throw new FormatException ("Invalid XML document.");
                }
            }
            CheckRss ();
        }

        public RssParser (string url, XmlDocument doc)
        {
            this.url = url;
            this.doc = doc;
            CheckRss ();
        }

        public Feed CreateFeed ()
        {
            return UpdateFeed (new Feed ());
        }

        public Feed UpdateFeed (Feed feed)
        {
            try {
                if (feed.Title == null || feed.Title.Trim () == "" || feed.Title == Mono.Unix.Catalog.GetString ("Unknown Podcast")) {
                    feed.Title = StringUtil.RemoveNewlines (GetXmlNodeText (doc, "/rss/channel/title"));
                    if (String.IsNullOrEmpty (feed.Title)) {
                        feed.Title = Mono.Unix.Catalog.GetString ("Unknown Podcast");
                    }
                }

                feed.Description      = StringUtil.RemoveNewlines (GetXmlNodeText (doc, "/rss/channel/description"));
                feed.Copyright        = GetXmlNodeText (doc, "/rss/channel/copyright");
                feed.ImageUrl         = GetXmlNodeText (doc, "/rss/channel/itunes:image/@href");
                if (String.IsNullOrEmpty (feed.ImageUrl)) {
                    feed.ImageUrl = GetXmlNodeText (doc, "/rss/channel/image/url");
                }
                feed.Language         = GetXmlNodeText (doc, "/rss/channel/language");
                feed.LastBuildDate    = GetRfc822DateTime (doc, "/rss/channel/lastBuildDate");
                feed.Link             = GetXmlNodeText (doc, "/rss/channel/link");
                feed.PubDate          = GetRfc822DateTime (doc, "/rss/channel/pubDate");
                feed.Keywords         = GetXmlNodeText (doc, "/rss/channel/itunes:keywords");
                feed.Category         = GetXmlNodeText (doc, "/rss/channel/itunes:category/@text");

                return feed;
            } catch (Exception e) {
                 Hyena.Log.Exception ("Caught error parsing RSS channel", e);
            }

            return null;
        }

        public IEnumerable<FeedItem> GetFeedItems (Feed feed)
        {
            XmlNodeList nodes = null;
            try {
                nodes = doc.SelectNodes ("//item");
            } catch (Exception e) {
                Hyena.Log.Exception ("Unable to get any RSS items", e);
            }

            if (nodes != null) {
                foreach (XmlNode node in nodes) {
                    FeedItem item = null;

                    try {
                        item = ParseItem (node);
                        if (item != null) {
                            item.Feed = feed;
                        }
                    } catch (Exception e) {
                        Hyena.Log.Exception (e);
                    }

                    if (item != null) {
                        yield return item;
                    }
                }
            }
        }

        private FeedItem ParseItem (XmlNode node)
        {
            try {
                FeedItem item = new FeedItem ();
                item.Description = StringUtil.RemoveNewlines (GetXmlNodeText (node, "description"));
                item.UpdateStrippedDescription ();
                item.Title = StringUtil.RemoveNewlines (GetXmlNodeText (node, "title"));

                if (String.IsNullOrEmpty (item.Description) && String.IsNullOrEmpty (item.Title)) {
                    throw new FormatException ("node:  Either 'title' or 'description' node must exist.");
                }

                item.Author            = GetXmlNodeText (node, "author");
                item.Comments          = GetXmlNodeText (node, "comments");
                // Removed, since we form our own Guid, since feeds don't seem to be consistent
                // about including this element.
                //item.Guid              = GetXmlNodeText (node, "guid");
                item.Link              = GetXmlNodeText (node, "link");
                item.PubDate           = GetRfc822DateTime (node, "pubDate");
                item.Modified          = GetRfc822DateTime (node, "dcterms:modified");
                item.LicenseUri        = GetXmlNodeText (node, "creativeCommons:license");

                // TODO prefer <media:content> nodes over <enclosure>?
                item.Enclosure = ParseEnclosure (node) ?? ParseMediaContent (node);

                return item;
             } catch (Exception e) {
                 Hyena.Log.Exception ("Caught error parsing RSS item", e);
             }

             return null;
        }

        private FeedEnclosure ParseEnclosure (XmlNode node)
        {
            try {
                FeedEnclosure enclosure = new FeedEnclosure ();

                enclosure.Url = GetXmlNodeText (node, "enclosure/@url");
                if (String.IsNullOrEmpty (enclosure.Url)) {
                    return null;
                }

                enclosure.FileSize = Math.Max (0, GetInt64 (node, "enclosure/@length"));
                enclosure.MimeType = GetXmlNodeText (node, "enclosure/@type");
                enclosure.Duration = GetITunesDuration (node);
                enclosure.Keywords = GetXmlNodeText (node, "itunes:keywords");
                return enclosure;
             } catch (Exception e) {
                 Hyena.Log.Exception (String.Format ("Caught error parsing RSS enclosure in {0}", url), e);
             }

             return null;
        }

        // Parse one Media RSS media:content node
        // http://search.yahoo.com/mrss/
        private FeedEnclosure ParseMediaContent (XmlNode item_node)
        {
            try {
                XmlNode node = null;

                // Get the highest bitrate "full" content item
                // TODO allow a user-preference for a feed to decide what quality to get, if there
                // are options?
                int max_bitrate = 0;
                foreach (XmlNode test_node in item_node.SelectNodes ("media:content", mgr)) {
                    string expr = GetXmlNodeText (test_node, "@expression");
                    if (!(String.IsNullOrEmpty (expr) || expr == "full"))
                        continue;

                    int bitrate = GetInt32 (test_node, "@bitrate");
                    if (node == null || bitrate > max_bitrate) {
                        node = test_node;
                        max_bitrate = bitrate;
                    }
                }

                if (node == null)
                    return null;

                FeedEnclosure enclosure = new FeedEnclosure ();
                enclosure.Url = GetXmlNodeText (node, "@url");
                if (String.IsNullOrEmpty (enclosure.Url)) {
                    return null;
                }

                enclosure.FileSize = Math.Max (0, GetInt64 (node, "@fileSize"));
                enclosure.MimeType = GetXmlNodeText (node, "@type");
                enclosure.Duration = TimeSpan.FromSeconds (GetInt64 (node, "@duration"));
                enclosure.Keywords = GetXmlNodeText (item_node, "itunes:keywords");

                // TODO get the thumbnail URL

                return enclosure;
             } catch (Exception e) {
                 Hyena.Log.Exception ("Caught error parsing RSS media:content", e);
             }

             return null;
        }

        private void CheckRss ()
        {
            if (doc.SelectSingleNode ("/rss") == null) {
                throw new FormatException ("Invalid RSS document.");
            }

            if (doc.SelectSingleNode ("/rss/channel/title") == null) {
                throw new FormatException ("Invalid RSS document. Node 'title' is required");
            }

            mgr = new XmlNamespaceManager (doc.NameTable);
            mgr.AddNamespace ("itunes", "http://www.itunes.com/dtds/podcast-1.0.dtd");
            mgr.AddNamespace ("creativeCommons", "http://backend.userland.com/creativeCommonsRssModule");
            mgr.AddNamespace ("media", "http://search.yahoo.com/mrss/");
            mgr.AddNamespace ("dcterms", "http://purl.org/dc/terms/");
        }

        public TimeSpan GetITunesDuration (XmlNode node)
        {
            return GetITunesDuration (GetXmlNodeText (node, "itunes:duration"));
        }

        public static TimeSpan GetITunesDuration (string duration)
        {
            if (String.IsNullOrEmpty (duration)) {
                return TimeSpan.Zero;
            }

            try {
                int hours = 0, minutes = 0, seconds = 0;
                string [] parts = duration.Split (':');

                if (parts.Length > 0)
                    seconds = Int32.Parse (parts[parts.Length - 1]);

                if (parts.Length > 1)
                    minutes = Int32.Parse (parts[parts.Length - 2]);

                if (parts.Length > 2)
                    hours = Int32.Parse (parts[parts.Length - 3]);

                return TimeSpan.FromSeconds (hours * 3600 + minutes * 60 + seconds);
            } catch {
                return TimeSpan.Zero;
            }
        }

#region Xml Convienience Methods

        public string GetXmlNodeText (XmlNode node, string tag)
        {
            XmlNode n = node.SelectSingleNode (tag, mgr);
            return (n == null) ? null : n.InnerText.Trim ();
        }

        public DateTime GetRfc822DateTime (XmlNode node, string tag)
        {
            DateTime ret = DateTime.MinValue;
            string result = GetXmlNodeText (node, tag);

            if (!String.IsNullOrEmpty (result)) {
                if (Rfc822DateTime.TryParse (result, out ret)) {
                    return ret;
                }

                if (DateTime.TryParse (result, out ret)) {
                    return ret;
                }
            }

            return ret;
        }

        public long GetInt64 (XmlNode node, string tag)
        {
            long ret = 0;
            string result = GetXmlNodeText (node, tag);

            if (!String.IsNullOrEmpty (result)) {
                Int64.TryParse (result, out ret);
            }

            return ret;
        }

        public int GetInt32 (XmlNode node, string tag)
        {
            int ret = 0;
            string result = GetXmlNodeText (node, tag);

            if (!String.IsNullOrEmpty (result)) {
                Int32.TryParse (result, out ret);
            }

            return ret;
        }

#endregion
    }
}
