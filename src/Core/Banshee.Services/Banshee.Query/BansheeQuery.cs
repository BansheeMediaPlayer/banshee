//
// BansheeQuery.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//   Gabriel Burt <gburt@novell.com>
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
using System.Data;
using System.Text;
using System.Collections.Generic;

using Mono.Unix;

using Hyena.Query;

namespace Banshee.Query
{
    /*public interface IQueryDefines
    {
        QueryOrder [] Orders { get; }
        QueryLimit [] Limits { get; }
        QueryFieldSet FieldSet { get; }
        string GetSqlSort (string key, bool asc);
    }

    public class QueryDefines : IQueryDefines
    {

    }*/

    public static class BansheeQuery
    {
        private static bool asc = true;
        private static bool desc = false;

        public static readonly QueryOrder RandomOrder;
        public static readonly QueryOrder [] Orders;

        public static QueryLimit [] Limits = new QueryLimit [] {
            new QueryLimit ("songs",   Catalog.GetString ("items"), true),
            new QueryLimit ("minutes", Catalog.GetString ("minutes"), "CoreTracks.Duration/1000", (int) TimeFactor.Minute),
            new QueryLimit ("hours",   Catalog.GetString ("hours"), "CoreTracks.Duration/1000", (int) TimeFactor.Hour),
            new QueryLimit ("MB",      Catalog.GetString ("MB"), "CoreTracks.FileSize", (int) FileSizeFactor.MB),
            new QueryLimit ("GB",      Catalog.GetString ("GB"), "CoreTracks.FileSize", (int) FileSizeFactor.GB)
        };

#region QueryField Definitions

        public static QueryField ArtistField = new QueryField (
            "artist", "DisplayArtistName",
            Catalog.GetString ("Artist"), "CoreArtists.NameLowered", true,
            // Translators: These are unique search aliases for "artist". You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("artist"), Catalog.GetString ("by"), Catalog.GetString ("artists"),
            "by", "artist", "artists"
        );

        public static QueryField AlbumArtistField = new QueryField (
            "albumartist", "DisplayAlbumArtistName",
            Catalog.GetString ("Album Artist"), "CoreAlbums.ArtistNameLowered", false,
            // Translators: These are unique search aliases for "album artist". You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("albumartist"), Catalog.GetString ("compilationartist"),
            "albumartist", "compilationartist"
        );

        // TODO add IsCompilationField

        public static QueryField AlbumField = new QueryField (
            "album", "DisplayAlbumTitle",
            Catalog.GetString ("Album"), "CoreAlbums.TitleLowered", true,
            // Translators: These are unique search aliases for "album". You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("album"), Catalog.GetString ("on"), Catalog.GetString ("from"),
            "on", "album", "from", "albumtitle"
        );

        public static QueryField DiscNumberField = new QueryField (
            "disc", "DiscNumber",
            Catalog.GetString ("Disc"), "CoreTracks.Disc", typeof(NaturalIntegerQueryValue),
            // Translators: These are unique search fields (and nouns). You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("disc"), Catalog.GetString ("cd"), Catalog.GetString ("discnum"),
            "disc", "cd", "discnum"
        );

        public static QueryField DiscCountField = new QueryField (
            "disccount", "DiscCount",
            Catalog.GetString ("Disc Count"), "CoreTracks.DiscCount", typeof(NaturalIntegerQueryValue),
            // Translators: These are unique search fields (and nouns). You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("discs"), Catalog.GetString ("cds"),
            "discs", "cds"
        );

        public static QueryField TrackNumberField = new QueryField (
            "track", "TrackNumber",
            // Translators: noun
            Catalog.GetString ("Track Number"), "CoreTracks.TrackNumber", typeof(NaturalIntegerQueryValue),
            // Translators: These are unique search fields. You can use CSV for synonyms. Please, no spaces. Blank ok.
            "#", Catalog.GetString ("track"), Catalog.GetString ("trackno"), Catalog.GetString ("tracknum"),
            "track", "trackno", "tracknum"
        );

        public static QueryField TrackCountField = new QueryField (
            "trackcount", "TrackCount",
            // Translators: noun
            Catalog.GetString ("Track Count"), "CoreTracks.TrackCount", typeof(NaturalIntegerQueryValue),
            // Translators: These are unique search fields. You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("tracks"), Catalog.GetString ("trackcount"),
            "tracks", "trackcount"
        );

