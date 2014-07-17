//
// LibraryWatcherService.cs
//
// Authors:
//   Alexander Hixon <hixon.alexander@mediati.org>
//   Christian Martellini <christian.martellini@gmail.com>
//
// Copyright (C) 2008 Alexander Hixon
// Copyright (C) 2009 Christian Martellini
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

using Banshee.Base ;
using Banshee.Sources;
using Banshee.ServiceStack;
using Banshee.Library;

using Banshee.Gui;
using Banshee.I18n;

using Hyena;

namespace Banshee.LibraryWatcher
{
    public class LibraryWatcherService : IExtensionService, IDisposable, IDelayedInitializeService
    {
        private readonly Dictionary<LibrarySource, SourceWatcher> watchers =
            new Dictionary<LibrarySource, SourceWatcher> ();

        string IService.ServiceName {
            get { return "LibraryWatcherService"; }
        }

        void IExtensionService.Initialize ()
        {
        }

        public void DelayedInitialize ()
        {
            // FIXME: Support sources other than the music and the video library (e.g. podcasts, audiobooks, etc)
            // The SourceWatcher uses LibraryImportManager which is specific to music/video.
            // To support other sources we need a separate importer for each of them.
            /*
            ServiceManager.SourceManager.SourceAdded += OnSourceAdded;
            ServiceManager.SourceManager.SourceRemoved += OnSourceRemoved;

            foreach (var library in ServiceManager.SourceManager.FindSources<LibrarySource> ()) {
                AddLibrary (library);
            }
            */
            AddLibrary (ServiceManager.SourceManager.MusicLibrary);
            AddLibrary (ServiceManager.SourceManager.VideoLibrary);

            if (ServiceManager.SourceManager.MusicLibrary.Count == 0) {
                new Banshee.Collection.RescanPipeline (ServiceManager.SourceManager.MusicLibrary, tracks => {

                    string msg = String.Format (
                        Catalog.GetPluralString (
                        // singular form unused b/c we know it's > 1, but we still need GetPlural
                        "Library Watcher extension just detected an action that would trigger the removal of one track from your music library.",
                        "Library Watcher extension just detected an action that would trigger the removal of {0} tracks from your music library.",
                        tracks),
                        tracks);

                    return TrackActions.ConfirmRemove (msg);
                });
            }

            if (ServiceManager.SourceManager.VideoLibrary.Count == 0) {
                new Banshee.Collection.RescanPipeline (ServiceManager.SourceManager.VideoLibrary, tracks => {

                    string msg = String.Format (
                        Catalog.GetPluralString (
                        // singular form unused b/c we know it's > 1, but we still need GetPlural
                        "Library Watcher extension just detected an action that would trigger the removal of one track from your video library.",
                        "Library Watcher extension just detected an action that would trigger the removal of {0} tracks from your video library.",
                        tracks),
                        tracks);

                    return TrackActions.ConfirmRemove (msg);
                });
            }
        }

        public void Dispose ()
        {
            lock (watchers) {
                foreach (var watcher in watchers.Values) {
                    watcher.Dispose ();
                }
                watchers.Clear ();
            }
        }

        /*
        private void OnSourceAdded (SourceAddedArgs args)
        {
            var library = args.Source as LibrarySource;
            if (library != null) {
                AddLibrary (library);
            }
        }

        private void OnSourceRemoved (SourceEventArgs args)
        {
            var library = args.Source as LibrarySource;
            if (library != null) {
                RemoveLibrary (library);
            }
        }
        */

        private void AddLibrary (LibrarySource library)
        {
            if (!Banshee.IO.Directory.Exists(library.BaseDirectoryWithSeparator)) {
                Hyena.Log.DebugFormat ("Will not watch library {0} because its folder doesn't exist: {1}",
                    library.Name, library.BaseDirectoryWithSeparator);
                return;
            }
            lock (watchers) {
                if (!watchers.ContainsKey (library)) {
                    try {
                        var dir = library.BaseDirectoryWithSeparator;
                        if (!Banshee.IO.Directory.Exists (dir)) {
                            Hyena.Log.DebugFormat ("Skipped LibraryWatcher for {0} ({1})", library.Name, dir);
                        } else {
                            watchers[library] = new SourceWatcher (library);
                            Hyena.Log.DebugFormat ("Started LibraryWatcher for {0} ({1})", library.Name, dir);
                        }
                    } catch (Exception e) {
                        Log.Error (e);
                    }
                }
            }
        }

        /*
        private void RemoveLibrary (LibrarySource library)
        {
            lock (watchers) {
                if (watchers.ContainsKey (library)) {
                    watchers[library].Dispose ();
                    watchers.Remove (library);
                }
            }
        }
        */
    }
}
