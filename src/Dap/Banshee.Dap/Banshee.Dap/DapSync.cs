//
// DapSync.cs
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
using System.Linq;
using System.Collections.Generic;

using Mono.Unix;

using Hyena;
using Hyena.Query;

using Banshee.Base;
using Banshee.Configuration;
using Banshee.Sources;
using Banshee.ServiceStack;
using Banshee.Library;
using Banshee.Playlist;
using Banshee.SmartPlaylist;
using Banshee.Query;
using Banshee.Preferences;

using Banshee.Gui;

namespace Banshee.Dap
{
    public sealed class DapSync : IDisposable
    {
        private DapSource dap;
        private string conf_ns;

        private List<DapLibrarySync> library_syncs = new List<DapLibrarySync> ();
        private object library_syncs_mutex = new object ();

        internal SchemaEntry<bool> LegacyManuallyManage;
        private SchemaEntry<bool> auto_sync;
        private Section sync_prefs;
        //private PreferenceBase manually_manage_pref;//, auto_sync_pref;
        private SchemaPreference<bool> auto_sync_pref;
        private List<Section> pref_sections = new List<Section> ();
        private RateLimiter sync_limiter;

        public event Action<DapSync> Updated;
        public event Action<DapLibrarySync> LibraryAdded;
        public event Action<DapLibrarySync> LibraryRemoved;

        internal string ConfigurationNamespace {
            get { return conf_ns; }
        }

        #region Public Properites

        public DapSource Dap {
            get { return dap; }
        }

        public IEnumerable<DapLibrarySync> Libraries {
            get {
                lock (library_syncs_mutex) {
                    return library_syncs.ToArray ();
                }
            }
        }

        public bool Enabled {
            get {
                lock (library_syncs_mutex) {
                    return library_syncs.Any (l => l.Enabled);
                }
            }
        }

        public bool AutoSync {
            get { return auto_sync.Get (); }
        }

        public IEnumerable<Section> PreferenceSections {
            get { return pref_sections; }
        }

        #endregion

        public DapSync (DapSource dapSource)
        {
            dap = dapSource;
            sync_limiter = new RateLimiter (RateLimitedSync);
            BuildPreferences ();
            BuildSyncLists ();
            UpdateSensitivities ();
        }

        public void Dispose ()
        {
            lock (library_syncs_mutex) {
                foreach (DapLibrarySync sync in library_syncs) {
                    sync.Library.TracksAdded -= OnLibraryChanged;
                    sync.Library.TracksDeleted -= OnLibraryChanged;
                    sync.Dispose ();
                }
            }

            var src_mgr = ServiceManager.SourceManager;
            src_mgr.SourceAdded   -= OnSourceAdded;
            src_mgr.SourceRemoved -= OnSourceRemoved;
        }

        private void OnSourceAdded (SourceEventArgs a)
        {
            AddLibrary (a.Source, true);
        }

        private void OnSourceRemoved (SourceEventArgs a)
        {
            RemoveLibrary (a.Source);
        }

        private void BuildPreferences ()
        {
            conf_ns = "sync";
            LegacyManuallyManage = dap.CreateSchema<bool> (conf_ns, "enabled", false, "", "");

            auto_sync = dap.CreateSchema<bool> (conf_ns, "auto_sync", false,
                Catalog.GetString ("Sync when first plugged in and when the libraries change"),
                Catalog.GetString ("Begin synchronizing the device as soon as the device is plugged in or the libraries change.")
            );

            sync_prefs = new Section ("sync", Catalog.GetString ("Sync Preferences"), 0);
            pref_sections.Add (sync_prefs);

            sync_prefs.Add (new VoidPreference ("library-options"));

            auto_sync_pref = sync_prefs.Add (auto_sync);
            auto_sync_pref.ValueChanged += OnAutoSyncChanged;
        }

        private bool dap_loaded = false;
        public void DapLoaded ()
        {
            dap_loaded = true;
        }

        private void BuildSyncLists ()
        {
            var src_mgr = ServiceManager.SourceManager;
            src_mgr.SourceAdded   += OnSourceAdded;
            src_mgr.SourceRemoved += OnSourceRemoved;

            var sources = src_mgr.Sources.ToList ();
            foreach (var src in sources) {
                AddLibrary (src, false);
            }

            SortLibraries ();

            dap.TracksAdded += OnDapChanged;
            dap.TracksDeleted += OnDapChanged;
        }

        private void AddLibrary (Source source, bool initialized)
        {
            var library = GetSyncableLibrary (source);
            if (library != null) {
                var sync = new DapLibrarySync (this, library);
                lock (library_syncs_mutex) {
                    library_syncs.Add (sync);
                }
                library.TracksAdded += OnLibraryChanged;
                library.TracksDeleted += OnLibraryChanged;

                if (initialized) {
                    SortLibraries ();

                    var h = LibraryAdded;
                    if (h != null) {
                        h (sync);
                    }
                }
            }
        }

