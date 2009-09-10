//
// PlayQueueTrackListModel.cs
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

using Banshee.Database;
using Banshee.Collection;
using Banshee.Collection.Database;

namespace Banshee.PlayQueue
{
    public class PlayQueueTrackListModel : DatabaseTrackListModel
    {
        private readonly PlayQueueSource source;

        public PlayQueueTrackListModel (
            BansheeDbConnection conn, IDatabaseTrackModelProvider provider, PlayQueueSource source) :
            base (conn, provider, source)
        {
            this.source = source;
        }

        public override TrackInfo this[int index] {
            get {
                lock (this) {
                    var track = cache.GetValue (index);
                    if (track != null) {
                        track.Enabled = source.IsTrackEnabled (index);
                    }
                    return track;
                }
            }
        }

        public override string UnfilteredQuery {
           get {
               return String.Format ("{0} AND ViewOrder >= {1}", base.UnfilteredQuery, source.Offset);
           }
       }
   }
}