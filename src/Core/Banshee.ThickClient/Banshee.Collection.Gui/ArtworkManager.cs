//
// ArtworkManager.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//   Andrés G. Aragoneses <knocte@gmail.com>
//
// Copyright (C) 2007-2008 Novell, Inc.
// Copyright (C) 2013 Andrés G. Aragoneses
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

using Gdk;

using Hyena;
using Hyena.Gui;
using Hyena.Collections;
using Hyena.Data.Sqlite;

using Banshee.Base;
using Banshee.IO;
using Banshee.Configuration;
using Banshee.ServiceStack;

namespace Banshee.Collection.Gui
{
    public class ArtworkManager : IService
    {
        private Dictionary<int, SurfaceCache> scale_caches  = new Dictionary<int, SurfaceCache> ();
        private HashSet<int> cacheable_cover_sizes = new HashSet<int> ();
        private HashSet<string> null_artwork_ids = new HashSet<string> ();

        private class SurfaceCache : LruCache<string, Cairo.ImageSurface>
        {
            public SurfaceCache (int max_items) : base (max_items)
            {
            }

            protected override void ExpireItem (Cairo.ImageSurface item)
            {
                if (item != null) {
                    ((IDisposable)item).Dispose ();
                }
            }
        }

        public ArtworkManager ()
        {
            Init ();
        }

        protected virtual void Init ()
        {
            AddCachedSize (36);
            AddCachedSize (40);
            AddCachedSize (42);
            AddCachedSize (48);
            AddCachedSize (64);
            AddCachedSize (90);
            AddCachedSize (300);

            try {
                MigrateCacheDir ();
            } catch (Exception e) {
                Log.Exception ("Could not migrate album artwork cache directory", e);
            }

            if (ApplicationContext.CommandLine.Contains ("fetch-artwork")) {
                ResetScanResultCache ();
            }

            Banshee.Metadata.MetadataService.Instance.ArtworkUpdated += OnArtworkUpdated;
        }

        public void Dispose ()
        {
            Banshee.Metadata.MetadataService.Instance.ArtworkUpdated -= OnArtworkUpdated;
        }

        private void OnArtworkUpdated (IBasicTrackInfo track)
        {
            ClearCacheFor (track.ArtworkId, true);
        }

        public Cairo.ImageSurface LookupSurface (string id)
        {
            return LookupScaleSurface (id, 0);
        }

        public Cairo.ImageSurface LookupScaleSurface (string id, int size)
        {
            return LookupScaleSurface (id, size, false);
        }

        public Cairo.ImageSurface LookupScaleSurface (string id, int size, bool useCache)
        {
            SurfaceCache cache = null;
            Cairo.ImageSurface surface = null;

            if (id == null) {
                return null;
            }

            if (useCache && scale_caches.TryGetValue (size, out cache) && cache.TryGetValue (id, out surface)) {
                return surface;
            }

            Pixbuf pixbuf = LookupScalePixbuf (id, size);
            if (pixbuf == null || pixbuf.Handle == IntPtr.Zero) {
                // no need to add to null_artwork_ids here, LookupScalePixbuf already did it
                return null;
            }

            try {
                surface = PixbufImageSurface.Create (pixbuf);
                if (surface == null) {
                    return null;
                }

                if (!useCache) {
                    return surface;
                }

                if (cache == null) {
                    int bytes = 4 * size * size;
                    int max = (1 << 20) / bytes;

                    ChangeCacheSize (size, max);
                    cache = scale_caches[size];
                }

                cache.Add (id, surface);
                return surface;
            } finally {
                DisposePixbuf (pixbuf);
            }
        }

        public Pixbuf LookupPixbuf (string id)
        {
            return LookupScalePixbuf (id, 0);
        }

