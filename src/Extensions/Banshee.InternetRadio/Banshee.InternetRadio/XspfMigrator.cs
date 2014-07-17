//
// XspfMigrator.cs
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

using Hyena;

using Banshee.Base;
using Banshee.ServiceStack;
using Banshee.Configuration;
using Banshee.Collection.Database;

using Media.Playlists.Xspf;

namespace Banshee.InternetRadio
{
    public class XspfMigrator
    {
        private InternetRadioSource source;

        public XspfMigrator (InternetRadioSource source)
        {
            this.source = source;
        }

        public bool LoadDefaults ()
        {
            // Load system-wide defaults into user DB on first start
            if (DatabaseConfigurationClient.Client.Get<bool> ("InternetRadio.SystemXspfLoaded", false)) {
                return false;
            }
            DatabaseConfigurationClient.Client.Set<bool> ("InternetRadio.SystemXspfLoaded", true);

            string xspf_system_path = Hyena.Paths.GetInstalledDataDirectory ("stations");
            if (!Directory.Exists (xspf_system_path)) {
                return false;
            }
            
            try {
                foreach (string file in Directory.GetFiles (xspf_system_path, "*.xspf")) {
                    Log.DebugFormat ("Loading default Internet Radio Stations from {0}", file);
                    MigrateXspf (file);
                }
            } catch (Exception e) {
                Log.Error ("Error loading Internet Radio Stations from system default", e);
            }

            return true;
        }

        public bool Migrate ()
        {
            if (DatabaseConfigurationClient.Client.Get<bool> ("InternetRadio.LegacyXspfMigrated", false)) {
                return false;
            }

            DatabaseConfigurationClient.Client.Set<bool> ("InternetRadio.LegacyXspfMigrated", true);

            string xspf_path = Paths.Combine (LegacyPaths.ApplicationData, "plugins", "stations");

            try {
                foreach (string file in Directory.GetFiles (Paths.Combine (xspf_path, "user"), "*.xspf")) {
                    MigrateXspf (file);
                }

                foreach (string file in Directory.GetFiles (xspf_path, "*.xspf")) {
                    MigrateXspf (file);
                }
            } catch (Exception e) {
                Log.Error ("Error migrating Internet Radio Stations", e);
            }

            return true;
        }

        private void MigrateXspf (string path)
        {
            try {
                Media.Playlists.Xspf.Playlist playlist = new Media.Playlists.Xspf.Playlist ();
                playlist.Load (path);

                foreach (Track track in playlist.Tracks) {
                    try {
                        MigrateXspfTrack (playlist, track);
                    } catch (Exception e) {
                        Log.Error ("Could not migrate XSPF track", e);
                    }
                }
            } catch (Exception e) {
                Log.Error ("Could not migrate XSPF playlist", e);
            }
        }

        private void MigrateXspfTrack (Media.Playlists.Xspf.Playlist playlist, Track track)
        {
            if (track.LocationCount <= 0) {
                return;
            }

            DatabaseTrackInfo station = new DatabaseTrackInfo ();
            station.PrimarySource = source;
            station.IsLive = true;

            station.Uri = GetSafeUri (track.Locations[0]);

            if (!String.IsNullOrEmpty (track.Title)) {
                station.TrackTitle = track.Title;
            }

            if (!String.IsNullOrEmpty (track.Creator)) {
                station.ArtistName = track.Creator;
            }

            if (!String.IsNullOrEmpty (track.Annotation)) {
                station.Comment = track.Annotation;
            }

            if (!String.IsNullOrEmpty (playlist.Title)) {
                station.Genre = playlist.Title;
            }

            if (track.Info != null) {
                station.MoreInfoUri = GetSafeUri (track.Info);
            }

            station.Save ();
        }

        private SafeUri GetSafeUri (Uri uri)
        {
            try {
                if (uri == null) {
                    return null;
                }

                string absolute = uri.AbsoluteUri;
                return String.IsNullOrEmpty (absolute) ? null : new SafeUri (absolute);
            } catch {
                return null;
            }
        }
    }
}