        public static QueryField BpmField = new QueryField (
            "bpm", "Bpm",
            Catalog.GetString ("Beats per Minute"), "CoreTracks.BPM", typeof(NaturalIntegerQueryValue),
            // Translators: These are unique search fields. You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("bpm"),
            "bpm"
        );

        public static QueryField BitRateField = new QueryField (
            "bitrate", "BitRate",
            // Translators: noun
            Catalog.GetString ("Bit Rate"), "CoreTracks.BitRate", typeof(NaturalIntegerQueryValue),
            // Translators: These are unique search fields. You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("bitrate"), Catalog.GetString ("kbs"), Catalog.GetString ("kps"),
            "bitrate", "kbs", "kps"
        );

        public static QueryField SampleRateField = new QueryField (
            "samplerate", "SampleRate",
            // Translators: noun
            Catalog.GetString ("Sample Rate"), "CoreTracks.SampleRate", typeof(NaturalIntegerQueryValue),
            // Translators: These are unique search fields. You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("samplerate"), Catalog.GetString ("Hz"),
            "samplerate", "Hz"
        );

        public static QueryField BitsPerSampleField = new QueryField (
            "bitspersample", "BitsPerSample",
            // Translators: noun
            Catalog.GetString ("Bits Per Sample"), "CoreTracks.BitsPerSample", typeof(NaturalIntegerQueryValue),
            // Translators: These are unique search fields. You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("bitspersample"), Catalog.GetString ("bitdepth"), Catalog.GetString ("quantization"),
            "bitspersample", "bitdepth", "quantization"
        );

        public static QueryField TitleField = new QueryField (
            "title", "DisplayTrackTitle",
            Catalog.GetString ("Name"), "CoreTracks.TitleLowered", true,
            // Translators: These are unique search fields. You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("title"), Catalog.GetString ("titled"), Catalog.GetString ("name"), Catalog.GetString ("named"),
            "title", "titled", "name", "named"
        );

        public static QueryField YearField = new QueryField (
            "year", "Year",
            Catalog.GetString ("Year"), "CoreTracks.Year", typeof(YearQueryValue),
            // Translators: These are unique search fields. You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("year"), Catalog.GetString ("released"), Catalog.GetString ("yr"),
            "year", "released", "yr"
        );

        public static QueryField GenreField = new QueryField (
            "genre", "Genre",
            Catalog.GetString ("Genre"), "CoreTracks.Genre", false,
            // Translators: These are unique search fields. You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("genre"), "genre"
        );

        public static QueryField ComposerField = new QueryField (
            "composer", "Composer",
            Catalog.GetString ("Composer"), "CoreTracks.Composer", false,
            // Translators: These are unique search fields. You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("composer"), "composer"
        );

        public static QueryField ConductorField = new QueryField (
            "conductor", "Conductor",
            Catalog.GetString ("Conductor"), "CoreTracks.Conductor", false,
            // Translators: These are unique search fields. You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("conductor"), "conductor"
        );

        public static QueryField GroupingField = new QueryField (
            "grouping", "Grouping",
            Catalog.GetString ("Grouping"), "CoreTracks.Grouping", false,
            // Translators: These are unique search fields. You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("grouping"), "grouping"
        );

        public static QueryField CommentField = new QueryField (
            "comment", "Comment",
            // Translators: noun
            Catalog.GetString ("Comment"), "CoreTracks.Comment", false,
            // Translators: These are unique search fields (and nouns). You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("comment"), "comment"
        );

        public static QueryField LicenseUriField = new QueryField (
            "licenseuri", "LicenseUri",
            // Translators: noun
            Catalog.GetString ("License"), "CoreTracks.LicenseUri", typeof(ExactStringQueryValue),
            // Translators: These are unique search fields (and nouns). You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("license"), Catalog.GetString ("licensed"), Catalog.GetString ("under"),
            "license", "licensed", "under"
        );

        public static QueryField RatingField = new QueryField (
            "rating", "SavedRating",
            Catalog.GetString ("Rating"), "CoreTracks.Rating", new Type [] {typeof(RatingQueryValue)},//, typeof(NullQueryValue)},
            // Translators: These are unique search fields. You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("rating"), Catalog.GetString ("stars"),
            "rating", "stars"
        );

        public static QueryField PlayCountField = new QueryField (
            "playcount", "PlayCount",
            Catalog.GetString ("Play Count"), "CoreTracks.PlayCount", typeof(NaturalIntegerQueryValue),
            // Translators: These are unique search fields (and nouns). You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("plays"), Catalog.GetString ("playcount"), Catalog.GetString ("listens"),
            "plays", "playcount", "numberofplays", "listens"
        );