        public Pixbuf LookupScalePixbuf (string id, int size)
        {
            if (id == null || (size != 0 && size < 10)) {
                // explicitly don't add this id into null_artwork_ids here,
                // otherwise it would blacklist all other non-invalid sizes
                return null;
            }

            if (null_artwork_ids.Contains (id)) {
                return null;
            }

            // Find the scaled, cached file
            string path = CoverArtSpec.GetPathForSize (id, size);
            if (File.Exists (new SafeUri (path))) {
                try {
                    return new Pixbuf (path);
                } catch {
                    null_artwork_ids.Add (id);
                    return null;
                }
            }

            string orig_path = CoverArtSpec.GetPathForSize (id, 0);
            bool orig_exists = File.Exists (new SafeUri (orig_path));

            if (!orig_exists) {
                // It's possible there is an image with extension .cover that's waiting
                // to be converted into a jpeg
                string unconverted_path = System.IO.Path.ChangeExtension (orig_path, "cover");
                if (File.Exists (new SafeUri (unconverted_path))) {
                    try {
                        Pixbuf pixbuf = new Pixbuf (unconverted_path);
                        if (pixbuf.Width < 50 || pixbuf.Height < 50) {
                            Hyena.Log.DebugFormat ("Ignoring cover art {0} because less than 50x50", unconverted_path);
                            null_artwork_ids.Add (id);
                            return null;
                        }

                        pixbuf.Save (orig_path, "jpeg");
                        orig_exists = true;
                    } catch {
                    } finally {
                        File.Delete (new SafeUri (unconverted_path));
                    }
                }
            }

            if (orig_exists && size >= 10) {
                try {
                    Pixbuf pixbuf = new Pixbuf (orig_path);

                    // Make it square if width and height difference is within 20%
                    const double max_ratio = 1.2;
                    double ratio = (double)pixbuf.Height / pixbuf.Width;
                    int width = size, height = size;
                    if (ratio > max_ratio) {
                        width = (int)Math.Round (size / ratio);
                    }else if (ratio < 1d / max_ratio) {
                        height = (int)Math.Round (size * ratio);
                    }

                    Pixbuf scaled_pixbuf = pixbuf.ScaleSimple (width, height, Gdk.InterpType.Bilinear);

                    if (IsCachedSize (size)) {
                        Directory.Create (System.IO.Path.GetDirectoryName (path));
                        scaled_pixbuf.Save (path, "jpeg");
                    } else {
                        Log.InformationFormat ("Uncached artwork size {0} requested", size);
                    }

                    DisposePixbuf (pixbuf);
                    if (scaled_pixbuf == null || scaled_pixbuf.Handle == IntPtr.Zero) {
                        null_artwork_ids.Add (id);
                    }
                    return scaled_pixbuf;
                } catch {}
            }

            null_artwork_ids.Add (id);
            return null;
        }

        public void ClearCacheFor (string id)
        {
            ClearCacheFor (id, false);
        }

        public void ClearCacheFor (string id, bool inMemoryCacheOnly)
        {
            if (String.IsNullOrEmpty (id)) {
                return;
            }

            // Clear from the in-memory cache
            foreach (int size in scale_caches.Keys) {
                scale_caches[size].Remove (id);
            }

            null_artwork_ids.Remove (id);

            if (inMemoryCacheOnly) {
                return;
            }

            // And delete from disk
            foreach (int size in CachedSizes ()) {
                var uri = new SafeUri (CoverArtSpec.GetPathForSize (id, size));
                if (File.Exists (uri)) {
                    File.Delete (uri);
                }
            }
        }

        public void AddCachedSize (int size)
        {
            cacheable_cover_sizes.Add (size);
        }

        public bool IsCachedSize (int size)
        {
            return cacheable_cover_sizes.Contains (size);
        }

        public IEnumerable<int> CachedSizes ()
        {
            return cacheable_cover_sizes;
        }

        public void ChangeCacheSize (int size, int max_count)
        {
            SurfaceCache cache;
            if (scale_caches.TryGetValue (size, out cache)) {
                if (max_count > cache.MaxCount) {
                    Log.DebugFormat (
                        "Growing surface cache for {0}px images to {1:0.00} MiB ({2} items)",
                        size, 4 * size * size * max_count / 1048576d, max_count);
                    cache.MaxCount = max_count;
                }
            } else {
                Log.DebugFormat (
                    "Creating new surface cache for {0}px images, capped at {1:0.00} MiB ({2} items)",
                    size, 4 * size * size * max_count / 1048576d, max_count);
                scale_caches.Add (size, new SurfaceCache (max_count));
            }
        }

        private static int dispose_count = 0;
        public static void DisposePixbuf (Pixbuf pixbuf)
        {
            if (pixbuf != null && pixbuf.Handle != IntPtr.Zero) {
                pixbuf.Dispose ();
                pixbuf = null;

                // There is an issue with disposing Pixbufs where we need to explicitly
                // call the GC otherwise it doesn't get done in a timely way.  But if we
                // do it every time, it slows things down a lot; so only do it every 100th.
                if (++dispose_count % 100 == 0) {
                    System.GC.Collect ();
                    dispose_count = 0;
                }
            }
        }

        string IService.ServiceName {
            get { return "ArtworkManager"; }
        }

#region Cache Directory Versioning/Migration

