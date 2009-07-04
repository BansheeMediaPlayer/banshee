//
// AudioCdDisc.cs
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
using System.Threading;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Mono.Unix;
using MusicBrainz;

using Hyena;
using Banshee.Base;
using Banshee.Hardware;
using Banshee.Collection;
using Banshee.Collection.Database;

namespace Banshee.AudioCd
{
    public class AudioCdDiscModel : MemoryTrackListModel
    {
        // 44.1 kHz sample rate * 16 bit channel resolution * 2 channels (stereo)
        private const long PCM_FACTOR = 176400;
    
        private IDiscVolume volume;
        
        public event EventHandler MetadataQueryStarted;
        public event EventHandler MetadataQueryFinished;
        public event EventHandler EnabledCountChanged;
        
        private bool metadata_query_success;
        private DateTime metadata_query_start_time;
        
        public bool MetadataQuerySuccess {
            get { return metadata_query_success; }
        }
        
        private TimeSpan duration;
        public TimeSpan Duration {
            get { return duration; }
        }
        
        private long file_size;
        public long FileSize {
            get { return file_size; }
        }
        
        public AudioCdDiscModel (IDiscVolume volume)
        {
            this.volume = volume;
            disc_title = Catalog.GetString ("Audio CD");
        }
        
        public void NotifyUpdated ()
        {
            OnReloaded ();
        }
        
        public void LoadModelFromDisc ()
        {
            Clear ();
        
            LocalDisc mb_disc = LocalDisc.GetFromDevice (volume.DeviceNode);
            if (mb_disc == null) {
                throw new ApplicationException ("Could not read contents of the disc. Platform may not be supported.");
            }

            TimeSpan[] durations = mb_disc.GetTrackDurations ();
            for (int i = 0, n = durations.Length; i < n; i++) {
                AudioCdTrackInfo track = new AudioCdTrackInfo (this, volume.DeviceNode, i);
                track.TrackNumber = i + 1;
                track.TrackCount = n;
                track.DiscNumber = 1;
                track.Duration = durations[i];
                track.ArtistName = Catalog.GetString ("Unknown Artist");
                track.AlbumTitle = Catalog.GetString ("Unknown Album");
                track.TrackTitle = String.Format (Catalog.GetString ("Track {0}"), track.TrackNumber);
                track.FileSize = PCM_FACTOR * (uint)track.Duration.TotalSeconds;
                Add (track);
                
                duration += track.Duration;
                file_size += track.FileSize;
            }
            
            EnabledCount = Count;
            
            Reload ();
            
            ThreadPool.QueueUserWorkItem (LoadDiscMetadata, mb_disc);
        }
        
