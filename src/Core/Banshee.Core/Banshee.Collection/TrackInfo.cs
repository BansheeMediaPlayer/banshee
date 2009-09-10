// TrackInfo.cs
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
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using Mono.Unix;

using Hyena;
using Hyena.Data;
using Banshee.Base;
using Banshee.Streaming;

namespace Banshee.Collection
{
    // WARNING: Be extremely careful when changing property names flagged with [Exportable]!
    // There are third party applications depending on them!

    public class TrackInfo : CacheableItem, ITrackInfo
    {
        public const string ExportVersion = "1.0";

        public static readonly string UnknownTitle = Catalog.GetString ("Unknown Title");

        public class ExportableAttribute : Attribute
        {
            private string export_name;
            public string ExportName {
                get { return export_name; }
                set { export_name = value; }
            }
        }
    
        public delegate bool IsPlayingHandler (TrackInfo track);
        public static IsPlayingHandler IsPlayingMethod;

        public delegate void PlaybackFinishedHandler (TrackInfo track, double percentCompleted);
        public static event PlaybackFinishedHandler PlaybackFinished;

        private SafeUri uri;
        private SafeUri more_info_uri;
        private string mimetype;
        private long filesize;
        private long file_mtime;

        private string artist_name;
        private string artist_name_sort;
        private string album_title;
        private string album_title_sort;
        private string album_artist;
        private string album_artist_sort;
        private bool is_compilation;
        private string track_title;
        private string track_title_sort;
        private string genre;
        private string composer;
        private string conductor;
        private string grouping;
        private string copyright;
        private string license_uri;
        private string musicbrainz_id;

        private string comment;
        private int track_number;
        private int track_count;
        private int disc_number;
        private int disc_count;
        private int year;
        private int rating;
        private int score;
        private int bpm;
        private int bit_rate;

        private TimeSpan duration;
        private DateTime release_date;
        private DateTime date_added;
        private DateTime last_synced;

        private int play_count;
        private int skip_count;
        private DateTime last_played;
        private DateTime last_skipped;
        
        private StreamPlaybackError playback_error = StreamPlaybackError.None;

        public TrackInfo ()
        {
        }

        public virtual void OnPlaybackFinished (double percentCompleted)
        {
            double total_plays = PlayCount + SkipCount;
            if (total_plays <= 0) {
                Score = (int) Math.Floor ((Score + (percentCompleted * 100)) / 2);
            } else {
                Score = (int) Math.Floor ((((double)Score * total_plays) + (percentCompleted * 100)) / (total_plays + 1));
            }

            if (percentCompleted <= 0.5) {
                LastSkipped = DateTime.Now;
                SkipCount++;
            } else {
                LastPlayed = DateTime.Now;
                PlayCount++;
            }

            PlaybackFinishedHandler handler = PlaybackFinished;
            if (handler != null) {
                handler (this, percentCompleted);
            }
        }

        public override string ToString ()
        {
            return String.Format ("{0} - {1} (on {2}) <{3}> [{4}]", ArtistName, TrackTitle, 
                AlbumTitle, Duration, Uri == null ? "<unknown>" : Uri.AbsoluteUri);
        }

        public virtual bool TrackEqual (TrackInfo track)
        {
            if (track == null || track.Uri == null || Uri == null) {
                return false;
            }
            
            return track.Uri.AbsoluteUri == Uri.AbsoluteUri;
        }
        
        public bool ArtistAlbumEqual (TrackInfo track)
        {
            if (track == null) {
                return false;
            }
            
            return ArtworkId == track.ArtworkId;
        }

        public virtual void Save ()
        {
        }
        
        public bool IsPlaying {
            get { return (IsPlayingMethod != null) ? IsPlayingMethod (this) : false; }
        }

        [Exportable (ExportName = "URI")]
        public virtual SafeUri Uri {
            get { return uri; }
            set { uri = value; }
        }
        