        private const int CUR_VERSION = 3;
        private void MigrateCacheDir ()
        {
            int version = CacheVersion;
            if (version == CUR_VERSION) {
                return;
            }

            var legacy_root_path = CoverArtSpec.LegacyRootPath;

            if (version < 1) {
                string legacy_artwork_path = Paths.Combine (LegacyPaths.ApplicationData, "covers");

                if (!Directory.Exists (legacy_root_path)) {
                    Directory.Create (legacy_root_path);

                    if (Directory.Exists (legacy_artwork_path)) {
                        Directory.Move (new SafeUri (legacy_artwork_path), new SafeUri (legacy_root_path));
                    }
                }

                if (Directory.Exists (legacy_artwork_path)) {
                    Log.InformationFormat ("Deleting old (Banshee < 1.0) artwork cache directory {0}", legacy_artwork_path);
                    Directory.Delete (legacy_artwork_path, true);
                }
            }

            if (version < 2) {
                int deleted = 0;
                foreach (string dir in Directory.GetDirectories (legacy_root_path)) {
                    int size;
                    string dirname = System.IO.Path.GetFileName (dir);
                    if (Int32.TryParse (dirname, out size) && !IsCachedSize (size)) {
                        Directory.Delete (dir, true);
                        deleted++;
                    }
                }

                if (deleted > 0) {
                    Log.InformationFormat ("Deleted {0} extraneous album-art cache directories", deleted);
                }
            }

            if (version < 3) {
                Log.Information ("Migrating album-art cache directory");
                var started = DateTime.Now;
                int count = 0;

                var root_path = CoverArtSpec.RootPath;
                if (!Directory.Exists (root_path)) {
                    Directory.Create (root_path);
                }

                string sql = "SELECT Title, ArtistName FROM CoreAlbums";
                using (var reader = new HyenaDataReader (ServiceManager.DbConnection.Query (sql))) {
                    while (reader.Read ()) {
                        var album = reader.Get<string>(0);
                        var artist = reader.Get<string>(1);
                        var old_file = CoverArtSpec.CreateLegacyArtistAlbumId (artist, album);
                        var new_file = CoverArtSpec.CreateArtistAlbumId (artist, album);

                        if (String.IsNullOrEmpty (old_file) || String.IsNullOrEmpty (new_file)) {
                            continue;
                        }

                        old_file = String.Format ("{0}.jpg", old_file);
                        new_file = String.Format ("{0}.jpg", new_file);

                        var old_path = new SafeUri (Paths.Combine (legacy_root_path, old_file));
                        var new_path = new SafeUri (Paths.Combine (root_path, new_file));

                        if (Banshee.IO.File.Exists (old_path) && !Banshee.IO.File.Exists (new_path)) {
                            Banshee.IO.File.Move (old_path, new_path);
                            count++;
                        }
                    }
                }

                if (ServiceManager.DbConnection.TableExists ("PodcastSyndications")) {
                    sql = "SELECT Title FROM PodcastSyndications";
                    foreach (var title in ServiceManager.DbConnection.QueryEnumerable<string> (sql)) {
                        var old_digest = CoverArtSpec.LegacyEscapePart (title);
                        var new_digest = CoverArtSpec.Digest (title);

                        if (String.IsNullOrEmpty (old_digest) || String.IsNullOrEmpty (new_digest)) {
                            continue;
                        }

                        var old_file = String.Format ("podcast-{0}.jpg", old_digest);
                        var new_file = String.Format ("podcast-{0}.jpg", new_digest);

                        var old_path = new SafeUri (Paths.Combine (legacy_root_path, old_file));
                        var new_path = new SafeUri (Paths.Combine (root_path, new_file));

                        if (Banshee.IO.File.Exists (old_path) && !Banshee.IO.File.Exists (new_path)) {
                            Banshee.IO.File.Move (old_path, new_path);
                            count++;
                        }
                    }
                }

                if (count == 0) {
                    ResetScanResultCache ();
                }

                Directory.Delete (legacy_root_path, true);
                Log.InformationFormat ("Migrated {0} files in {1}s", count, DateTime.Now.Subtract(started).TotalSeconds);
            }

            CacheVersion = CUR_VERSION;
        }

        private void ResetScanResultCache ()
        {
            try {
                ServiceManager.DbConnection.Execute ("DELETE FROM CoverArtDownloads");
                DatabaseConfigurationClient.Client.Set<DateTime> ("last_cover_art_scan", DateTime.MinValue);
                Log.InformationFormat ("Reset CoverArtDownloads table so missing artwork will get fetched");
            } catch {}
        }

        private static SafeUri cache_version_file = new SafeUri (Paths.Combine (CoverArtSpec.RootPath, ".cache_version"));
        private static int CacheVersion {
            get {
                var file = cache_version_file;
                if (!Banshee.IO.File.Exists (file)) {
                    file = new SafeUri (Paths.Combine (CoverArtSpec.LegacyRootPath, ".cache_version"));
                    if (!Banshee.IO.File.Exists (file)) {
                        file = null;
                    }
                }

                if (file != null) {
                    using (var reader = new System.IO.StreamReader (Banshee.IO.File.OpenRead (file))) {
                        int version;
                        if (Int32.TryParse (reader.ReadLine (), out version)) {
                            return version;
                        }
                    }
                }

                return 0;
            }
            set {
                using (var writer = new System.IO.StreamWriter (Banshee.IO.File.OpenWrite (cache_version_file, true))) {
                    writer.Write (value.ToString ());
                }
            }
        }

#endregion

    }
}
