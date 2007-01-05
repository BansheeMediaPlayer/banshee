/***************************************************************************
 *  LibraryTrackInfo.cs
 *
 *  Copyright (C) 2005 Novell
 *  Written by Aaron Bockover (aaron@aaronbock.net)
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
using System.Text.RegularExpressions;
using System.IO;
using System.Data;
using System.Collections;
using System.Threading;
using System.Text;

using Banshee.Database;
using Banshee.Configuration.Schema;

namespace Banshee.Base
{
    public class LibraryTrackInfo : TrackInfo
    {
        public static int GetId(SafeUri lookup)
        {
            try {
                return Convert.ToInt32(Globals.Library.Db.QuerySingle(new DbCommand(@"
                    SELECT TrackID
                    FROM Tracks
                    WHERE Uri = :uri
                    LIMIT 1", 
                    "uri", lookup.AbsoluteUri
                )));
            } catch(Exception) {
                return 0;
            }
        }
        
        protected LibraryTrackInfo()
        {
            CanSaveToDatabase = true;
        }
    
        private void CheckIfExists(SafeUri uri)
        {
              bool exists = false;
              try {
                exists = Globals.Library.TracksFnKeyed[Library.MakeFilenameKey(uri)] != null;
              } catch(Exception) {
                exists = false;
              }
              
              if(exists) {
                  // TODO: we should actually probably take this as a hint to
                  // reparse metadata
                  throw new ApplicationException("Song is already in library");
              } 
        }

        private void CheckIfExists(string filename)
        {
            CheckIfExists(new SafeUri(filename));
        }

        private string MoveToPlace(string old_filename, bool initial_import)
        {
            bool in_library = old_filename.StartsWith (Globals.Library.Location + Path.DirectorySeparatorChar);
//            Console.WriteLine ("\"{0}\" in \"{1}\": {2}", old_filename, Core.Library.Location, in_library);

            if (initial_import && !in_library) {
                bool copy = LibrarySchema.CopyOnImport.Get();

                if (copy) {
                    string new_filename = FileNamePattern.BuildFull(this,
                        Path.GetExtension (old_filename).Substring(1));
                    CheckIfExists(new_filename);

                    try {

//                        Console.WriteLine ("!in_library: {0}", new_filename);

//                        Console.WriteLine ("if (File.Exists (\"{0}\"): {1}", new_filename, File.Exists (new_filename));
                        if (File.Exists (new_filename))
                            return null;
//                        Console.WriteLine ("File.Copy(\"{0}\", \"{1}\", false);", old_filename, new_filename);
                        File.Copy(old_filename, new_filename, false);
                        return new_filename;
                    } catch {
                        return null;
                    }
                }
            }

            if (in_library) {
                bool move = LibrarySchema.MoveOnInfoSave.Get();
    
                if (move) {
                    string new_filename = FileNamePattern.BuildFull(this,
                        Path.GetExtension (old_filename).Substring(1));
//                    Console.WriteLine ("in_library: {0}", new_filename);
                    CheckIfExists(new_filename);

                    try {
                        if (File.Exists (new_filename))
                            return null;

                        if (old_filename != new_filename) {
                            // Move and set uri.
                            File.Move (old_filename, new_filename);
    
                            // Delete old directories if empty.
                            try {
                                string old_dir = Path.GetDirectoryName (old_filename);
                                while (old_dir != null && old_dir != String.Empty) {
                                    Directory.Delete (old_dir);
                                    old_dir = Path.GetDirectoryName (old_dir);
                                }
                            } catch {}

                            return new_filename;
                        }
                    } catch {
                        return null;
                    }
                }
            }
            return null;
        }

        public LibraryTrackInfo(SafeUri uri, string artist, string album, 
           string title, string genre, uint track_number, uint track_count,
           int year, TimeSpan duration, string asin, RemoteLookupStatus remote_lookup_status)
        {
            this.uri = uri;
            track_id = 0;
    
            mimetype = null;
            
            this.artist = artist;
            this.album = album;
            this.title = title;
            this.genre = genre;
            this.track_number = track_number;
            this.track_count = track_count;
            this.year = year;
            this.duration = duration;
            this.asin = asin;
            this.remote_lookup_status = remote_lookup_status;
            
            this.date_added = DateTime.Now;
            
            CheckIfExists(uri);
            
            SaveToDatabase(true);
            Globals.Library.SetTrack(track_id, this);
            
            PreviousTrack = Gtk.TreeIter.Zero;
        }
        
        public LibraryTrackInfo(SafeUri uri, TrackInfo track) : this(
            uri, track.Artist, track.Album, track.Title, track.Genre,
            track.TrackNumber, track.TrackCount, track.Year, track.Duration, 
            track.Asin, track.RemoteLookupStatus)
        {
        }
    
        public LibraryTrackInfo(string filename) : this()
        {
            uri = new SafeUri(filename);
            CheckIfExists(uri);
            if(!LoadFromDatabase(uri)) {
                LoadFromFile(filename);
                string new_filename = MoveToPlace(filename, true);
                uri = new SafeUri(new_filename != null ? new_filename : filename);
                CheckIfExists(uri);
                SaveToDatabase(true);
            }

            Globals.Library.SetTrack(track_id, this);

            PreviousTrack = Gtk.TreeIter.Zero;
        }
        
        public LibraryTrackInfo(IDataReader reader) : this()
        {
            LoadFromDatabaseReader(reader);
            Globals.Library.SetTrack(track_id, this);
            PreviousTrack = Gtk.TreeIter.Zero;
        }
        
        private void ParseUri(string path)
        {
            artist = String.Empty;
            album = String.Empty;
            title = String.Empty;
            track_number = 0;
            Match match;

            SafeUri uri = new SafeUri(path);
            string fileName = path;
            if(uri.IsLocalPath) {
                fileName = uri.AbsolutePath;
            }
            
            fileName = Path.GetFileNameWithoutExtension(fileName);
        
            match = Regex.Match(fileName, @"^(\d+)\.? *(.*)$");
            if(match.Success) {
		try {
		    track_number = Convert.ToUInt32(match.Groups[1].ToString());
		    fileName = match.Groups[2].ToString().Trim();
		    if(fileName.Length == 0) {
			/* If we only have a number in the filename,
			 * use that for the title too */
			fileName = Convert.ToString(track_number);
		    }
		} catch {
		}
            }

            /* Artist - Album - Title */
            match = Regex.Match(fileName, @"\s*(.*)-\s*(.*)-\s*(.*)$");
            if(match.Success) {
                artist = match.Groups[1].ToString();
                album = match.Groups[2].ToString();
                title = match.Groups[3].ToString();
            } else {
                /* Artist - Title */
                match = Regex.Match(fileName, @"\s*(.*)-\s*(.*)$");
                if(match.Success) {
                    artist = match.Groups[1].ToString();
                    title = match.Groups[2].ToString();
                } else {
                    /* Title */
                    title = fileName;
                }
            }

            while (path != null && path != String.Empty) {
                path = Path.GetDirectoryName(path);
                fileName = Path.GetFileName (path);
                if (album == String.Empty) {
                    album = fileName;
                    continue;
                }
                if (artist == String.Empty) {
                    artist = fileName;
                    continue;
                }
                break;
            }
            
            artist = artist.Trim();
            album = album.Trim();
            title = title.Trim();
            
            if(artist.Length == 0)
                artist = /*"Unknown Artist"*/ null;
            if(album.Length == 0)
                album = /*"Unknown Album"*/ null;
            if(title.Length == 0)
                title = /*"Unknown Title"*/ null;
        }
        
        private static DbCommand BuildCommand(int id, string table, params object [] args)
        {
            if(id <= 0) {
                return BuildInsertCommand(table, args);
            }
            
            return BuildUpdateCommand(id, table, args);
        }
        
        private static DbCommand BuildInsertCommand(string table, params object [] args)
        {
            StringBuilder statement = new StringBuilder();
            StringBuilder columns = new StringBuilder();
            StringBuilder values = new StringBuilder();
            
            statement.Append("INSERT INTO ");
            statement.Append(table);
            
            columns.Append(" (");
            values.Append(" VALUES (");
            
            for(int i = 0; i < args.Length; i += 2) {
                string name = (string)args[i];
                
                columns.Append(name);
                if(i < args.Length - 2) {
                    columns.Append(", ");
                }
                
                values.Append(":");
                values.Append(name);
                if(i < args.Length - 2) {
                    values.Append(", ");
                }
            }
            
            columns.Append(")");
            values.Append(")");
            
            statement.Append(columns.ToString());
            statement.Append(values.ToString());
            
            return new DbCommand(statement.ToString(), args);
        }
        
        private static DbCommand BuildUpdateCommand(int id, string table, params object [] args)
        {
            StringBuilder statement = new StringBuilder();
            
            statement.Append("UPDATE ");
            statement.Append(table);
            statement.Append(" SET ");
            
            for(int i = 0; i < args.Length; i += 2) {
                string name = (string)args[i];
                
                statement.Append(name);
                statement.Append(" = :");
                statement.Append(name);
                
                if(i < args.Length - 2) {
                    statement.Append(", ");
                }
            }
            
            statement.Append(" WHERE TrackID = :TrackID");
            return new DbCommand(statement.ToString(), args);
        }
        
        private void SaveToDatabase(bool retryIfFail)
        {
            DbCommand command = BuildCommand(track_id, "Tracks",
                "TrackID", track_id <= 0 ? null : (object)track_id,
                "Uri", uri.AbsoluteUri,
                "MimeType", mimetype, 
                "Artist", artist, 
                "Performer", performer, 
                "AlbumTitle", album,
                "ASIN", asin,
                "Label", label,
                "Title", title, 
                "Genre", genre, 
                "Year", year,
                "DateAddedStamp", DateTimeUtil.FromDateTime(date_added), 
                "TrackNumber", track_number, 
                "TrackCount", track_count, 
                "Duration", (int)duration.TotalSeconds, 
                /*"TrackGain", track_gain, 
                "TrackPeak", track_peak, 
                "AlbumGain", album_gain, 
                "AlbumPeak", album_peak,*/ 
                "Rating", rating, 
                "NumberOfPlays", play_count, 
                "LastPlayedStamp", DateTimeUtil.FromDateTime(last_played),
                "RemoteLookupStatus", (int)remote_lookup_status);

            try {
                Globals.Library.Db.Execute(command);
            } catch(Exception e) {
                throw new Banshee.Library.DatabaseWriteException(e, command.CommandText);
            }

            if(track_id <= 0) { 
               track_id = GetId(uri); /* OPTIMIZE! Seems like an unnecessary query */
            }
        }
        
        private bool LoadFromDatabase(object id)
        {
            IDataReader reader = Globals.Library.Db.Query(new DbCommand(@"
                SELECT * 
                FROM Tracks
                WHERE Uri = :uri
                    OR TrackID = :uri
                LIMIT 1", 
                "uri", id is string ? id as string : Convert.ToString(id)
            ));
            
            if(reader == null)
                return false;
            
            if(!reader.Read())
                return false;
                
            LoadFromDatabaseReader(reader);
            
            return true;
        }
        
        private void LoadFromDatabaseReader(IDataReader reader)
        {
            track_id = Convert.ToInt32(reader["TrackID"]);

            uri = new SafeUri(reader["Uri"] as string, true);
            mimetype = reader["MimeType"] as string;
            
            album = reader["AlbumTitle"] as string;
            artist = reader["Artist"] as string;
            performer = reader["Performer"] as string;
            title = reader["Title"] as string;
            genre = reader["Genre"] as string;
            asin = reader["ASIN"] as string;
            
            if(genre == "Unknown") {
                genre = String.Empty;
            }
            
            year = Convert.ToInt32(reader["Year"]);
            track_number = Convert.ToUInt32(reader["TrackNumber"]);
            track_count = Convert.ToUInt32(reader["TrackCount"]);
            rating = Convert.ToUInt32(reader["Rating"]);
            play_count = Convert.ToUInt32(reader["NumberOfPlays"]);
            
            remote_lookup_status = (RemoteLookupStatus)Convert.ToInt32(reader["RemoteLookupStatus"]);
            
            duration = new TimeSpan(Convert.ToInt64(reader["Duration"]) * TimeSpan.TicksPerSecond);
            
            last_played = DateTime.MinValue;
            date_added = DateTime.MinValue;
            
            long temp_stamp = Convert.ToInt64(reader["LastPlayedStamp"]);
            if(temp_stamp > 0) {
                last_played = DateTimeUtil.ToDateTime(temp_stamp);
            }
            
            temp_stamp = Convert.ToInt64(reader["DateAddedStamp"]);
            if(temp_stamp > 0) {
                date_added = DateTimeUtil.ToDateTime(temp_stamp);
            }
        }
		
        private void LoadFromFile(string filename)
        {
            ParseUri(filename);
            track_id = 0;            
            StreamTagger.TrackInfoMerge(this, StreamTagger.ProcessUri(Uri));
            this.date_added = DateTime.Now;
        }

        public override void Save()
        {
            try {
                string new_filename = MoveToPlace (uri.AbsolutePath, false);
                if (new_filename != null) {
                    this.uri = new SafeUri(new_filename);
                }
            } catch {}
            
            SaveToDatabase(true);

            OnChanged();
        }
        
        public override void IncrementPlayCount()
        {
            play_count++;
            last_played = DateTime.Now;
            
            /*Statement query = new Update("Tracks",
                "NumberOfPlays", PlayCount, 
                "LastPlayed", last_played.ToString(ci.DateTimeFormat)) +
                new Where(new Compare("TrackID", Op.EqualTo, track_id));
                //new Limit(1);

            Core.Library.Db.Execute(query);*/
            
            Save();
        }
        
        protected override void SaveRating()
        {
            /*Statement query = new Update("Tracks",
                "Rating", rating) +
                new Where(new Compare("TrackID", Op.EqualTo, track_id));
            Core.Library.Db.Execute(query);*/
            Save();
        }
    }
}