        [Exportable]
        public string LocalPath {
            get { return Uri == null || !Uri.IsLocalPath ? null : Uri.LocalPath; }
        }

        [Exportable]
        public SafeUri MoreInfoUri {
            get { return more_info_uri; }
            set { more_info_uri = value; }
        }

        [Exportable]
        public virtual string MimeType {
            get { return mimetype; }
            set { mimetype = value; }
        }

        [Exportable]
        public virtual long FileSize {
            get { return filesize; }
            set { filesize = value; }
        }

        public virtual long FileModifiedStamp {
            get { return file_mtime; }
            set { file_mtime = value; }
        }

        public virtual DateTime LastSyncedStamp {
            get { return last_synced; }
            set { last_synced = value; }
        }

        [Exportable (ExportName = "artist")]
        public virtual string ArtistName {
            get { return artist_name; }
            set { artist_name = value; }
        }

        [Exportable (ExportName = "artistsort")]
        public virtual string ArtistNameSort {
            get { return artist_name_sort; }
            set { artist_name_sort = String.IsNullOrEmpty (value) ? null : value; }
        }

        [Exportable (ExportName = "album")]
        public virtual string AlbumTitle {
            get { return album_title; }
            set { album_title = value; }
        }

        [Exportable (ExportName = "albumsort")]
        public virtual string AlbumTitleSort {
            get { return album_title_sort; }
            set { album_title_sort = String.IsNullOrEmpty (value) ? null : value; }
        }

        [Exportable]
        public virtual string AlbumArtist {
            get { return IsCompilation ? album_artist ?? Catalog.GetString ("Various Artists") : ArtistName; }
            set { album_artist = value; }
        }
        
        [Exportable]
        public virtual string AlbumArtistSort {
            get { return album_artist_sort; }
            set { album_artist_sort = String.IsNullOrEmpty (value) ? null : value; }
        }
        
        [Exportable]
        public virtual bool IsCompilation {
            get { return is_compilation; }
            set { is_compilation = value; }
        }

        [Exportable (ExportName = "name")]
        public virtual string TrackTitle {
            get { return track_title; }
            set { track_title = value; }
        }
        
        [Exportable (ExportName = "namesort")]
        public virtual string TrackTitleSort {
            get { return track_title_sort; }
            set { track_title_sort = String.IsNullOrEmpty (value) ? null : value; }
        }
        
        [Exportable]
        public virtual string MusicBrainzId {
            get { return musicbrainz_id; }
            set { musicbrainz_id = value; }
        }
        
        [Exportable]
        public virtual string ArtistMusicBrainzId {
            get { return null; }
        }
        
        [Exportable]
        public virtual string AlbumMusicBrainzId {
            get { return null; }
        }
        
        public virtual DateTime ReleaseDate {
            get { return release_date; }
            set { release_date = value; }
        }        

        public virtual object ExternalObject {
            get { return null; }
        }
        
        public string DisplayArtistName { 
            get { return StringUtil.MaybeFallback (ArtistName, ArtistInfo.UnknownArtistName); } 
        }

        public string DisplayAlbumArtistName {
            get { return StringUtil.MaybeFallback (AlbumArtist, DisplayArtistName); }
        }

        public string DisplayAlbumTitle { 
            get { return StringUtil.MaybeFallback (AlbumTitle, AlbumInfo.UnknownAlbumTitle); } 
        }

        public string DisplayTrackTitle { 
            get { return StringUtil.MaybeFallback (TrackTitle, UnknownTitle); } 
        }

        public string DisplayGenre { 
            get { 
                string genre = Genre == null ? null : Genre.Trim ();
                return String.IsNullOrEmpty (genre)
                    ? String.Empty
                    : genre;
            }
        }
        
        [Exportable (ExportName = "artwork-id")]
        public virtual string ArtworkId { 
            get { return CoverArtSpec.CreateArtistAlbumId (AlbumArtist, AlbumTitle); }
        }

        [Exportable]
        public virtual string Genre {
            get { return genre; }
            set { genre = value; }
        }

