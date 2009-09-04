//
// OpmlParser.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//   Brandan Lloyd
//
// Copyright (C) 2008 Novell, Inc.
// Copyright (C) 2008 Brandan Lloyd
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
using System.Collections.Generic;

namespace Banshee.Paas.Utils
{
    public class OpmlParser
    {
        private XmlDocument doc;
        private System.Collections.Generic.List<string> feeds = null;

        public OpmlParser (string xml, bool fromFile)
        {
            doc = new XmlDocument ();
            try {
                if (fromFile) {
                    doc.Load (xml);
                } else {
                    doc.LoadXml (xml);
                }
            } catch (XmlException) {
                throw new FormatException ("Invalid xml document.");
            }
            VerifyOpml ();
        }

        public IEnumerable<string> Feeds {
            get {
                if (null == feeds) {
                    feeds = new List<string> ();
                    GetFeeds (doc.SelectSingleNode ("/opml/body"));
                }
                return feeds;
            }
        }

        private void GetFeeds (XmlNode baseNode)
        {
            XmlNodeList nodes = baseNode.SelectNodes ("./outline");
            foreach (XmlNode node in nodes) {
                if (node.Attributes["xmlUrl"] != null) {
                    feeds.Add (node.Attributes["xmlUrl"].Value);
                } else if (node.Attributes["url"] != null) {
                    feeds.Add (node.Attributes["url"].Value);
                }

                GetFeeds (node);
            }
        }

        private void VerifyOpml ()
        {
            if (doc.SelectSingleNode ("/opml") == null)
                throw new FormatException ("Invalid OPML document.");

            if (doc.SelectSingleNode ("/opml/head") == null)
                throw new FormatException ("Invalid OPML document.");

            if (doc.SelectSingleNode ("/opml/body") == null)
                throw new FormatException ("Invalid OPML document.");
        }
    }
}