        public static QueryField SkipCountField = new QueryField (
            "skipcount", "SkipCount",
            Catalog.GetString ("Skip Count"), "CoreTracks.SkipCount", typeof(NaturalIntegerQueryValue),
            // Translators: These are unique search fields (and nouns). You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("skips"), Catalog.GetString ("skipcount"),
            "skips", "skipcount"
        );

        public static QueryField FileSizeField = new QueryField (
            "filesize", "FileSize",
            Catalog.GetString ("File Size"), "CoreTracks.FileSize", typeof(FileSizeQueryValue),
            // Translators: These are unique search fields (and nouns). You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("size"), Catalog.GetString ("filesize"),
            "size", "filesize"
        );

        public static QueryField UriField = new QueryField (
            "uri", "Uri",
            Catalog.GetString ("File Location"), "CoreTracks.Uri", typeof(ExactUriStringQueryValue),
            // Translators: These are unique search fields. You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("uri"), Catalog.GetString ("path"), Catalog.GetString ("file"), Catalog.GetString ("location"),
            "uri", "path", "file", "location"
        );

        public static QueryField DurationField = new QueryField (
            "duration", "Duration",
            Catalog.GetString ("Time"), "CoreTracks.Duration", typeof(TimeSpanQueryValue),
            // Translators: These are unique search fields. You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("duration"), Catalog.GetString ("length"), Catalog.GetString ("time"),
            "duration", "length", "time"
        );

        public static QueryField MimeTypeField = new QueryField (
            "mimetype", "MimeType",
            Catalog.GetString ("Mime Type"), "CoreTracks.MimeType", typeof(ExactStringQueryValue),
            // Translators: These are unique search fields. You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("type"), Catalog.GetString ("mimetype"), Catalog.GetString ("format"), Catalog.GetString ("ext"),
            "type", "mimetype", "format", "ext", "mime"
        );

        public static QueryField LastPlayedField = new QueryField (
            "lastplayed", "LastPlayed",
            Catalog.GetString ("Last Played"), "CoreTracks.LastPlayedStamp", new Type [] {typeof(RelativeTimeSpanQueryValue), typeof(DateQueryValue)},
            // Translators: These are unique search fields. You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("lastplayed"), Catalog.GetString ("played"), Catalog.GetString ("playedon"),
            "lastplayed", "played", "playedon"
        );

        public static QueryField LastSkippedField = new QueryField (
            "lastskipped", "LastSkipped",
            Catalog.GetString ("Last Skipped"), "CoreTracks.LastSkippedStamp", new Type [] {typeof(RelativeTimeSpanQueryValue), typeof(DateQueryValue)},
            // Translators: These are unique search fields. You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("lastskipped"), Catalog.GetString ("skipped"), Catalog.GetString ("skippedon"),
            "lastskipped", "skipped", "skippedon"
        );

        public static QueryField DateAddedField = new QueryField (
            "added", "DateAdded",
            Catalog.GetString ("Date Added"), "CoreTracks.DateAddedStamp", new Type [] {typeof(RelativeTimeSpanQueryValue), typeof(DateQueryValue)},
            // Translators: These are unique search fields. You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("added"), Catalog.GetString ("imported"), Catalog.GetString ("addedon"), Catalog.GetString ("dateadded"), Catalog.GetString ("importedon"),
            "added", "imported", "addedon", "dateadded", "importedon"
        );

        public static QueryField PlaylistField = new QueryField (
            "playlistid", null, Catalog.GetString ("Playlist"),
            "CoreTracks.TrackID {2} IN (SELECT TrackID FROM CorePlaylistEntries WHERE PlaylistID = {1})", typeof(PlaylistQueryValue),
            "playlistid", "playlist"
        );

        public static QueryField SmartPlaylistField = new QueryField (
            "smartplaylistid", null, Catalog.GetString ("Smart Playlist"),
            "CoreTracks.TrackID {2} IN (SELECT TrackID FROM CoreSmartPlaylistEntries WHERE SmartPlaylistID = {1})", typeof(SmartPlaylistQueryValue),
            "smartplaylistid", "smartplaylist"
        );

