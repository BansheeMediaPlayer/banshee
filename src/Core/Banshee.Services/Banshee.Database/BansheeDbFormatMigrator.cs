//
// BansheeDbFormatMigrator.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2007-2009 Novell, Inc.
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
using System.Reflection;
using System.Text;
using System.Threading;
using Mono.Unix;

using Hyena;
using Hyena.Jobs;
using Hyena.Data.Sqlite;
using Timer=Hyena.Timer;

using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Streaming;

// MIGRATION NOTE: Return true if the step should allow the driver to continue
//                 Return false if the step should terminate driver

namespace Banshee.Database
{
    public class BansheeDbFormatMigrator
    {
        // NOTE: Whenever there is a change in ANY of the database schema,
        //       this version MUST be incremented and a migration method
        //       MUST be supplied to match the new version number
        protected const int CURRENT_VERSION = 45;
        protected const int CURRENT_METADATA_VERSION = 8;

#region Migration Driver

        public delegate void SlowStartedHandler(string title, string message);

        public event SlowStartedHandler SlowStarted;
        public event EventHandler SlowPulse;
        public event EventHandler SlowFinished;

        public event EventHandler Started;
        public event EventHandler Finished;

        protected sealed class DatabaseVersionAttribute : Attribute
        {
            private int version;

            public DatabaseVersionAttribute(int version)
            {
                this.version = version;
            }

            public int Version {
                get { return version; }
            }
        }

        private HyenaSqliteConnection connection;

        public BansheeDbFormatMigrator (HyenaSqliteConnection connection)
        {
            this.connection = connection;
        }

        protected virtual void OnSlowStarted(string title, string message)
        {
            SlowStartedHandler handler = SlowStarted;
            if(handler != null) {
                handler(title, message);
            }
        }

        protected virtual void OnSlowPulse()
        {
            EventHandler handler = SlowPulse;
            if(handler != null) {
                handler(this, EventArgs.Empty);
            }
        }

        protected virtual void OnSlowFinished()
        {
            EventHandler handler = SlowFinished;
            if(handler != null) {
                handler(this, EventArgs.Empty);
            }
        }

        protected virtual void OnStarted ()
        {
            EventHandler handler = Started;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }

        protected virtual void OnFinished ()
        {
            EventHandler handler = Finished;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }

        public void Migrate ()
        {
            try {

                if (DatabaseVersion > CURRENT_VERSION) {
                    throw new DatabaseVersionTooHigh (CURRENT_VERSION, DatabaseVersion);
                } else if (DatabaseVersion < CURRENT_VERSION) {
                    Execute ("BEGIN");
                    InnerMigrate ();
                    Execute ("COMMIT");
                } else {
                    Log.DebugFormat ("Database version {0} is up to date", DatabaseVersion);
                }

                // Trigger metadata refreshes if necessary
                int metadata_version = connection.Query<int> ("SELECT Value FROM CoreConfiguration WHERE Key = 'MetadataVersion'");
                if (DatabaseVersion == CURRENT_VERSION && metadata_version < CURRENT_METADATA_VERSION) {
                    ServiceManager.ServiceStarted += OnServiceStarted;
                }
            } catch (DatabaseVersionTooHigh) {
                throw;
            } catch (Exception) {
                Log.Warning ("Rolling back database migration");
                Execute ("ROLLBACK");
                throw;
            }

            OnFinished ();
        }

        private void InnerMigrate ()
        {
            MethodInfo [] methods = GetType ().GetMethods (BindingFlags.Instance | BindingFlags.NonPublic);
            bool terminate = false;
            bool ran_migration_step = false;

            Log.DebugFormat ("Migrating from database version {0} to {1}", DatabaseVersion, CURRENT_VERSION);
            for (int i = DatabaseVersion + 1; i <= CURRENT_VERSION; i++) {
                foreach (MethodInfo method in methods) {
                    foreach (DatabaseVersionAttribute attr in method.GetCustomAttributes (
                        typeof (DatabaseVersionAttribute), false)) {
                        if (attr.Version != i) {
                            continue;
                        }

                        if (!ran_migration_step) {
                            ran_migration_step = true;
                            OnStarted ();
                        }

                        if (!(bool)method.Invoke (this, null)) {
                            terminate = true;
                        }

                        break;
                    }
                }

                if (terminate) {
                    break;
                }
            }

            Execute (String.Format ("UPDATE CoreConfiguration SET Value = {0} WHERE Key = 'DatabaseVersion'", CURRENT_VERSION));
        }

        protected bool TableExists(string tableName)
        {
            return connection.TableExists (tableName);
        }

        protected void Execute(string query)
        {
            connection.Execute (query);
        }

        protected int DatabaseVersion {
            get {
                if (!TableExists("CoreConfiguration")) {
                    return 0;
                }

                return connection.Query<int> ("SELECT Value FROM CoreConfiguration WHERE Key = 'DatabaseVersion'");
            }
        }

#endregion

#pragma warning disable 0169

#region Version 1

        [DatabaseVersion (1)]
        private bool Migrate_1 ()
        {
            if (TableExists("Tracks")) {
                InitializeFreshDatabase (true);

                uint timer_id = Log.DebugTimerStart ("Database Schema Migration");

                OnSlowStarted (Catalog.GetString ("Upgrading your Banshee Database"),
                    Catalog.GetString ("Please wait while your old Banshee database is migrated to the new format."));

                Thread thread = new Thread (MigrateFromLegacyBanshee);
                thread.Name = "Database Migrator";
                thread.Start ();

                while (thread.IsAlive) {
                    OnSlowPulse ();
                    Thread.Sleep (100);
                }

                Log.DebugTimerPrint (timer_id);

                OnSlowFinished ();

                return false;
            } else {
                InitializeFreshDatabase (false);
                return false;
            }
        }

#endregion

#region Version 2

        [DatabaseVersion (2)]
        private bool Migrate_2 ()
        {
            Execute (String.Format ("ALTER TABLE CoreTracks ADD COLUMN Attributes INTEGER  DEFAULT {0}",
                (int)TrackMediaAttributes.Default));
            return true;
        }

#endregion

#region Version 3

        [DatabaseVersion (3)]
        private bool Migrate_3 ()
        {
            Execute ("ALTER TABLE CorePlaylists ADD COLUMN PrimarySourceID INTEGER");
            Execute ("UPDATE CorePlaylists SET PrimarySourceID = 1");

            Execute ("ALTER TABLE CoreSmartPlaylists ADD COLUMN PrimarySourceID INTEGER");
            Execute ("UPDATE CoreSmartPlaylists SET PrimarySourceID = 1");
            return true;
        }

#endregion

#region Version 4

