//
// DapLibrarySync.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
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
using Hyena.Query;

using Banshee.Configuration;
using Banshee.Preferences;
using Banshee.Sources;
using Banshee.ServiceStack;
using Banshee.Library;
using Banshee.Playlist;
using Banshee.SmartPlaylist;
using Banshee.Query;

namespace Banshee.Dap
{ 
    public sealed class DapLibrarySync
    {
        private DapSync sync;
        private LibrarySource library;
        private string conf_ns;
        private SchemaEntry<bool> enabled, sync_entire_library;
        private SchemaEntry<string[]> playlist_ids;
        private SmartPlaylistSource sync_src, to_add, to_remove;
        private Section library_prefs_section;
        
        #region Public Properties

        public bool Enabled {
            get { return sync.Enabled && enabled.Get (); }
        }
        
        public bool SyncEntireLibrary {
            get { return sync_entire_library.Get (); }
        }

        public Section PrefsSection {
            get { return library_prefs_section; }
        }

        public LibrarySource Library {
            get { return library; }
        }
        
        #endregion
        
        public string [] SyncPlaylistIds {
            get { return playlist_ids.Get (); }
        }
        
        private IList<AbstractPlaylistSource> GetSyncPlaylists ()
        {
            List<AbstractPlaylistSource> playlists = new List<AbstractPlaylistSource> ();
            foreach (Source child in library.Children) {
                if (child is AbstractPlaylistSource) {
                    playlists.Add (child as AbstractPlaylistSource);
                }
            }
            return playlists;
        }

        internal string SmartPlaylistId {
            get { return sync_src.DbId.ToString (); }
        }
        
        internal DapLibrarySync (DapSync sync, LibrarySource library)
        {
            this.sync = sync;
            this.library = library;

            BuildPreferences ();
            BuildSyncLists ();
        }

        internal void Dispose ()
        {
            if (to_add != null)
                to_add.Unmap ();

            if (to_remove != null)
                to_remove.Unmap ();

            if (sync_src != null)
                sync_src.Unmap ();
        }

        private void BuildPreferences ()
        {
            conf_ns = String.Format ("{0}.{1}", sync.ConfigurationNamespace, library.ParentConfigurationId);
            
            enabled = sync.Dap.CreateSchema<bool> (conf_ns, "enabled", true,
                String.Format (Catalog.GetString ("Sync {0}"), library.Name), "");
            
            sync_entire_library = sync.Dap.CreateSchema<bool> (conf_ns, "sync_entire_library", true,
                "Whether to sync the entire library and all playlists.", "");
            
            playlist_ids = sync.Dap.CreateSchema<string[]> (conf_ns, "playlist_ids", new string [0],
                "If sync_entire_library is false, this contains a list of playlist ids specifically to sync", "");

            library_prefs_section = new Section (String.Format ("{0} sync", library.Name), library.Name, 0);
            library_prefs_section.Add<bool> (enabled);
        }

        private void BuildSyncLists ()
        {
            // This smart playlist is the list of items we want on the device - nothing more, nothing less
            sync_src = new SmartPlaylistSource ("sync_list", library);
            sync_src.IsTemporary = true;
            sync_src.Save ();
            sync_src.AddCondition (library.AttributesCondition);
            sync_src.AddCondition (library.SyncCondition);

            // This is the same as the previous list with the items that are already on the device removed
            to_add = new SmartPlaylistSource ("to_add", library);
            to_add.IsTemporary = true;
            to_add.Save ();
            to_add.ConditionTree = UserQueryParser.Parse (String.Format ("smartplaylistid:{0}", sync_src.DbId),
                Banshee.Query.BansheeQuery.FieldSet);
            to_add.DatabaseTrackModel.AddCondition (String.Format (
                "MetadataHash NOT IN (SELECT MetadataHash FROM CoreTracks WHERE PrimarySourceId = {0})", sync.Dap.DbId
            ));

            // Any items on the device that aren't in the sync lists need to be removed
            to_remove = new SmartPlaylistSource ("to_remove", sync.Dap);
            to_remove.IsTemporary = true;
            to_remove.Save ();
            to_remove.AddCondition (library.AttributesCondition);
            to_remove.AddCondition (String.Format (
                @"MetadataHash NOT IN (SELECT MetadataHash FROM CoreTracks, CoreSmartPlaylistEntries 
                    WHERE CoreSmartPlaylistEntries.SmartPlaylistID = {0} AND
                        CoreTracks.TrackID = CoreSmartPlaylistEntries.TrackID)",
                sync_src.DbId
            ));
        }
        
