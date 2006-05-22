/***************************************************************************
 *  PlaylistSource.cs
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@abock.org>
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
using System.Collections;
using Mono.Unix;

using Banshee.Base;

namespace Banshee.Sources
{
    public class PlaylistSource : Source
    {
        private static ArrayList playlists = new ArrayList();
        public static IEnumerable Playlists {
            get {
                return playlists;
            }
        }
        
        public static int PlaylistCount {
            get {
                return playlists.Count;
            }
        }
        
        private ArrayList tracks = new ArrayList();
        private Queue remove_queue = new Queue();
        private Queue append_queue = new Queue();
        private int id;

        public int Id {
            get { return id; }
        }
    
        public PlaylistSource() : this(0)
        {
        }
    
        public PlaylistSource(int id) : base(Catalog.GetString("New Playlist"), 500)
        {
            this.id = id;
            
            if(id < 0) {
                return;
            } else if(id == 0) {
                CreateNewPlaylist();
            } else {
                LoadFromDatabase();
            }
            
            Globals.Library.TrackRemoved += OnLibraryTrackRemoved;
            playlists.Add(this);
        }
        
        private void CreateNewPlaylist()
        {
            id = Globals.Library.Db.Execute(String.Format(
                @"INSERT INTO Playlists
                    VALUES (NULL, '{0}')",
                    Sql.Statement.EscapeQuotes(Name))
            );
        }
        
        private void LoadFromDatabase()
        {   
            Name = (string)Globals.Library.Db.QuerySingle(String.Format(
                @"SELECT Name
                    FROM Playlists
                    WHERE PlaylistID = '{0}'",
                    id
            ));
         
            // check to see if ViewOrder has ever been set, if not, perform
            // a default ordering as a compatibility update
            if(Convert.ToInt32(Globals.Library.Db.QuerySingle(String.Format(
                @"SELECT COUNT(*) 
                    FROM PlaylistEntries
                    WHERE PlaylistID = '{0}'
                        AND ViewOrder > 0",
                    id))) <= 0) {
                Console.WriteLine("Performing compatibility update on playlist '{0}'", Name);
                Globals.Library.Db.Execute(String.Format(
                    @"UPDATE PlaylistEntries
                        SET ViewOrder = (ROWID -
                            (SELECT COUNT(*) 
                                FROM PlaylistEntries
                                WHERE PlaylistID < '{0}'))
                        WHERE PlaylistID = '{0}'",
                        id
                ));
            }
   
            IDataReader reader = Globals.Library.Db.Query(String.Format(
                @"SELECT TrackID 
                    FROM PlaylistEntries
                    WHERE PlaylistID = '{0}'
                    ORDER BY ViewOrder",
                    id
            ));
            
            lock(TracksMutex) {
                while(reader.Read()) {
                    tracks.Add(Globals.Library.Tracks[Convert.ToInt32(reader[0])]);
                }
            }
            
            reader.Dispose();
        }
        
        protected override bool UpdateName(string oldName, string newName)
        {
            if(oldName.Equals(newName)) {
                return false;
            }
            
            if(PlaylistUtil.PlaylistExists(newName)) {
                LogCore.Instance.PushWarning(
                    Catalog.GetString("Cannot Rename Playlist"),
                    Catalog.GetString("A playlist with this name already exists. Please choose another name."));
                return false;
            }
            
            string query = String.Format(
                @"UPDATE Playlists
                    SET Name = '{0}'
                    WHERE PlaylistID = '{1}'",
                    Sql.Statement.EscapeQuotes(newName),
                    id
            );
          
            try {
                Globals.Library.Db.Execute(query);
                Name = newName;
                return true;
            } catch(Exception) {
                return false;
            }
        }
 
        public override void AddTrack(TrackInfo track)
        {
            if(track is LibraryTrackInfo) {
                lock(TracksMutex) {
                    tracks.Add(track);
                    append_queue.Enqueue(track);
                }
                OnUpdated();
            }
        }

        public void ClearTracks()
        {
            tracks.Clear();
        }
        
        public override void RemoveTrack(TrackInfo track)
        {
            lock(TracksMutex) {
                tracks.Remove(track);
                remove_queue.Enqueue(track);
            }
        }

        public bool ContainsTrack(TrackInfo track)
        {
            return tracks.Contains(track);
        }
        
        public void Delete()
        {
            Globals.Library.Db.Execute(String.Format(
                @"DELETE FROM PlaylistEntries
                    WHERE PlaylistID = '{0}'",
                    id
            ));
            
            Globals.Library.Db.Execute(String.Format(
                @"DELETE FROM Playlists
                    WHERE PlaylistID = '{0}'",
                    id
            ));
            
            tracks.Clear();
            append_queue.Clear();
            remove_queue.Clear();
            
            SourceManager.RemoveSource(this);
            playlists.Remove(this);
        }
        
        public override void Commit()
        {
            if(remove_queue.Count > 0) {
                lock(TracksMutex) {
                    while(remove_queue.Count > 0) {
                        TrackInfo track = remove_queue.Dequeue() as TrackInfo;
                        Globals.Library.Db.Execute(String.Format(
                            @"DELETE FROM PlaylistEntries
                                WHERE PlaylistID = '{0}'
                                AND TrackID = '{1}'",
                                id, track.TrackId
                        ));
                    }
                }
            }
            
            if(append_queue.Count > 0) {
                lock(TracksMutex) {
                    while(append_queue.Count > 0) {
                        TrackInfo track = append_queue.Dequeue() as TrackInfo;
                        Globals.Library.Db.Execute(String.Format(
                            @"INSERT INTO PlaylistEntries 
                                VALUES (NULL, '{0}', '{1}', (
                                    SELECT CASE WHEN MAX(ViewOrder)
                                        THEN MAX(ViewOrder) + 1
                                        ELSE 1 END
                                    FROM PlaylistEntries 
                                    WHERE PlaylistID = '{0}')
                                )", id, track.TrackId
                        ));
                    }
                }
            }
        }
        
        public override void Reorder(TrackInfo track, int position)
        {
            lock(TracksMutex) {
                int sql_position = 1;
            
                if(position > 0) {
                    TrackInfo sibling = tracks[position] as TrackInfo;
                    if(sibling == track || sibling == null) {
                        return;
                    }
                    
                    sql_position = Convert.ToInt32(Globals.Library.Db.QuerySingle(String.Format(
                        @"SELECT ViewOrder
                            FROM PlaylistEntries
                            WHERE PlaylistID = '{0}'
                                AND TrackID = '{1}'
                            LIMIT 1", id, sibling.TrackId)
                    ));
                } else if(tracks[position] == track) {
                    return;
                } 
                
                Globals.Library.Db.Execute(String.Format(
                    @"UPDATE PlaylistEntries
                        SET ViewOrder = ViewOrder + 1
                        WHERE PlaylistID = '{0}'
                            AND ViewOrder >= '{1}'",
                    id, sql_position
                ));
                
                Globals.Library.Db.Execute(String.Format(
                    @"UPDATE PlaylistEntries
                        SET ViewOrder = '{1}'
                        WHERE PlaylistID = '{0}'
                            AND TrackID = '{2}'",
                    id, sql_position, track.TrackId
                ));
                
                tracks.Remove(track);
                tracks.Insert(position, track);
            }
        }
        
        private void OnLibraryTrackRemoved(object o, LibraryTrackRemovedArgs args)
        {
            if(args.Track != null) {
                if(tracks.Contains(args.Track)) {
                    RemoveTrack(args.Track);
                    
                    if(Count == 0) {
                        Delete();
                    } else {
                        Commit();
                    }
                }
                
                return;
            } else if(args.Tracks == null) {
                return;
            }
            
            int removed_count = 0;
            
            lock(TracksMutex) {
                foreach(TrackInfo track in args.Tracks) {
                    if(tracks.Contains(track)) {
                        tracks.Remove(track);
                        remove_queue.Enqueue(track);
                        removed_count++;
                    }
                }
            }
            
            if(removed_count > 0) {
                if(Count == 0) {
                    Delete();
                } else {
                    Commit();
                }
            }
        }
        
        public override IEnumerable Tracks {
            get {
                return tracks;
            }
        }
        
        public override object TracksMutex {
            get { return tracks.SyncRoot; }
        }
        
        public override int Count {
            get {
                return tracks.Count;
            }
        }  
        
        public override Gdk.Pixbuf Icon {
            get {
                return IconThemeUtils.LoadIcon(22, "source-playlist");
            }
        }
    }
    
    public static class PlaylistUtil
    {
        public static void LoadSources()
        {
            IDataReader reader = Globals.Library.Db.Query("SELECT PlaylistID FROM Playlists");
            while(reader.Read()) {
                PlaylistSource playlist = new PlaylistSource(Convert.ToInt32(reader[0]));
                SourceManager.AddSource(playlist);
            }
            reader.Dispose();
        }
    
        internal static int GetPlaylistID(string name)
        {
            string query = String.Format(
                @"SELECT PlaylistID
                    FROM Playlists
                    WHERE Name = '{0}'
                    LIMIT 1",
                    Sql.Statement.EscapeQuotes(name)
            );
            
            try {
                object result = Globals.Library.Db.QuerySingle(query);
                int id = Convert.ToInt32(result);
                return id;
            } catch(Exception) {
                return 0;
            }
        }
        
        internal static bool PlaylistExists(string name)
        {
            return GetPlaylistID(name) > 0;
        }
        
        internal static string PostfixDuplicate(string prefix)
        {
            string name = prefix;
            for(int i = 1; true; i++) {
                if(!PlaylistExists(name)) {
                    return name;
                }
                
                name = prefix + " " + i;
            }
        }
        
        public static string UniqueName {
            get {
                return PostfixDuplicate(Catalog.GetString("New Playlist"));
            }
        }
        
        public static string GoodUniqueName(IEnumerable tracks)
        {
            ArrayList names = new ArrayList();
            Hashtable groups = new Hashtable();
            
            int track_count = 0;
            
            foreach(TrackInfo ti in tracks) {
                bool haveArtist = ti.Artist != null && !ti.Artist.Equals(String.Empty);
                bool haveAlbum = ti.Album != null && !ti.Album.Equals(String.Empty);
            
                if(haveArtist && haveAlbum) {
                    names.Add(ti.Artist + " - " + ti.Album);
                } else if(haveArtist) {
                    names.Add(ti.Artist);
                } else if(haveAlbum) {
                    names.Add(ti.Album);
                } else {
                    names.Add(Catalog.GetString("New Playlist"));
                }
                
                track_count++;
            }
            
            if(track_count == 0) {
                names.Add(Catalog.GetString("New Playlist"));
            }
                
            names.Sort();
            groups[names[0]] = 1;
            
            for(int i = 1; i < names.Count; i++) {
                bool match = false;
                foreach(string key in groups.Keys) {
                    if(names[i].Equals(key)) {
                        groups[key] = ((int)groups[key]) + 1;
                        match = true;
                        break;
                    }
                }
            
                if(match) {
                    continue;
                }
                
                groups[names[i]] = 1;
            }
            
            string bestMatch = String.Empty;
            int maxValue = 0;
            
            foreach(int count in groups.Values) {
                if(count > maxValue) {
                    maxValue = count;
                    foreach(string key in groups.Keys) {
                        if((int)groups[key] == maxValue) {
                            bestMatch = key;
                            break;
                        }
                    }
                }
            }
            
            if(bestMatch.Equals(String.Empty)) {
                return UniqueName;
            }
                
            return PostfixDuplicate(bestMatch);
        }
    }
}