        private void LoadDiscMetadata (object state)
        {
            try {
                LocalDisc mb_disc = (LocalDisc)state;
                
                OnMetadataQueryStarted (mb_disc);
                
                Release release = Release.Query (mb_disc).PerfectMatch ();
    
                var tracks = release.GetTracks ();
                if (release == null || tracks.Count != Count) {
                    OnMetadataQueryFinished (false);
                    return;
                }
                            
                disc_title = release.GetTitle ();
                
                int disc_number = 1;
                int i = 0;
                
                foreach (Disc disc in release.GetDiscs ()) {
                    i++;
                    if (disc.Id == mb_disc.Id) {
                        disc_number = i;
                    }
                }
                
                DateTime release_date = DateTime.MaxValue;
     
                foreach (Event release_event in release.GetEvents ()) {
                    if (release_event.Date != null) {
                        try {
                            // Handle "YYYY" dates
                            var date_str = release_event.Date;
                            DateTime date = DateTime.Parse (
                                date_str.Length > 4 ? date_str : date_str + "-01",
                                ApplicationContext.InternalCultureInfo
                            );
    
                            if (date < release_date) {
                                release_date = date;
                            }
                        } catch {
                        }
                    }
                }
                
                DatabaseArtistInfo artist = new DatabaseArtistInfo ();
                var mb_artist = release.GetArtist ();
                artist.Name = mb_artist.GetName ();
                artist.NameSort = mb_artist.GetSortName ();
                artist.MusicBrainzId = mb_artist.Id;
                bool is_compilation = false;
                
                DatabaseAlbumInfo album = new DatabaseAlbumInfo ();
                album.Title = disc_title;
                album.ArtistName = artist.Name;
                album.MusicBrainzId = release.Id;
                album.ReleaseDate = release_date == DateTime.MaxValue ? DateTime.MinValue : release_date;
                
                i = 0;
                foreach (Track track in tracks) {
                    AudioCdTrackInfo model_track = (AudioCdTrackInfo)this[i++];
                    var mb_track_artist = track.GetArtist ();
                    
                    model_track.MusicBrainzId = track.Id;
                    model_track.TrackTitle = track.GetTitle ();
                    model_track.ArtistName = mb_track_artist.GetName ();
                    model_track.AlbumTitle = disc_title;
                    model_track.DiscNumber = disc_number;
                    model_track.Album = album;
    
                    model_track.Artist = new DatabaseArtistInfo ();
                    model_track.Artist.Name = model_track.ArtistName;
                    model_track.Artist.NameSort = mb_track_artist.GetSortName ();
                    model_track.Artist.MusicBrainzId = mb_track_artist.Id;
                    
                    if (release_date != DateTime.MinValue) {
                        model_track.Year = release_date.Year;
                    }
    
                    if (!is_compilation && mb_track_artist.Id != artist.MusicBrainzId) {
                        is_compilation = true;
                    }
                }
    
                if (is_compilation) {
                    album.IsCompilation = true;
                    for (i = 0; i < tracks.Count; i++) {
                        AudioCdTrackInfo model_track = (AudioCdTrackInfo)this[i];
                        model_track.IsCompilation = true;
                        model_track.AlbumArtist = artist.Name;
                        model_track.AlbumArtistSort = artist.NameSort;
                    }
                }
                
                OnMetadataQueryFinished (true);
            } catch (Exception ex) {
                Log.DebugException (ex);
                OnMetadataQueryFinished (false);
            }
        }
        
        private void OnMetadataQueryStarted (LocalDisc mb_disc)
        {
            metadata_query_success = false;
            metadata_query_start_time = DateTime.Now;
            Log.InformationFormat ("Querying MusicBrainz for Disc Release ({0})", mb_disc.Id);
        
            ThreadAssist.ProxyToMain (delegate { 
                EventHandler handler = MetadataQueryStarted;
                if (handler != null) {
                    handler (this, EventArgs.Empty);
                }
            });
        }
        
        private void OnMetadataQueryFinished (bool success)
        {
            metadata_query_success = success;
            Log.InformationFormat ("Query finished (success: {0}, {1} seconds)", 
                success, (DateTime.Now - metadata_query_start_time).TotalSeconds);
            
            ThreadAssist.ProxyToMain (delegate {
                Reload ();
                
                EventHandler handler = MetadataQueryFinished;
                if (handler != null) {
                    handler (this, EventArgs.Empty);
                }
            });
        }
        
        private void OnEnabledCountChanged ()
        {
            EventHandler handler = EnabledCountChanged;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }
        
        private ICdromDevice Drive {
            get { return Volume == null ? null : (Volume.Parent as ICdromDevice); }
        }
        
        public bool LockDoor ()
        {
            ICdromDevice drive = Drive;
            return drive != null ? drive.LockDoor () : false;
        }
        
        public bool UnlockDoor ()
        {
            ICdromDevice drive = Drive;
            return drive != null ? drive.UnlockDoor () : false;
        }
        
        public bool IsDoorLocked {
            get { 
                ICdromDevice drive = Drive;
                return drive != null ? drive.IsDoorLocked : false;
            }
        }
        
        public IDiscVolume Volume {
            get { return volume; }
        }
        
        private string disc_title;
        public string Title {
            get { return disc_title; }
        }
        
        private int enabled_count;
        public int EnabledCount {
            get { return enabled_count; }
            internal set { 
                enabled_count = value; 
                OnEnabledCountChanged (); 
            }
        }
    }
}
