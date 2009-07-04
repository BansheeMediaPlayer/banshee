//
// RandomByRating.cs
//
// Authors:
//   Elena Grassi <grassi.e@gmail.com>
//   Alexander Kojevnikov <alexander@kojevnikov.com>
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2008 Elena Grassi
// Copyright (C) 2009 Alexander Kojevnikov
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
using System.Linq;

using Hyena;
using Hyena.Data;
using Hyena.Data.Sqlite;

using Banshee.ServiceStack;
using Banshee.PlaybackController;

namespace Banshee.Collection.Database
{
    public class RandomByRating : RandomBy
    {
        private static Random random = new Random ();

        private static string track_condition = String.Format ("AND (CoreTracks.Rating = ? OR (? = 3 AND CoreTracks.Rating = 0)) {0} ORDER BY RANDOM()", RANDOM_CONDITION);
        private HyenaSqliteCommand query;
        private int rating;

        public RandomByRating () : base (PlaybackShuffleMode.Rating)
        {
        }

        protected override void OnModelAndCacheUpdated ()
        {
            query = null;
        }

        public override void Reset ()
        {
            rating = 0;
        }

        public override bool IsReady { get { return rating != 0; } }

        public override bool Next (DateTime after)
        {
            Reset ();

            // Default rating for unrated songs.
            const int unrated_rating = 3;
            // counts[x] = number of tracks rated x + 1.
            int[] counts = new int[5];
            // Get the distribution of ratings for tracks that haven't been played since stamp.
            Console.WriteLine ("About to run rating query: {0}", Query.Text);
            using (var reader = ServiceManager.DbConnection.Query (Query, after, after)) {
                while (reader.Read ()) {
                    int r = Convert.ToInt32 (reader[0]);
                    int count = Convert.ToInt32 (reader[1]);

                    if (r < 1 || r > 5) {
                        r = unrated_rating;
                    }

                    counts[r - 1] += count;
                }
            }

            if (counts.Sum () == 0) {
                rating = 0;
                return false;
            }

            // We will use powers of phi as weights. Such weights result in songs rated R
            // played as often as songs rated R-1 and R-2 combined.
            const double phi = 1.618033989;

            // If you change the weights make sure ALL of them are strictly positive.
            var weights = Enumerable.Range (0, 5).Select (i => Math.Pow (phi, i)).ToArray ();

            // Apply weights to the counts.
            var weighted_counts = counts.Select ((c, i) => c * weights[i]);

            // Normalise the counts.
            var weighted_total = weighted_counts.Sum ();
            weighted_counts = weighted_counts.Select (c => c / weighted_total);

            // Now that we have our counts, get a weighted random rating.
            double random_value = random.NextDouble ();
            int current_rating = 0;
            foreach (var weighted_count in weighted_counts) {
                current_rating++;
                random_value -= weighted_count;
                if (random_value <= 0.0) {
                    break;
                }
            }

            rating = current_rating;
            return IsReady;
        }

        public override TrackInfo GetTrack (DateTime after)
        {
            return !IsReady ? null : Cache.GetSingle (track_condition, rating, rating, after, after);
        }

        private HyenaSqliteCommand Query {
            get {
                if (query == null) {
                    query = new HyenaSqliteCommand (String.Format (@"
                        SELECT
                            CoreTracks.Rating, COUNT(*)
                        FROM
                            CoreTracks, CoreCache {0}
                        WHERE
                            {1}
                            CoreCache.ModelID = {2} AND
                            CoreTracks.LastStreamError = 0 AND
                            (CoreTracks.LastPlayedStamp < ? OR CoreTracks.LastPlayedStamp IS NULL) AND
                            (CoreTracks.LastSkippedStamp < ? OR CoreTracks.LastSkippedStamp IS NULL)
                            {3}
                        GROUP BY CoreTracks.Rating",
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