        [Exportable]
        public virtual int TrackNumber {
            get { return track_number; }
            set { track_number = value; }
        }

        [Exportable]
        public virtual int TrackCount {
            get { return (track_count != 0 && track_count < TrackNumber) ? TrackNumber : track_count; }
            set { track_count = value; }
        }

        [Exportable]
        public virtual int DiscNumber {
            get { return disc_number; }
            set { disc_number = value; }
        }
        
        [Exportable]
        public virtual int DiscCount {
            get { return (disc_count != 0 && disc_count < DiscNumber) ? DiscNumber : disc_count; }
            set { disc_count = value; }
        }

        [Exportable]
        public virtual int Year {
            get { return year; }
            set { year = value; }
        }

        [Exportable]
        public virtual string Composer {
            get { return composer; }
            set { composer = value; }
        }

        [Exportable]
        public virtual string Conductor {
            get { return conductor; }
            set { conductor = value; }
        }
        
        [Exportable]
        public virtual string Grouping {
            get { return grouping; }
            set { grouping = value; }
        }
        
        [Exportable]
        public virtual string Copyright {
            get { return copyright; }
            set { copyright = value; }
        }

        [Exportable]        
        public virtual string LicenseUri {
            get { return license_uri; }
            set { license_uri = value; }
        }

        [Exportable]
        public virtual string Comment {
            get { return comment; }
            set { comment = value; }
        }

        [Exportable]
        public virtual int Rating {
            get { return rating; }
            set { rating = value; }
        }
        
        [Exportable]
        public virtual int Score {
            get { return score; }
            set {score = value; }
        }

        [Exportable]
        public virtual int Bpm {
            get { return bpm; }
            set { bpm = value; }
        }

        [Exportable]
        public virtual int BitRate {
            get { return bit_rate; }
            set { bit_rate = value; }
        }

        [Exportable]
        public virtual int PlayCount {
            get { return play_count; }
            set { play_count = value; }
        }

        [Exportable]
        public virtual int SkipCount {
            get { return skip_count; }
            set { skip_count = value; }
        }

        [Exportable (ExportName = "length")]
        public virtual TimeSpan Duration {
            get { return duration; }
            set { duration = value; }
        }
        
        [Exportable]
        public virtual DateTime DateAdded {
            get { return date_added; }
            set { date_added = value; }
        }

        [Exportable]
        public virtual DateTime LastPlayed {
            get { return last_played; }
            set { last_played = value; }
        }

        [Exportable]
        public virtual DateTime LastSkipped {
            get { return last_skipped; }
            set { last_skipped = value; }
        }
        
        public virtual StreamPlaybackError PlaybackError {
            get { return playback_error; }
            set { playback_error = value; }
        }

        public void SavePlaybackError (StreamPlaybackError value)
        {
            if (PlaybackError != value) {
                PlaybackError = value;
                Save ();
            }
        }

        private bool can_save_to_database = true;
        public bool CanSaveToDatabase {
            get { return can_save_to_database; }
            set { can_save_to_database = value; }
        }
        
        private bool is_live = false;
        public bool IsLive {
            get { return is_live; }
            set { is_live = value; }
        }

        private bool can_play = true;
        public bool CanPlay {
            get { return can_play; }
            set { can_play = value; }
        }

        private bool enabled = true;
        public bool Enabled {
            get { return enabled && can_play; }
            set { enabled = value; }
        }
        
        public virtual string MetadataHash {
            get {
                System.Text.StringBuilder sb = new System.Text.StringBuilder ();
                sb.Append (AlbumTitle);
                sb.Append (ArtistName);
                sb.Append ((int)Duration.TotalSeconds);
                sb.Append (Genre);
                sb.Append (TrackTitle);
                sb.Append (TrackNumber);
                sb.Append (Year);
                return Hyena.CryptoUtil.Md5Encode (sb.ToString (), System.Text.Encoding.UTF8);
            }
        }

