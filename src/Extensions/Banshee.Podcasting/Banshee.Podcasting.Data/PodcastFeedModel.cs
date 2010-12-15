/***************************************************************************
 *  PodcastFeedModel.cs
 *
 *  Copyright (C) 2007 Michael C. Urbanski
 *  Written by Mike Urbanski <michael.c.urbanski@gmail.com>
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW:
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),
 *  to deal in the Software without restriction, including without limitation
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,
 *  and/or sell copies of the Software, and to permit persons to whom the
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 *  DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Mono.Unix;

using Hyena.Data;

using Banshee.Database;
using Banshee.Collection.Database;
using Banshee.Podcasting.Data;

using Migo.Syndication;

namespace Banshee.Podcasting.Gui
{
    public class PodcastFeedModel : DatabaseFilterListModel<Feed, Feed>
    {
        public PodcastFeedModel (Banshee.Sources.DatabaseSource source, DatabaseTrackListModel trackModel, BansheeDbConnection connection, string uuid)
            : base ("podcast", Catalog.GetString ("Podcast"), source, trackModel, connection, Feed.Provider, new Feed (null, FeedAutoDownload.None), uuid)
        {
            ReloadFragmentFormat = @"
                FROM PodcastSyndications WHERE FeedID IN
                    (SELECT DISTINCT PodcastSyndications.FeedID FROM PodcastItems, CoreTracks, PodcastEnclosures, PodcastSyndications, CoreCache{0}
                        WHERE PodcastSyndications.FeedID = PodcastItems.FeedID AND
                          PodcastItems.ItemID = CoreTracks.ExternalID AND PodcastEnclosures.ItemID = PodcastItems.ItemID AND
                          CoreCache.ModelID = {1} AND CoreCache.ItemId = {2} {3})
                    ORDER BY HYENA_COLLATION_KEY(Title)";
        }

        public override string FilterColumn {
            get { return Feed.Provider.PrimaryKey; }
        }

        protected override string ItemToFilterValue (object item)
        {
            return (item != select_all_item && item is Feed) ? (item as Feed).DbId.ToString () : null;
        }

        public override void UpdateSelectAllItem (long count)
        {
            select_all_item.Title = String.Format (Catalog.GetString ("All Podcasts ({0})"), count);
        }
    }
}
