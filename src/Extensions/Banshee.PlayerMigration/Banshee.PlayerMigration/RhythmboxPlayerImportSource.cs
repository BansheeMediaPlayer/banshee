//
// RhythmboxPlayerImportSource.cs
//
// Author:
//   Sebastian Dröge <slomo@circular-chaos.org>
//   Paul Lange <palango@gmx.de>
//
// Copyright (C) 2006 Sebastian Dröge
// Copyright (C) 2008 Paul Lange
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
using System.Data;
using System.IO;
using System.Xml;

using Mono.Unix;

using Hyena.Data.Sqlite;

using Banshee.Base;
using Banshee.Collection.Database;
using Banshee.Library;
using Banshee.ServiceStack;
using Banshee.Playlist;

namespace Banshee.PlayerMigration
{
    public sealed class RhythmboxPlayerImportSource : ThreadPoolImportSource
    {
        private static readonly SafeUri rhythmbox_db_uri = new SafeUri (Banshee.Base.Paths.Combine (
            Environment.GetFolderPath (Environment.SpecialFolder.Personal), ".local", "share", "rhythmbox", "rhythmdb.xml"
        ));

        private static readonly SafeUri rhythmbox_db_uri_old = new SafeUri (Banshee.Base.Paths.Combine (
            Environment.GetFolderPath (Environment.SpecialFolder.Personal), ".gnome2", "rhythmbox", "rhythmdb.xml"
        ));

        private int count, processed;

        protected override void ImportCore ()
        {
            LibraryImportManager import_manager = ServiceManager.Get<LibraryImportManager> ();

            SafeUri db_uri = rhythmbox_db_uri;
            // Check if library is located in the old place (.gnome2/rhythmbox/rhythmdb.db)
            if (!Banshee.IO.File.Exists (rhythmbox_db_uri)) {
                db_uri = rhythmbox_db_uri_old;
            }

            if (!IsValidXmlDocument (db_uri)) {
                LogError (SafeUri.UriToFilename (db_uri), "Rhythmbox library is corrupted.");
                return;
            }

            // Load Rhythmbox library
            Stream stream_db = Banshee.IO.File.OpenRead (db_uri);
            XmlDocument xml_doc_db = new XmlDocument ();
            xml_doc_db.Load (stream_db);
            XmlElement db_root = xml_doc_db.DocumentElement;
            stream_db.Close ();

            if (db_root == null || !db_root.HasChildNodes || db_root.Name != "rhythmdb") {
                LogError (SafeUri.UriToFilename (db_uri), "Unable to open Rhythmbox library.");
                return;
            }
            
            count = db_root.ChildNodes.Count;
            processed = 0;

            // Import Rhythmbox playlists if playlist file is available
            SafeUri rhythmbox_playlists_uri = new SafeUri (Banshee.Base.Paths.Combine (
                Environment.GetFolderPath (Environment.SpecialFolder.Personal),
                ".local", "share", "rhythmbox", "playlists.xml"
            ));

            bool playlists_available = Banshee.IO.File.Exists (rhythmbox_playlists_uri);

            // Look at the old location too
            if (!playlists_available) {
                rhythmbox_playlists_uri = new SafeUri (Banshee.Base.Paths.Combine (
                    Environment.GetFolderPath (Environment.SpecialFolder.Personal),
                    ".gnome2", "rhythmbox", "playlists.xml"
                ));

                playlists_available = Banshee.IO.File.Exists (rhythmbox_playlists_uri);
            }

            XmlElement playlists_root = null;
            
            if (playlists_available) {
                if (IsValidXmlDocument (rhythmbox_playlists_uri)) {
                    Stream stream_playlists = Banshee.IO.File.OpenRead (rhythmbox_playlists_uri);
                    XmlDocument xml_doc_playlists = new XmlDocument ();
                    xml_doc_playlists.Load (stream_playlists);
                    playlists_root = xml_doc_playlists.DocumentElement;
                    stream_playlists.Close ();
 
                    if (playlists_root == null || !playlists_root.HasChildNodes || playlists_root.Name != "rhythmdb-playlists") {
                        playlists_available = false;
                    } else {
                        count += playlists_root.ChildNodes.Count;
                    }
                } else {
                    LogError (SafeUri.UriToFilename (rhythmbox_playlists_uri), "Rhythmbox playlists are corrupted.");
                    playlists_available = false;
                }
            }

            ImportSongs (import_manager, db_root.SelectNodes ("/rhythmdb/entry[@type='song']"));

            //ImportPodcasts (import_manager, db_root.SelectNodes ("/rhythmdb/entry[@type='podcast-post']"));
            
            if (playlists_available) {
                ImportStaticPlaylists(playlists_root.SelectNodes ("/rhythmdb-playlists/playlist[@type='static']"));
            }

            import_manager.NotifyAllSources ();
        }

