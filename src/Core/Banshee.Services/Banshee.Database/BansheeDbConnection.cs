//
// BansheeDbConnection.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2007-2008 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using Hyena;
using Hyena.Data;
using Hyena.Jobs;
using Hyena.Data.Sqlite;

using Banshee.Base;
using Banshee.ServiceStack;
using Banshee.Configuration;

namespace Banshee.Database
{
    public sealed class BansheeDbConnection : HyenaSqliteConnection, IInitializeService, IRequiredService
    {
        private BansheeDbFormatMigrator migrator;
        private DatabaseConfigurationClient configuration;
        private bool validate_schema = false;
        public DatabaseConfigurationClient Configuration {
            get { return configuration; }
        }

        private bool? likelihood_support = null;
        internal bool LikelihoodSupport {
            get {
                if (!likelihood_support.HasValue) {
                    likelihood_support = Query<bool> ("SELECT sqlite_version () >= '3.8.1'");
                }
                return likelihood_support.Value;
            }
        }

        public BansheeDbConnection () : this (DatabaseFile)
        {
            validate_schema = ApplicationContext.CommandLine.Contains ("validate-db-schema");
        }

        internal BansheeDbConnection (string db_path) : base (db_path)
        {
            // Each cache page is about 1.5K, so 32768 pages = 49152K = 48M
            int cache_size = (TableExists ("CoreTracks") && Query<long> ("SELECT COUNT(*) FROM CoreTracks") > 10000) ? 32768 : 16384;
            Execute ("PRAGMA cache_size = ?", cache_size);
            Execute ("PRAGMA synchronous = OFF");
            Execute ("PRAGMA temp_store = MEMORY");

            // TODO didn't want this on b/c smart playlists used to rely on it, but
            // now they shouldn't b/c we have smart custom functions we use for sorting/searching.
            // See BGO #603665 for discussion about turning this back on.
            //Execute ("PRAGMA case_sensitive_like=ON");

            Log.DebugFormat ("Opened SQLite (version {1}) connection to {0}", db_path, ServerVersion);

            migrator = new BansheeDbFormatMigrator (this);
            configuration = new DatabaseConfigurationClient (this);

            if (ApplicationContext.CommandLine.Contains ("debug-sql")) {
                Hyena.Data.Sqlite.HyenaSqliteCommand.LogAll = true;
                WarnIfCalledFromThread = ThreadAssist.MainThread;
            }
        }

        internal IEnumerable<string> SortedTableColumns (string table)
        {
            return GetSchema (table).Keys.OrderBy (n => n);
        }

        void IInitializeService.Initialize ()
        {
            lock (this) {
                migrator.Migrate ();
                migrator = null;

                try {
                    OptimizeDatabase ();
                } catch (Exception e) {
                    Log.Error ("Error determining if ANALYZE is necessary", e);
                }

                // Update cached sorting keys
                BeginTransaction ();
                try {
                    SortKeyUpdater.Update ();
                    CommitTransaction ();
                } catch {
                    RollbackTransaction ();
                }
            }

            if (Banshee.Metrics.BansheeMetrics.EnableCollection.Get ()) {
                Banshee.Metrics.BansheeMetrics.Start ();
            }

            if (validate_schema) {
                ValidateSchema ();
            }
        }

