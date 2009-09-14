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

// This makes no attempt to cache values.

using System;
using System.Linq;

using System.Xml;
using System.Collections.Generic;

using Migo2.Utils;

using Banshee.Paas.Data;

namespace Banshee.Paas.Aether.MiroGuide
{
    public class AetherDelta
    {
        private enum AetherAction  {
            Added,
            Removed,
            Modified
        }
        
        private XmlDocument doc;

        public string Updated {
            get { return XmlUtils.GetXmlNodeText (doc, "//@updated"); }
        }

        public IEnumerable<PaasChannel> NewChannels {
            get { return GetChannels (AetherAction.Added); }
        }
        
        public IEnumerable<long> RemovedChannels {
            get { 
                return GetChannels (AetherAction.Removed).Select (c => c.ExternalID);      
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
                return GetItems (AetherAction.Removed).Select (i => i.ExternalID);      
            }
        }

        public IEnumerable<PaasItem> ModifiedItems {
            get { return GetItems (AetherAction.Removed); }
        }        

        public IEnumerable<long> NewDownloads {
            get { return GetDownloads (AetherAction.Added); }
        }

        public IEnumerable<long> CancelledDownloads {
            get { return GetDownloads (AetherAction.Removed); }
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

        private IEnumerable<long> GetDownloads (AetherAction action)
        {
            XmlNodeList nodes = null;
            
            try {
                nodes = doc.SelectNodes (String.Format ("//downloads/download[@action='{0}']", ActionToString (action)));
            } catch {}
            
            if (nodes != null) {
                foreach (XmlNode node in nodes) {
                    yield return XmlUtils.GetInt64 (node, "id");
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
                channel.ClientID      = (long)AetherClientID.MiroGuide;
                channel.ExternalID    = XmlUtils.GetInt64 (node, "id");
                
                channel.Name          = XmlUtils.GetXmlNodeText (node, "name");
                channel.Description   = XmlUtils.GetXmlNodeText (node, "description");
                channel.LastBuildDate = XmlUtils.GetRfc822DateTime (node, "feed_modified");
                channel.License       = XmlUtils.GetXmlNodeText (node, "license");
                channel.Link          = XmlUtils.GetXmlNodeText (node, "website_url");
                channel.Publisher     = XmlUtils.GetXmlNodeText (node, "publisher");
                channel.Url           = XmlUtils.GetXmlNodeText (node, "url");
                channel.ImageUrl      = XmlUtils.GetXmlNodeText (node, "thumb_url");

                channel.LastDownloadTime = DateTime.Now;

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
                item.ClientID    = (int) AetherClientID.MiroGuide;                                    
                item.ExternalID  = XmlUtils.GetInt64 (node, "id");

                item.IsNew       = true;
                item.Name        = XmlUtils.GetXmlNodeText (node, "name");
                item.PubDate     = XmlUtils.GetRfc822DateTime (node, "date");                                
                item.Description = XmlUtils.GetXmlNodeText (node, "description");
                                                
                item.Url         = XmlUtils.GetXmlNodeText (node, "url");
                item.ImageUrl    = XmlUtils.GetXmlNodeText (node, "thumb_url");

                item.MimeType    = XmlUtils.GetXmlNodeText (node, "mime_type");
                item.Size        = XmlUtils.GetInt64 (node, "size");
                
                item.ExternalChannelID = XmlUtils.GetInt64 (node, "channel_id");
                return item;
             } catch (Exception e) {
                 Hyena.Log.Exception ("Caught error extracting item", e);
             }
             
             return null;        
        }
    }
}