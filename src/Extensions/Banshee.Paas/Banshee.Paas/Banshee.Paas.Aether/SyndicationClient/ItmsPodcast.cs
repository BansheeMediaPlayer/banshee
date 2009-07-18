//
// ItmsPodcast.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//
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
using System.Text.RegularExpressions;

using Banshee.Web;

namespace Banshee.Paas.Aether
{
    public class ItmsPodcast
    {  
        private string itms_uri;
        private string feed_url;
        private string xml;

        public ItmsPodcast (string itmsUri)
        {
            Fetch (itmsUri, 2);
            feed_url = GetString ("feedURL");
        }

        public string FeedUrl {
            get { return feed_url; }
        }

        private void Fetch (string url, int tries)
        {
            url = url.Replace ("itms://", "http://");

            // Get rid of all the url variables except the id
            int args_start = url.IndexOf ("?");
            Regex id_regex = new Regex ("[?&]id=(\\d+)", RegexOptions.IgnoreCase);
            Match match = id_regex.Match (url, args_start);
            url = String.Format ("{0}?id={1}",
                url.Substring (0, args_start),
                match.Groups[1]
            );

            using (HttpRequest req = new HttpRequest (url)) {
                req.Request.KeepAlive = true;
                req.Request.Accept = "*/*";
                req.GetResponse ();

                if (req.Response.ContentType.StartsWith ("text/html")) {
                    if (tries > 0) {
                        string start = "onload=\"return itmsOpen('";
                        string rsp_body = req.ResponseBody;
                        int value_start = rsp_body.IndexOf (start) + start.Length;
                        int value_end   = rsp_body.IndexOf ("','", value_start);
                        string new_url  = rsp_body.Substring (value_start, value_end - value_start);
                        new_url = System.Web.HttpUtility.HtmlDecode (new_url);
                        Fetch (new_url, tries--);
                    }
                } else {
                    xml = req.ResponseBody;
                    itms_uri = url;
                }
            }
        }

        private string GetString (string key_name)
        {
            try {
                int entry_start = xml.IndexOf (String.Format ("<key>{0}</key>", key_name));
                int value_start = xml.IndexOf ("<string>", entry_start) + 8;
                int value_end   = xml.IndexOf ("</string>", value_start);
                return xml.Substring (value_start, value_end - value_start);
            } catch (Exception) {
                throw new Exception (String.Format (
                    "Unable to get value for {0} from {1}", key_name, itms_uri));
            }
        }
    }
}
