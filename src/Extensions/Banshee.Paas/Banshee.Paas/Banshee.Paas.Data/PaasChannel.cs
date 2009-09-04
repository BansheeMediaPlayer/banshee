// 
// PaasChannel.cs
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

using Banshee.ServiceStack;

using Hyena;
using Hyena.Data;
using Hyena.Data.Sqlite;

using Banshee.Paas.Utils;

namespace Banshee.Paas.Data
{
    public class PaasChannelProvider : SqliteModelProvider<PaasChannel>
    {
        public PaasChannelProvider (HyenaSqliteConnection connection) : base (connection, "PaasChannels")
        {
        }
    }

    public class PaasChannel : ICacheableItem
    {
        private static PaasChannelProvider provider;
        
        static PaasChannel () {
            provider = new PaasChannelProvider (ServiceManager.DbConnection);
        }

        public static PaasChannelProvider Provider {
            get { return provider; }
        }
        
        private long dbid;        
        [DatabaseColumn ("ID", Constraints = DatabaseColumnConstraints.PrimaryKey)]
        public long DbId {
            get { return dbid; }
            protected set { dbid = value; }
        }

        private long client_id;
        [DatabaseColumn ("ClientID", Index = "PaasChannelClientIDIndex")]
        public long ClientID {
            get { return client_id; }
            set { client_id = value; }
        }

        private long external_id;
        [DatabaseColumn ("ExternalID", Index = "PaasChannelExternalIDIndex")]
        public long ExternalID {
            get { return external_id; }
            set { external_id = value; }
        }

        private string category;
        [DatabaseColumn]
        public string Category {
            get { return category; }
            set { category = value; }
        }     

        private string copyright;
        [DatabaseColumn]
        public string Copyright {
            get { return copyright; }
            set { copyright = value; }
        }        

        private string description;        
        [DatabaseColumn]
        public string Description {
            get { return description; }
            set { 
                description = value;
                StrippedDescription = StringUtils.StripHtml (value);
            }
        }

        private DownloadPreference download_preference;        
        [DatabaseColumn]
        public DownloadPreference DownloadPreference {
            get { return download_preference; }
            set { download_preference = value; }
        }

        private string image_url;
        [DatabaseColumn]
        public string ImageUrl {
            get { return image_url; }
            set { image_url = value; }
        }        

        private string language;
        [DatabaseColumn]
        public string Language {
            get { return language; }
            set { language = value; }
        }        

        private DateTime last_build_date;
        [DatabaseColumn]
        public DateTime LastBuildDate {
            get { return last_build_date; }
            set { last_build_date = value; }
        }        

        private DateTime last_download_time;
        [DatabaseColumn]
        public DateTime LastDownloadTime {
            get { return last_download_time; }
            set { last_download_time = value; }
        } 

        private string license;        
        [DatabaseColumn]
        public string License {
            get { return license; }
            set { license = value; }
        }

        private string link;
        [DatabaseColumn]
        public string Link {
            get { return link; }
            set { link = value; }
        }

        private string local_enclosure_path;
        [DatabaseColumn]        
        public string LocalEnclosurePath {
            get { return local_enclosure_path; }
            set { local_enclosure_path = value; }
        }
        
        private string name;
        [DatabaseColumn (Index = "PaasChannelNameIndex")]
        public string Name {
            get { 
                return String.IsNullOrEmpty (name) ? url : name; 
            }
            
            set { name = value; }
        }

        private DateTime pub_date;
        [DatabaseColumn]
        public DateTime PubDate {
            get { return pub_date; }
            set { pub_date = value; }
        }   

        private string publisher;
        [DatabaseColumn]
        public string Publisher {
            get { return publisher; }
            set { publisher = value; }
        }
        
        private string stripped_description;
        [DatabaseColumn]
        public string StrippedDescription {
            get { return stripped_description; }
            set { stripped_description = value; }
        }
        
        private string keywords;
        [DatabaseColumn]
        public string Keywords {
            get { return keywords; }
            set { keywords = value; }
        }
        
        private string url;
        [DatabaseColumn]
        public string Url {
            get { return url; }
            set { url = value; }
        }

        private object cache_entry_id;
        public object CacheEntryId {
            get { return cache_entry_id; }
            set { cache_entry_id = value; }
        }

        private long cache_model_id;
        public long CacheModelId {
            get { return cache_model_id; }
            set { cache_model_id = value; }
        }

        public IEnumerable<PaasItem> Items {
            get {
                foreach (PaasItem item in PaasItem.Provider.FetchAllMatching ("ChannelID = ?", DbId)) {
                    yield return item;
                }
            }
        }

        public void Save ()
        {                
            Provider.Save (this);
        }
    }
}