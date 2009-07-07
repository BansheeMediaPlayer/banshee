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

using Migo2.Utils;

using Banshee.Paas.Data;

namespace Banshee.Paas.Aether
{
    public class RssParser
    {
        private XmlDocument doc;
        private XmlNamespaceManager mgr;
        
        public RssParser (string xml)
        {
            xml = xml.TrimStart ();
            doc = new XmlDocument ();
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
                    } catch {}
                }

                if (!loaded) {
                    Hyena.Log.Exception (e);
                    throw new FormatException ("Invalid XML document.");                                  
                }
            }
            CheckRss ();
        }
        
        public RssParser (XmlDocument doc)
        {
            this.doc = doc;
            CheckRss ();
        }

        public void UpdateChannel (PaasChannel channel)
        {
            try {
                channel.Name          = StringUtil.RemoveNewlines (XmlUtils.GetXmlNodeText (doc, "/rss/channel/title", mgr));
                channel.Description   = StringUtil.RemoveNewlines (XmlUtils.GetXmlNodeText (doc, "/rss/channel/description", mgr));
                channel.Copyright     = XmlUtils.GetXmlNodeText (doc, "/rss/channel/copyright", mgr);
                channel.ImageUrl      = XmlUtils.GetXmlNodeText (doc, "/rss/channel/itunes:image/@href", mgr);

                if (String.IsNullOrEmpty (channel.ImageUrl)) {
                    channel.ImageUrl = XmlUtils.GetXmlNodeText (doc, "/rss/channel/image/url", mgr);
                }
                
                channel.Language      = XmlUtils.GetXmlNodeText (doc, "/rss/channel/language", mgr);
                channel.LastBuildDate = XmlUtils.GetRfc822DateTime (doc, "/rss/channel/lastBuildDate");
                channel.Link          = XmlUtils.GetXmlNodeText (doc, "/rss/channel/link", mgr); 
                channel.PubDate       = XmlUtils.GetRfc822DateTime (doc, "/rss/channel/pubDate");
                channel.Keywords      = XmlUtils.GetXmlNodeText (doc, "/rss/channel/itunes:keywords", mgr);
                channel.Category      = XmlUtils.GetXmlNodeText (doc, "/rss/channel/itunes:category/@text", mgr);
                
                channel.LastDownloadTime = DateTime.Now;
            } catch (Exception e) {
                Hyena.Log.Exception ("Caught error parsing RSS feed", e);
                throw;                
            }
        }
        
        public IEnumerable<PaasItem> GetItems ()
        {
            XmlNodeList nodes = null;
            
            try {
                nodes = doc.SelectNodes ("//item");
            } catch (Exception e) {
                Hyena.Log.Exception ("Unable to get any RSS items", e);
            }
            
            if (nodes != null) {
                foreach (XmlNode node in nodes) {
                    PaasItem item = null;
                    
                    try {
                        item = ParseItem (node);
                    } catch (Exception e) {
                        Hyena.Log.Exception (e);
                    }
                    
                    if (item != null) {
                        yield return item;
                    }
                }
            }
        }
        
        private PaasItem ParseItem (XmlNode node)
        {
            try {
                PaasItem item = new PaasItem ();

                if (!TryParseMediaContent (node, item) && !TryParseEnclosure (node, item)) {
                    return null;
                }

                item.Description = StringUtil.RemoveNewlines (XmlUtils.GetXmlNodeText (node, "description", mgr));
                item.Name = StringUtil.RemoveNewlines (XmlUtils.GetXmlNodeText (node, "title", mgr));
            
                if (String.IsNullOrEmpty (item.Description) && String.IsNullOrEmpty (item.Name)) {
                    throw new FormatException ("node:  Either 'title' or 'description' node must exist.");
                }
                
                item.Author     = XmlUtils.GetXmlNodeText (node, "author", mgr);
                item.Comments   = XmlUtils.GetXmlNodeText (node, "comments", mgr);
                
                item.Link       = XmlUtils.GetXmlNodeText (node, "link", mgr);
                item.PubDate    = XmlUtils.GetRfc822DateTime (node, "pubDate");
                item.Modified   = XmlUtils.GetRfc822DateTime (node, "dcterms:modified");
                item.LicenseUri = XmlUtils.GetXmlNodeText (node, "creativeCommons:license", mgr);

                return item;
             } catch (Exception e) {
                 Hyena.Log.Exception ("Caught error parsing RSS item", e);
             }
             
             return null;
        }
        
        private bool TryParseEnclosure (XmlNode node, PaasItem item)
        {
            try {
                item.Url = XmlUtils.GetXmlNodeText (node, "enclosure/@url", mgr);

                Console.WriteLine ("Url:  {0}", item.Url);

                if (item.Url == null) {
                    return false;
                }
                
                item.Size       = Math.Max (0, XmlUtils.GetInt64 (node, "enclosure/@length"));
                item.MimeType   = XmlUtils.GetXmlNodeText (node, "enclosure/@type", mgr);
                item.Duration   = GetITunesDuration (node);
                item.Keywords   = XmlUtils.GetXmlNodeText (node, "itunes:keywords", mgr);
                
                return true;
             } catch (Exception e) {
                 Hyena.Log.Exception ("Caught error parsing RSS enclosure", e);
             }
             
             return false;
        }

        // Parse one Media RSS media:content node
        // http://search.yahoo.com/mrss/
        private bool TryParseMediaContent (XmlNode item_node, PaasItem item)
        {
            try {
                XmlNode node = null;
                
                // Get the highest bitrate "full" content item
                // TODO allow a user-preference for a feed to decide what quality to get, if there
                // are options?
                int max_bitrate = 0;
                foreach (XmlNode test_node in item_node.SelectNodes ("media:content", mgr)) {
                    string expr = XmlUtils.GetXmlNodeText (test_node, "@expression", mgr);
                    if (!(String.IsNullOrEmpty (expr) || expr == "full"))
                        continue;
                    
                    int bitrate = XmlUtils.GetInt32 (test_node, "@bitrate");
                    if (node == null || bitrate > max_bitrate) {
                        node = test_node;
                        max_bitrate = bitrate;
                    }
                }
                
                if (node == null)
                    return false;

                item.Url = XmlUtils.GetXmlNodeText (node, "@url", mgr);
                
                if (item.Url == null) {
                    return false;
                }
                
                item.Size       = Math.Max (0, XmlUtils.GetInt64 (node, "@fileSize"));
                item.MimeType   = XmlUtils.GetXmlNodeText (node, "@type", mgr);
                item.Duration   = TimeSpan.FromSeconds (XmlUtils.GetInt64 (node, "@duration", mgr));
                item.Keywords   = XmlUtils.GetXmlNodeText (node, "itunes:keywords", mgr);
                
                return true;
             } catch (Exception e) {
                 Hyena.Log.Exception ("Caught error parsing RSS media:content", e);
             }
             
             return false;
        }
  
        private void CheckRss ()
        {            
            if (doc.SelectSingleNode ("/rss") == null) {
                throw new FormatException ("Invalid RSS document.");
            }
            
            if (XmlUtils.GetXmlNodeText (doc, "/rss/channel/title", mgr) == String.Empty) {
                throw new FormatException (
                    "node: 'title', 'description', and 'link' nodes must exist."
                );                
            }
            
            mgr = new XmlNamespaceManager (doc.NameTable);
            mgr.AddNamespace ("itunes", "http://www.itunes.com/dtds/podcast-1.0.dtd");
            mgr.AddNamespace ("creativeCommons", "http://backend.userland.com/creativeCommonsRssModule");
            mgr.AddNamespace ("media", "http://search.yahoo.com/mrss/");
            mgr.AddNamespace ("dcterms", "http://purl.org/dc/terms/");
        }
        
        private TimeSpan GetITunesDuration (XmlNode node)
        {
            return GetITunesDuration (XmlUtils.GetXmlNodeText (node, "itunes:duration", mgr));
        }
        
        private TimeSpan GetITunesDuration (string duration)
        {
            if (String.IsNullOrEmpty (duration)) {
                return TimeSpan.Zero;
            }

            int hours = 0, minutes = 0, seconds = 0;
            string [] parts = duration.Split (':');
            
            if (parts.Length > 0)
                seconds = Int32.Parse (parts[parts.Length - 1]);
                
            if (parts.Length > 1)
                minutes = Int32.Parse (parts[parts.Length - 2]);
                
            if (parts.Length > 2)
                hours = Int32.Parse (parts[parts.Length - 3]);
            
            return TimeSpan.FromSeconds (hours * 3600 + minutes * 60 + seconds);
        }
    }
}
