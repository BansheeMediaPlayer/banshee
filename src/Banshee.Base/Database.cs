/***************************************************************************
 *  Database.cs
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@aaronbock.net>
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
using Banshee.Database;

namespace Banshee.Base
{
    public class BansheeDatabase : QueuedSqliteDatabase 
    {
        public BansheeDatabase(string dbFile) : base(dbFile)
        {
            Execute("PRAGMA synchronous = OFF");
            CreateTables();
            CompatabilityUpdate();
        }
        
        private void CreateTables()
        {
            if(!TableExists("Tracks")) {
                Console.WriteLine("Creating track table");
                Execute(@"
                CREATE TABLE Tracks (
                   	TrackID INTEGER PRIMARY KEY,
                   	Uri TEXT NOT NULL,
                   	MimeType TEXT,
                   	
                   	Artist TEXT,
                   	Performer TEXT,
                   	AlbumTitle TEXT,
                   	ReleaseDate Date,
                   	ASIN TEXT,
                   	Label TEXT,
                   	Title TEXT,
                   	Genre TEXT,
                   	Year INTEGER,
                   	
                   	TrackNumber INTEGER,
                   	TrackCount INTEGER,
                   	Duration INTEGER,
                   	
                   	TrackGain FLOAT,
                   	TrackPeak FLOAT,
                   	AlbumGain FLOAT,
                   	AlbumPeak FLOAT,
                   	
                   	Rating INTEGER,
                   	NumberOfPlays INTEGER,
                   	LastPlayedStamp INTEGER,
                   	DateAddedStamp INTEGER,
                   	
                   	RemoteLookupStatus INTEGER
                )");
            }
            
            if(!TableExists("Playlists")) {
                Console.WriteLine("Creating playlists table");
                Execute(@"
                CREATE TABLE Playlists (
                    PlaylistID INTEGER PRIMARY KEY,
                    Name TEXT NOT NULL
                )");
            }
            
            if(!TableExists("PlaylistEntries")) {
                Console.WriteLine("Creating playlistentries table");
                Execute(@"
                CREATE TABLE PlaylistEntries (
                    EntryID INTEGER PRIMARY KEY,
                    PlaylistID INTEGER NOT NULL,
                    TrackID INTEGER NOT NULL,
                    ViewOrder INTEGER NOT NULL DEFAULT 0
                )");
            }
        }
        
        private void CompatabilityUpdate()
        {
            try {
                QuerySingle("SELECT LastPlayedStamp FROM Tracks LIMIT 1");
            } catch(ApplicationException) {
                LogCore.Instance.PushDebug("Adding new database column", "LastPlayedStamp INTEGER");
                Execute("ALTER TABLE Tracks ADD LastPlayedStamp INTEGER");
            }
            
            try {
                QuerySingle("SELECT DateAddedStamp FROM Tracks LIMIT 1");
            } catch(ApplicationException) {
                LogCore.Instance.PushDebug("Adding new database column", "DateAddedStamp INTEGER");
                Execute("ALTER TABLE Tracks ADD DateAddedStamp INTEGER");
            }
            
            try {
                QuerySingle("SELECT RemoteLookupStatus FROM Tracks LIMIT 1");
            } catch(ApplicationException) {
                LogCore.Instance.PushDebug("Adding new database column", "RemoteLookupStatus INTEGER");
                Execute("ALTER TABLE Tracks ADD RemoteLookupStatus INTEGER");
            }            
            
            try {
                QuerySingle("SELECT ViewOrder FROM PlaylistEntries LIMIT 1");
            } catch(ApplicationException) {
                LogCore.Instance.PushDebug("Adding new database column", "ViewOrder INTEGER");
                Execute("ALTER TABLE PlaylistEntries ADD ViewOrder INTEGER NOT NULL DEFAULT 0");
            }
        }
    }
}