        private bool IsValidXmlDocument (SafeUri file)
        {
            XmlReaderSettings settings = new XmlReaderSettings ();
            settings.ValidationType = ValidationType.None;
            settings.ConformanceLevel = ConformanceLevel.Document;

            XmlReader validator = XmlReader.Create (Banshee.IO.File.OpenRead (file), settings);

            try {
                while (validator.Read ()) {
                }
            } catch (Exception) {
                return false;
            }
            return true;
        }

        private void ImportSongs (LibraryImportManager manager, XmlNodeList songs)
        {
            foreach (XmlElement song in songs) {
                if (CheckForCanceled ()) {
                    break;
                }

                processed++;
                    
                string title = String.Empty,
                       genre = String.Empty,
                       artist = String.Empty,
                       album = String.Empty;
                int year = 0,
                    rating = 0,
                    play_count = 0,
                    track_number = 0;
                DateTime date_added = DateTime.Now,
                         last_played = DateTime.MinValue;
                SafeUri uri = null;

                foreach (XmlElement child in song.ChildNodes) {
                    if (child == null || child.InnerText == null || child.InnerText == String.Empty) {
                        continue;
                    }

                    try {
                        switch (child.Name) {
                            case "title":
                                title = child.InnerText;
                                break;
                            case "genre":
                                genre = child.InnerText;
                                break;
                            case "artist":
                                artist = child.InnerText;
                                break;
                            case "album":
                                album = child.InnerText;
                                break;
                            case "track-number":
                                track_number = Int32.Parse (child.InnerText);
                                break;
                            case "location":
                                uri = new SafeUri (child.InnerText);
                                break;
                            case "date":
                                if (child.InnerText != "0") {
                                    year = (new DateTime (1, 1, 1).AddDays (Double.Parse (child.InnerText))).Year;
                                }
                                break;
                            case "rating":
                                rating = Int32.Parse (child.InnerText);
                                break;
                            case "play-count":
                                play_count = Int32.Parse (child.InnerText);
                                break;
                            case "last-played":
                                last_played =  Hyena.DateTimeUtil.ToDateTime (Int64.Parse (child.InnerText));
                                break;
                            case "first-seen":
                                date_added =  Hyena.DateTimeUtil.ToDateTime (Int64.Parse (child.InnerText));;
                                break;
                        }
                    } catch (Exception) {
                        // parsing InnerText failed
                    }
                }
                
                if (uri == null) {
                    continue;
                }

                UpdateUserJob (processed, count, artist, title);

                try {
                    DatabaseTrackInfo track = manager.ImportTrack (uri);
                    
                    if (track == null) {
                        LogError (SafeUri.UriToFilename (uri), Catalog.GetString ("Unable to import song."));
                        continue;
                    }

                    track.TrackTitle = title;
                    track.ArtistName = artist;
                    track.Genre = genre;
                    track.AlbumTitle = album;
                    track.TrackNumber = track_number;
                    track.Year = year;
                    track.DateAdded = date_added;
                    
                    track.Rating = (rating >= 0 && rating <= 5) ? rating : 0;
                    track.PlayCount = (play_count >= 0) ? play_count : 0;
                    track.LastPlayed = last_played;
                    
                    track.Save (false);
                } catch (Exception e) {
                    LogError (SafeUri.UriToFilename (uri), e);
                }
            }
        }

