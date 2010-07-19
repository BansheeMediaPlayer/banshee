//
// BaseMetadataProvider.cs
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
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Banshee.Base;
using Banshee.Collection;
using Banshee.Streaming;

namespace Banshee.Metadata
{
    public abstract class BaseMetadataProvider : IMetadataProvider
    {
        public event MetadataLookupResultHandler HaveResult;
        public event Action<IBasicTrackInfo> ArtworkUpdated;

        protected BaseMetadataProvider()
        {
        }

        public abstract IMetadataLookupJob CreateJob(IBasicTrackInfo track);

        public virtual void Lookup(IBasicTrackInfo track)
        {
            IMetadataLookupJob job = CreateJob(track);
            job.Run();
        }

        public virtual void Cancel(IBasicTrackInfo track)
        {
        }

        public virtual void Cancel()
        {
        }

        protected virtual void OnHaveResult(IBasicTrackInfo track, IList<StreamTag> tags)
        {
            if(tags == null || tags.Count == 0) {
                return;
            }

            if (tags.Any (t => t.Name == CommonTags.AlbumCoverId)) {
                var artwork_handler = ArtworkUpdated;
                if (artwork_handler != null) {
                    artwork_handler (track);
                }
            }

            MetadataLookupResultHandler handler = HaveResult;
            if(handler != null) {
                handler(this, new MetadataLookupResultArgs(track,
                    new ReadOnlyCollection<StreamTag>(tags)));
            }
        }
    }
}
