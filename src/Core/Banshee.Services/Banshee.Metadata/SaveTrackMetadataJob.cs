//
// SaveTrackMetadataJob.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//   Gabriel Burt <gburt@novell.com>
//   Ruben Vermeersch <ruben@savanne.be>
//
// Copyright (C) 2006-2009 Novell, Inc.
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
using Mono.Unix;

using Hyena.Jobs;
using Hyena.Data.Sqlite;

using Banshee.Base;
using Banshee.Streaming;
using Banshee.Collection.Database;
using Banshee.Library;
using Banshee.ServiceStack;
using Banshee.Configuration.Schema;
using Banshee.Preferences;

namespace Banshee.Metadata
{
    public class SaveTrackMetadataJob : DbIteratorJob
    {
        private LibrarySource source = ServiceManager.SourceManager.MusicLibrary;

        public SaveTrackMetadataJob () : base (Catalog.GetString ("Saving Metadata to File"))
        {
            SetResources (Resource.Cpu, Resource.Disk, Resource.Database);
            IsBackground = true;

            CountCommand = new HyenaSqliteCommand (
                "SELECT COUNT(*) FROM CoreTracks WHERE DateUpdatedStamp > LastSyncedStamp AND PrimarySourceID = ?",
                source.DbId
            );

            SelectCommand = DatabaseTrackInfo.Provider.CreateFetchCommand (String.Format (
                "DateUpdatedStamp > LastSyncedStamp AND PrimarySourceID = {0}", source.DbId)
            );
        }

        public bool WriteEnabled { get; set; }
        public bool RenameEnabled { get; set; }

        private HyenaSqliteCommand update_synced_at;
    
        protected override void IterateCore (HyenaDataReader reader)
        {
            DatabaseTrackInfo track = DatabaseTrackInfo.Provider.Load (reader.Reader);

            bool wrote = false;
            bool renamed = false;
            try {
                if (WriteEnabled) {
                    Hyena.Log.DebugFormat ("Saving metadata for {0}", track);
                    wrote = StreamTagger.SaveToFile (track);
                }

                if (RenameEnabled) {
                    Hyena.Log.DebugFormat ("Updating file name for {0}", track);
                    renamed = RenameFile (track);
                    if (renamed && !wrote) {
                        track.LastSyncedStamp = DateTime.Now;
                    }
                }
            } catch (Exception) {
                Hyena.Log.ErrorFormat ("Error writing to or renaming {0}", track);
            } finally {
                if (wrote || renamed) {
                    // Save the resulting changes to FileSize, LastSyncedStamp, possibly to Uri, etc
                    // Clear track model caches if URI changed
                    track.Save (renamed);
                } else {
                    if (update_synced_at == null) {
                        update_synced_at = new HyenaSqliteCommand (
                            "UPDATE CoreTracks SET LastSyncedStamp = ? WHERE TrackID = ?");
                    }

                    ServiceManager.DbConnection.Execute (update_synced_at, DateTime.Now, track.TrackId);
                }
            }
        }

        private bool RenameFile (DatabaseTrackInfo track)
        {
            SafeUri old_uri = track.Uri;
            bool in_library = old_uri.AbsolutePath.StartsWith (source.BaseDirectoryWithSeparator);

            if (!in_library) {
                return false;
            }

            string new_filename = FileNamePattern.BuildFull (source.BaseDirectory, track, System.IO.Path.GetExtension (old_uri.ToString ()));
            SafeUri new_uri = new SafeUri (new_filename);

            if (!new_uri.Equals (old_uri) && !Banshee.IO.File.Exists (new_uri)) {
                Banshee.IO.File.Move (old_uri, new_uri);
                Banshee.IO.Utilities.TrimEmptyDirectories (old_uri);
                track.Uri = new_uri;
                return true;
            }

            return false;
        }
    }
}
