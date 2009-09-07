//
// SaveTrackMetadataService.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2009 Novell, Inc.
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

using Banshee.Streaming;
using Banshee.Collection.Database;
using Banshee.Sources;
using Banshee.ServiceStack;
using Banshee.Configuration.Schema;
using Banshee.Preferences;

namespace Banshee.Metadata
{
    public class SaveTrackMetadataService : IService
    {
        public static SchemaPreference<bool> WriteEnabled = new SchemaPreference<bool> (
                LibrarySchema.WriteMetadata, 
                Catalog.GetString ("Write _metadata to files"),
                Catalog.GetString ("Enable this option to save tags and other metadata inside supported audio files.")
        );

        public static SchemaPreference<bool> RenameEnabled = new SchemaPreference<bool> (
                LibrarySchema.MoveOnInfoSave,
                Catalog.GetString ("_Update file and folder names"),
                Catalog.GetString ("Enabling this option ensures that files and folders are renamed according to the metadata.")
        );

        private SaveTrackMetadataJob job;
        private object sync = new object ();
        private bool inited = false;

        public SaveTrackMetadataService ()
        {
            Banshee.ServiceStack.Application.RunTimeout (10000, delegate {
                WriteEnabled.ValueChanged += OnEnabledChanged;
                RenameEnabled.ValueChanged += OnEnabledChanged;
                ServiceManager.SourceManager.MusicLibrary.TracksChanged += OnTracksChanged;
                Save ();
                inited = true;
                return false;
            });
        }

        public void Dispose ()
        {
            if (inited) {
                ServiceManager.SourceManager.MusicLibrary.TracksChanged -= OnTracksChanged;

                if (job != null) {
                    ServiceManager.JobScheduler.Cancel (job);
                }
            }
        }

        private void Save ()
        {
            if (!(WriteEnabled.Value || RenameEnabled.Value))
                return;

            lock (sync) {
                if (job != null) {
                    job.WriteEnabled  = WriteEnabled.Value;
                    job.RenameEnabled = RenameEnabled.Value;
                    return;
                } else {
                    job = new SaveTrackMetadataJob ();
                    job.WriteEnabled  = WriteEnabled.Value;
                    job.RenameEnabled = RenameEnabled.Value;
                }
            }

            job.Finished += delegate { job = null; };
            job.Register ();
        }

        private void OnTracksChanged (Source sender, TrackEventArgs args)
        {
            Save ();
        }

        private void OnEnabledChanged (Root pref)
        {
            if (WriteEnabled.Value || RenameEnabled.Value) {
                Save ();
            } else {
                if (job != null) {
                    ServiceManager.JobScheduler.Cancel (job);
                }
            }
        }

        string IService.ServiceName {
            get { return "SaveMetadataService"; }
        }

        // Reserve strings in preparation for the forthcoming string freeze.
        public void ReservedStrings ()
        {
            Catalog.GetString ("Write _ratings and play counts to files");
            Catalog.GetString ("Enable this option to save rating and play count metadata inside supported audio files whenever the rating is changed.");
            Catalog.GetString ("Import _ratings");
            Catalog.GetString ("Import play _counts");
        }
    }
}
