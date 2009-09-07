//
// RandomBySlot.cs
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
    public abstract class RandomBySlot : RandomBy
    {
        private static Random random = new Random ();

        private HyenaSqliteCommand query;
        protected int slot;

        public RandomBySlot (PlaybackShuffleMode mode) : base (mode)
        {
        }

        protected override void OnModelAndCacheUpdated ()
        {
            query = null;
        }

        public override void Reset ()
        {
            slot = -1;
        }

        public override bool IsReady { get { return slot != -1; } }

        public override bool Next (DateTime after)
        {
            Reset ();

            // counts[x] = number of tracks in slot x.
            int[] counts = new int[Slots];
            int default_slot = (Slots - 1) / 2;

            // Get the distribution for tracks that haven't been played since stamp.
            using (var reader = ServiceManager.DbConnection.Query (Query, after, after)) {
                while (reader.Read ()) {
                    int s = Convert.ToInt32 (reader[0]);
                    int count = Convert.ToInt32 (reader[1]);

                    if (s < 0 || s >= Slots) {
                        s = default_slot;
                    }

                    counts[s] += count;
                }
            }

            if (counts.Sum () == 0) {
                slot = -1;
                return false;
            }

            // We will use powers of phi as weights. Such weights result in songs rated R played as often as songs
            // rated R-1 and R-2 combined. The exponent is adjusted to the number of slots when it's different from 5.
            const double phi = 1.618033989;

            // If you change the weights make sure ALL of them are strictly positive.
            var weights = Enumerable.Range (0, Slots).Select (i => Math.Pow (phi, i * 5 / (double) Slots)).ToArray ();

            // Apply weights to the counts.
            var weighted_counts = counts.Select ((c, i) => c * weights[i]);

            // Normalise the counts.
            var weighted_total = weighted_counts.Sum ();
            weighted_counts = weighted_counts.Select (c => c / weighted_total);

            // Now that we have our counts, get the slot a weighted random track belongs to.
            double random_value = random.NextDouble ();
            int current_slot = -1;
            foreach (var weighted_count in weighted_counts) {
                current_slot++;
                random_value -= weighted_count;
                if (random_value <= 0.0) {
                    break;
                }
            }

            slot = current_slot;
            return IsReady;
        }

        private HyenaSqliteCommand Query {
            get {
                if (query == null) {
                    query = new HyenaSqliteCommand (String.Format (QuerySql,
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

        protected abstract int Slots { get; }
        protected abstract string QuerySql { get; }
    }
}