        public static QueryField ScoreField = new QueryField (
            "score", "Score",
            Catalog.GetString ("Score"), "CoreTracks.Score", typeof(IntegerQueryValue),
            //Translators: These are unique search fields (and nouns). You can use CSV for synonyms. Please, no spaces. Blank ok.
            Catalog.GetString ("score"),
            "score"
        );

        public static QueryField PlaybackErrorField = new QueryField (
            "playbackerror", "PlaybackError",
            Catalog.GetString ("Playback Error"), "CoreTracks.LastStreamError", typeof(PlaybackErrorQueryValue),
            //Translators: These are unique search fields (and nouns). Please, no spaces. Blank ok.
            Catalog.GetString ("playbackerror"),
            "playbackerror", "error"
        );

#endregion

        public static QueryFieldSet FieldSet = new QueryFieldSet (
            ArtistField, AlbumField, AlbumArtistField, TitleField, TrackNumberField, TrackCountField, DiscNumberField, DiscCountField,
            YearField, GenreField, ComposerField, ConductorField, GroupingField, CommentField, LicenseUriField, RatingField, PlayCountField,
            SkipCountField, FileSizeField, UriField, DurationField, MimeTypeField, LastPlayedField, LastSkippedField,
            BpmField, BitRateField, SampleRateField, BitsPerSampleField, DateAddedField, PlaylistField, SmartPlaylistField, ScoreField, PlaybackErrorField
        );

        // Type Initializer
        static BansheeQuery ()
        {
            // Translators: noun
            BpmField.ShortLabel         = Catalog.GetString ("BPM");
            SkipCountField.ShortLabel   = Catalog.GetString ("Skips");
            PlayCountField.ShortLabel   = Catalog.GetString ("Plays");

            default_sort = String.Format (default_sort_template, "");
            default_sort_by_year = String.Format (default_sort_template, "CoreTracks.Year ASC, ");

            RandomOrder = CreateQueryOrder ("Random", asc, Catalog.GetString ("Random"), null);

            Orders = new QueryOrder [] {
                RandomOrder,
                CreateQueryOrder ("Album",      asc,  Catalog.GetString ("Album"), AlbumField),
                CreateQueryOrder ("Artist",     asc,  Catalog.GetString ("Artist"), ArtistField),
                // Translators: noun
                CreateQueryOrder ("Title",      asc,  Catalog.GetString ("Name"), TitleField),
                CreateQueryOrder ("Genre",      asc,  Catalog.GetString ("Genre"), GenreField),
                null,
                CreateQueryOrder ("Rating",     desc, Catalog.GetString ("Highest Rating"), RatingField),
                CreateQueryOrder ("Rating",     asc,  Catalog.GetString ("Lowest Rating"), RatingField),
                null,
                CreateQueryOrder ("Score",      desc, Catalog.GetString ("Highest Score"), ScoreField),
                CreateQueryOrder ("Score",      asc,  Catalog.GetString ("Lowest Score"), ScoreField),
                null,
                CreateQueryOrder ("PlayCount",  desc, Catalog.GetString ("Most Often Played"), PlayCountField),
                CreateQueryOrder ("PlayCount",  asc,  Catalog.GetString ("Least Often Played"), PlayCountField),
                null,
                CreateQueryOrder ("LastPlayedStamp", desc, Catalog.GetString ("Most Recently Played"), LastPlayedField),
                CreateQueryOrder ("LastPlayedStamp", asc,  Catalog.GetString ("Least Recently Played"), LastPlayedField),
                null,
                CreateQueryOrder ("DateAddedStamp",  desc, Catalog.GetString ("Most Recently Added"), DateAddedField),
                CreateQueryOrder ("DateAddedStamp",  asc,  Catalog.GetString ("Least Recently Added"), DateAddedField)
            };
        }

        private const string default_sort_template = @"CoreAlbums.ArtistNameSortKey ASC, {0}CoreAlbums.TitleSortKey ASC, CoreTracks.Disc ASC, CoreTracks.TrackNumber ASC";
        private static readonly string default_sort;
        private static readonly string default_sort_by_year;

        public static string GetSort (string key)
        {
            return GetSort (key, false);
        }