        [DatabaseVersion (4)]
        private bool Migrate_4 ()
        {
            Execute ("ALTER TABLE CoreTracks ADD COLUMN LastSkippedStamp INTEGER");
            return true;
        }

#endregion

#region Version 5

        [DatabaseVersion (5)]
        private bool Migrate_5 ()
        {
            Execute ("ALTER TABLE CoreTracks ADD COLUMN TitleLowered TEXT");
            Execute ("ALTER TABLE CoreArtists ADD COLUMN NameLowered TEXT");
            Execute ("ALTER TABLE CoreAlbums ADD COLUMN TitleLowered TEXT");

            // Set default so sorting isn't whack while we regenerate
            Execute ("UPDATE CoreTracks SET TitleLowered = lower(Title)");
            Execute ("UPDATE CoreArtists SET NameLowered = lower(Name)");
            Execute ("UPDATE CoreAlbums SET TitleLowered = lower(Title)");

            // Drop old indexes
            Execute ("DROP INDEX IF EXISTS CoreTracksPrimarySourceIndex");
            Execute ("DROP INDEX IF EXISTS CoreTracksArtistIndex");
            Execute ("DROP INDEX IF EXISTS CoreTracksAlbumIndex");
            Execute ("DROP INDEX IF EXISTS CoreTracksRatingIndex");
            Execute ("DROP INDEX IF EXISTS CoreTracksLastPlayedStampIndex");
            Execute ("DROP INDEX IF EXISTS CoreTracksDateAddedStampIndex");
            Execute ("DROP INDEX IF EXISTS CoreTracksPlayCountIndex");
            Execute ("DROP INDEX IF EXISTS CoreTracksTitleIndex");
            Execute ("DROP INDEX IF EXISTS CoreAlbumsIndex");
            Execute ("DROP INDEX IF EXISTS CoreAlbumsArtistID");
            Execute ("DROP INDEX IF EXISTS CoreArtistsIndex");
            Execute ("DROP INDEX IF EXISTS CorePlaylistEntriesIndex");
            Execute ("DROP INDEX IF EXISTS CorePlaylistTrackIDIndex");
            Execute ("DROP INDEX IF EXISTS CoreSmartPlaylistEntriesPlaylistIndex");
            Execute ("DROP INDEX IF EXISTS CoreSmartPlaylistEntriesTrackIndex");

            // Create new indexes
            Execute ("CREATE INDEX IF NOT EXISTS CoreTracksIndex ON CoreTracks(ArtistID, AlbumID, PrimarySourceID, Disc, TrackNumber, Uri)");
            Execute ("CREATE INDEX IF NOT EXISTS CoreArtistsIndex ON CoreArtists(NameLowered)");
            Execute ("CREATE INDEX IF NOT EXISTS CoreAlbumsIndex       ON CoreAlbums(ArtistID, TitleLowered)");
            Execute ("CREATE INDEX IF NOT EXISTS CoreSmartPlaylistEntriesIndex ON CoreSmartPlaylistEntries(SmartPlaylistID, TrackID)");
            Execute ("CREATE INDEX IF NOT EXISTS CorePlaylistEntriesIndex ON CorePlaylistEntries(PlaylistID, TrackID)");

            return true;
        }

#endregion

#region Version 6

        [DatabaseVersion (6)]
        private bool Migrate_6 ()
        {
            Execute ("INSERT INTO CoreConfiguration (EntryID, Key, Value) VALUES (null, 'MetadataVersion', 0)");
            return true;
        }

#endregion

#region Version 7

        [DatabaseVersion (7)]
        private bool Migrate_7 ()
        {
            try { Execute ("UPDATE CorePrimarySources SET StringID = 'MusicLibrarySource-Library' WHERE StringID = 'Library'"); } catch {}
            try { Execute ("UPDATE CorePrimarySources SET StringID = 'VideoLibrarySource-VideoLibrary' WHERE StringID = 'VideoLibrary'"); } catch {}
            try { Execute ("UPDATE CorePrimarySources SET StringID = 'PodcastSource-podcasting' WHERE StringID = 'podcasting'"); } catch {}
            try { Execute ("DELETE FROM CoreCache; DELETE FROM CoreCacheModels"); } catch {}
            return true;
        }

#endregion

#region Version 8

        [DatabaseVersion (8)]
        private bool Migrate_8 ()
        {
            Execute ("ALTER TABLE CorePrimarySources ADD COLUMN CachedCount INTEGER");
            Execute ("ALTER TABLE CorePlaylists ADD COLUMN CachedCount INTEGER");
            Execute ("ALTER TABLE CoreSmartPlaylists ADD COLUMN CachedCount INTEGER");

            // This once, we need to reload all the sources at start up. Then never again, woo!
            Application.ClientStarted += ReloadAllSources;
            return true;
        }

#endregion

#region Version 9

        [DatabaseVersion (9)]
        private bool Migrate_9 ()
        {
            Execute (String.Format ("ALTER TABLE CoreTracks ADD COLUMN LastStreamError INTEGER DEFAULT {0}",
                (int)StreamPlaybackError.None));
            return true;
        }

#endregion

#region Version 10

        [DatabaseVersion (10)]
        private bool Migrate_10 ()
        {
            // Clear these out for people who ran the pre-alpha podcast plugin
            Execute ("DROP TABLE IF EXISTS PodcastEnclosures");
            Execute ("DROP TABLE IF EXISTS PodcastItems");
            Execute ("DROP TABLE IF EXISTS PodcastSyndications");
            Execute ("ALTER TABLE CoreTracks ADD COLUMN ExternalID INTEGER");
            return true;
        }

#endregion

#region Version 11

        [DatabaseVersion (11)]
        private bool Migrate_11 ()
        {
            Execute("CREATE INDEX CoreTracksExternalIDIndex ON CoreTracks(PrimarySourceID, ExternalID)");
            return true;
        }

#endregion

#region Version 12

        [DatabaseVersion (12)]
        private bool Migrate_12 ()
        {
            Execute ("ALTER TABLE CoreAlbums ADD COLUMN ArtistName STRING");
            Execute ("ALTER TABLE CoreAlbums ADD COLUMN ArtistNameLowered STRING");
            return true;
        }

#endregion

#region Version 13

        [DatabaseVersion (13)]
        private bool Migrate_13 ()
        {
            Execute("CREATE INDEX CoreAlbumsArtistIndex ON CoreAlbums(TitleLowered, ArtistNameLowered)");
            Execute("CREATE INDEX CoreTracksUriIndex ON CoreTracks(PrimarySourceID, Uri)");
            return true;
        }

#endregion

#region Version 14

        [DatabaseVersion (14)]
        private bool Migrate_14 ()
        {
            InitializeOrderedTracks ();
            return true;
        }

#endregion

#region Version 15

