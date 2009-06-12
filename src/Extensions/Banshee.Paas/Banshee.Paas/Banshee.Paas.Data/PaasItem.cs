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
using System.Collections.Generic;

using Banshee.ServiceStack;

using Hyena;
using Hyena.Data;
using Hyena.Data.Sqlite;

using Banshee.Paas.Utils;

namespace Banshee.Paas.Data
{
    public class PaasItemProvider : SqliteModelProvider<PaasItem>
    {
        public PaasItemProvider (HyenaSqliteConnection connection) : base (connection, "PaasItems")
        {
        }
    }

    public class PaasItem : ICacheableItem
    {
        private long dbid;
        private long channel_id;
        private long miro_guide_id;        

        private string name;
        private DateTime date;

        private string description;
        private string stripped_description;

        private bool is_new;
        private string guid;
        private string url;
        private string image;        
        private string mime_type;
        private long size;

        private PaasChannel channel;

        private static SqliteModelProvider<PaasItem> provider;
        
        static PaasItem () {
            provider = new PaasItemProvider (ServiceManager.DbConnection);
        }

        public static SqliteModelProvider<PaasItem> Provider {
            get { return provider; }
        }        
        
        [DatabaseColumn ("ID", Constraints = DatabaseColumnConstraints.PrimaryKey)]
        public long DbId {
            get { return dbid; }
            protected set { dbid = value; }
        }

        [DatabaseColumn (
            "MiroGuideID",
            Index = "PaasItemsMiroGuideIDIndex",
            Constraints = DatabaseColumnConstraints.Unique
        )]
        public long MiroGuideID {
            get { return miro_guide_id; }
            set { miro_guide_id = value; }
        }        
        
        [DatabaseColumn ("ChannelID", Index = "PaasChannelIDIndex")]
        public long ChannelID {
            get { return channel_id; }
            set { channel_id = value; }            
        }        
        
        [DatabaseColumn]
        public string Name {
            get { return name; } 
            set { name = value; }
        }
        
        [DatabaseColumn]
        public DateTime PubDate {
            get { return date; } 
            set { date = value; }
        }  
        
        [DatabaseColumn]
        public string Description {
            get { return description; }
            set { 
                description = value;
                StrippedDescription = StringUtils.StripHtml (value);
            }
        }  

        [DatabaseColumn]
        public bool IsNew {
            get { return is_new; }
            set { is_new = value; }
        }         

        [DatabaseColumn]
        public string Guid {
            get { return guid; }
            set { guid = value; }
        } 

        [DatabaseColumn]
        public string Url {
            get { return url; }
            set { url = value; }
        }         
        
        [DatabaseColumn]
        public string ImageUrl {
            get { return image; }
            set { image = value; }
        }
                
        [DatabaseColumn]
        public string MimeType {
            get { return mime_type; }
            set { mime_type = value; }
        }          
        
        [DatabaseColumn]
        public long Size {
            get { return size; } 
            set { size = value; }
        }

        [DatabaseColumn]
        public string StrippedDescription {
            get { return stripped_description; }
            set { stripped_description = value; }
        }

        public PaasChannel Channel {
            get { 
                if (channel == null && ChannelID > 0) {
                    channel = PaasChannel.Provider.FetchFirstMatching ("MiroGuideID = ?", ChannelID);
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
            //bool is_new = DbId < 1;
            Provider.Save (this);
            Hyena.Log.Information ("Handle Item Updates!!!!!!!!!!!!!!!");
            
/*
            if (is_new) {
                Manager.OnItemAdded (this);
            } else {
                Manager.OnItemChanged (this);
            }
*/            
        }
        
        public void Delete (bool delete_file)
        {
            Hyena.Log.Information ("Handle File Deletions!!!!!!!!!!!!!!!");
            Provider.Delete (this);
            //Manager.OnItemRemoved (this);
        }
    }
}