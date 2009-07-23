// 
// QueuedDownloadTask.cs
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
using Banshee.Collection.Database;

namespace Banshee.Paas.DownloadManager.Data
{
    public class QueuedDownloadTaskProvider : SqliteModelProvider<QueuedDownloadTask>
    {
        public QueuedDownloadTaskProvider (HyenaSqliteConnection connection) : base (connection, "QueuedDownloadTasks")
        {
        }
    }

    public class QueuedDownloadTask
    {
        private static QueuedDownloadTaskProvider provider;

        public static QueuedDownloadTaskProvider Provider {
            get { return provider; }
        } 
       
        static QueuedDownloadTask () {
            provider = new QueuedDownloadTaskProvider (ServiceManager.DbConnection);
        }

        private long dbid;
        [DatabaseColumn ("ID", Constraints = DatabaseColumnConstraints.PrimaryKey)]
        public long DbId {
            get { return dbid; }
            protected set { dbid = value; }
        }

        private long primary_source_id;
        [DatabaseColumn ("PrimarySourceID", Index = "QueuedDownloadTaskPrimarySourceIDIndex")]
        public long PrimarySourceID {
            get { return primary_source_id; }
            set { primary_source_id = value; }
        }  

        private long external_id;
        [DatabaseColumn ("ExternalID", Index = "QueuedDownloadTaskExternalIDIndex")]
        public long ExternalID {
            get { return external_id; }
            set { external_id = value; }
        }

        private long position;
        [DatabaseColumn ("Position", Index = "QueuedDownloadTaskPositionIndex")]
        public long Position {
            get { return position; }
            set { position = value; }
        }                
    }
}