        private TrackMediaAttributes media_attributes = TrackMediaAttributes.Default;
        
        [Exportable]
        public virtual TrackMediaAttributes MediaAttributes {
            get { return media_attributes; }
            set { media_attributes = value; }
        }
        
        public bool HasAttribute (TrackMediaAttributes attr)
        {
            return (MediaAttributes & attr) != 0;
        }

        protected void SetAttributeIf (bool condition, TrackMediaAttributes attr)
        {
            if (condition) {
                MediaAttributes |= attr;
            }
        }

        // TODO turn this into a PrimarySource-owned delegate?
        private static readonly string restart_podcast = Catalog.GetString ("_Restart Podcast");
        private static readonly string restart_video = Catalog.GetString ("_Restart Video");
        private static readonly string restart_song = Catalog.GetString ("_Restart Song");
        private static readonly string restart_item = Catalog.GetString ("_Restart Item");

        public string RestartLabel {
            get {
                if (HasAttribute (TrackMediaAttributes.Podcast))
                    return restart_podcast;
                if (HasAttribute (TrackMediaAttributes.VideoStream))
                    return restart_video;
                if (HasAttribute (TrackMediaAttributes.Music))
                    return restart_song;
                return restart_item;
            }
        }

        private static readonly string jump_to_podcast = Catalog.GetString ("_Jump to Playing Podcast");
        private static readonly string jump_to_video = Catalog.GetString ("_Jump to Playing Video");
        private static readonly string jump_to_song = Catalog.GetString ("_Jump to Playing Song");
        private static readonly string jump_to_item = Catalog.GetString ("_Jump to Playing Item");

        public string JumpToLabel {
            get {
                if (HasAttribute (TrackMediaAttributes.Podcast))
                    return jump_to_podcast;
                if (HasAttribute (TrackMediaAttributes.VideoStream))
                    return jump_to_video;
                if (HasAttribute (TrackMediaAttributes.Music))
                    return jump_to_song;
                return jump_to_item;
            }
        }

#region Exportable Properties
        
        public static void ExportableMerge (TrackInfo source, TrackInfo dest)
        {
            // Use the high level TrackInfo type if the source and dest types differ
            Type type = dest.GetType ();
            if (source.GetType () != type) {
                type = typeof (TrackInfo);
            }
            
            foreach (KeyValuePair<string, PropertyInfo> iter in GetExportableProperties (type)) {
                try {
                    PropertyInfo property = iter.Value;
                    if (property.CanWrite && property.CanRead) {
                        property.SetValue (dest, property.GetValue (source, null), null);
                    }
                } catch (Exception e) {
                    Log.Exception (e);
                }
            }
        }
        
        public static IEnumerable<KeyValuePair<string, PropertyInfo>> GetExportableProperties (Type type)
        {
            FindExportableProperties (type);
            
            Dictionary<string, PropertyInfo> properties = null;
            if (exportable_properties.TryGetValue (type, out properties)) {
                foreach (KeyValuePair<string, PropertyInfo> property in properties) {
                    yield return property;
                }
            }
        }
        
        public IDictionary<string, object> GenerateExportable ()
        {
            return GenerateExportable (null);
        }
        
        public IDictionary<string, object> GenerateExportable (string [] fields)
        {
            Dictionary<string, object> dict = new Dictionary<string, object> ();

            foreach (KeyValuePair<string, PropertyInfo> property in GetExportableProperties (GetType ())) {
                if (fields != null) {
                    bool found = false;
                    foreach (string field in fields) {
                        if (field == property.Key) {
                            found = true;
                            break;
                        }
                    }
                    
                    if (!found) {
                        continue;
                    }
                }
                
                object value = property.Value.GetValue (this, null);
                if (value == null) {
                    continue;
                }
                
                if (value is TimeSpan) {
                    value = ((TimeSpan)value).TotalSeconds;
                } else if (value is DateTime) {
                    DateTime date = (DateTime)value;
                    value = date == DateTime.MinValue ? 0l : DateTimeUtil.ToTimeT (date);
                } else if (value is SafeUri) {
                    value = ((SafeUri)value).AbsoluteUri;
                } else if (value is TrackMediaAttributes) {
                    value = value.ToString ();
                } else if (!(value.GetType ().IsPrimitive || value is string)) {
                    Log.WarningFormat ("Invalid property in {0} marked as [Exportable]: ({1} is a {2})", 
                        property.Value.DeclaringType, property.Value.Name, value.GetType ());
                    continue;
                }
                
                // A bit lame
                if (!(value is string)) {
                    string str_value = value.ToString ();
                    if (str_value == "0" || str_value == "0.0") {
                        continue;
                    }
                }
                
                dict.Add (property.Key, value);
            }
            
            return dict;
        }
        
