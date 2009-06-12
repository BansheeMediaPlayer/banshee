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
        private long dbid;
        private long miro_guide_id;
        
        private string description;
        private string stripped_description;

        private string license;        
        private string link;
        private DateTime modified;
        private string name;
        private string publisher;
        private string url;
        
        private static PaasChannelProvider provider;
        
        static PaasChannel () {
            provider = new PaasChannelProvider (ServiceManager.DbConnection);
        }

        public static SqliteModelProvider<PaasChannel> Provider {
            get { return provider; }
        }        
        
        [DatabaseColumn ("ID", Constraints = DatabaseColumnConstraints.PrimaryKey)]
        public long DbId {
            get { return dbid; }
            protected set { dbid = value; }
        }

        [DatabaseColumn (
            "MiroGuideID",
            Index = "PaasChannelMiroGuideIDIndex",
            Constraints = DatabaseColumnConstraints.Unique
        )]
        public long MiroGuideID {
            get { return miro_guide_id; }
            set { miro_guide_id = value; }
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
        public string License {
            get { return license; }
            set { license = value; }
        }

        [DatabaseColumn]
        public string Link {
            get { return link; }
            set { link = value; }
        }
        
        [DatabaseColumn]
        public string Name {
            get { return name; }
            set { name = value; }
        }

        [DatabaseColumn]
        public DateTime Modified {
            get { return modified; }
            set { modified = value; }
        }

        [DatabaseColumn]
        public string Publisher {
            get { return publisher; }
            set { publisher = value; }
        }

        [DatabaseColumn]
        public string StrippedDescription {
            get { return stripped_description; }
            set { stripped_description = value; }
        }

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

        public void Delete ()
        {
            Delete (true);
            //Manager.OnFeedsChanged ();                    
        }
            
        public void Delete (bool deleteEnclosures)
        {
            Hyena.Log.Information ("Handle Channel File Deletions!!!!!!!!!!!!!!!");
            Hyena.Log.Information ("Handle Item File Deletions!!!!!!!!!!!!!!!");            
        
            //lock (sync) {
                //if (deleted)
                //    return;
                
                //if (updating) {
                //    Manager.CancelUpdate (this);                 
                //}

                //foreach (FeedItem item in Items) {
                   // item.Delete (deleteEnclosures);
                //}

                Provider.Delete (this);
            //}
            
            //updatingHandle.WaitOne ();
            //Manager.OnFeedsChanged ();
        }

//        public void MarkAllItemsRead ()
//        {
//            lock (sync) {
//                foreach (FeedItem i in Items) {
//                    i.IsRead = true;
//                }
//            }
//        }

        public void Save ()
        {
            Save (true);
        }

        public void Save (bool notify)
        {
            Hyena.Log.Information ("PaasChannel.Save Still needs work!!!!!");         
                
            Provider.Save (this);
/*            
            if (LastBuildDate > LastAutoDownload) {
                CheckForItemsToDownload ();
            }

            if (notify) {
                Manager.OnFeedsChanged ();
            }
*/            
        }
    }
}