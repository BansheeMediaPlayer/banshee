using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using Mono.Unix;

using Hyena;
using Hyena.Data.Sqlite;

using Banshee.Configuration;
using Banshee.ServiceStack;
using Banshee.Database;
using Banshee.Sources;
using Banshee.Playlists.Formats;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Streaming;

namespace Banshee.Playlist
{
    public class PlaylistImportCanceledException : ApplicationException
    {
        public PlaylistImportCanceledException (string message) : base (message)
        {
        }

        public PlaylistImportCanceledException () : base ()
        {
        }
    }

    public static class PlaylistFileUtil
    {
        public static readonly SchemaEntry<string> DefaultExportFormat = new SchemaEntry<string> (
            "player_window", "default_export_format",
            String.Empty,
            "Export Format",
            "The default playlist export format"
        );

        private static PlaylistFormatDescription [] export_formats = new PlaylistFormatDescription [] {
            M3uPlaylistFormat.FormatDescription,
            PlsPlaylistFormat.FormatDescription,
            XspfPlaylistFormat.FormatDescription
        };

        public static readonly string [] PlaylistExtensions = new string [] {
            M3uPlaylistFormat.FormatDescription.FileExtension,
            PlsPlaylistFormat.FormatDescription.FileExtension,
            XspfPlaylistFormat.FormatDescription.FileExtension
        };

        public static PlaylistFormatDescription [] ExportFormats {
            get { return export_formats; }
        }

        public static bool IsSourceExportSupported (Source source)
        {
            bool supported = true;

            if (source == null || !(source is AbstractPlaylistSource)) {
                supported = false;
            }

            return supported;
        }

        public static PlaylistFormatDescription GetDefaultExportFormat ()
        {
            PlaylistFormatDescription default_format = null;
            try {
                string exportFormat = DefaultExportFormat.Get ();
                PlaylistFormatDescription [] formats = PlaylistFileUtil.ExportFormats;
                foreach (PlaylistFormatDescription format in formats) {
                    if (format.FileExtension.Equals (exportFormat)) {
                        default_format = format;
                        break;
                    }
                }
            } catch {
                // Ignore errors, return our default if we encounter an error.
            } finally {
                if (default_format == null) {
                    default_format = M3uPlaylistFormat.FormatDescription;
                }
            }
            return default_format;
        }

        public static void SetDefaultExportFormat (PlaylistFormatDescription format)
        {
            try {
                DefaultExportFormat.Set (format.FileExtension);
            } catch (Exception) {
            }
        }

        public static int GetFormatIndex (PlaylistFormatDescription [] formats, PlaylistFormatDescription playlist)
        {
            int default_export_index = -1;
            foreach (PlaylistFormatDescription format in formats) {
                default_export_index++;
                if (format.FileExtension.Equals (playlist.FileExtension)) {
                    break;
                }
            }
            return default_export_index;
        }

        public static string [] ImportPlaylist (string playlistUri)
        {
            return ImportPlaylist (playlistUri, null);
        }

        public static string [] ImportPlaylist (string playlistUri, Uri baseUri)
        {
            IPlaylistFormat playlist = Load (playlistUri, baseUri);
            List<string> uris = new List<string> ();
            if (playlist != null) {
                foreach (Dictionary<string, object> element in playlist.Elements) {
                    uris.Add (((Uri)element["uri"]).AbsoluteUri);
                }
            }
            return uris.ToArray ();
        }

        public static bool PathHasPlaylistExtension (string playlistUri)
        {
            if (System.IO.Path.HasExtension (playlistUri)) {
                string extension = System.IO.Path.GetExtension (playlistUri).ToLower ();
                foreach (PlaylistFormatDescription format in PlaylistFileUtil.ExportFormats) {
                    if (extension.Equals ("." + format.FileExtension)) {
                        return true;
                    }
                }
            }

            return false;
        }

        public static IPlaylistFormat Load (string playlistUri, Uri baseUri)
        {
            PlaylistFormatDescription [] formats = PlaylistFileUtil.ExportFormats;

            // If the file has an extenstion, rearrange the format array so that the
            // appropriate format is tried first.
            if (System.IO.Path.HasExtension (playlistUri)) {
                string extension = System.IO.Path.GetExtension (playlistUri);
                extension = extension.ToLower ();

                int index = -1;
                foreach (PlaylistFormatDescription format in formats) {
                    index++;
                    if (extension.Equals ("." + format.FileExtension)) {
                        break;
                    }
                }

                if (index != -1 && index != 0 && index < formats.Length) {
                    // Move to first position in array.
                    PlaylistFormatDescription preferredFormat = formats[index];
                    formats[index] = formats[0];
                    formats[0] = preferredFormat;
                }
            }

            foreach (PlaylistFormatDescription format in formats) {
                try {
                    IPlaylistFormat playlist = (IPlaylistFormat)Activator.CreateInstance (format.Type);
                    playlist.BaseUri = baseUri;
                    playlist.Load (Banshee.IO.File.OpenRead (new SafeUri (playlistUri)), true);
                    return playlist;
                } catch (InvalidPlaylistException) {
                }
            }

            return null;
        }

