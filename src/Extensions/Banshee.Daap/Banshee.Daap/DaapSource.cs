//
// DaapSource.cs
//
// Authors:
//   Alexander Hixon <hixon.alexander@mediati.org>
//
// Copyright (C) 2008 Alexander Hixon
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

using Banshee.Base;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Library;
using Banshee.Sources;
using Banshee.ServiceStack;

using DAAP = Daap;

namespace Banshee.Daap
{
    public class DaapSource : PrimarySource, IDurationAggregator, IUnmapableSource, IImportSource
    {
        private DAAP.Service service;
        private DAAP.Client client;
        private DAAP.Database database;
        private bool connected = false;

        public DAAP.Database Database {
            get { return database; }
        }

        private bool is_activating;

        public DaapSource (DAAP.Service service) : base (Catalog.GetString ("Music Share"), service.Name,
                                                    (service.Address.ToString () + service.Port).Replace (":", "").Replace (".", ""), 300, true)
        {
            this.service = service;
            Properties.SetString ("UnmapSourceActionLabel", Catalog.GetString ("Disconnect"));
            Properties.SetString ("UnmapSourceActionIconName", "gtk-disconnect");

            SupportsPlaylists = false;
            SavedCount = 0;
            UpdateIcon ();

            AfterInitialized ();
        }



        private void UpdateIcon ()
        {
            if (service != null && !service.IsProtected) {
                Properties.SetStringList ("Icon.Name", "computer", "network-server");
            } else {
                Properties.SetStringList ("Icon.Name", "system-lock-screen", "computer", "network-server");
            }
        }

        public override void CopyTrackTo (DatabaseTrackInfo track, SafeUri uri, BatchUserJob job)
        {
            if (track.PrimarySource == this && track.Uri.Scheme.StartsWith ("http")) {
                foreach (double percent in database.DownloadTrack ((int)track.ExternalId, track.MimeType, uri.AbsolutePath)) {
                    job.DetailedProgress = percent;
                }
            }
        }


        public override void Activate ()
        {
            if (client != null || is_activating) {
                return;
            }

            ClearErrorView ();

            is_activating = true;
            base.Activate ();

            SetStatus (String.Format (Catalog.GetString ("Connecting to {0}"), service.Name), false);

            ThreadAssist.Spawn (delegate {
                try {
                    client = new DAAP.Client (service);
                    client.Updated += OnClientUpdated;

                    if (client.AuthenticationMethod == DAAP.AuthenticationMethod.None) {
                        client.Login ();
                    } else {
                        ThreadAssist.ProxyToMain (PromptLogin);
                    }
                } catch(Exception e) {
                    ThreadAssist.ProxyToMain (delegate {
                        ShowErrorView (DaapErrorType.BrokenAuthentication);
                    });
                    Log.Error (e);
                }

                is_activating = false;
            });
        }

        private void ShowErrorView (DaapErrorType error_type)
        {
            PurgeTracks ();
            Reload ();
            client = null;
            DaapErrorView error_view = new DaapErrorView (this, error_type);
            error_view.Show ();
            Properties.Set<Banshee.Sources.Gui.ISourceContents> ("Nereid.SourceContents", error_view);
            HideStatus ();
        }

        private void ClearErrorView ()
        {
            Properties.Remove ("Nereid.SourceContents");
        }

        internal bool Disconnect (bool logout)
        {
            // Stop currently playing track if its from us.
            try {
                if (ServiceManager.PlayerEngine.CurrentState == Banshee.MediaEngine.PlayerState.Playing) {
                    DatabaseTrackInfo track = ServiceManager.PlayerEngine.CurrentTrack as DatabaseTrackInfo;
                    if (track != null && track.PrimarySource == this) {
                        ServiceManager.PlayerEngine.Close ();
                    }
                }
            } catch {}

            connected = false;

            // Remove tracks associated with this source, since we don't want
            // them after we unmap - we'll refetch.
            PurgeTracks ();

            if (client != null) {
                if (logout) {
                    client.Logout ();
                }

                client.Dispose ();
                client = null;
                database = null;
            }

            if (database != null) {
                try {
                    DaapService.ProxyServer.UnregisterDatabase (database);
                } catch {}
                database.TrackAdded -= OnDatabaseTrackAdded;
                database.TrackRemoved -= OnDatabaseTrackRemoved;
                database = null;
            }

            List<Source> children = new List<Source> (Children);
            foreach (Source child in children) {
                if (child is Banshee.Sources.IUnmapableSource) {
                    (child as Banshee.Sources.IUnmapableSource).Unmap ();
                }
            }

            ClearChildSources ();

            return true;
        }

        public override void Dispose ()
        {
            Disconnect (true);
            base.Dispose ();
        }

