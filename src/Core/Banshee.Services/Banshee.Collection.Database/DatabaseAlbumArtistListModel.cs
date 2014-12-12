//
// DatabaseAlbumArtistListModel.cs
//
// Author:
//   Frank Ziegler <funtastix@googlemail.com>
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

using Hyena;
using Hyena.Data.Sqlite;
using Hyena.Query;

using Banshee.Database;

namespace Banshee.Collection.Database
{
    public class DatabaseAlbumArtistListModel : DatabaseFilterListModel<DatabaseAlbumArtistInfo, ArtistInfo>
    {
        public DatabaseAlbumArtistListModel (Banshee.Sources.DatabaseSource source, DatabaseTrackListModel trackModel, BansheeDbConnection connection, string uuid)
            : base (Banshee.Query.BansheeQuery.AlbumArtistField.Name, Banshee.Query.BansheeQuery.AlbumArtistField.Label,
                    source, trackModel, connection, DatabaseAlbumArtistInfo.Provider, new ArtistInfo (null, null), uuid)
        {
            QueryFields = new QueryFieldSet (Banshee.Query.BansheeQuery.ArtistField);
            ReloadFragmentFormat = @"
                FROM CoreArtists WHERE CoreArtists.ArtistID IN
                    (SELECT CoreAlbums.ArtistID FROM CoreAlbums, CoreTracks, CoreCache{0}
                        WHERE {4}(CoreCache.ModelID = {1}) AND
                              CoreTracks.AlbumID = CoreAlbums.AlbumID AND
                              EXISTS (SELECT 1 FROM CoreArtists WHERE ArtistID = CoreAlbums.ArtistID) AND
                              CoreCache.ItemID = {2} {3})
                        ORDER BY NameSortKey";
        }

        public override string FilterColumn {
            get { return "CoreTracks.AlbumID"; }
        }

        protected override string ItemToFilterValue (object item)
        {
            return (item is DatabaseAlbumArtistInfo) ? "SELECT CoreAlbums.AlbumID FROM CoreAlbums WHERE CoreAlbums.ArtistID = " + (item as DatabaseAlbumArtistInfo).DbId.ToString () : null;
        }

        public override string GetSqlFilter ()
        {
            string res = base.GetSqlFilter ();
            if (String.IsNullOrEmpty (res)) {
                return res;
            }
            return res.Replace (","," UNION ");
        }

        public override void UpdateSelectAllItem (long count)
        {
            select_all_item.Name = String.Format (Catalog.GetString ("All Album Artists ({0})"), count);
        }
    }
}
