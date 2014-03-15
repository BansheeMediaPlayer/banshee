//
// DatabaseImportManager.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2007-2008 Novell, Inc.
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
using System.IO;

using Mono.Unix;

using Hyena;
using Hyena.Data.Sqlite;

using Banshee.Base;
using Banshee.Sources;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Configuration.Schema;
using Banshee.ServiceStack;
using Banshee.Streaming;

namespace Banshee.Collection.Database
{
    public class DatabaseImportManager : ImportManager
    {
        // This is a list of known media files that we may encounter. The extensions
        // in this list do not mean they are actually supported - this list is just
        // used to see if we should allow the file to be processed by TagLib. The
        // point is to rule out, at the path level, files that we won't support.

        public static readonly Banshee.IO.ExtensionSet WhiteListFileExtensions = new Banshee.IO.ExtensionSet (
            "3g2",  "3gp",  "3gp2", "3gpp", "aac",  "ac3",  "aif",  "aifc",
            "aiff", "al",   "alaw", "ape",  "asf",  "asx",  "au",   "avi",
            "cda",  "cdr",  "divx", "dv",   "flac", "flv",  "gvi",  "gvp",
            "m1v",  "m21",  "m2p",  "m2v",  "m4a",  "m4b",  "m4e",  "m4p",
            "m4u",  "m4v",  "mp+",  "mid",  "midi", "mjp",  "mkv",  "moov",
            "mov",  "movie","mp1",  "mp2",  "mp21", "mp3",  "mp4",  "mpa",
            "mpc",  "mpe",  "mpeg", "mpg",  "mpga", "mpp",  "mpu",  "mpv",
            "mpv2", "oga",  "ogg",  "ogv",  "ogm",  "omf",  "qt",   "ra",
            "ram",  "raw",  "rm",   "rmvb", "rts",  "smil", "swf",  "tivo",
            "u",    "vfw",  "vob",  "wav",  "wave", "wax",  "wm",   "wma",
            "wmd",  "wmv",  "wmx",  "wv",   "wvc",  "wvx",  "yuv",  "f4v",
            "f4a",  "f4b",  "669",  "it",   "med",  "mod",  "mol",  "mtm",
            "nst",  "s3m",  "stm",  "ult",  "wow",  "xm",   "xnm",  "spx",
            "ts",   "webm", "spc",  "mka",  "opus", "amr"
        );

        public static bool IsWhiteListedFile (string path)
        {
            return WhiteListFileExtensions.IsMatchingFile (path);
        }

        public delegate PrimarySource TrackPrimarySourceChooser (DatabaseTrackInfo track);

        private TrackPrimarySourceChooser trackPrimarySourceChooser;
        private Dictionary<int, int> counts;
        private ErrorSource error_source;
        private int [] primary_source_ids;
        private string base_directory;
        private bool force_copy;

        public event DatabaseImportResultHandler ImportResult;

        public DatabaseImportManager (PrimarySource psource) :
            this (psource.ErrorSource, delegate { return psource; }, new int [] {psource.DbId}, psource.BaseDirectory)
        {
        }

        public DatabaseImportManager (ErrorSource error_source, TrackPrimarySourceChooser chooser,
            int [] primarySourceIds, string baseDirectory) : this (chooser)
        {
            this.error_source = error_source;
            primary_source_ids = primarySourceIds;
            base_directory = baseDirectory;
        }

        public DatabaseImportManager (TrackPrimarySourceChooser chooser)
        {
            trackPrimarySourceChooser = chooser;
            counts = new Dictionary<int, int> ();
        }

        protected virtual ErrorSource ErrorSource {
            get { return error_source; }
        }

        protected virtual int [] PrimarySourceIds {
            get { return primary_source_ids; }
            set { primary_source_ids = value; }
        }

        protected virtual string BaseDirectory {
            get { return base_directory; }
            set { base_directory = value; }
        }

        protected virtual bool ForceCopy {
            get { return force_copy; }
            set { force_copy = value; }
        }

        protected override void OnImportRequested (string path)
        {
            try {
                DatabaseTrackInfo track = ImportTrack (path);
                if (track != null && track.TrackId > 0) {
                    UpdateProgress (String.Format ("{0} - {1}",
                        track.DisplayArtistName, track.DisplayTrackTitle));
                } else {
                    UpdateProgress (null);
                }

                OnImportResult (track, path, null);
            } catch (Exception e) {
                LogError (SafeUri.UriToFilename (path), e);
                UpdateProgress (null);
                OnImportResult (null, path, e);
            }
        }

