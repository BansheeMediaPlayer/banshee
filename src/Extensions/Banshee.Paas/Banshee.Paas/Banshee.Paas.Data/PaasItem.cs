// 
// PaasItem.cs
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
using System.Data;
using System.Collections.Generic;

using Hyena;
using Hyena.Data;
using Hyena.Data.Sqlite;

using Banshee.ServiceStack;
using Banshee.Collection.Database;

using Banshee.Paas.Utils;
using Banshee.Paas.DownloadManager.Data;

namespace Banshee.Paas.Data
{
    public class PaasItemProvider : SqliteModelProvider<PaasItem>
    {
        public PaasItemProvider (HyenaSqliteConnection connection) : base (connection, "PaasItems")
        {
        }

        public IEnumerable<PaasItem> FetchQueued (long primarySourceID)
        {
            string restore_command = String.Format (
                @"SELECT {0} FROM {1} 
                    JOIN {2} ON {1}.ID = {2}.ExternalID 
                  WHERE PrimarySourceID = ? AND {1}.LocalPath IS NULL
                    ORDER BY {2}.Position ASC", Select, From, 
                    QueuedDownloadTask.Provider.From
            );
            
            using (IDataReader reader = Connection.Query (restore_command, primarySourceID)) {
                while (reader.Read ()) {
                    yield return Load (reader);
                }
            } 
        }
    }

    public class PaasItem : ICacheableItem
    {
        private long dbid;

        private static PaasItemProvider provider;
        
        static PaasItem () {
            provider = new PaasItemProvider (ServiceManager.DbConnection);
        }

        public static PaasItemProvider Provider {
            get { return provider; }
        }        
        
        [DatabaseColumn ("ID", Constraints = DatabaseColumnConstraints.PrimaryKey)]
        public long DbId {
            get { return dbid; }
            protected set { dbid = value; }
        }

        private long client_id;
        [DatabaseColumn ("ClientID", Index = "PaasItemsClientIDIndex")]
        public long ClientID {
            get { return client_id; }
            set { client_id = value; }
        } 

        private long external_id;
        [DatabaseColumn ("ExternalID", Index = "PaasItemsExternalIDIndex")]
        public long ExternalID {
            get { return external_id; }
            set { external_id = value; }
        }        

        private long external_channel_id;
        [DatabaseColumn ("ExternalChannelID", Index = "PaasItemsExternalChannelIDIndex")]
        public long ExternalChannelID {
            get { return external_channel_id; }
            set { external_channel_id = value; }
        }  

        private long channel_id;
        [DatabaseColumn ("ChannelID", Index = "PaasItemChannelIDIndex")]
        public long ChannelID {
            get { return channel_id; }
            set { 
                channel = null;
                channel_id = value;
            }
        }

        private bool active = true;
        [DatabaseColumn (Index = "PaasItemActiveIndex")]
        public bool Active {
            get { return active; } 
            set { active = value; }
        }  

        private DateTime date;
        [DatabaseColumn (Index = "PaasItemPubDateIndex")]
        public DateTime PubDate {
            get { return date; } 
            set { date = value; }
        }  

        private string author;
        [DatabaseColumn]
        public string Author {
            get { return author; }
            set { author = value; }
        }  

        private string comments;
        [DatabaseColumn]
        public string Comments {
            get { return comments; }
            set { comments = value; }
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
        
        private DateTime downloaded_at;
        [DatabaseColumn (Index = "PaasItemDownloadedAtIndex")]
        public DateTime DownloadedAt {
            get { return downloaded_at; }
            internal set { downloaded_at = value; }
        }

        private TimeSpan duration;
        [DatabaseColumn]
        public TimeSpan Duration { 
            get { return duration; } 
            set { duration = value; }
        }

        private bool error;
        [DatabaseColumn]
        public bool Error {
            get { return error; }
            set { error = value; }
        }
        
        public bool IsDownloaded {
            get { 
                return /*(DownloadedAt != DateTime.MinValue) || */!String.IsNullOrEmpty (LocalPath); 
            }
        } 

        private bool is_new;
        [DatabaseColumn (Index = "PaasItemIsNewIndex")]
        public bool IsNew {
            get { return is_new; }
            set { is_new = value; }
        }            

        private string image;        
        [DatabaseColumn]
        public string ImageUrl {
            get { return image; }
            set { image = value; }
        }

        private string keywords;
        [DatabaseColumn]
        public string Keywords { 
            get { return keywords; } 
            set { keywords = value; }
        }
        
        private string license_uri;
        [DatabaseColumn]
        public string LicenseUri {
            get { return license_uri; }
            set { license_uri = value; }
        }   

        private string link;
        [DatabaseColumn]
        public string Link {
            get { return link; }
            set { link = value; }
        }   

        private string local_path;
        [DatabaseColumn (Index = "PaasItemLocalPathIndex")]
        public string LocalPath { 
            get { return local_path; }
            set { local_path = value; }
        }

        private string mime_type;
        [DatabaseColumn]
        public string MimeType {
            get { return mime_type; }
            set {
                mime_type = value;
                
                if (String.IsNullOrEmpty (mime_type)) {
                    mime_type = "application/octet-stream";
                }
            }
        }    
        
        private DateTime modified;
        [DatabaseColumn]
        public DateTime Modified {
            get { return modified; } 
            set { modified = value; }
        }
        
        private string name;
        [DatabaseColumn (Index = "PaasItemNameIndex")]
        public string Name {
            get { return name; } 
            set { name = value; }
        }

        private long size;
        [DatabaseColumn]
        public long Size {
            get { return size; } 
            set { size = value; }
        }

        private string stripped_description;
        public string StrippedDescription {
            get { return stripped_description; }
            protected set { 
                stripped_description = value;
            }
        }

        private string url;
        [DatabaseColumn]
        public string Url { 
            get { return url; } 
            set { url = value; }
        }
        
        private PaasChannel channel;
        public PaasChannel Channel {
            get { 
                if (channel == null && ChannelID > 0) {
                    channel = PaasChannel.Provider.FetchSingle (ChannelID);
                }
                
                return channel;
            }
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

        public void Save ()
        {
            Provider.Save (this);
        }
        
        public void Delete (bool delete_file)
        {
            Provider.Delete (this);
        }
    }

    public class PaasItemEqualityComparer : IEqualityComparer<PaasItem>
    {
        public bool Equals (PaasItem lhs, PaasItem rhs)
        {
            return (lhs.Url == rhs.Url || lhs.Name == rhs.Name) && lhs.PubDate == rhs.PubDate;
        }

        public int GetHashCode (PaasItem obj)
        {
            return base.GetHashCode ();
        }
    }
}