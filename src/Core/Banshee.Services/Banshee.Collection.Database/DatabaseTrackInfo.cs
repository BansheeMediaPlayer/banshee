//
// DatabaseTrackInfo.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2007 Novell, Inc.
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
using System.Collections.Generic;
using System.Data;
using System.IO;

using Hyena;
using Hyena.Data;
using Hyena.Data.Sqlite;
using Hyena.Query;

using Banshee.Base;
using Banshee.Configuration.Schema;
using Banshee.Database;
using Banshee.Query;
using Banshee.Sources;
using Banshee.ServiceStack;
using Banshee.Streaming;

// Disabling "is never used" warnings here because there are a lot
// of properties/fields that are set via reflection at the database
// layer - that is, they really are used, but the compiler doesn't
// think so.

#pragma warning disable 0169

namespace Banshee.Collection.Database
{
    public class DatabaseTrackInfo : TrackInfo
    {
        private static DatabaseTrackModelProvider<DatabaseTrackInfo> provider;

        public static DatabaseTrackModelProvider<DatabaseTrackInfo> Provider {
            get { return provider ?? (provider = new DatabaseTrackModelProvider<DatabaseTrackInfo> (ServiceManager.DbConnection)); }
        }

        private bool artist_changed = false, album_changed = false;
        
        public DatabaseTrackInfo () : base ()
        {
        }

        public DatabaseTrackInfo (DatabaseTrackInfo original) : base ()
        {
            Provider.Copy (original, this);
        }

        public override void OnPlaybackFinished (double percentCompleted)
        {
            if (ProviderRefresh()) {
                base.OnPlaybackFinished (percentCompleted);
                Save (true, BansheeQuery.ScoreField, BansheeQuery.SkipCountField, BansheeQuery.LastSkippedField, 
                    BansheeQuery.PlayCountField, BansheeQuery.LastPlayedField);
            }
        }

        public override bool TrackEqual (TrackInfo track)
        {
            if (PrimarySource != null && PrimarySource.TrackEqualHandler != null) {
                return PrimarySource.TrackEqualHandler (this, track);
            }
            
            DatabaseTrackInfo db_track = track as DatabaseTrackInfo;
            if (db_track == null) {
                return base.TrackEqual (track);
            }
            return TrackEqual (this, db_track);
        }

        public override string ArtworkId {
            get {
                if (PrimarySource != null && PrimarySource.TrackArtworkIdHandler != null) {
                    return PrimarySource.TrackArtworkIdHandler (this);
                }
                return base.ArtworkId;
            }
        }

        public static bool TrackEqual (DatabaseTrackInfo a, DatabaseTrackInfo b)
        {
            return a != null && b != null && a.TrackId == b.TrackId;
        }
        
        public DatabaseArtistInfo Artist {
            get { return DatabaseArtistInfo.FindOrCreate (ArtistName, ArtistNameSort); }
        }

        public DatabaseAlbumInfo Album {
            get { return DatabaseAlbumInfo.FindOrCreate (DatabaseArtistInfo.FindOrCreate (AlbumArtist, AlbumArtistSort), AlbumTitle, AlbumTitleSort, IsCompilation); }
        }

        private static bool notify_saved = true;
        public static bool NotifySaved {
            get { return notify_saved; }
            set { notify_saved = value; }
        }

        public override void Save ()
        {
            Save (NotifySaved);
        }

        // Changing these fields shouldn't change DateUpdated (which triggers file save)
        private static HashSet<QueryField> transient_fields = new HashSet<QueryField> {
            BansheeQuery.ScoreField,
            BansheeQuery.SkipCountField,
            BansheeQuery.LastSkippedField,
            BansheeQuery.PlayCountField,
            BansheeQuery.LastPlayedField,
            BansheeQuery.RatingField
        };

        public void Save (bool notify, params QueryField [] fields_changed)
        {
            // If either the artist or album changed, 
            if (ArtistId == 0 || AlbumId == 0 || artist_changed == true || album_changed == true) {
                DatabaseArtistInfo artist = Artist;
                ArtistId = artist.DbId;
           
                DatabaseAlbumInfo album = Album;
                AlbumId = album.DbId;
                
                // TODO get rid of unused artists/albums
            }
            
            if (fields_changed.Length == 0 || !transient_fields.IsSupersetOf (fields_changed)) {
                DateUpdated = DateTime.Now;
            }

            bool is_new = (TrackId == 0);
            if (is_new) {
                DateAdded = DateUpdated = DateTime.Now;
            }

            ProviderSave ();

            if (notify && PrimarySource != null) {
                if (is_new) {
                    PrimarySource.NotifyTracksAdded ();
                } else {
                    PrimarySource.NotifyTracksChanged (fields_changed);
                }
            }
        }
        
