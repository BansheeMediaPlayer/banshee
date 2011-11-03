//
// RandomByScore.cs
//
// Author:
//   Alexander Kojevnikov <alexander@kojevnikov.com>
//
// Copyright (C) 2009 Alexander Kojevnikov
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

using Mono.Unix;

using Banshee.Query;
using Banshee.PlaybackController;

namespace Banshee.Collection.Database
{
    public class RandomByScore : RandomBySlot
    {
        public RandomByScore () : base ("score")
        {
            Label = Catalog.GetString ("Shuffle by S_core");
            Adverb = Catalog.GetString ("by score");
            Description = Catalog.GetString ("Play songs randomly, prefer higher scored songs");

            Condition = String.Format ("({0} BETWEEN ? AND ? OR (? = 50 AND {0} = 0))",
                                       BansheeQuery.ScoreField.Column);
            OrderBy = BansheeQuery.GetRandomSort ();
        }

        protected override IEnumerable<object> GetConditionParameters (DateTime after)
        {
            int min = slot * 100 / Slots + 1;
            int max = (slot + 1) * 100 / Slots;

            yield return min;
            yield return max;
            yield return max;
            Reset ();
        }

        protected override int Slots {
            get { return 20; }
        }

        protected override string PlaybackSlotQuerySql {
            get {
                // NOTE: SQLite wrongly assumes that (-1)/5 == 0, the CASE WHEN works around this.
                return @"
                    SELECT
                        CASE WHEN IFNULL(CoreTracks.Score, 0) = 0 THEN -1 ELSE (CoreTracks.Score - 1) * 20 / 100 END AS Slot, COUNT(*)
                    FROM
                        CoreTracks, CoreCache {0}
                    WHERE
                        {1}
                        CoreCache.ModelID = {2} AND
                        CoreTracks.LastStreamError = 0 AND
                        (CoreTracks.LastPlayedStamp < ? OR CoreTracks.LastPlayedStamp IS NULL) AND
                        (CoreTracks.LastSkippedStamp < ? OR CoreTracks.LastSkippedStamp IS NULL)
                        {3}
                    GROUP BY Slot";
            }
        }

        protected override string ShufflerSlotQuerySql {
            get {
                // NOTE: SQLite wrongly assumes that (-1)/5 == 0, the CASE WHEN works around this.
                return @"
                    SELECT
                        CASE WHEN IFNULL(CoreTracks.Score, 0) = 0 THEN -1 ELSE (CoreTracks.Score - 1) * 20 / 100 END AS Slot, COUNT(*)
                    FROM
                        CoreTracks LEFT OUTER JOIN CoreShuffles ON (CoreShuffles.ShufflerId = " + Shuffler.DbId.ToString () +
                    @" AND CoreShuffles.TrackID = CoreTracks.TrackID)
                        {0}
                    WHERE
                        CoreTracks.LastStreamError = 0 AND
                        (CoreShuffles.LastShuffledAt < ? OR CoreShuffles.LastShuffledAt IS NULL)
                        {3}
                    GROUP BY Slot";
            }
        }
    }
}
