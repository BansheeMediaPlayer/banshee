//
// PaasTrackListModel.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//   Mike Urbanski <michael.c.urbanski@gmail.com>
//
// Copyright (C) 2009 Michael C. Urbanski
// Copyright (C) 2008 Novell, Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;

using Hyena.Data.Sqlite;

using Banshee.Sources;
using Banshee.Database;

using Banshee.Collection;
using Banshee.Collection.Database;

using Banshee.Paas.Gui;

namespace Banshee.Paas.Data
{
    public class PaasTrackListModel : DatabaseTrackListModel
    {
        public PaasTrackListModel (BansheeDbConnection conn, IDatabaseTrackModelProvider provider, DatabaseSource source) : base (conn, provider, source)
        {
            From = String.Format ("{0}, {1}, {2}", provider.From, PaasChannel.Provider.TableName, PaasItem.Provider.TableName);

            int paas_library_dbid = (source as PaasSource ?? source.Parent as PaasSource).DbId;
            AddCondition (From, String.Format (
                @"CoreTracks.PrimarySourceID = {2} AND 
                    {0}.ID = {1}.ChannelID         AND 
                    CoreTracks.ExternalID = {1}.ID AND
                    {1}.Active = 1",
                PaasChannel.Provider.TableName, PaasItem.Provider.TableName, paas_library_dbid
            ));            
        }

        protected override void GenerateSortQueryPart ()
        {
            SortQuery = (SortColumn == null)
                ? GetSort ("album", true)
                : GetSort (SortColumn.SortKey, SortColumn.SortType == Hyena.Data.SortType.Ascending);
        }
        
        public override void UpdateUnfilteredAggregates ()
        {
            HyenaSqliteCommand count_command = new HyenaSqliteCommand (String.Format (
                "SELECT COUNT(*) {0} AND PaasItems.Active = 1 AND PaasItems.DownloadedAt NOT NULL", UnfilteredQuery
            ));
            
            UnfilteredCount = Connection.Query<int> (count_command);
        }

        public static string GetSort (string key, bool asc)
        {
            string ascDesc = asc ? "ASC" : "DESC";
            string sort_query = null;
            Console.WriteLine ("Key:  {0}", key);
            switch (key) {
                case "PubDate":
                    sort_query = String.Format ("PaasItems.PubDate {0}", ascDesc);
                    break;
                case "IsNew":
                    sort_query = String.Format ("-PaasItems.IsNew {0}, PaasItems.PubDate DESC", ascDesc);
                    break;
                case "IsDownloaded":
                    sort_query = String.Format (@"
                        PaasItems.LocalPath IS NOT NULL {0}, PaasItems.PubDate DESC", ascDesc);
                    break;
                case "album":
                    sort_query = String.Format ("PaasChannels.Name {0}, PaasItems.PubDate DESC", ascDesc);
                    break;              
            }

            return sort_query ?? Banshee.Query.BansheeQuery.GetSort (key, asc);
        }
    }
}
