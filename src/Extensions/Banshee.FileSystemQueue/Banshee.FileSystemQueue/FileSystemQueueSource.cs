//
// FileSystemQueueSource.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
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
using System.IO;
using Mono.Unix;
using Gtk;

using Hyena;

using Banshee.Base;
using Banshee.Sources;
using Banshee.ServiceStack;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Configuration;
using Banshee.Kernel;
using Banshee.Playlist;

using Banshee.Gui;

namespace Banshee.FileSystemQueue
{
    public class FileSystemQueueSource : PrimarySource, IDisposable
    {
        private DatabaseImportManager importer;
        private bool visible = false;
        private bool actions_loaded = false;
        private bool play_enqueued = false;
        private string path_to_play;
        
        public FileSystemQueueSource () : base (Catalog.GetString ("File System Queue"), 
            Catalog.GetString ("File System Queue"), "file-system-queue", 30)
        {
            TypeUniqueId = "file-system-queue";
            Properties.SetStringList ("Icon.Name", "system-file-manager");
            Properties.Set<bool> ("AutoAddSource", false);
            IsLocal = true;
            
            ServiceManager.Get<DBusCommandService> ().ArgumentPushed += OnCommandLineArgument;
            
            AfterInitialized ();
            
            InterfaceActionService uia_service = ServiceManager.Get<InterfaceActionService> ();
            uia_service.GlobalActions.AddImportant (
                new ActionEntry ("ClearFileSystemQueueAction", Stock.Clear,
                    Catalog.GetString ("Clear"), null,
                    Catalog.GetString ("Remove all tracks from the file system queue"),
                    OnClearFileSystemQueue)
            );
            
            uia_service.GlobalActions.Add (new ToggleActionEntry [] {
                new ToggleActionEntry ("ClearFileSystemQueueOnQuitAction", null,
                    Catalog.GetString ("Clear on Quit"), null, 
                    Catalog.GetString ("Clear the file system queue when quitting"), 
                    OnClearFileSystemQueueOnQuit, ClearOnQuitSchema.Get ())
            });
            
            uia_service.UIManager.AddUiFromResource ("GlobalUI.xml");
            
            Properties.SetString ("ActiveSourceUIResource", "ActiveSourceUI.xml");
            Properties.SetString ("GtkActionPath", "/FileSystemQueueContextMenu");
            
            actions_loaded = true;
            
            UpdateActions ();
            ServiceManager.SourceManager.ActiveSourceChanged += delegate { Banshee.Base.ThreadAssist.ProxyToMain (UpdateActions); };
            TrackModel.Reloaded += OnTrackModelReloaded;
            
            Reload ();

            play_enqueued = ApplicationContext.CommandLine.Contains ("play-enqueued");
            
            foreach (string path in ApplicationContext.CommandLine.Files) {
                // If it looks like a URI with a protocol, leave it as is
                if (System.Text.RegularExpressions.Regex.IsMatch (path, "^\\w+\\:\\/")) {
                    Log.DebugFormat ("URI file : {0}", path);
                    Enqueue (path);
                } else {
                    Log.DebugFormat ("Relative file : {0} -> {1}", path, Path.GetFullPath (path));
                    Enqueue (Path.GetFullPath (path));
                }
            }
        }
        
        public void Enqueue (string path)
        {
            try {
                SafeUri uri = new SafeUri (path);
                if (uri.IsLocalPath && !String.IsNullOrEmpty (uri.LocalPath)) {
                    path = uri.LocalPath;
                }
            } catch {
            }

            lock (this) {
                if (importer == null) {
                    importer = new DatabaseImportManager (this);
                    importer.KeepUserJobHidden = true;
                    importer.ImportResult += delegate (object o, DatabaseImportResultArgs args) {
                        Banshee.ServiceStack.Application.Invoke (delegate {
                            if (args.Error != null || path_to_play != null) {
                                return;
                            }
                            
                            path_to_play = args.Path;
                            if (args.Track == null) {
                                // Play immediately if the track is already in the source,
                                // otherwise the call will be deferred until the track has
                                // been imported and loaded into the cache
                                PlayEnqueued ();
                            }
                        });
                    };

                    importer.Finished += delegate {
                        if (visible) {
                            Banshee.Base.ThreadAssist.ProxyToMain (delegate {
                                ServiceManager.SourceManager.SetActiveSource (this);
                            });
                        }
                    };
                }
            
                if (PlaylistFileUtil.PathHasPlaylistExtension (path)) {
                    Banshee.Kernel.Scheduler.Schedule (new DelegateJob (delegate {
                        // If it's in /tmp it probably came from Firefox - just play it
                        if (path.StartsWith (Paths.SystemTempDir)) {
                            Banshee.Streaming.RadioTrackInfo.OpenPlay (path);
                        } else {
                            PlaylistFileUtil.ImportPlaylistToLibrary (path, this, importer);
                        }
                    }));
                } else {
                    importer.Enqueue (path);
                }
            }
        }
        
