//
// RandomByArtist.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2009 Novell, Inc.
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

using Hyena;
using Hyena.Data;
using Hyena.Data.Sqlite;

using Banshee.ServiceStack;
using Banshee.PlaybackController;
using Mono.Unix;

namespace Banshee.Collection.Database
{
    public class RandomByArtist : RandomBy
    {
        private HyenaSqliteCommand query;
        private int? id;

        public RandomByArtist () : base ("artist")
        {
            Label = Catalog.GetString ("Shuffle by A_rtist");
            Adverb = Catalog.GetString ("by artist");
            Description = Catalog.GetString ("Play all songs by an artist, then randomly choose another artist");

            Condition = "CoreAlbums.ArtistID = ?";
            OrderBy = "CoreTracks.Year, CoreTracks.AlbumID ASC, Disc ASC, TrackNumber ASC";
        }

        protected override void OnModelAndCacheUpdated ()
        {
            query = null;
        }

        public override void Reset ()
        {
            id = null;
        }

        public override bool IsReady { get { return id != null; } }

        public override bool Next (DateTime after)
        {
            Reset ();

            using (var reader = ServiceManager.DbConnection.Query (Query, after, after)) {
                if (reader.Read ()) {
                    id = Convert.ToInt32 (reader[0]);
                }
            }

            return IsReady;
        }

        public override void SetLastTrack (TrackInfo track)
        {
            var dbtrack = track as DatabaseTrackInfo;
            if (dbtrack != null) {
                var new_id = dbtrack.Album.ArtistId;
                if (new_id != id) {
                    id = new_id;
                }
            }
        }

        protected override IEnumerable<object> GetConditionParameters (DateTime after)
        {
            yield return id == null ? (int)0 : (int)id;
        }

        private HyenaSqliteCommand Query {
            get {
                if (query == null) {
                    query = new HyenaSqliteCommand (String.Format (@"
                            SELECT
                                CoreAlbums.ArtistID,
                                CoreAlbums.ArtistName,
                                MAX(CoreTracks.LastPlayedStamp) as LastPlayed,
                                MAX(CoreTracks.LastSkippedStamp) as LastSkipped
                            FROM
                                CoreTracks, CoreAlbums, CoreCache {0}
                            WHERE
                                {1}
                                CoreCache.ModelID = {2} AND
                                CoreTracks.AlbumID = CoreAlbums.AlbumID AND
                                CoreTracks.LastStreamError = 0
                                {3}
                            GROUP BY CoreTracks.AlbumID
                            HAVING
                                (LastPlayed < ? OR LastPlayed IS NULL) AND
                                (LastSkipped < ? OR LastSkipped IS NULL)
                            ORDER BY RANDOM()
                            LIMIT 1",
                        Model.JoinFragment,
                        Model.CachesJoinTableEntries
                            ? String.Format ("CoreCache.ItemID = {0}.{1} AND", Model.JoinTable, Model.JoinPrimaryKey)
                            : "CoreCache.ItemId = CoreTracks.TrackID AND",
                        Model.CacheId,
                        Model.ConditionFragment
                    ));
                }
                return query;
            }
        }
    }
}