        [DatabaseVersion (15)]
        private bool Migrate_15 ()
        {
            string [] columns = new string [] {"Genre", "Composer", "Copyright", "LicenseUri", "Comment"};
            foreach (string column in columns) {
                Execute (String.Format ("UPDATE CoreTracks SET {0} = NULL WHERE {0} IS NOT NULL AND trim({0}) = ''", column));
            }
            return true;
        }

#endregion

#region Version 16

        [DatabaseVersion (16)]
        private bool Migrate_16 ()
        {
            // The CoreCache table is now created as needed, and as a TEMP table
            Execute ("DROP TABLE CoreCache");
            Execute ("COMMIT; VACUUM; BEGIN");
            Execute ("ANALYZE");
            return true;
        }

#endregion

#region Version 17

        [DatabaseVersion (17)]
        private bool Migrate_17 ()
        {
            Execute ("CREATE INDEX CoreTracksCoverArtIndex ON CoreTracks (PrimarySourceID, AlbumID, DateUpdatedStamp)");
            Execute ("ANALYZE");
            return true;
        }

#endregion

#region Version 18

        [DatabaseVersion (18)]
        private bool Migrate_18 ()
        {
            Execute ("ALTER TABLE CoreTracks ADD COLUMN MetadataHash TEXT");
            return true;
        }

#endregion

#region Version 19

        [DatabaseVersion (19)]
        private bool Migrate_19 ()
        {
            Execute ("ALTER TABLE CoreAlbums ADD COLUMN IsCompilation INTEGER DEFAULT 0");
            Execute ("ALTER TABLE CoreTracks ADD COLUMN BPM INTEGER");
            Execute ("ALTER TABLE CoreTracks ADD COLUMN DiscCount INTEGER");
            Execute ("ALTER TABLE CoreTracks ADD COLUMN Conductor TEXT");
            Execute ("ALTER TABLE CoreTracks ADD COLUMN Grouping TEXT");
            Execute ("ALTER TABLE CoreTracks ADD COLUMN BitRate INTEGER DEFAULT 0");
            return true;
        }

#endregion

#region Version 20

        [DatabaseVersion (20)]
        private bool Migrate_20 ()
        {
            Execute ("ALTER TABLE CoreSmartPlaylists ADD COLUMN IsTemporary INTEGER DEFAULT 0");
            Execute ("ALTER TABLE CorePlaylists ADD COLUMN IsTemporary INTEGER DEFAULT 0");
            Execute ("ALTER TABLE CorePrimarySources ADD COLUMN IsTemporary INTEGER DEFAULT 0");
            return true;
        }

#endregion

#region Version 21

        [DatabaseVersion (21)]
        private bool Migrate_21 ()
        {
            // We had a bug where downloaded podcast episodes would no longer have the Podcast attribute.
            int id = connection.Query<int> ("SELECT PrimarySourceId FROM CorePrimarySources WHERE StringID = 'PodcastSource-PodcastLibrary'");
            if (id > 0) {
                connection.Execute ("UPDATE CoreTracks SET Attributes = Attributes | ? WHERE PrimarySourceID = ?", (int)TrackMediaAttributes.Podcast, id);
            }
            return true;
        }

#endregion

#region Version 22

        [DatabaseVersion (22)]
        private bool Migrate_22 ()
        {
            Execute ("ALTER TABLE CoreTracks ADD COLUMN LastSyncedStamp INTEGER DEFAULT NULL");
            Execute ("ALTER TABLE CoreTracks ADD COLUMN FileModifiedStamp INTEGER DEFAULT NULL");
            Execute ("UPDATE CoreTracks SET LastSyncedStamp = DateAddedStamp;");
            return true;
        }

#endregion

#region Version 23

        [DatabaseVersion (23)]
        private bool Migrate_23 ()
        {
            Execute ("ALTER TABLE CoreArtists ADD COLUMN NameSort TEXT");
            Execute ("ALTER TABLE CoreAlbums ADD COLUMN ArtistNameSort TEXT");
            Execute ("ALTER TABLE CoreAlbums ADD COLUMN TitleSort TEXT");
            Execute ("ALTER TABLE CoreTracks ADD COLUMN TitleSort TEXT");

            return true;
        }

#endregion

#region Version 24
        [DatabaseVersion (24)]
        private bool Migrate_24 ()
        {
            Execute ("UPDATE CoreArtists SET NameLowered = HYENA_SEARCH_KEY(Name)");
            Execute ("UPDATE CoreAlbums SET ArtistNameLowered = HYENA_SEARCH_KEY(ArtistName)");
            Execute ("UPDATE CoreAlbums SET TitleLowered = HYENA_SEARCH_KEY(Title)");
            Execute ("UPDATE CoreTracks SET TitleLowered = HYENA_SEARCH_KEY(Title)");
            return true;
        }
#endregion

#region Version 25

        [DatabaseVersion (25)]
        private bool Migrate_25 ()
        {
            Execute ("ALTER TABLE CoreArtists ADD COLUMN NameSortKey BLOB");
            Execute ("ALTER TABLE CoreAlbums ADD COLUMN ArtistNameSortKey BLOB");
            Execute ("ALTER TABLE CoreAlbums ADD COLUMN TitleSortKey BLOB");
            Execute ("ALTER TABLE CoreTracks ADD COLUMN TitleSortKey BLOB");
            return true;
        }

#endregion

#region Version 26

        [DatabaseVersion (26)]
        private bool Migrate_26 ()
        {
            connection.Execute ("UPDATE CoreArtists SET Name = NULL, NameLowered = HYENA_SEARCH_KEY(?)" +
                                " WHERE Name  IN ('', ?, ?) OR Name IS NULL",
                                ArtistInfo.UnknownArtistName,
                                ArtistInfo.UnknownArtistNameUntranslated,
                                ArtistInfo.UnknownArtistName);

            connection.Execute ("UPDATE CoreAlbums SET ArtistName = NULL, ArtistNameLowered = HYENA_SEARCH_KEY(?)" +
                                " WHERE ArtistName IN ('', ?, ?) OR ArtistName IS NULL",
                                ArtistInfo.UnknownArtistName,
                                ArtistInfo.UnknownArtistNameUntranslated,
                                ArtistInfo.UnknownArtistName);

            connection.Execute ("UPDATE CoreAlbums SET Title = NULL, TitleLowered = HYENA_SEARCH_KEY(?)" +
                                " WHERE Title IN ('', ?, ?) OR Title IS NULL",
                                AlbumInfo.UnknownAlbumTitle,
                                AlbumInfo.UnknownAlbumTitleUntranslated,
                                AlbumInfo.UnknownAlbumTitle);

            connection.Execute ("UPDATE CoreTracks SET Title = NULL, TitleLowered = HYENA_SEARCH_KEY(?)" +
                                " WHERE Title IN ('', ?, ?) OR Title IS NULL",
                                TrackInfo.UnknownTitle,
                                TrackInfo.UnknownTitleUntranslated,
                                TrackInfo.UnknownTitle);

            return true;
        }

#endregion

#region Version 27