        public static void ImportPlaylistToLibrary (string path)
        {
            ImportPlaylistToLibrary (path, null, null);
        }

        public static void ImportPlaylistToLibrary (string path, PrimarySource source, DatabaseImportManager importer)
        {
            try {
                Log.InformationFormat ("Importing playlist {0} to library", path);
                SafeUri uri = new SafeUri (path);
                PlaylistParser parser = new PlaylistParser ();
                string relative_dir = System.IO.Path.GetDirectoryName (uri.LocalPath);
                if (relative_dir[relative_dir.Length - 1] != System.IO.Path.DirectorySeparatorChar) {
                    relative_dir = relative_dir + System.IO.Path.DirectorySeparatorChar;
                }
                parser.BaseUri = new Uri (relative_dir);
                if (parser.Parse (uri)) {
                    List<string> uris = new List<string> ();
                    foreach (Dictionary<string, object> element in parser.Elements) {
                        Uri elt = (Uri)element["uri"];
                        if (elt.IsFile) {
                            uris.Add (elt.LocalPath);
                        } else {
                            Log.InformationFormat ("Ignoring invalid playlist element: {0}", elt.OriginalString);
                        }
                    }

                    if (source == null) {
                        if (uris.Count > 0) {
                            // Get the media attribute of the 1st Uri in Playlist 
                            // and then determine whether the playlist belongs to Video or Music
                            SafeUri uri1 = new SafeUri (uris[0]);
                            var track = new TrackInfo ();
                            StreamTagger.TrackInfoMerge (track, uri1);

                            if (track.HasAttribute (TrackMediaAttributes.VideoStream))
                                source = ServiceManager.SourceManager.VideoLibrary;
                            else
                                source = ServiceManager.SourceManager.MusicLibrary;
                        }
                    }

                    // Give source a fallback value - MusicLibrary when it's null
                    if (source == null)
                        source = ServiceManager.SourceManager.MusicLibrary;
                    
                    // Only import an non-empty playlist
                    if (uris.Count > 0) {
                        ImportPlaylistWorker worker = new ImportPlaylistWorker (
                            parser.Title,
                            uris.ToArray (), source, importer);
                        worker.Import ();
                    }
                }
            } catch (Exception e) {
                Hyena.Log.Exception (e);
            }
        }
    }

    public class ImportPlaylistWorker
    {
        private string [] uris;
        private string name;

        private PrimarySource source;
        private DatabaseImportManager importer;
        private bool finished;

        public ImportPlaylistWorker (string name, string [] uris, PrimarySource source, DatabaseImportManager importer)
        {
            this.name = name;
            this.uris = uris;
            this.source = source;
            this.importer = importer;
        }

        public void Import ()
        {
            try {
                if (importer == null) {
                    importer = new Banshee.Library.LibraryImportManager ();
                }
                finished = false;
                importer.Finished += CreatePlaylist;
                importer.Enqueue (uris);
            } catch (PlaylistImportCanceledException e) {
                Hyena.Log.Exception (e);
            }
        }

        private void CreatePlaylist (object o, EventArgs args)
        {
            if (finished) {
                return;
            }

            finished = true;

            try {
                PlaylistSource playlist = new PlaylistSource (name, source);
                playlist.Save ();
                source.AddChildSource (playlist);

                HyenaSqliteCommand insert_command = new HyenaSqliteCommand (String.Format (
                    @"INSERT INTO CorePlaylistEntries (PlaylistID, TrackID) VALUES ({0}, ?)", playlist.DbId));

                //ServiceManager.DbConnection.BeginTransaction ();
                foreach (string uri in uris) {
                    // FIXME: Does the following call work if the source is just a PrimarySource (not LibrarySource)?
                    int track_id = source.GetTrackIdForUri (uri);
                    if (track_id > 0) {
                        ServiceManager.DbConnection.Execute (insert_command, track_id);
                    }
                }

                playlist.Reload ();
                playlist.NotifyUser ();
            } catch (Exception e) {
                Hyena.Log.Exception (e);
            }
        }
    }
}