        protected virtual void ProviderSave ()
        {
            Provider.Save (this);
        }

        public void Refresh ()
        {
            ProviderRefresh ();
        }
        
        protected virtual bool ProviderRefresh ()
        {
            return Provider.Refresh (this);
        }
        
        private int track_id;
        [DatabaseColumn ("TrackID", Constraints = DatabaseColumnConstraints.PrimaryKey)]
        public int TrackId {
            get { return track_id; }
            protected set { track_id = value; }
        }

        private int primary_source_id;
        [DatabaseColumn ("PrimarySourceID")]
        public int PrimarySourceId {
            get { return primary_source_id; }
            set { primary_source_id = value; }
        }

        public PrimarySource PrimarySource {
            get { return PrimarySource.GetById (primary_source_id); }
            set { PrimarySourceId = value.DbId; }
        }

        private int artist_id;
        [DatabaseColumn ("ArtistID")]
        public int ArtistId {
            get { return artist_id; }
            set { artist_id = value; }
        }
        
        private int album_id;
        [DatabaseColumn ("AlbumID")]
        public int AlbumId {
            get { return album_id; }
            set { album_id = value; }
        }

        [VirtualDatabaseColumn ("Name", "CoreArtists", "ArtistID", "ArtistID")]
        protected string ArtistNameField {
            get { return ArtistName; }
            set { base.ArtistName = value; }
        }

        public override string ArtistName {
            get { return base.ArtistName; }
            set {
                value = CleanseString (value, ArtistName);
                if (value == null)
                    return;

                base.ArtistName = value;
                artist_changed = true;
            }
        }

        [VirtualDatabaseColumn ("NameSort", "CoreArtists", "ArtistID", "ArtistID")]
        protected string ArtistNameSortField {
            get { return ArtistNameSort; }
            set { base.ArtistNameSort = value; }
        }

        public override string ArtistNameSort {
            get { return base.ArtistNameSort; }
            set {
                value = CleanseString (value, ArtistNameSort);
                if (value == null)
                    return;

                base.ArtistNameSort = value;
                artist_changed = true;
            }
        }

        [VirtualDatabaseColumn ("Title", "CoreAlbums", "AlbumID", "AlbumID")]
        protected string AlbumTitleField {
            get { return AlbumTitle; }
            set { base.AlbumTitle = value; }
        }

        public override string AlbumTitle {
            get { return base.AlbumTitle; }
            set {
                value = CleanseString (value, AlbumTitle);
                if (value == null)
                    return;

                base.AlbumTitle = value;
                album_changed = true;
            }
        }

        [VirtualDatabaseColumn ("TitleSort", "CoreAlbums", "AlbumID", "AlbumID")]
        protected string AlbumTitleSortField {
            get { return AlbumTitleSort; }
            set { base.AlbumTitleSort = value; }
        }

        public override string AlbumTitleSort {
            get { return base.AlbumTitleSort; }
            set {
                value = CleanseString (value, AlbumTitleSort);
                if (value == null)
                    return;

                base.AlbumTitleSort = value;
                album_changed = true;
            }
        }

        [VirtualDatabaseColumn ("ArtistName", "CoreAlbums", "AlbumID", "AlbumID")]
        protected string AlbumArtistField {
            get { return AlbumArtist; }
            set { base.AlbumArtist = value; }
        }

        public override string AlbumArtist {
            get { return base.AlbumArtist; }
            set {
                value = CleanseString (value, AlbumArtist);
                if (value == null)
                    return;

                base.AlbumArtist = value;
                album_changed = true;
            }
        }
        
        [VirtualDatabaseColumn ("ArtistNameSort", "CoreAlbums", "AlbumID", "AlbumID")]
        protected string AlbumArtistSortField {
            get { return AlbumArtistSort; }
            set { base.AlbumArtistSort = value; }
        }

        public override string AlbumArtistSort {
            get { return base.AlbumArtistSort; }
            set {
                value = CleanseString (value, AlbumArtistSort);
                if (value == null)
                    return;

                base.AlbumArtistSort = value;
                album_changed = true;
            }
        }
        
        [VirtualDatabaseColumn ("IsCompilation", "CoreAlbums", "AlbumID", "AlbumID")]
        protected bool IsCompilationField {
            get { return IsCompilation; }
            set { base.IsCompilation = value; }
        }

        public override bool IsCompilation {
            get { return base.IsCompilation; }
            set {
                base.IsCompilation = value;
                album_changed = true;
            }
        }
        