        public static string GetSort (string key, bool asc)
        {
            bool sort_by_year = Banshee.Configuration.Schema.LibrarySchema.SortByAlbumYear.Get ();
            string sort_by_year_part = sort_by_year ? "CoreTracks.Year ASC," : "";
            string sort = sort_by_year ? default_sort_by_year : default_sort;

            string ascDesc = asc ? "ASC" : "DESC";
            string sort_query = null;
            // TODO use the QueryFields here instead of matching on a string key
            string column = null;
            switch (key.ToLower ()) {
                case "track":
                case "grouping":
                    sort_query = String.Format (@"
                        CoreAlbums.ArtistNameSortKey ASC,
                        {1}
                        CoreAlbums.TitleSortKey ASC,
                        CoreTracks.Disc ASC,
                        CoreTracks.TrackNumber {0}", ascDesc, sort_by_year_part);
                    break;

                case "albumartist":
                    sort_query = String.Format (@"
                        CoreAlbums.ArtistNameSortKey {0},
                        {1}
                        CoreAlbums.TitleSortKey ASC,
                        CoreTracks.Disc ASC,
                        CoreTracks.TrackNumber ASC", ascDesc, sort_by_year_part);
                    break;

                case "artist":
                    sort_query = String.Format (@"
                        CoreArtists.NameSortKey {0},
                        {1}
                        CoreAlbums.TitleSortKey ASC,
                        CoreTracks.Disc ASC,
                        CoreTracks.TrackNumber ASC", ascDesc, sort_by_year_part);
                    break;

                case "album":
                    sort_query = String.Format (@"
                        CoreAlbums.TitleSortKey {0},
                        {1}
                        CoreTracks.Disc ASC,
                        CoreTracks.TrackNumber ASC", ascDesc, sort_by_year_part);
                    break;

                case "title":
                    sort_query = String.Format (@"
                        CoreTracks.TitleSortKey {0},
                        CoreAlbums.ArtistNameSortKey ASC,
                        {1}
                        CoreAlbums.TitleSortKey ASC", ascDesc, sort_by_year_part);
                    break;

                case "random":
                    sort_query = "RANDOM ()";
                    break;

                case "disc":
                    sort_query = String.Format (@"
                        CoreAlbums.ArtistNameSortKey ASC,
                        {1}
                        CoreAlbums.TitleSortKey ASC,
                        CoreTracks.Disc {0},
                        CoreTracks.TrackNumber ASC", ascDesc, sort_by_year_part);
                    break;

                // FIXME hacks to aid in migration of these sort keys to actually
                // using the QueryField (or at least their .Names)
                case "lastplayed":
                case "lastskipped":
                    column = String.Format ("{0}stamp", key);
                    goto case "year";
                case "added":
                    column = "dateaddedstamp";
                    goto case "year";

                case "conductor":
                case "genre":
                case "composer":
                case "comment":
                    sort_query = String.Format (
                        "HYENA_COLLATION_KEY(CoreTracks.{0}) {1}, {2}",
                        column ?? key, ascDesc, sort
                    );
                    break;

                case "score":
                    sort_query = String.Format (@"
                        CoreTracks.Score {0},
                        CoreTracks.Playcount {0}, {1}", ascDesc, sort);
                    break;

                case "year":
                case "bitrate":
                case "samplerate":
                case "bitspersample":
                case "bpm":
                case "trackcount":
                case "disccount":
                case "duration":
                case "rating":
                case "playcount":
                case "skipcount":
                case "filesize":
                case "lastplayedstamp":
                case "lastskippedstamp":
                case "dateaddedstamp":
                case "uri":
                case "mimetype":
                case "licenseuri":
                    sort_query = String.Format (
                        "CoreTracks.{0} {1}, {2}",
                        column ?? key, ascDesc, sort
                    );
                    break;
                default:
                    Hyena.Log.ErrorFormat ("Unknown sort key passed in! {0} not recognized", key);
                    break;
            }
            return sort_query;
        }

        private static QueryOrder CreateQueryOrder (string name, bool asc, string label, QueryField field)
        {
            return new QueryOrder (CreateOrderName (name, asc), label, GetSort (name, asc), field);
        }

        public static QueryLimit FindLimit (string name)
        {
            foreach (QueryLimit limit in Limits) {
                if (limit.Name == name)
                    return limit;
            }
            return null;
        }

        public static QueryOrder FindOrder (string name, bool asc)
        {
            return FindOrder (CreateOrderName (name, asc));
        }

        public static QueryOrder FindOrder (string name)
        {
            foreach (QueryOrder order in Orders) {
                if (order != null && order.Name == name) {
                    return order;
                }
            }
            return null;
        }

        private static string CreateOrderName (string name, bool asc)
        {
            return String.Format ("{0}-{1}", name, asc ? "ASC" : "DESC");
        }
    }
}
