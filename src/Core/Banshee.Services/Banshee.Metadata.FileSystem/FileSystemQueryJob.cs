//
// FileSystemdQueryJob.cs
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
using System.IO;
using System.Collections.Generic;

using Hyena;

using Banshee.Base;
using Banshee.Metadata;
using Banshee.Collection;
using Banshee.Streaming;

namespace Banshee.Metadata.FileSystem
{
    public class FileSystemQueryJob : MetadataServiceJob
    {
        private TrackInfo track;
        public FileSystemQueryJob (IBasicTrackInfo track)
        {
            Track = track;
            this.track = track as TrackInfo;
        }

        public override void Run ()
        {
            if (Track == null || CoverArtSpec.CoverExists (Track.ArtworkId)) {
                return;
            }

            Fetch ();
        }

        private static string [] extensions = new string [] { ".jpg", ".jpeg", ".png", ".bmp" };
        private static string [] filenames = new string [] { "cover", "folder", "front", "album", "albumart" };

        protected void Fetch ()
        {
            if (Track.Uri == null || !Track.Uri.IsFile ||
                    Track.ArtworkId == null || !Banshee.IO.File.Exists (Track.Uri)) {
                return;
            }

            string directory = System.IO.Path.GetDirectoryName (Track.Uri.AbsolutePath);

            // Get the largest (in terms of file size) JPEG in the directory
            long max_size = 0;
            string best_file = null;
            int items_in_directory = 0;
            bool found_definite_best = false;
            int track_count = track.TrackCount;
            if (track.DiscCount > 0) {
                track_count = track.TrackCount * track.DiscCount;
            }
            int max_acceptable_items = Math.Max (30, track_count + 8);
            foreach (string file in Banshee.IO.Directory.GetFiles (directory)) {
                // Ignore directories with tons of songs in them; this lookup is only intended for when the
                // music file is in a directory specific to its album.
                if (++items_in_directory > max_acceptable_items) {
                    if (best_file != null) {
                        Log.Debug ("Ignoring image file in folder because too many files in folder", directory);
                    }
                    return;
                }

                if (found_definite_best) {
                    continue;
                }

                string extension = System.IO.Path.GetExtension (file).ToLower ();
                if (Array.IndexOf (extensions, extension) != -1) {
                    string filename = System.IO.Path.GetFileNameWithoutExtension (file).ToLower ();
                    if (Array.IndexOf (filenames, filename) != -1) {
                        best_file = file;
                        found_definite_best = true;
                    } else {
                        long size = Banshee.IO.File.GetSize (new SafeUri (file));
                        if (size > max_size) {
                            max_size = size;
                            best_file = file;
                        }
                    }
                }
            }

            if (best_file != null) {
                try {
                    // Copy the file to the cover art directory
                    SaveAtomically (CoverArtSpec.GetPathForNewFile (Track.ArtworkId, best_file), Banshee.IO.File.OpenRead (new SafeUri (best_file)));

                    // Send the new StreamTag
                    StreamTag tag = new StreamTag ();
                    tag.Name = CommonTags.AlbumCoverId;
                    tag.Value = Track.ArtworkId;
                    AddTag (tag);

                    Log.Debug ("Got cover art from track's folder", best_file);
                } catch (Exception e) {
                    Log.Error (e);
                }
            }
        }
    }
}
