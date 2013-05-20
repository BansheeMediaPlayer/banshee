//
// RandomByAlbum.cs
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

using Banshee.Query;
using Banshee.ServiceStack;
using Banshee.PlaybackController;
using Mono.Unix;

namespace Banshee.Collection.Database
{
    public class RandomByAlbum : RandomBy
    {
        private HyenaSqliteCommand album_query;
        private long? album_id;

        public RandomByAlbum () : base ("album")
        {
            Label = Catalog.GetString ("Shuffle by A_lbum");
            Adverb = Catalog.GetString ("by album");
            Description = Catalog.GetString ("Play all songs from an album, then randomly choose another album");

            Condition = "CoreTracks.AlbumID = ?";
            OrderBy = "Disc ASC, TrackNumber ASC";
        }

        protected override void OnModelAndCacheUpdated ()
        {
            album_query = null;
        }

        public override void Reset ()
        {
            album_id = null;
        }

        public override bool IsReady {
            get { return album_id != null; }
        }

        public override bool Next (DateTime after)
        {
            Reset ();

            using (var reader = ServiceManager.DbConnection.Query (AlbumQuery, after, after)) {
                if (reader.Read ()) {
                    album_id = Convert.ToInt32 (reader[0]);
                }
            }

            return IsReady;
        }

        public override void SetLastTrack (TrackInfo track)
        {
            var dbtrack = track as DatabaseTrackInfo;
            if (dbtrack != null && dbtrack.AlbumId != album_id) {
                album_id = dbtrack.AlbumId;
            }
        }

        protected override IEnumerable<object> GetConditionParameters (DateTime after)
        {
            yield return album_id == null ? (int)0 : (int)album_id;
        }

        private HyenaSqliteCommand AlbumQuery {
            get {
                if (album_query == null) {
                    album_query = new HyenaSqliteCommand (String.Format (@"
                            SELECT
                                CoreAlbums.AlbumID,
                                CoreAlbums.Title,
                                MAX({4}) as LastPlayed,
                                MAX({5}) as LastSkipped
                            FROM
                                CoreTracks, CoreAlbums, CoreCache {0}
                            WHERE
                                {1}
                                CoreCache.ModelID = {2} AND
                                CoreTracks.AlbumID = CoreAlbums.AlbumID AND
                                {6} = 0
                                {3}
                            GROUP BY CoreTracks.AlbumID
                            HAVING
                                (LastPlayed < ? OR LastPlayed IS NULL) AND
                                (LastSkipped < ? OR LastSkipped IS NULL)
                            ORDER BY {7}
                            LIMIT 1",
                        Model.JoinFragment,
                        Model.CachesJoinTableEntries
                            ? String.Format ("CoreCache.ItemID = {0}.{1} AND", Model.JoinTable, Model.JoinPrimaryKey)
                            : "CoreCache.ItemId = CoreTracks.TrackID AND",
                        Model.CacheId,
                        Model.ConditionFragment,
                        BansheeQuery.LastPlayedField.Column,
                        BansheeQuery.LastSkippedField.Column,
                        BansheeQuery.PlaybackErrorField.Column,
                        BansheeQuery.GetRandomSort ()
                    ));
                }
                return album_query;
            }
        }
    }
}