        private void PlayEnqueued ()
        {   
            if (!play_enqueued || path_to_play == null) {
                return;
            }
            
            SafeUri uri = null;
            
            ServiceManager.PlaybackController.NextSource = this;
            
            try {
                uri = new SafeUri (path_to_play);
            } catch {
            }
            
            if (uri == null) {
                return;
            }
            
            int id = DatabaseTrackInfo.GetTrackIdForUri (uri, Paths.MakePathRelative (
                uri.AbsolutePath, BaseDirectory), new int [] { DbId });
            
            if (id >= 0) {
                int index = (int)TrackCache.IndexOf ((long)id);
                if (index >= 0) {
                    TrackInfo track = TrackModel[index];
                    if (track != null) {
                        ServiceManager.PlayerEngine.OpenPlay (track);
                        play_enqueued = false;
                    }
                }
            }
        }

        // until we implement DeleteTrack, at least
        public override bool CanDeleteTracks {
            get { return false; }
        }
        
        public override void Dispose ()
        {
            ServiceManager.Get<DBusCommandService> ().ArgumentPushed -= OnCommandLineArgument;
            if (ClearOnQuitSchema.Get ()) {
                OnClearFileSystemQueue (this, EventArgs.Empty);
            }
            base.Dispose ();
        }
        
        private void OnCommandLineArgument (string argument, object value, bool isFile)
        {
            if (!isFile) {
                if (argument == "play-enqueued") {
                    play_enqueued = true;
                    path_to_play = null;
                }
                return;
            }
            
            Log.DebugFormat ("FSQ Enqueue: {0}", argument);
            
            try {
                if (Banshee.IO.Directory.Exists (argument) || Banshee.IO.File.Exists (new SafeUri (argument))) {
                    Enqueue (argument);
                }
            } catch {
            }
        }
        
        protected override void OnUpdated ()
        {
            base.OnUpdated ();
            
            if (actions_loaded) {
                UpdateActions ();
            }
        }
        
        private void OnTrackModelReloaded (object sender, EventArgs args)
        {
            if (Count > 0 && !visible) {
                ServiceManager.SourceManager.AddSource (this);
                visible = true;
            } else if (Count <= 0 && visible) {
                ServiceManager.SourceManager.RemoveSource (this);
                visible = false;
            }
            
            if (Count > 0) {
                PlayEnqueued ();
            }
        }
        
        private void OnClearFileSystemQueue (object o, EventArgs args)
        {
            // Delete any child playlists
            ClearChildSources ();
            ServiceManager.DbConnection.Execute (@"
                DELETE FROM CorePlaylistEntries WHERE PlaylistID IN
                    (SELECT PlaylistID FROM CorePlaylists WHERE PrimarySourceID = ?);
                DELETE FROM CorePlaylists WHERE PrimarySourceID = ?;",
                this.DbId, this.DbId
            );

            RemoveTrackRange ((DatabaseTrackListModel)TrackModel, new Hyena.Collections.RangeCollection.Range (0, Count));
            Reload ();
        }
        
        private void OnClearFileSystemQueueOnQuit (object o, EventArgs args)
        {
            InterfaceActionService uia_service = ServiceManager.Get<InterfaceActionService> ();
            if (uia_service == null) {
                return;
            }
            
            ToggleAction action = (ToggleAction)uia_service.GlobalActions["ClearFileSystemQueueOnQuitAction"];
            ClearOnQuitSchema.Set (action.Active);
        }
        
        private void UpdateActions ()
        {   
            InterfaceActionService uia_service = ServiceManager.Get<InterfaceActionService> ();
            if (uia_service == null) {
                return;
            }
            
            uia_service.GlobalActions.UpdateAction ("ClearFileSystemQueueAction", true, Count > 0);
        }
        
        public static readonly SchemaEntry<bool> ClearOnQuitSchema = new SchemaEntry<bool> (
            "plugins.file_system_queue", "clear_on_quit",
            false,
            "Clear on Quit",
            "Clear the file system queue when quitting"
        );
    }
}
