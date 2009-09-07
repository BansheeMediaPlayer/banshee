// 
// MiroGuideChannelInfo.cs
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

using System;
using System.Collections.Generic;

using Hyena.Json;

namespace Banshee.Paas.Aether.MiroGuide
{
    public class MiroGuideChannelInfo
    {
        private JsonObject channel_info;
        
        public MiroGuideChannelInfo (JsonObject channelInfo)
        {
            channel_info = channelInfo;
        }
        
        public long ID {
            get { return Int32.Parse (channel_info["id"].ToString ()); }
        }

        public IEnumerable<string> Categories {
            get {
                if (channel_info.ContainsKey ("category")) {
                    foreach (string j in channel_info["category"] as JsonArray) {
                        if (j != null) {
                            yield return j;
                        }
                    }                
                }
            }
        }

        public string Description {
            get { return channel_info["description"].ToString (); }
        }

        public bool IsHD {
            get { return Boolean.Parse (channel_info["hi_def"].ToString ()); }
        }

        public IEnumerable<string> Languages {
            get {
                if (channel_info.ContainsKey ("language")) {                
                    foreach (string j in channel_info["language"] as JsonArray) {
                        if (j != null) {
                            yield return j;
                        }
                    }
                }                    
            }
        }

        public string Name {
            get { return channel_info["name"].ToString (); }
        }

        public string Publisher {
            get { return channel_info["publisher"].ToString (); }
        }

        public IEnumerable<string> Tags {
            get {
                if (channel_info.ContainsKey ("tag")) {                            
                    foreach (string j in channel_info["tag"] as JsonArray) {
                        if (j != null) {
                            yield return j;
                        }
                    }
                }
            }        
        }

        public string ThumbUrl {
            get { return channel_info["thumbnail_url"].ToString (); }
        }

        public string Url {
            get { return channel_info["url"].ToString (); }
        }

        public string WebsiteUrl {
            get { return channel_info["website_url"].ToString (); }
        }
    }
}