        // Commented out for now - this method doesn't handle actually subscribing to the feeds
        // (the most important task in migrating podcasts), and it looks like it imports podcast files
        // into the Music Library.
        /*private void ImportPodcasts(LibraryImportManager manager, XmlNodeList podcasts)
        {
            foreach (XmlElement entry in podcasts) {
                if (CheckForCanceled ()) {
                    break;
                }

                processed++;
                    
                string title = String.Empty, feed = String.Empty;
                SafeUri uri = null;

                foreach (XmlElement child in entry.ChildNodes) {
                    if (child == null || child.InnerText == null || child.InnerText == String.Empty) {
                        continue;
                    }

                    try {
                        switch (child.Name) {
                            case "title":
                                title = child.InnerText;
                                break;
                            case "album":
                                feed = child.InnerText;
                                break;
                            case "mountpoint":
                                uri = new SafeUri (child.InnerText);
                                break;
                        }
                    } catch (Exception) {
                        // parsing InnerText failed
                    }
                }
                
                if (uri == null) {
                    continue;
                }

                UpdateUserJob (processed, count, "", title);

                try {
                    DatabaseTrackInfo track = manager.ImportTrack (uri);
                    
                    if (track == null) {
                        LogError (SafeUri.UriToFilename (uri), Catalog.GetString ("Unable to import podcast."));
                        continue;
                    }
                    
                    track.TrackTitle = title;
                    track.AlbumTitle = feed;
                    track.Genre = "Podcast";
                    
                    track.Save (false);
                } catch (Exception e) {
                    LogError (SafeUri.UriToFilename (uri), e);
                }
            }
        }*/

        private void ImportStaticPlaylists(XmlNodeList playlists)
        {
            foreach (XmlElement list in playlists) {
                if (CheckForCanceled ()) {
                    break;
                }

                processed++;
                
                try {
                    string title = String.Empty;
                    if (list.HasAttribute ("name")) {
                        title = list.GetAttribute ("name");
                    }
                    
                    UpdateUserJob (processed, count, "", title);
        
                    PlaylistSource playlist = new PlaylistSource (title, ServiceManager.SourceManager.MusicLibrary);
                    playlist.Save ();
                    ServiceManager.SourceManager.MusicLibrary.AddChildSource (playlist);
        
                    
                    HyenaSqliteCommand insert_command = new HyenaSqliteCommand (String.Format (
                        @"INSERT INTO CorePlaylistEntries (PlaylistID, TrackID) VALUES ({0}, ?)", playlist.DbId));
        
                    foreach (XmlElement entry in list.ChildNodes) {
                        if (entry.Name != "location") {
                            continue;
                        }
                        
                        int track_id = ServiceManager.SourceManager.MusicLibrary.GetTrackIdForUri (entry.InnerText);
                        if (track_id > 0) {
                            ServiceManager.DbConnection.Execute (insert_command, track_id);
                        }
                    }
                            
                    playlist.Reload ();
                    playlist.NotifyUser ();
                } catch (Exception e) {
                    LogError ("", e);
                }
            }
        }
        
        public override bool CanImport {
            get { return Banshee.IO.File.Exists (rhythmbox_db_uri) || Banshee.IO.File.Exists (rhythmbox_db_uri_old); }
        }
        
        public override string Name {
            get { return Catalog.GetString ("Rhythmbox Music Player"); }
        }

        public override string [] IconNames {
            get { return new string [] { "rhythmbox", "system-search" }; }
        }
        
        public override int SortOrder {
            get { return 40; }
        }

        // Reserve strings for the iTunes importer in preparation for the forthcoming string freeze.
        public void ReservedStrings ()
        {
            Catalog.GetString ("iTunes Importer");
            Catalog.GetString ("_Import");
            Catalog.GetString ("Import _playlists");
            Catalog.GetString (@"Locate your ""{0}"" file...");
            Catalog.GetString (@"Locate ""{0}""");
            Catalog.GetString ("Locate iTunes Music Directory");
            Catalog.GetString(
                "The iTunes library refers to your music directory as \"{0}\" but " +
                "Banshee was not able to infer the location of this directory. Please locate it.");
            Catalog.GetString ("iTunes Media Player");
            Catalog.GetString (
                "Banshee is not familiar with this version of the iTunes library format." +
                " Importing may or may not work as expected, or at all. Would you like to attempt to import anyway?");
            Catalog.GetString ("Unable to import song.");
        }
    }
}
