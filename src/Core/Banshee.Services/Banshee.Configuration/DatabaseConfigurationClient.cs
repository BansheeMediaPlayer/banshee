/***************************************************************************
 *  DatabaseConfigurationClient.cs
 *
 *  Written by Scott Peterson <lunchtimemama@gmail.com>
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Data;

using Hyena.Data.Sqlite;

using Banshee.Database;
using Banshee.ServiceStack;

namespace Banshee.Configuration
{
    public class DatabaseConfigurationClient : IConfigurationClient
    {
        public static DatabaseConfigurationClient Client {
            get { return ServiceManager.DbConnection.Configuration; }
        }
        
        private readonly BansheeDbConnection connection;
        private readonly HyenaSqliteCommand select_value_command;
        private readonly HyenaSqliteCommand select_id_command;
        private readonly HyenaSqliteCommand insert_command;
        private readonly HyenaSqliteCommand update_command;

        public DatabaseConfigurationClient(BansheeDbConnection connection)
        {
            this.connection = connection;
            
            select_value_command = new HyenaSqliteCommand (String.Format (
                "SELECT Value FROM {0} WHERE Key=?", TableName));
            
            select_id_command = new HyenaSqliteCommand (String.Format (
                "SELECT EntryID FROM {0} WHERE Key=?", TableName));
            
            insert_command = new HyenaSqliteCommand (String.Format (
                "INSERT INTO {0} (EntryID, Key, Value) VALUES (NULL, ?, ?)", TableName));
            
            update_command = new HyenaSqliteCommand (String.Format (
                "UPDATE {0} SET Value=? WHERE Key=?", TableName));
        }

        public T Get <T> (SchemaEntry<T> entry)
        {
            return Get (entry.Namespace, entry.Key, entry.DefaultValue);
        }

        public T Get <T> (SchemaEntry<T> entry, T fallback)
        {
            return Get (entry.Namespace, entry.Key, fallback);
        }

        public T Get <T> (string key, T fallback)
        {
            return Get (null, key, fallback);
        }

        public T Get <T> (string namespce, string key, T fallback)
        {
            using (IDataReader reader = Get (namespce, key)) {
                if (reader.Read ()) {
                    return (T) Convert.ChangeType (reader.GetString (0), typeof (T));
                } else {
                    return fallback;
                }
            }
        }
        
        private IDataReader Get (string namespce, string key)
        {
            return connection.Query (select_value_command, 
                Banshee.Configuration.MemoryConfigurationClient.MakeKey (namespce, key));
        }

        public void Set <T> (SchemaEntry<T> entry, T value)
        {
            Set (entry.Namespace, entry.Key, value);
        }

        public void Set <T> (string key, T value)
        {
            Set (null, key, value);
        }

        public void Set <T> (string namespce, string key, T value)
        {
            string fq_key = Banshee.Configuration.MemoryConfigurationClient.MakeKey (namespce, key);
            if (connection.Query<int> (select_id_command, fq_key) > 0) {
                connection.Execute (update_command, value.ToString (), fq_key);
            } else {
                connection.Execute (insert_command, fq_key, value.ToString ());
            }
        }

        protected virtual string TableName {
            get { return "CoreConfiguration"; }
        }
    }
}