        private void RemoveLibrary (Source source)
        {
            var library = GetSyncableLibrary (source);
            if (library != null) {

                DapLibrarySync sync = null;
                lock (library_syncs_mutex) {
                    sync = library_syncs.FirstOrDefault (s => s.Library == library);
                }
                if (sync == null) {
                    return;
                }

                library_syncs.Remove (sync);
                sync.Library.TracksAdded -= OnLibraryChanged;
                sync.Library.TracksDeleted -= OnLibraryChanged;

                var h = LibraryRemoved;
                if (h != null) {
                    h (sync);
                }

                sync.Dispose ();
            }
        }

        private void SortLibraries ()
        {
            lock (library_syncs_mutex) {
                library_syncs.Sort ((a, b) => a.Library.Order.CompareTo (b.Library.Order));
            }
        }

        private void UpdateSensitivities ()
        {
            bool enabled = Enabled;
            if (!enabled && auto_sync_pref.Value) {
                auto_sync_pref.Value = false;
            }
            auto_sync_pref.Sensitive = enabled;
        }

        private void OnAutoSyncChanged (Root preference)
        {
            MaybeTriggerAutoSync ();
        }

        private void OnDapChanged (Source sender, TrackEventArgs args)
        {
            if (!AutoSync && dap_loaded && !Syncing) {
                lock (library_syncs_mutex) {
                    foreach (DapLibrarySync lib_sync in library_syncs) {
                        lib_sync.CalculateSync ();
                    }
                }
            }
        }

        private void OnLibraryChanged (Source sender, TrackEventArgs args)
        {
            if (!Enabled) {
                return;
            }

            DapLibrarySync lib_to_sync = null;
            lock (library_syncs_mutex) {
                lib_to_sync = library_syncs.FirstOrDefault (lib_sync => lib_sync.Library == sender);
            }
            if (lib_to_sync != null) {
                if (AutoSync && lib_to_sync.Enabled) {
                    Sync ();
                } else {
                    lib_to_sync.CalculateSync ();
                    OnUpdated ();
                }
            }
        }

        private LibrarySource GetSyncableLibrary (Source source)
        {
            var library = source as LibrarySource;
            if (library == null)
                return null;

                //List<Source> sources = new List<Source> (ServiceManager.SourceManager.Sources);
                //sources.Sort (delegate (Source a, Source b) {
                    //return a.Order.CompareTo (b.Order);
                //});

            if (!dap.SupportsVideo && library == ServiceManager.SourceManager.VideoLibrary) {
                return null;
            }

            if (!dap.SupportsPodcasts && library.UniqueId == "PodcastSource-PodcastLibrary") {
                return null;
            }

            return library;
        }

        public int ItemCount {
            get { return 0; }
        }

        public long FileSize {
            get { return 0; }
        }

        public TimeSpan Duration {
            get { return TimeSpan.Zero; }
        }

        public void CalculateSync ()
        {
            lock (library_syncs_mutex) {
                foreach (DapLibrarySync library_sync in library_syncs) {
                    library_sync.CalculateSync ();
                }
            }

            OnUpdated ();
        }

        internal void MaybeTriggerAutoSync ()
        {
            UpdateSensitivities ();
            OnUpdated ();
            if (Enabled && AutoSync) {
                Sync ();
            }
        }

        internal void OnUpdated ()
        {
            Action<DapSync> handler = Updated;
            if (handler != null) {
                handler (this);
            }
        }

        public override string ToString ()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder ();
            lock (library_syncs_mutex) {
                foreach (DapLibrarySync library_sync in library_syncs) {
                    sb.Append (library_sync.ToString ());
                    sb.Append ("\n");
                }
            }
            return sb.ToString ();
        }

        public void Sync ()
        {
            ThreadAssist.SpawnFromMain (delegate {
                sync_limiter.Execute ();
            });
        }

        private void RateLimitedSync ()
        {
            syncing = true;

            bool sync_playlists = false;
            if (dap.SupportsPlaylists) {
                lock (library_syncs_mutex) {
                    foreach (DapLibrarySync library_sync in library_syncs) {
                        if (library_sync.Library.SupportsPlaylists) {
                            sync_playlists = true;
                            break;
                        }
                    }
                }
            }

            if (sync_playlists) {
                dap.RemovePlaylists ();
            }

            foreach (DapLibrarySync library_sync in Libraries) {
                try {
                    library_sync.Sync ();
                } catch (DapLibrarySync.PossibleUserErrorException e) {

                    var tracks_to_remove = e.TracksToRemove;
                    string msg = String.Format (
                        Catalog.GetPluralString (
                        // singular form unused b/c we know it's > 1, but we still need GetPlural
                        "The sync operation will remove one track from your device.",
                        "The sync operation will remove {0} tracks from your device.",
                        tracks_to_remove),
                        tracks_to_remove);

                    if (TrackActions.ConfirmRemove (msg)) {
                        library_sync.Sync (true);
                    }

                }
            }

            if (sync_playlists) {
                dap.SyncPlaylists ();
            }

            syncing = false;
        }

        private bool syncing = false;
        public bool Syncing {
            get { return syncing; }
        }
    }
}