        private static Dictionary<Type, Dictionary<string, PropertyInfo>> exportable_properties;
        private static object exportable_properties_mutex = new object ();
        
        private static void FindExportableProperties (Type type)
        {
            lock (exportable_properties_mutex) {
                if (exportable_properties == null) {
                    exportable_properties = new Dictionary<Type, Dictionary<string, PropertyInfo>> ();
                } else if (exportable_properties.ContainsKey (type)) {
                    return;
                }
                
                // Build a stack of types to reflect
                Stack<Type> probe_types = new Stack<Type> ();
                Type probe_type = type;
                bool is_track_info = false;
                while (probe_type != null) {
                    probe_types.Push (probe_type);
                    if (probe_type == typeof (TrackInfo)) {
                        is_track_info = true;
                        break;
                    }
                    probe_type = probe_type.BaseType;
                }
                
                if (!is_track_info) {
                    throw new ArgumentException ("Type must derive from Banshee.Collection.TrackInfo", "type");
                }
            
                // Iterate through all types
                while (probe_types.Count > 0) {
                    probe_type = probe_types.Pop ();
                    if (exportable_properties.ContainsKey (probe_type)) {
                        continue;
                    }
                    
                    Dictionary<string, PropertyInfo> properties = null;
                    
                    // Reflect the type for exportable properties
                    foreach (PropertyInfo property in probe_type.GetProperties (BindingFlags.Public | BindingFlags.Instance)) {
                        if (property.DeclaringType != probe_type) {
                            continue;
                        }
                        
                        object [] exportable_attrs = property.GetCustomAttributes (typeof (ExportableAttribute), true);
                        if (exportable_attrs == null || exportable_attrs.Length == 0) {
                            continue;
                        }
                        
                        string export_name = ((ExportableAttribute)exportable_attrs[0]).ExportName
                            ?? StringUtil.CamelCaseToUnderCase (property.Name, '-');
                        
                        if (String.IsNullOrEmpty (export_name) || (properties != null && properties.ContainsKey (export_name))) {
                            continue;
                        }
                        
                        if (properties == null) {
                            properties = new Dictionary<string, PropertyInfo> ();
                            exportable_properties.Add (probe_type, properties);
                        }
                        
                        properties.Add (export_name, property);
                    }
                    
                    // Merge properties in the type hierarchy through linking or aggregation
                    Type parent_type = probe_type.BaseType;
                    bool link = !exportable_properties.ContainsKey (probe_type);
                    
                    while (parent_type != null) {
                        Dictionary<string, PropertyInfo> parent_properties = null;
                        if (!exportable_properties.TryGetValue (parent_type, out parent_properties)) {
                            parent_type = parent_type.BaseType;
                            continue;
                        }
                        
                        if (link) {
                            // Link entire property set between types
                            exportable_properties.Add (probe_type, parent_properties);
                            return;
                        } else {
                            // Aggregate properties in parent sets
                            foreach (KeyValuePair<string, PropertyInfo> parent_property in parent_properties) {
                                properties.Add (parent_property.Key, parent_property.Value);
                            }
                        }
                        
                        parent_type = parent_type.BaseType;
                    }
                }
            }
        }
        
#endregion

    }
}