        private void PromptLogin ()
        {
            SetStatus (Catalog.GetString ("Logging in to {0}."), false);

            DaapLoginDialog dialog = new DaapLoginDialog (client.Name,
            client.AuthenticationMethod == DAAP.AuthenticationMethod.UserAndPassword);
            if (dialog.Run () == (int) Gtk.ResponseType.Ok) {
                AuthenticatedLogin (dialog.Username, dialog.Password);
            } else {
                ShowErrorView (DaapErrorType.UserDisconnect);
            }

            dialog.Destroy ();
        }

        private void AuthenticatedLogin (string username, string password)
        {
            ThreadAssist.Spawn (delegate {
                try {
                    client.Login (username, password);
                } catch (DAAP.AuthenticationException) {
                    ThreadAssist.ProxyToMain (delegate {
                        ShowErrorView (DaapErrorType.InvalidAuthentication);
                    });
                }
            });
        }

        private void OnClientUpdated (object o, EventArgs args)
        {
            try {
                if (database == null && client.Databases.Count > 0) {
                    database = client.Databases[0];
                    DaapService.ProxyServer.RegisterDatabase (database);
                    database.TrackAdded += OnDatabaseTrackAdded;
                    database.TrackRemoved += OnDatabaseTrackRemoved;

                    SetStatus (String.Format (Catalog.GetPluralString (
                        "Loading {0} track", "Loading {0} tracks", database.TrackCount),
                        database.TrackCount), false
                    );

                    // Notify (eg reload the source before sync is done) at most 5 times
                    int notify_every = Math.Max (250, (database.Tracks.Count / 4));
                    notify_every -= notify_every % 250;

                    int count = 0;
                    DaapTrackInfo daap_track = null;

                    HyenaSqliteConnection conn = ServiceManager.DbConnection;
                    conn.BeginTransaction ();
                    foreach (DAAP.Track track in database.Tracks) {
                        daap_track = new DaapTrackInfo (track, this);

                        // Only notify once in a while because otherwise the source Reloading slows things way down
                        if (++count % notify_every == 0) {
                            conn.CommitTransaction ();
                            daap_track.Save (true);
                            conn.BeginTransaction ();
                        } else {
                            daap_track.Save (false);
                        }
                    }
                    conn.CommitTransaction ();

                    // Save the last track once more to trigger the NotifyTrackAdded
                    if (daap_track != null) {
                        daap_track.Save ();
                    }

                    SetStatus (Catalog.GetString ("Loading playlists"), false);
                    AddPlaylistSources ();
                    connected = true;
                    Reload ();
                    HideStatus ();
                }

                Name = client.Name;

                UpdateIcon ();
                OnUpdated ();
            } catch (Exception e) {
                Log.Error ("Caught exception while loading daap share", e);
                ThreadAssist.ProxyToMain (delegate {
                    HideStatus ();
                    ShowErrorView (DaapErrorType.UserDisconnect);
                });
            }
        }

        private void AddPlaylistSources ()
        {
            foreach (DAAP.Playlist pl in database.Playlists) {
                DaapPlaylistSource source = new DaapPlaylistSource (pl, this);
                ThreadAssist.ProxyToMain (delegate {
                    if (source.Count == 0) {
                        source.Unmap ();
                    } else {
                        AddChildSource (source);
                    }
                });
            }
        }

        public void OnDatabaseTrackAdded (object o, DAAP.TrackArgs args)
        {
            DaapTrackInfo track = new DaapTrackInfo (args.Track, this);
            track.Save ();
        }

        public void OnDatabaseTrackRemoved (object o, DAAP.TrackArgs args)
        {
            //RemoveTrack (
        }

        public override bool CanRemoveTracks {
            get { return false; }
        }

        public override bool CanDeleteTracks {
            get { return false; }
        }

        public override bool ConfirmRemoveTracks {
            get { return false; }
        }

        public override bool HasEditableTrackProperties {
            get { return false; }
        }

        public bool Unmap ()
        {
            // Disconnect and clear out our tracks and such.
            Disconnect (true);
            ShowErrorView (DaapErrorType.UserDisconnect);

            return true;
        }

        public bool CanUnmap {
            get { return connected; }
        }

        public bool ConfirmBeforeUnmap {
            get { return false; }
        }

        public void Import ()
        {
            ServiceManager.SourceManager.MusicLibrary.MergeSourceInput (this, SourceMergeType.All);
        }

        public bool CanImport {
            get { return connected; }
        }

        int IImportSource.SortOrder {
            get { return 30; }
        }

        string IImportSource.ImportLabel {
            get { return null; }
        }

        public string [] IconNames {
            get { return Properties.GetStringList ("Icon.Name"); }
        }
    }
}
