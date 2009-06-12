// 
// AetherDelta.cs
//  
// Author:
//       Mike Urbanski <michael.c.urbanski@gmail.com>
// 
// Copyright (c) 2009 Michael C. Urbanski
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

// Use System.Xml.Linq at some point.  Need to learn it...

using System;
using System.Linq;

using System.Xml;
using System.Collections.Generic;

using Banshee.Paas.Data;

namespace Banshee.Paas.Aether
{
    public class AetherDelta
    {
        private enum AetherAction  {
            Added,
            Removed,
            Modified
        }
        
        private XmlDocument doc;

        public IEnumerable<PaasChannel> NewChannels {
            get { return GetChannels (AetherAction.Added); }
        }
        
        public IEnumerable<long> RemovedChannels {
            get { 
                return GetChannels (AetherAction.Removed).Select (c => c.MiroGuideID);      
            }
        }

        public IEnumerable<PaasChannel> ModifiedChannels {
            get { return GetChannels (AetherAction.Removed); }
        }        
   
        public IEnumerable<PaasItem> NewItems {
            get { return GetItems (AetherAction.Added); }
        }
        
        public IEnumerable<long> RemovedItems {
            get { 
                return GetItems (AetherAction.Removed).Select (i => i.MiroGuideID);      
            }
        }

        public IEnumerable<PaasItem> ModifiedItems {
            get { return GetItems (AetherAction.Removed); }
        }        
   
        public AetherDelta (string xml)
        { 
            xml = xml.Trim ();
            doc = new XmlDocument ();
            
            try {
                doc.LoadXml (xml);
            } catch (XmlException e) {
                Hyena.Log.Exception (e);
                throw new FormatException ("Invalid XML document.");                                  
            }
            
            CheckAether ();            
        }    
        
        public AetherDelta (XmlDocument doc)
        { 
            if (doc == null) {
                throw new ArgumentNullException ("doc");
            }
        
            this.doc = doc;
            CheckAether ();
        }

        private void CheckAether ()
        {            
            if (doc.SelectSingleNode ("/aether") == null) {
                throw new FormatException ("Invalid Aether document.");
            }
        }        

        private IEnumerable<PaasChannel> GetChannels (AetherAction action)
        {
            XmlNodeList nodes = null;
            
            try {
                nodes = doc.SelectNodes (String.Format ("//channels/channel[@action='{0}']", ActionToString (action)));
            } catch {}
            
            if (nodes != null) {
                PaasChannel channel = null;

                foreach (XmlNode node in nodes) {
                    channel = null;

                    try {
                        channel = ExtractChannel (node);
                    } catch (Exception e) {
                        Hyena.Log.Exception (e);
                    }
                    
                    if (channel != null) {
                        yield return channel;
                    }
                }
            }
        }

        private IEnumerable<PaasItem> GetItems (AetherAction action)
        {
            XmlNodeList nodes = null;
            
            try {
                nodes = doc.SelectNodes (String.Format ("//items/item[@action='{0}']", ActionToString (action)));
            } catch {}
            
            if (nodes != null) {
                PaasItem item = null;

                foreach (XmlNode node in nodes) {
                    item = null;

                    try {
                        item = ExtractItem (node);
                    } catch (Exception e) {
                        Hyena.Log.Exception (e);
                    }
                    
                    if (item != null) {
                        yield return item;
                    }
                }
            }            
        }
        
        private string ActionToString (AetherAction action)
        {
            switch (action)
            {
            case AetherAction.Added:
                return "added";
            case AetherAction.Removed:
                return "removed";
            case AetherAction.Modified:
                return "modified";
            default:
                throw new ArgumentOutOfRangeException ("action");
            }
        }

        private PaasChannel ExtractChannel (XmlNode node)
        {
            PaasChannel channel = new PaasChannel ();            
            
            try {
                channel.MiroGuideID = GetInt64 (node, "id");
                
                channel.Name        = GetXmlNodeText (node, "name");
                channel.Description = GetXmlNodeText (node, "description");
                channel.Modified    = GetDateTime    (node, "feed_modified");
                channel.License     = GetXmlNodeText (node, "license");
                channel.Link        = GetXmlNodeText (node, "website_url");
                channel.Publisher   = GetXmlNodeText (node, "publisher");
                channel.Url         = GetXmlNodeText (node, "url");

                return channel;
             } catch (Exception e) {
                 Hyena.Log.Exception ("Caught error extracting channel", e);
             }
             
             return null;        
        }
        
        private PaasItem ExtractItem (XmlNode node)
        {
            PaasItem item = new PaasItem ();            
            
            try {
                item.MiroGuideID = GetInt64 (node, "id");
                item.ChannelID   = GetInt64 (node, "channel_id");

                item.IsNew       = true;
                item.Name        = GetXmlNodeText (node, "name");
                item.Guid        = GetXmlNodeText (node, "guid");
                item.PubDate     = GetDateTime    (node, "date");                                
                item.Description = GetXmlNodeText (node, "description");
                                                
                item.Url         = GetXmlNodeText (node, "url");
                item.ImageUrl    = GetXmlNodeText (node, "thumb_url");

                item.MimeType    = GetXmlNodeText (node, "mime_type");
                item.Size        = GetInt64 (node, "size");
                
                return item;
             } catch (Exception e) {
                 Hyena.Log.Exception ("Caught error extracting item", e);
             }
             
             return null;        
        }
        
#region Xml Convienience Methods
    
        public string GetXmlNodeText (XmlNode node, string tag)
        {
            XmlNode n = node.SelectSingleNode (tag);
            return (n == null) ? null : n.InnerText.Trim ();
        }
        
        public DateTime GetDateTime (XmlNode node, string tag)
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
                    
            return ret.ToLocalTime ();              
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