        private static string CleanseString (string input, string old_val)
        {
            if (input == old_val)
                return null;
                    
            if (input != null)
                input = input.Trim ();
            
            if (input == old_val)
                return null;
            
            return input;
        }
        
        private int tag_set_id;
        [DatabaseColumn]
        public int TagSetID {
            get { return tag_set_id; }
            set { tag_set_id = value; }
        }
        
        [DatabaseColumn ("MusicBrainzID")]
        public override string MusicBrainzId {
            get { return base.MusicBrainzId; }
            set { base.MusicBrainzId = value; }
        }

        [DatabaseColumn ("Uri")]
        protected string UriField {
            get { return Uri.AbsoluteUri; }
            set { Uri = new SafeUri (value); }
        }
        
        [DatabaseColumn]
        public override string MimeType {
            get { return base.MimeType; }
            set { base.MimeType = value; }
        }
        
        [DatabaseColumn]
        public override long FileSize {
            get { return base.FileSize; }
            set { base.FileSize = value; }
        }

        [DatabaseColumn]
        public override long FileModifiedStamp {
            get { return base.FileModifiedStamp; }
            set { base.FileModifiedStamp = value; }
        }

        [DatabaseColumn]
        public override DateTime LastSyncedStamp {
            get { return base.LastSyncedStamp; }
            set { base.LastSyncedStamp = value; }
        }
        
        [DatabaseColumn ("Attributes")]
        public override TrackMediaAttributes MediaAttributes {
            get { return base.MediaAttributes; }
            set { base.MediaAttributes = value; }
        }
        
        [DatabaseColumn ("Title")]
        public override string TrackTitle {
            get { return base.TrackTitle; }
            set { base.TrackTitle = value; }
        }
        
        [DatabaseColumn ("TitleSort")]
        public override string TrackTitleSort {
            get { return base.TrackTitleSort; }
            set { base.TrackTitleSort = value; }
        }
        
        [DatabaseColumn("TitleSortKey", Select = false)]
        internal byte[] TrackTitleSortKey {
            get { return Hyena.StringUtil.SortKey (TrackTitleSort ?? DisplayTrackTitle); }
        }
        
        [DatabaseColumn(Select = false)]
        internal string TitleLowered {
            get { return Hyena.StringUtil.SearchKey (DisplayTrackTitle); }
        }

        [DatabaseColumn(Select = false)]
        public override string MetadataHash {
            get { return base.MetadataHash; }
        }
        
        [DatabaseColumn]
        public override int TrackNumber {
            get { return base.TrackNumber; }
            set { base.TrackNumber = value; }
        }
        
        [DatabaseColumn]
        public override int TrackCount {
            get { return base.TrackCount; }
            set { base.TrackCount = value; }
        }
        
        [DatabaseColumn ("Disc")]
        public override int DiscNumber {
            get { return base.DiscNumber; }
            set { base.DiscNumber = value; }
        }

        [DatabaseColumn]
        public override int DiscCount {
            get { return base.DiscCount; }
            set { base.DiscCount = value; }
        }
        
        [DatabaseColumn]
        public override TimeSpan Duration {
            get { return base.Duration; }
            set { base.Duration = value; }
        }
        
        [DatabaseColumn]
        public override int Year {
            get { return base.Year; }
            set { base.Year = value; }
        }

        [DatabaseColumn]
        public override string Genre {
            get { return base.Genre; }
            set { base.Genre = value; }
        }

        [DatabaseColumn]
        public override string Composer {
            get { return base.Composer; }
            set { base.Composer = value; }
        }

        [DatabaseColumn]
        public override string Conductor {
            get { return base.Conductor; }
            set { base.Conductor = value; }
        }

        [DatabaseColumn]
        public override string Grouping {
            get { return base.Grouping; }
            set { base.Grouping = value; }
        }

        [DatabaseColumn]
        public override string Copyright {
            get { return base.Copyright; }
            set { base.Copyright = value; }
        }

        [DatabaseColumn]
        public override string LicenseUri {
            get { return base.LicenseUri; }
            set { base.LicenseUri = value; }
        }

        [DatabaseColumn]
        public override string Comment {
            get { return base.Comment; }
            set { base.Comment = value; }
        }
        
        [DatabaseColumn("BPM")]
        public override int Bpm {
            get { return base.Bpm; }
            set { base.Bpm = value; }
        }

        [DatabaseColumn]
        public override int BitRate {
            get { return base.BitRate; }
            set { base.BitRate = value; }
        }
        