        protected virtual void OnImportResult (DatabaseTrackInfo track, string path, Exception error)
        {
            DatabaseImportResultHandler handler = ImportResult;
            if (handler != null) {
                handler (this, new DatabaseImportResultArgs (track, path, error));
            }
        }

        public DatabaseTrackInfo ImportTrack (string path)
        {
            return ImportTrack (new SafeUri (path));
        }

        public DatabaseTrackInfo ImportTrack (SafeUri uri)
        {
            if (!IsWhiteListedFile (uri.AbsoluteUri)) {
                return null;
            }

            if (DatabaseTrackInfo.ContainsUri (uri, PrimarySourceIds)) {
                // TODO add DatabaseTrackInfo.SyncedStamp property, and if the file has been
                // updated since the last sync, fetch its metadata into the db.
                return null;
            }

            if (Banshee.IO.File.GetSize (uri) == 0) {
                throw new InvalidFileException (String.Format (
                    Catalog.GetString ("File is empty so it could not be imported: {0}"),
                    Path.GetFileName (uri.LocalPath)));
            }

            DatabaseTrackInfo track = new DatabaseTrackInfo () { Uri = uri };
            using (var file = StreamTagger.ProcessUri (uri)) {
                StreamTagger.TrackInfoMerge (track, file, false, true, true);
            }

            track.Uri = uri;

            if (FindOutdatedDupe (track)) {
                return null;
            }

            track.PrimarySource = trackPrimarySourceChooser (track);

            // TODO note, there is deadlock potential here b/c of locking of shared commands and blocking
            // because of transactions.  Needs to be fixed in HyenaDatabaseConnection.
            ServiceManager.DbConnection.BeginTransaction ();
            try {
                bool save_track = true;
                if (track.PrimarySource is Banshee.Library.LibrarySource) {
                    save_track = track.CopyToLibraryIfAppropriate (force_copy);
                }

                if (save_track) {
                    track.Save (false);
                }

                ServiceManager.DbConnection.CommitTransaction ();
            } catch (Exception) {
                ServiceManager.DbConnection.RollbackTransaction ();
                throw;
            }

            counts[track.PrimarySourceId] = counts.ContainsKey (track.PrimarySourceId) ? counts[track.PrimarySourceId] + 1 : 1;

            // Reload every 20% or every 250 tracks, whatever is more (eg at most reload 5 times during an import)
            if (counts[track.PrimarySourceId] >= Math.Max (TotalCount/5, 250)) {
                counts[track.PrimarySourceId] = 0;
                track.PrimarySource.NotifyTracksAdded ();
            }

            return track;
        }

        private bool FindOutdatedDupe (DatabaseTrackInfo track)
        {
            if (DatabaseTrackInfo.MetadataHashCount (track.MetadataHash, PrimarySourceIds) != 1) {
                return false;
            }

            var track_to_update = DatabaseTrackInfo.GetTrackForMetadataHash (track.MetadataHash, PrimarySourceIds);

            if (track_to_update == null || Banshee.IO.File.Exists (track_to_update.Uri)) {
                return false;
            }

            track_to_update.Uri = track.Uri;
            track_to_update.Save ();
            return true;
        }

        private void LogError (string path, Exception e)
        {
            LogError (path, e.Message);

            if (!(e is TagLib.CorruptFileException) && !(e is TagLib.UnsupportedFormatException)) {
                Log.DebugFormat ("Full import exception: {0}", e.ToString ());
            }
        }

        private void LogError (string path, string msg)
        {
            ErrorSource.AddMessage (path, msg);
            Log.Error (path, msg, false);
        }

        public void NotifyAllSources ()
        {
            var all_primary_source_ids = new List<int> (counts.Keys);
            foreach (int primary_source_id in all_primary_source_ids) {
                PrimarySource.GetById (primary_source_id).NotifyTracksAdded ();
            }
            counts.Clear ();
        }

        protected override void OnFinished ()
        {
            NotifyAllSources ();
            base.OnFinished ();
        }
    }
}
