// 
// MiroGuideClientInfo.cs
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

using Hyena;
using Hyena.Data;
using Hyena.Data.Sqlite;

using Banshee.ServiceStack;

namespace Banshee.Paas.Aether.MiroGuide
{
    public class MiroGuideClientInfoProvider : SqliteModelProvider<MiroGuideClientInfo>
    {
        public MiroGuideClientInfoProvider (HyenaSqliteConnection connection) : base (connection, "MiroGuideClientInfo")
        {
        }
    }

    public class MiroGuideClientInfo
    {
        private static MiroGuideClientInfoProvider provider;
        
        static MiroGuideClientInfo () 
        {
            provider = new MiroGuideClientInfoProvider (ServiceManager.DbConnection);
        }

        public static MiroGuideClientInfoProvider Provider {
            get { return provider; }
        }        
        
        private long dbid;
        [DatabaseColumn ("ID", Constraints = DatabaseColumnConstraints.PrimaryKey)]
        public long DbId {
            get { return dbid; }
            protected set { dbid = value; }
        }

        private string client_id;
        [DatabaseColumn ("ClientID", Index = "MiroGuideClientInfoClientIDIndex")]
        public string ClientID {
            get { return client_id; }
            set { client_id = value; }
        }
        
        private string last_updated = "1";
        [DatabaseColumn ("LastUpdated")]
        public string LastUpdated {
            get { return last_updated; }
            set { last_updated = value; }
        }  

        public void Save ()
        {
            Provider.Save (this);
        }
        
        public void Delete ()
        {
            Provider.Delete (this);
        }
    }
}