        [DatabaseColumn("Rating")]
        protected int rating;
        public override int Rating {
            get { return rating; }
            set { rating = value; }
        }

        [DatabaseColumn]
        public override int Score {
            get { return base.Score; }
            set { base.Score = value; }
        }

        public int SavedRating {
            get { return rating; }
            set {
                if (rating != value) {
                    rating = value;
                    Save (true, BansheeQuery.RatingField);
                }
            }
        }
        
        [DatabaseColumn]
        public override int PlayCount {
            get { return base.PlayCount; }
            set { base.PlayCount = value; }
        }
        
        [DatabaseColumn]
        public override int SkipCount {
            get { return base.SkipCount; }
            set { base.SkipCount = value; }
        }
        
        private long external_id;
        [DatabaseColumn ("ExternalID")]
        public long ExternalId {
            get { return external_id; }
            set { external_id = value; }
        }

        private object external_object;
        public override object ExternalObject {
            get {
                if (external_id > 0 && external_object == null && PrimarySource != null && PrimarySource.TrackExternalObjectHandler != null) {
                    external_object = PrimarySource.TrackExternalObjectHandler (this);
                }
                return external_object;
            }
        }
        
        [DatabaseColumn ("LastPlayedStamp")]
        public override DateTime LastPlayed {
            get { return base.LastPlayed; }
            set { base.LastPlayed = value; }
        }

        [DatabaseColumn ("LastSkippedStamp")]
        public override DateTime LastSkipped {
            get { return base.LastSkipped; }
            set { base.LastSkipped = value; }
        }
        
        [DatabaseColumn ("DateAddedStamp")]
        public override DateTime DateAdded {
            get { return base.DateAdded; }
            set { base.DateAdded = value; }
        }

        private DateTime date_updated;
        [DatabaseColumn ("DateUpdatedStamp")]
        public DateTime DateUpdated {
            get { return date_updated; }
            set { date_updated = value; }
        }
        
        [DatabaseColumn ("LastStreamError")]
        protected StreamPlaybackError playback_error;
        public override StreamPlaybackError PlaybackError {
            get { return playback_error; }
            set {
                if (playback_error == value) {
                    return;
                }
                
                playback_error = value; 
            }
        }

        public void CopyToLibraryIfAppropriate (bool force_copy)
        {
            SafeUri old_uri = this.Uri;
            if (old_uri == null) {
                // Get out quick, no URI set yet.
                return;
            }
            
            bool in_library = old_uri.AbsolutePath.StartsWith (PrimarySource.BaseDirectoryWithSeparator);

            if (!in_library && (LibrarySchema.CopyOnImport.Get () || force_copy)) {
                string new_filename = FileNamePattern.BuildFull (PrimarySource.BaseDirectory, this, Path.GetExtension (old_uri.ToString ()));
                SafeUri new_uri = new SafeUri (new_filename);

                try {
                    if (Banshee.IO.File.Exists (new_uri)) {
                        Hyena.Log.DebugFormat ("Not copying {0} to library because there is already a file at {1}", old_uri, new_uri);
                        return;
                    }
                    
                    Banshee.IO.File.Copy (old_uri, new_uri, false);
                    Uri = new_uri;
                } catch (Exception e) {
                    Log.ErrorFormat ("Exception copying into library: {0}", e);
                }
            }
        }

        private static HyenaSqliteCommand get_uri_id_cmd = new HyenaSqliteCommand ("SELECT TrackID FROM CoreTracks WHERE Uri = ? LIMIT 1");
        public static int GetTrackIdForUri (string uri)
        {
            return ServiceManager.DbConnection.Query<int> (get_uri_id_cmd, new SafeUri (uri).AbsoluteUri);
        }

        private static HyenaSqliteCommand get_track_id_by_uri = new HyenaSqliteCommand (
            "SELECT TrackID FROM CoreTracks WHERE PrimarySourceId IN (?) AND Uri = ? LIMIT 1"
        );

        public static int GetTrackIdForUri (SafeUri uri, int [] primary_sources)
        {
            return ServiceManager.DbConnection.Query<int> (get_track_id_by_uri,
                primary_sources, uri.AbsoluteUri);
        }

        public static int GetTrackIdForUri (string absoluteUri, int [] primary_sources)
        {
            return ServiceManager.DbConnection.Query<int> (get_track_id_by_uri,
                primary_sources, absoluteUri);
        }

        public static bool ContainsUri (SafeUri uri, int [] primary_sources)
        {
            return GetTrackIdForUri (uri, primary_sources) > 0;
        }
    }
}

#pragma warning restore 0169