        internal void CalculateSync ()
        {
            if (SyncEntireLibrary) {
                sync_src.ConditionTree = null;
            }/* else if (SyncPlaylistIds.Length > 0) {
                QueryListNode playlists_node = new QueryListNode (Keyword.Or);
                foreach (AbstractPlaylistSource src in SyncPlaylists) {
                    if (src is PlaylistSource) {
                        playlists_node.AddChild (UserQueryParser.Parse (String.Format ("playlistid:{0}", src.DbId), BansheeQuery.FieldSet));
                    } else if (src is SmartPlaylistSource) {
                        playlists_node.AddChild (UserQueryParser.Parse (String.Format ("smartplaylistid:{0}", src.DbId), BansheeQuery.FieldSet));
                    }
                }
                sync_src.ConditionTree = playlists_node;
            }*/
            sync_src.RefreshAndReload ();
            to_add.RefreshAndReload ();
            to_remove.RefreshAndReload ();
        }

        public override string ToString ()
        {
            return String.Format ("Sync calculated for {1}: to add: {0} items, remove {2} items; sync_src.cacheid = {5}, to_add.cacheid = {3}, to_remove.cacheid = {4}", to_add.Count, library.Name, to_remove.Count,
                                  to_add.DatabaseTrackModel.CacheId, to_remove.DatabaseTrackModel.CacheId, sync_src.DatabaseTrackModel.CacheId);
        }
        
        internal void Sync ()
        {
            if (Enabled) {
                Log.InformationFormat ("Syncing {0}, in main thread? {1}", library, Banshee.Base.ThreadAssist.InMainThread);
                if (to_remove.Count > 0) {
                    Log.DebugFormat ("deleting items for {0} - {1} items; to_remove is smartplaylist {2}, with cachid {3}", library.Name, to_remove.Count, to_remove.DbId, to_remove.DatabaseTrackModel.CacheId);
                    sync.Dap.DeleteAllTracks (to_remove);
                }

                if (to_add.Count > 0) {
                    sync.Dap.AddAllTracks (to_add);
                }

                if (library.SupportsPlaylists && sync.Dap.SupportsPlaylists) {
                    
                    // Now create the playlists, taking snapshots of smart playlists and saving them
                    // as normal playlists
                    IList<AbstractPlaylistSource> playlists = GetSyncPlaylists ();
                    foreach (AbstractPlaylistSource from in playlists) {
                        if (from.Count == 0) {
                            continue;
                        }
                        PlaylistSource to = new PlaylistSource (from.Name, sync.Dap);
                        to.Save ();

                        ServiceManager.DbConnection.Execute (
                            String.Format (
                                @"INSERT INTO CorePlaylistEntries (PlaylistID, TrackID)
                                    SELECT ?, TrackID FROM CoreTracks WHERE PrimarySourceID = ? AND MetadataHash IN 
                                        (SELECT MetadataHash FROM {0} WHERE {1})",
                                from.DatabaseTrackModel.ConditionFromFragment, from.DatabaseTrackModel.Condition),
                            to.DbId, sync.Dap.DbId
                        );
                        to.UpdateCounts ();

                        if (to.Count == 0) {
                            // If it's empty, don't leave it on the device
                            to.Unmap ();
                        } else {
                            sync.Dap.AddChildSource (to);
                        }
                    }
                }

                CalculateSync ();
                sync.OnUpdated ();
            }
        }
    }
}