        [DatabaseVersion (27)]
        private bool Migrate_27 ()
        {
            // One time fixup to MetadataHash now that our unknown metadata is handled properly
            string sql_select = @"
                SELECT t.TrackID, al.Title, ar.Name, t.Duration,
                t.Genre, t.Title, t.TrackNumber, t.Year
                FROM CoreTracks AS t
                JOIN CoreAlbums AS al ON al.AlbumID=t.AlbumID
                JOIN CoreArtists AS ar ON ar.ArtistID=t.ArtistID
                WHERE t.Title IS NULL OR ar.Name IS NULL OR al.Title IS NULL
            ";

            HyenaSqliteCommand sql_update = new HyenaSqliteCommand (@"
                UPDATE CoreTracks SET MetadataHash = ? WHERE TrackID = ?
            ");

            StringBuilder sb = new StringBuilder ();
            using (var reader = new HyenaDataReader (connection.Query (sql_select))) {
                while (reader.Read ()) {
                    sb.Length = 0;
                    sb.Append (reader.Get<string> (1));
                    sb.Append (reader.Get<string> (2));
                    sb.Append ((int)reader.Get<TimeSpan> (3).TotalSeconds);
                    sb.Append (reader.Get<string> (4));
                    sb.Append (reader.Get<string> (5));
                    sb.Append (reader.Get<int> (6));
                    sb.Append (reader.Get<int> (7));
                    string hash = Hyena.CryptoUtil.Md5Encode (sb.ToString (), System.Text.Encoding.UTF8);
                    connection.Execute (sql_update, hash, reader.Get<int> (0));
                }
            }

            return true;
        }

#endregion

#region Version 28

        [DatabaseVersion (28)]
        private bool Migrate_28 ()
        {
            // Update search keys for new space-stripping behavior.
            connection.Execute ("UPDATE CoreArtists SET NameLowered = HYENA_SEARCH_KEY(IFNULL(Name, ?))",
                                ArtistInfo.UnknownArtistName);
            connection.Execute ("UPDATE CoreAlbums SET ArtistNameLowered = HYENA_SEARCH_KEY(IFNULL(ArtistName, ?))," +
                                "                      TitleLowered = HYENA_SEARCH_KEY(IFNULL(Title, ?))",
                                ArtistInfo.UnknownArtistName, AlbumInfo.UnknownAlbumTitle);
            connection.Execute ("UPDATE CoreTracks SET TitleLowered = HYENA_SEARCH_KEY(IFNULL(Title, ?))",
                                TrackInfo.UnknownTitle);
            return true;
        }

#endregion

#region Version 29

        [DatabaseVersion (29)]
        private bool Migrate_29 ()
        {
            Execute ("ALTER TABLE CoreTracks ADD COLUMN Score INTEGER DEFAULT 0");
            Execute (@"
                UPDATE CoreTracks
                SET Score = CAST(ROUND(100.00 * PlayCount / (PlayCount + SkipCount)) AS INTEGER)
                WHERE PlayCount + SkipCount > 0
            ");
            return true;
        }

#endregion

#region Version 30

        [DatabaseVersion (30)]
        private bool Migrate_30 ()
        {
            Execute ("DROP INDEX IF EXISTS CoreAlbumsIndex");
            Execute ("DROP INDEX IF EXISTS CoreAlbumsArtistIndex");
            Execute ("DROP INDEX IF EXISTS CoreArtistsIndex");
            Execute ("CREATE INDEX CoreAlbumsIndex ON CoreAlbums(ArtistID, TitleSortKey)");
            Execute ("CREATE INDEX CoreAlbumsArtistIndex ON CoreAlbums(TitleSortKey, ArtistNameSortKey)");
            Execute ("CREATE INDEX CoreArtistsIndex ON CoreArtists(NameSortKey)");
            Execute ("ANALYZE");
            return true;
        }

#endregion


#region Version 31

        [DatabaseVersion (31)]
        private bool Migrate_31 ()
        {
            try {
                // Make paths not relative for Music Library items
                string library_path = Banshee.Library.LibrarySource.OldLocationSchema.Get (Banshee.Library.MusicLibrarySource.GetDefaultBaseDirectory ());
                if (library_path != null) {
                    connection.AddFunction<MigratePartialFunction> ();

                    int podcast_src_id = connection.Query<int> ("SELECT PrimarySourceID FROM CorePrimarySources WHERE StringID = 'PodcastSource-PodcastLibrary'");

                    connection.Execute (@"
                        UPDATE CoreTracks SET Uri = BANSHEE_MIGRATE_PARTIAL(?, Uri)
                        WHERE UriType = 1
                          AND PrimarySourceID != ?", library_path, podcast_src_id);

                    string podcast_path = Paths.Combine (library_path, "Podcasts");
                    connection.Execute (@"
                        UPDATE CoreTracks SET Uri = BANSHEE_MIGRATE_PARTIAL(?, Uri)
                        WHERE UriType = 1
                          AND PrimarySourceID = ?", podcast_path, podcast_src_id);

                    connection.RemoveFunction<MigratePartialFunction> ();
                }
            } catch (Exception e) {
                Hyena.Log.Error (e);
            }
            return true;
        }

#endregion

#region Version 32

        [DatabaseVersion (32)]
        private bool Migrate_32 ()
        {
            Execute ("DROP INDEX IF EXISTS CoreSmartPlaylistEntriesPlaylistIndex");
            Execute ("DROP INDEX IF EXISTS CoreSmartPlaylistEntriesIndex");
            Execute ("ANALYZE");
            return true;
        }

#endregion

#region Version 33

        [DatabaseVersion (33)]
        private bool Migrate_33 ()
        {
            // We used to have a bug where MimeType didn't get set for tracks we ripped,
            // so set any blank ones now.  See BGO #536590
            foreach (var ext in new string [] {"mp3", "ogg", "flac", "aac", "oga", "wma", "wm"}) {
                Execute (String.Format (
                    "UPDATE CoreTracks SET MimeType = 'taglib/{0}' WHERE PrimarySourceId = 1 AND (MimeType IS NULL OR MimeType = '') AND Uri LIKE '%.{0}'", ext
                ));
            }
            return true;
        }

#endregion

#region Version 34

        [DatabaseVersion (34)]
        private bool Migrate_34 ()
        {
            Execute ("CREATE INDEX IF NOT EXISTS CoreSmartPlaylistEntriesIndex ON CoreSmartPlaylistEntries(SmartPlaylistID, TrackID)");
            return true;
        }

#endregion

#region Version 35

        [DatabaseVersion (35)]
        private bool Migrate_35 ()
        {
            if (!connection.ColumnExists ("CorePlaylistEntries", "Generated")) {
                Execute ("ALTER TABLE CorePlaylistEntries ADD COLUMN Generated INTEGER NOT NULL DEFAULT 0");
            }
            return true;
        }

#endregion

#region Version 36

        [DatabaseVersion (36)]
        private bool Migrate_36 ()
        {
            Execute(@"
                CREATE TABLE CoreShuffles (
                    ShufflerId          INTEGER,
                    TrackID             INTEGER,
                    LastShuffledAt      INTEGER,
                    CONSTRAINT one_entry_per_track UNIQUE (ShufflerID, TrackID)
                )
            ");
            Execute("CREATE INDEX CoreShufflesIndex ON CoreShuffles (ShufflerId, TrackID, LastShuffledAt)");

            Execute(@"
                CREATE TABLE CoreShufflers (
                    ShufflerId      INTEGER PRIMARY KEY,
                    Id              TEXT UNIQUE
                )
            ");

            return true;
        }

#endregion

#region Version 37

        [DatabaseVersion (37)]
        private bool Migrate_37 ()
        {
            SortKeyUpdater.ForceUpdate ();
            return true;
        }

#endregion

#region Version 38

        [DatabaseVersion (38)]
        private bool Migrate_38 ()
        {
            Execute ("ALTER TABLE CoreTracks ADD COLUMN SampleRate INTEGER DEFAULT 0");
            Execute ("ALTER TABLE CoreTracks ADD COLUMN BitsPerSample INTEGER DEFAULT 0");
            return true;
        }

#endregion

#region Version 39

        [DatabaseVersion (39)]
        private bool Migrate_39 ()
        {
            // One time fixup to MetadataHash, since we no longer include the Duration
            string sql_select = @"
                SELECT t.TrackID, al.Title, ar.Name,
                t.Genre, t.Title, t.TrackNumber, t.Year
                FROM CoreTracks AS t
                JOIN CoreAlbums AS al ON al.AlbumID=t.AlbumID
                JOIN CoreArtists AS ar ON ar.ArtistID=t.ArtistID
            ";

            HyenaSqliteCommand sql_update = new HyenaSqliteCommand (@"
                UPDATE CoreTracks SET MetadataHash = ? WHERE TrackID = ?
            ");

            StringBuilder sb = new StringBuilder ();
            using (var reader = new HyenaDataReader (connection.Query (sql_select))) {
                while (reader.Read ()) {
                    sb.Length = 0;
                    sb.Append (reader.Get<string> (1));
                    sb.Append (reader.Get<string> (2));
                    sb.Append (reader.Get<string> (3));
                    sb.Append (reader.Get<string> (4));
                    sb.Append (reader.Get<int> (5));
                    sb.Append (reader.Get<int> (6));
                    string hash = Hyena.CryptoUtil.Md5Encode (sb.ToString (), System.Text.Encoding.UTF8);
                    connection.Execute (sql_update, hash, reader.Get<int> (0));
                }
            }

            return true;
        }

#endregion

        [DatabaseVersion (40)]
        private bool Migrate_40 ()
        {
            Execute(@"
                CREATE TABLE CoreShuffleDiscards (
                    ShufflerId           INTEGER,
                    TrackID              INTEGER,
                    LastDiscardedAt      INTEGER,
                    CONSTRAINT one_entry_per_track UNIQUE (ShufflerID, TrackID)
                )
            ");
            Execute("CREATE INDEX CoreShuffleDiscardsIndex ON CoreShuffleDiscards (ShufflerId, TrackID, LastDiscardedAt)");
            return true;
        }

        [DatabaseVersion (41)]
        private bool Migrate_41 ()
        {
            Execute ("DROP TABLE IF EXISTS CoreShuffleDiscards");
            Execute ("DROP INDEX IF EXISTS CoreShuffleDiscardsIndex");

            Execute (@"
                CREATE TABLE CoreShuffleModifications (
                    ShufflerId           INTEGER,
                    TrackID              INTEGER,
                    LastModifiedAt       INTEGER,
                    ModificationType     INTEGER,
                    CONSTRAINT one_entry_per_track UNIQUE (ShufflerID, TrackID)
                )
            ");
            Execute ("CREATE INDEX CoreShuffleModificationsIndex ON CoreShuffleModifications (ShufflerId, TrackID, LastModifiedAt, ModificationType)");
            return true;
        }

        [DatabaseVersion (42)]
        private bool Migrate_42 ()
        {
            // Unset the Music attribute for any videos or podcasts
            connection.Execute (
                @"UPDATE CoreTracks SET Attributes = Attributes & ? WHERE (Attributes & ?) != 0",
                (int)(~TrackMediaAttributes.Music),
                (int)(TrackMediaAttributes.VideoStream | TrackMediaAttributes.Podcast)
            );
            return true;
        }

        [DatabaseVersion (43)]
        private bool Migrate_43 ()
        {
            Execute ("ALTER TABLE CoreSmartPlaylists ADD COLUMN IsHiddenWhenEmpty INTEGER");
            return true;
        }

#region Version 44
        [DatabaseVersion (44)]
        private bool Migrate_44 ()
        {
            Execute ("ALTER TABLE CoreAlbums ADD COLUMN ArtworkID TEXT");
            return true;
        }
#endregion

        [DatabaseVersion (45)]
        private bool Migrate_45 ()
        {
            connection.AddFunction<FixUriEncodingFunction> ();
            Execute ("UPDATE CoreTracks SET Uri = BANSHEE_FIX_URI_ENCODING (Uri) WHERE Uri LIKE 'file:///%'");
            connection.RemoveFunction<FixUriEncodingFunction> ();
            return true;
        }

#pragma warning restore 0169

#region Fresh database setup

        private void InitializeFreshDatabase (bool refresh_metadata)
        {
            DropTables ();

            CreateConfiguration (refresh_metadata);

            CreateTablesAndIndexes ();
        }

        private void DropTables ()
        {
            Execute("DROP TABLE IF EXISTS CoreConfiguration");
            Execute("DROP TABLE IF EXISTS CoreTracks");
            Execute("DROP TABLE IF EXISTS CoreArtists");
            Execute("DROP TABLE IF EXISTS CoreAlbums");
            Execute("DROP TABLE IF EXISTS CorePlaylists");
            Execute("DROP TABLE IF EXISTS CorePlaylistEntries");
            Execute("DROP TABLE IF EXISTS CoreSmartPlaylists");
            Execute("DROP TABLE IF EXISTS CoreSmartPlaylistEntries");
            Execute("DROP TABLE IF EXISTS CoreRemovedTracks");
            Execute("DROP TABLE IF EXISTS CoreTracksCache");
            Execute("DROP TABLE IF EXISTS CoreCache");
        }

        private void CreateConfiguration (bool refresh_metadata)
        {
            Execute(@"
                CREATE TABLE CoreConfiguration (
                    EntryID             INTEGER PRIMARY KEY,
                    Key                 TEXT,
                    Value               TEXT
                )
            ");
            Execute (String.Format ("INSERT INTO CoreConfiguration (EntryID, Key, Value) VALUES (null, 'DatabaseVersion', {0})", CURRENT_VERSION));
            if (!refresh_metadata) {
                Execute (String.Format ("INSERT INTO CoreConfiguration (EntryID, Key, Value) VALUES (null, 'MetadataVersion', {0})", CURRENT_METADATA_VERSION));
            }
        }

        private void CreateTablesAndIndexes ()
        {
            Execute(@"
                CREATE TABLE CorePrimarySources (
                    PrimarySourceID     INTEGER PRIMARY KEY,
                    StringID            TEXT UNIQUE,
                    CachedCount         INTEGER,
                    IsTemporary         INTEGER DEFAULT 0
                )
            ");
            Execute ("INSERT INTO CorePrimarySources (StringID) VALUES ('MusicLibrarySource-Library')");

            // TODO add these:
            // Others to consider:
            // AlbumArtist (TPE2) (in CoreAlbums?)
            Execute(String.Format (@"
                CREATE TABLE CoreTracks (
                    PrimarySourceID     INTEGER NOT NULL,
                    TrackID             INTEGER PRIMARY KEY,
                    ArtistID            INTEGER,
                    AlbumID             INTEGER,
                    TagSetID            INTEGER,
                    ExternalID          INTEGER,

                    MusicBrainzID       TEXT,

                    Uri                 TEXT,
                    MimeType            TEXT,
                    FileSize            INTEGER,
                    BitRate             INTEGER,
                    SampleRate          INTEGER,
                    BitsPerSample       INTEGER,
                    Attributes          INTEGER DEFAULT {0},
                    LastStreamError     INTEGER DEFAULT {1},

                    Title               TEXT,
                    TitleLowered        TEXT,
                    TitleSort           TEXT,
                    TitleSortKey        BLOB,
                    TrackNumber         INTEGER,
                    TrackCount          INTEGER,
                    Disc                INTEGER,
                    DiscCount           INTEGER,
                    Duration            INTEGER,
                    Year                INTEGER,
                    Genre               TEXT,
                    Composer            TEXT,
                    Conductor           TEXT,
                    Grouping            TEXT,
                    Copyright           TEXT,
                    LicenseUri          TEXT,

                    Comment             TEXT,
                    Rating              INTEGER,
                    Score               INTEGER,
                    PlayCount           INTEGER,
                    SkipCount           INTEGER,
                    LastPlayedStamp     INTEGER,
                    LastSkippedStamp    INTEGER,
                    DateAddedStamp      INTEGER,
                    DateUpdatedStamp    INTEGER,
                    MetadataHash        TEXT,
                    BPM                 INTEGER,
                    LastSyncedStamp     INTEGER,
                    FileModifiedStamp   INTEGER
                )
            ", (int)TrackMediaAttributes.Default, (int)StreamPlaybackError.None));

            Execute("CREATE INDEX CoreTracksPrimarySourceIndex ON CoreTracks(ArtistID, AlbumID, PrimarySourceID, Disc, TrackNumber, Uri)");
            Execute("CREATE INDEX CoreTracksAggregatesIndex ON CoreTracks(FileSize, Duration)");
            Execute("CREATE INDEX CoreTracksExternalIDIndex ON CoreTracks(PrimarySourceID, ExternalID)");
            Execute("CREATE INDEX CoreTracksUriIndex ON CoreTracks(PrimarySourceID, Uri)");
            Execute("CREATE INDEX CoreTracksCoverArtIndex ON CoreTracks (PrimarySourceID, AlbumID, DateUpdatedStamp)");

            Execute(@"
                CREATE TABLE CoreAlbums (
                    AlbumID             INTEGER PRIMARY KEY,
                    ArtistID            INTEGER,
                    TagSetID            INTEGER,

                    MusicBrainzID       TEXT,

                    Title               TEXT,
                    TitleLowered        TEXT,
                    TitleSort           TEXT,
                    TitleSortKey        BLOB,

                    ReleaseDate         INTEGER,
                    Duration            INTEGER,
                    Year                INTEGER,
                    IsCompilation       INTEGER DEFAULT 0,

                    ArtistName          TEXT,
                    ArtistNameLowered   TEXT,
                    ArtistNameSort      TEXT,
                    ArtistNameSortKey   BLOB,

                    Rating              INTEGER,

                    ArtworkID           TEXT
                )
            ");
            Execute ("CREATE INDEX CoreAlbumsIndex ON CoreAlbums(ArtistID, TitleSortKey)");
            Execute ("CREATE INDEX CoreAlbumsArtistIndex ON CoreAlbums(TitleSortKey, ArtistNameSortKey)");

            Execute(@"
                CREATE TABLE CoreArtists (
                    ArtistID            INTEGER PRIMARY KEY,
                    TagSetID            INTEGER,
                    MusicBrainzID       TEXT,
                    Name                TEXT,
                    NameLowered         TEXT,
                    NameSort            TEXT,
                    NameSortKey         BLOB,
                    Rating              INTEGER
                )
            ");
            Execute ("CREATE INDEX CoreArtistsIndex ON CoreArtists(NameSortKey)");

            Execute(@"
                CREATE TABLE CorePlaylists (
                    PrimarySourceID     INTEGER,
                    PlaylistID          INTEGER PRIMARY KEY,
                    Name                TEXT,
                    SortColumn          INTEGER NOT NULL DEFAULT -1,
                    SortType            INTEGER NOT NULL DEFAULT 0,
                    Special             INTEGER NOT NULL DEFAULT 0,
                    CachedCount         INTEGER,
                    IsTemporary         INTEGER DEFAULT 0
                )
            ");

            Execute(@"
                CREATE TABLE CorePlaylistEntries (
                    EntryID             INTEGER PRIMARY KEY,
                    PlaylistID          INTEGER NOT NULL,
                    TrackID             INTEGER NOT NULL,
                    ViewOrder           INTEGER NOT NULL DEFAULT 0,
                    Generated           INTEGER NOT NULL DEFAULT 0
                )
            ");
            Execute("CREATE INDEX CorePlaylistEntriesIndex ON CorePlaylistEntries(PlaylistID, TrackID)");

            Execute(@"
                CREATE TABLE CoreSmartPlaylists (
                    PrimarySourceID     INTEGER,
                    SmartPlaylistID     INTEGER PRIMARY KEY,
                    Name                TEXT NOT NULL,
                    Condition           TEXT,
                    OrderBy             TEXT,
                    LimitNumber         TEXT,
                    LimitCriterion      TEXT,
                    CachedCount         INTEGER,
                    IsTemporary         INTEGER DEFAULT 0,
                    IsHiddenWhenEmpty   INTEGER DEFAULT 0
                )
            ");

            Execute(@"
                CREATE TABLE CoreSmartPlaylistEntries (
                    EntryID             INTEGER PRIMARY KEY,
                    SmartPlaylistID     INTEGER NOT NULL,
                    TrackID             INTEGER NOT NULL
                )
            ");
            Execute ("CREATE INDEX CoreSmartPlaylistEntriesIndex ON CoreSmartPlaylistEntries(SmartPlaylistID, TrackID)");

            Execute(@"
                CREATE TABLE CoreRemovedTracks (
                    TrackID             INTEGER NOT NULL,
                    Uri                 TEXT,
                    DateRemovedStamp    INTEGER
                )
            ");

            Execute(@"
                CREATE TABLE CoreCacheModels (
                    CacheID             INTEGER PRIMARY KEY,
                    ModelID             TEXT
                )
            ");

            // This index slows down queries were we shove data into the CoreCache.
            // Since we do that frequently, not using it.
            //Execute("CREATE INDEX CoreCacheModelId      ON CoreCache(ModelID)");

            Execute(@"
                CREATE TABLE CoreShuffles (
                    ShufflerId           INTEGER,
                    TrackID             INTEGER,
                    LastShuffledAt      INTEGER,
                    CONSTRAINT one_entry_per_track UNIQUE (ShufflerID, TrackID)
                )
            ");
            Execute("CREATE INDEX CoreShufflesIndex ON CoreShuffles (ShufflerId, TrackID, LastShuffledAt)");

            Execute(@"
                CREATE TABLE CoreShufflers (
                    ShufflerId      INTEGER PRIMARY KEY,
                    Id              TEXT UNIQUE
                )
            ");

            Execute (@"
                CREATE TABLE CoreShuffleModifications (
                    ShufflerId           INTEGER,
                    TrackID              INTEGER,
                    LastModifiedAt       INTEGER,
                    ModificationType     INTEGER,
                    CONSTRAINT one_entry_per_track UNIQUE (ShufflerID, TrackID)
                )
            ");
            Execute ("CREATE INDEX CoreShuffleModificationsIndex ON CoreShuffleModifications (ShufflerId, TrackID, LastModifiedAt, ModificationType)");
        }

#endregion

#region Legacy database migration

        private void MigrateFromLegacyBanshee()
        {
            Execute(@"
                INSERT INTO CoreArtists
                    (ArtistID, TagSetID, MusicBrainzID, Name, NameLowered, NameSort, Rating)
                    SELECT DISTINCT null, 0, null, Artist, NULL, NULL, 0
                        FROM Tracks
                        ORDER BY Artist
            ");

            Execute(@"
                INSERT INTO CoreAlbums
                    (AlbumID, ArtistID, TagSetID, MusicBrainzID, Title, TitleLowered, TitleSort, ReleaseDate,
                    Duration, Year, IsCompilation, ArtistName, ArtistNameLowered, ArtistNameSort, Rating)
                    SELECT DISTINCT null,
                        (SELECT ArtistID
                            FROM CoreArtists
                            WHERE Name = Tracks.Artist
                            LIMIT 1),
                        0, null, AlbumTitle, NULL, NULL, ReleaseDate, 0, 0, 0, Artist, NULL, NULL, 0
                        FROM Tracks
                        ORDER BY AlbumTitle
            ");

            Execute (String.Format (@"
                INSERT INTO CoreTracks
                    (PrimarySourceID, TrackID, ArtistID, AlbumID, TagSetID, ExternalID, MusicBrainzID, Uri, MimeType,
                    FileSize, BitRate, Attributes, LastStreamError, Title, TitleLowered, TrackNumber, TrackCount,
                    Disc, DiscCount, Duration, Year, Genre, Composer, Conductor, Grouping, Copyright, LicenseUri,
                    Comment, Rating, Score, PlayCount, SkipCount, LastPlayedStamp, LastSkippedStamp, DateAddedStamp,
                    DateUpdatedStamp, MetadataHash, BPM, LastSyncedStamp, FileModifiedStamp)
                    SELECT
                        1,
                        TrackID,
                        (SELECT ArtistID
                            FROM CoreArtists
                            WHERE Name = Artist),
                        (SELECT a.AlbumID
                            FROM CoreAlbums a, CoreArtists b
                            WHERE a.Title = AlbumTitle
                                AND a.ArtistID = b.ArtistID
                                AND b.Name = Artist),
                        0,
                        0,
                        0,
                        Uri,
                        MimeType,
                        0, 0,
                        {0},
                        {1},
                        Title, NULL,
                        TrackNumber,
                        TrackCount,
                        0, 0,
                        Duration * 1000,
                        Year,
                        Genre,
                        NULL, NULL, NULL, NULL, NULL, NULL,
                        Rating,
                        0,
                        NumberOfPlays,
                        0,
                        LastPlayedStamp,
                        NULL,
                        DateAddedStamp,
                        DateAddedStamp,
                        NULL, NULL, DateAddedStamp, NULL
                        FROM Tracks
            ", (int)TrackMediaAttributes.Default, (int)StreamPlaybackError.None));

            Execute ("UPDATE CoreTracks SET LastPlayedStamp = NULL WHERE LastPlayedStamp = -62135575200");

            // Old versions of Banshee had different columns for Playlists/PlaylistEntries, so be careful
            try {
                Execute(@"
                    INSERT INTO CorePlaylists (PlaylistID, Name, SortColumn, SortType)
                        SELECT * FROM Playlists;
                    INSERT INTO CorePlaylistEntries
                        (EntryID, PlaylistID, TrackID, ViewOrder)
                        SELECT * FROM PlaylistEntries
                ");
            } catch (Exception e) {
                Log.Error ("Must be a pre-0.13.2 banshee.db, attempting to migrate", e);
                try {
                    Execute(@"
                        INSERT INTO CorePlaylists (PlaylistID, Name)
                            SELECT PlaylistID, Name FROM Playlists;
                        INSERT INTO CorePlaylistEntries (EntryID, PlaylistID, TrackID)
                            SELECT EntryID, PlaylistID, TrackID FROM PlaylistEntries
                    ");
                    Log.Debug ("Success, was able to migrate older playlist information");
                } catch (Exception e2) {
                    Log.Error ("Failed to migrate playlists", e2);
                }
            }


            // Really old versions of Banshee didn't have SmartPlaylists, so ignore errors
            try {
                Execute(@"
                    INSERT INTO CoreSmartPlaylists (SmartPlaylistID, Name, Condition, OrderBy, LimitNumber, LimitCriterion)
                        SELECT * FROM SmartPlaylists
                ");
            } catch {}

            Execute ("UPDATE CoreSmartPlaylists SET PrimarySourceID = 1");
            Execute ("UPDATE CorePlaylists SET PrimarySourceID = 1");

            InitializeOrderedTracks ();
            Migrate_15 ();
        }

#endregion

#region Utilities / Source / Service Stuff

        private void InitializeOrderedTracks ()
        {
            foreach (long playlist_id in connection.QueryEnumerable<long> ("SELECT PlaylistID FROM CorePlaylists ORDER BY PlaylistID")) {
                if (connection.Query<long> (@"SELECT COUNT(*) FROM CorePlaylistEntries
                    WHERE PlaylistID = ? AND ViewOrder > 0", playlist_id) <= 0) {

                    long first_id = connection.Query<long> ("SELECT COUNT(*) FROM CorePlaylistEntries WHERE PlaylistID < ?", playlist_id);
                    connection.Execute (
                        @"UPDATE CorePlaylistEntries SET ViewOrder = (ROWID - ?) WHERE PlaylistID = ?",
                        first_id, playlist_id
                    );
                }
            }
        }

        private void OnServiceStarted (ServiceStartedArgs args)
        {
            if (args.Service is JobScheduler) {
                ServiceManager.ServiceStarted -= OnServiceStarted;
                if (ServiceManager.SourceManager.MusicLibrary != null) {
                    RefreshMetadataDelayed ();
                } else {
                    ServiceManager.SourceManager.SourceAdded += OnSourceAdded;
                }
            }
        }

        private void OnSourceAdded (SourceAddedArgs args)
        {
            if (ServiceManager.SourceManager.MusicLibrary != null && ServiceManager.SourceManager.VideoLibrary != null) {
                ServiceManager.SourceManager.SourceAdded -= OnSourceAdded;
                RefreshMetadataDelayed ();
            }
        }

        private void ReloadAllSources (Client client)
        {
            Application.ClientStarted -= ReloadAllSources;
            foreach (Source source in ServiceManager.SourceManager.Sources) {
                if (source is ITrackModelSource) {
                    ((ITrackModelSource)source).Reload ();
                }
            }
        }

#endregion

#region Metadata Refresh Driver

        private void RefreshMetadataDelayed ()
        {
            Application.RunTimeout (3000, RefreshMetadata);
        }

        private bool RefreshMetadata ()
        {
            ThreadPool.QueueUserWorkItem (RefreshMetadataThread);
            return false;
        }

        private void RefreshMetadataThread (object state)
        {
            int total = ServiceManager.DbConnection.Query<int> ("SELECT count(*) FROM CoreTracks");

            if (total <= 0) {
                return;
            }

            UserJob job = new UserJob (Catalog.GetString ("Refreshing Metadata"));
            job.SetResources (Resource.Cpu, Resource.Disk, Resource.Database);
            job.PriorityHints = PriorityHints.SpeedSensitive;
            job.Status = Catalog.GetString ("Scanning...");
            job.IconNames = new string [] { "system-search", "gtk-find" };
            job.Register ();

            HyenaSqliteCommand select_command = new HyenaSqliteCommand (
                String.Format (
                    "SELECT {0} FROM {1} WHERE {2}",
                    DatabaseTrackInfo.Provider.Select,
                    DatabaseTrackInfo.Provider.From,
                    DatabaseTrackInfo.Provider.Where
                )
            );

            int count = 0;
            using (var reader = ServiceManager.DbConnection.Query (select_command)) {
                while (reader.Read ()) {
                    DatabaseTrackInfo track = null;
                    try {
                        track = DatabaseTrackInfo.Provider.Load (reader);

                        if (track != null && track.Uri != null && track.Uri.IsFile) {
                            try {
                                using (var file = StreamTagger.ProcessUri (track.Uri)) {
                                    StreamTagger.TrackInfoMerge (track, file, true);
                                }
                            } catch (Exception e) {
                                Log.Warning (String.Format ("Failed to update metadata for {0}", track),
                                    e.GetType ().ToString (), false);
                            }

                            track.Save (false);
                            track.Artist.Save ();
                            track.Album.Save ();

                            job.Status = String.Format ("{0} - {1}", track.DisplayArtistName, track.DisplayTrackTitle);
                        }
                    } catch (Exception e) {
                        Log.Warning (String.Format ("Failed to update metadata for {0}", track), e.ToString (), false);
                    }

                    job.Progress = (double)++count / (double)total;
                }
            }

            if (ServiceManager.DbConnection.Query<int> ("SELECT count(*) FROM CoreConfiguration WHERE Key = 'MetadataVersion'") == 0) {
                Execute (String.Format ("INSERT INTO CoreConfiguration (EntryID, Key, Value) VALUES (null, 'MetadataVersion', {0})", CURRENT_METADATA_VERSION));
            } else {
                Execute (String.Format ("UPDATE CoreConfiguration SET Value = {0} WHERE Key = 'MetadataVersion'", CURRENT_METADATA_VERSION));
            }

            job.Finish ();
            ServiceManager.SourceManager.MusicLibrary.NotifyTracksChanged ();
        }

#endregion

        class DatabaseVersionTooHigh : ApplicationException
        {
            internal DatabaseVersionTooHigh (int currentVersion, int databaseVersion)
                : base (String.Format (
                "This version of Banshee was prepared to work with older database versions (=< {0}) thus it is too old to support the current version of the database ({1}).",
                currentVersion, databaseVersion))
            {
            }

            private DatabaseVersionTooHigh ()
            {
            }
        }

    }

    [SqliteFunction (Name = "BANSHEE_MIGRATE_PARTIAL", FuncType = FunctionType.Scalar, Arguments = 2)]
    internal class MigratePartialFunction : SqliteFunction
    {
        public override object Invoke (object[] args)
        {
            string library_path = (string)args[0];
            string filename_fragment = (string)args[1];
            string full_path = Paths.Combine (library_path, filename_fragment);
            return SafeUri.FilenameToUri (full_path);
        }
    }

    [SqliteFunction (Name = "BANSHEE_FIX_URI_ENCODING", FuncType = FunctionType.Scalar, Arguments = 1)]
    internal class FixUriEncodingFunction : SqliteFunction
    {
        public override object Invoke (object[] args)
        {
            string uri = (string)args[0];
            return SafeUri.FilenameToUri (SafeUri.UriToFilename (uri));
        }
    }
}
