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

namespace Banshee.Dap
{
    public sealed class DapSync : IDisposable
    {
        private DapSource dap;
        private string conf_ns;
        private List<DapLibrarySync> library_syncs = new List<DapLibrarySync> ();
        private SchemaEntry<bool> manually_manage, auto_sync;
        private Section dap_prefs_section;
        private PreferenceBase manually_manage_pref;//, auto_sync_pref;
        private SchemaPreference<bool> auto_sync_pref;
        private List<Section> pref_sections = new List<Section> ();
        private RateLimiter sync_limiter;

        public event Action<DapSync> Updated;

        internal string ConfigurationNamespace {
            get { return conf_ns; }
        }
        
        #region Public Properites
        
        public DapSource Dap {
            get { return dap; }
        }

        public IEnumerable<DapLibrarySync> LibrarySyncs {
            get { return library_syncs; }
        }
        
        public bool Enabled {
            get { return !manually_manage.Get (); }
        }
        
        public bool AutoSync {
            get { return Enabled && auto_sync.Get (); }
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
            foreach (LibrarySource source in Libraries) {
                source.TracksAdded -= OnLibraryChanged;
                source.TracksDeleted -= OnLibraryChanged;
            }

            foreach (DapLibrarySync sync in library_syncs) {
                sync.Dispose ();
            }
        }

        private void BuildPreferences ()
        {
            conf_ns = "sync";
            manually_manage = dap.CreateSchema<bool> (conf_ns, "enabled", true,
                Catalog.GetString ("Manually manage this device"),
                Catalog.GetString ("Manually managing your device means you can drag and drop items onto the device, and manually remove them.")
            );
            
            auto_sync = dap.CreateSchema<bool> (conf_ns, "auto_sync", false,
                Catalog.GetString ("Automatically sync the device when plugged in or when the libraries change"),
                Catalog.GetString ("Begin synchronizing the device as soon as the device is plugged in or the libraries change.")
            );

            dap_prefs_section = new Section ("dap", Catalog.GetString ("Sync Preferences"), 0);
            pref_sections.Add (dap_prefs_section);

            manually_manage_pref = dap_prefs_section.Add (manually_manage);
            manually_manage_pref.ShowDescription = true;
            manually_manage_pref.ShowLabel = false;
            manually_manage_pref.ValueChanged += OnManuallyManageChanged;
            
            auto_sync_pref = dap_prefs_section.Add (auto_sync);
            auto_sync_pref.ValueChanged += OnAutoSyncChanged;
            
            //manually_manage_pref.Changed += OnEnabledChanged;
            //auto_sync_pref.Changed += delegate { OnUpdated (); };
            //OnEnabledChanged (null);
        }

        private bool dap_loaded = false;
        public void DapLoaded ()
        {
            dap_loaded = true;
        }

        private void BuildSyncLists ()
        {
            int i = 0;
            foreach (LibrarySource source in Libraries) {
                DapLibrarySync library_sync = new DapLibrarySync (this, source);
                library_syncs.Add (library_sync);
                pref_sections.Add (library_sync.PrefsSection);
                library_sync.PrefsSection.Order = ++i;
                
                source.TracksAdded += OnLibraryChanged;
                source.TracksDeleted += OnLibraryChanged;
            }

            dap.TracksAdded += OnDapChanged;
            dap.TracksDeleted += OnDapChanged;
        }

        private void OnManuallyManageChanged (Root preference)
        {
            UpdateSensitivities ();
            OnUpdated ();
        }

        private void UpdateSensitivities ()
        {
            bool sync_enabled = Enabled;
            auto_sync_pref.Sensitive = sync_enabled;
            foreach (DapLibrarySync lib_sync in library_syncs) {
                lib_sync.PrefsSection.Sensitive = sync_enabled;
            }
        }
        
        private void OnAutoSyncChanged (Root preference)
        {
            OnUpdated ();
            if (AutoSync) {
                Sync ();
            }
        }

        private void OnDapChanged (Source sender, TrackEventArgs args)
        {
            if (!AutoSync && dap_loaded && !Syncing) {
                foreach (DapLibrarySync lib_sync in library_syncs) {
                    lib_sync.CalculateSync ();
                }
            }
        }

        private void OnLibraryChanged (Source sender, TrackEventArgs args)
        {
            if (!Enabled) {
                return;
            }
            
            foreach (DapLibrarySync lib_sync in library_syncs) {
                if (lib_sync.Library == sender) {
                    if (AutoSync) {
                        Sync ();
                    } else {
                        lib_sync.CalculateSync ();
                        OnUpdated ();
                    }
                    break;
                }
            }
         }

        private IEnumerable<LibrarySource> Libraries {
            get {
                List<Source> sources = new List<Source> (ServiceManager.SourceManager.Sources);
                sources.Sort (delegate (Source a, Source b) {
                    return a.Order.CompareTo (b.Order);
                });
    
                if (!dap.SupportsVideo) {
                    sources.Remove (ServiceManager.SourceManager.VideoLibrary);
                }
                
                foreach (Source source in sources) {
                    if (source is LibrarySource) {
                        yield return source as LibrarySource;
                    }
                }
            }
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
            foreach (DapLibrarySync library_sync in library_syncs) {
                library_sync.CalculateSync ();
            }

            Log.Information (ToString ());
            OnUpdated ();
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
            foreach (DapLibrarySync library_sync in library_syncs) {
                sb.Append (library_sync.ToString ());
                sb.Append ("\n");
            }
            return sb.ToString ();
        }

        public void Sync ()
        {
            if (Banshee.Base.ThreadAssist.InMainThread) {
                Banshee.Base.ThreadAssist.SpawnFromMain (delegate {
                    sync_limiter.Execute ();
                });
            } else {
                sync_limiter.Execute ();
            }
        }

        private void RateLimitedSync ()
        {
            syncing = true;

            bool sync_playlists = false;
            if (dap.SupportsPlaylists) {
                foreach (DapLibrarySync library_sync in library_syncs) {
                    if (library_sync.Library.SupportsPlaylists) {
                        sync_playlists = true;
                        break;
                    }
                }
            }
            
            if (sync_playlists) {
                dap.RemovePlaylists ();
            }

            foreach (DapLibrarySync library_sync in library_syncs) {
                library_sync.Sync ();
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