        private void OptimizeDatabase ()
        {
            bool needs_analyze = false;
            long analyze_threshold = configuration.Get<long> ("Database", "AnalyzeThreshold", 100);
            string [] tables_with_indexes = {"CoreTracks", "CoreArtists", "CoreAlbums",
                "CorePlaylistEntries", "PodcastItems", "PodcastEnclosures",
                "PodcastSyndications", "CoverArtDownloads"};

            if (TableExists ("sqlite_stat1")) {
                foreach (string table_name in tables_with_indexes) {
                    if (TableExists (table_name)) {
                        long count = Query<long> (String.Format ("SELECT COUNT(*) FROM {0}", table_name));
                        string stat = Query<string> ("SELECT stat FROM sqlite_stat1 WHERE tbl = ? LIMIT 1", table_name);
                        // stat contains space-separated integers,
                        // the first is the number of records in the table
                        long items_indexed = stat != null ? long.Parse (stat.Split (' ')[0]) : 0;

                        if (Math.Abs (count - items_indexed) > analyze_threshold) {
                            needs_analyze = true;
                            break;
                        }
                    }
                }
            } else {
                needs_analyze = true;
            }

            if (needs_analyze) {
                Log.DebugFormat ("Running ANALYZE against database to improve performance");
                Execute ("ANALYZE");
            }
        }

        public BansheeDbFormatMigrator Migrator {
            get { lock (this) { return migrator; } }
        }

        public bool ValidateSchema ()
        {
            bool is_valid = true;
            var new_db_path = Paths.GetTempFileName (Paths.TempDir);
            var new_db = new BansheeDbConnection (new_db_path);
            ((IInitializeService)new_db).Initialize ();

            Hyena.Log.DebugFormat ("Validating db schema for {0}", DbPath);

            var tables = new_db.QueryEnumerable<string> (
                "select name from sqlite_master where type='table' order by name"
            );

            foreach (var table in tables) {
                if (!TableExists (table)) {
                    Log.ErrorFormat ("Table {0} does not exist!", table);
                    is_valid = false;
                } else {
                    var a = new_db.SortedTableColumns (table);
                    var b = SortedTableColumns (table);

                    a.Except (b).ForEach (c => { is_valid = false; Hyena.Log.ErrorFormat ("Table {0} should contain column {1}", table, c); });
                    b.Except (a).ForEach (c => Hyena.Log.DebugFormat ("Table {0} has extra (probably obsolete) column {1}", table, c));
                }
            }

            using (var reader = new_db.Query (
                "select name,sql from sqlite_master where type='index' AND name NOT LIKE 'sqlite_autoindex%' order by name")) {
                while (reader.Read ()) {
                    string name = (string)reader[0];
                    string sql = (string)reader[1];
                    if (!IndexExists (name)) {
                        Log.ErrorFormat ("Index {0} does not exist!", name);
                        is_valid = false;
                    } else {
                        string our_sql = Query<string> ("select sql from sqlite_master where type='index' and name=?", name);
                        if (our_sql != sql) {
                            Log.ErrorFormat ("Index definition of {0} differs, should be `{1}` but is `{2}`", name, sql, our_sql);
                            is_valid = false;
                        }
                    }
                }
            }

            Hyena.Log.DebugFormat ("Done validating db schema for {0}", DbPath);
            System.IO.File.Delete (new_db_path);
            return is_valid;
        }

        public static string DatabaseFile {
            get {
                if (ApplicationContext.CommandLine.Contains ("db")) {
                    return ApplicationContext.CommandLine["db"];
                }

                string proper_dbfile = Path.Combine (Paths.ApplicationData, "banshee.db");
                if (File.Exists (proper_dbfile)) {
                    return proper_dbfile;
                }

                string dbfile = Path.Combine (Path.Combine (Environment.GetFolderPath (
                    Environment.SpecialFolder.ApplicationData),
                    "banshee"),
                    "banshee.db");

                if (!File.Exists (dbfile)) {
                    string tdbfile = Path.Combine (Path.Combine (Path.Combine (Environment.GetFolderPath (
                        Environment.SpecialFolder.Personal),
                        ".gnome2"),
                        "banshee"),
                        "banshee.db");

                    dbfile = tdbfile;
                }

                if (File.Exists (dbfile)) {
                    Log.InformationFormat ("Copying your old Banshee Database to {0}", proper_dbfile);
                    File.Copy (dbfile, proper_dbfile);
                }

                return proper_dbfile;
            }
        }

        string IService.ServiceName {
            get { return "DbConnection"; }
        }
    }
}
