//
// RandomBy.cs
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

using Hyena;
using Hyena.Data;
using Hyena.Data.Sqlite;

using Banshee.ServiceStack;
using Banshee.PlaybackController;

namespace Banshee.Collection.Database
{
    public abstract class RandomBy
    {
        protected const string RANDOM_CONDITION = "AND LastStreamError = 0 AND (LastPlayedStamp < ? OR LastPlayedStamp IS NULL) AND (LastSkippedStamp < ? OR LastSkippedStamp IS NULL)";

        protected DatabaseTrackListModel Model { get; private set; }
        protected IDatabaseTrackModelCache Cache { get; private set; }

        public virtual bool IsReady { get { return true; } }
        public PlaybackShuffleMode Mode { get; private set; }

        public RandomBy (PlaybackShuffleMode mode)
        {
            Mode = mode;
        }

        public void SetModelAndCache (DatabaseTrackListModel model, IDatabaseTrackModelCache cache)
        {
            if (Model != model) {
                Model = model;
                Cache = cache;
                Reset ();

                OnModelAndCacheUpdated ();
            }
        }

        protected virtual void OnModelAndCacheUpdated ()
        {
        }

        public virtual void Reset () {}
        public abstract bool Next (DateTime after);
        public abstract TrackInfo GetTrack (DateTime after);
    }
}
