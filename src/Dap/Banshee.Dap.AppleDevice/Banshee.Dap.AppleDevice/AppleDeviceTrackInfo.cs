// 
// AppleDeviceTrackInfo.cs
// 
// Author:
//   Alan McGovern <amcgovern@novell.com>
// 
// Copyright (c) 2010 Moonlight Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;

using Banshee.Base;
using Banshee.Streaming;
using Banshee.Collection;
using Banshee.Collection.Database;

using Hyena;

namespace Banshee.Dap.AppleDevice
{
    public class AppleDeviceTrackInfo : DatabaseTrackInfo
    {
        internal GPod.Track IpodTrack {
            get; set;
        }

        // libgpod stores rating as stars*ITDB_RATING_STEP which is 20.
        private const int ITDB_RATING_STEP = 20;

        private string mimetype;
        private string description; // Only used for podcasts.

        public AppleDeviceTrackInfo (GPod.Track track)
        {
            IpodTrack = track;
            LoadFromIpodTrack ();
            CanSaveToDatabase = true;
        }

        public AppleDeviceTrackInfo (TrackInfo track)
        {
            CanSaveToDatabase = true;

            if (track is AppleDeviceTrackInfo) {
                IpodTrack = ((AppleDeviceTrackInfo)track).IpodTrack;
                LoadFromIpodTrack ();
            } else {
                UpdateInfo (track);
            }
        }

        public void UpdateInfo (TrackInfo track)
        {
            if (track is AppleDeviceTrackInfo) {
                throw new ArgumentException ("Shouldn't update an AppleDeviceTrackInfo from an AppleDeviceTrackInfo");
            }

            IsCompilation = track.IsCompilation ;
            AlbumArtist = track.AlbumArtist;
            AlbumTitle = track.AlbumTitle;
            ArtistName = track.ArtistName;
            BitRate = track.BitRate;
            SampleRate = track.SampleRate;
            Bpm = track.Bpm;
            Comment = track.Comment;
            Composer = track.Composer;
            Conductor = track.Conductor;
            Copyright = track.Copyright;
            DateAdded = track.DateAdded;
            DiscCount = track.DiscCount;
            DiscNumber = track.DiscNumber;
            Duration = track.Duration;
            FileSize = track.FileSize;
            Genre = track.Genre;
            Grouping = track.Grouping;
            LastPlayed = track.LastPlayed;
            LastSkipped = track.LastSkipped;
            PlayCount = track.PlayCount;
            Rating = track.Rating;
            ReleaseDate = track.ReleaseDate;
            SkipCount = track.SkipCount;
            TrackCount = track.TrackCount;
            TrackNumber = track.TrackNumber;
            TrackTitle = track.TrackTitle;
            Year = track.Year;
            MediaAttributes = track.MediaAttributes;

            ArtistNameSort = track.ArtistNameSort;
            AlbumTitleSort = track.AlbumTitleSort;
            AlbumArtistSort = track.AlbumArtistSort;
            TrackTitleSort = track.TrackTitleSort;

            var podcast_info = track.ExternalObject as IPodcastInfo;
            if (podcast_info != null) {
                description = podcast_info.Description;
                ReleaseDate = podcast_info.ReleaseDate;
            }

            mimetype = track.MimeType;
        }

        private void LoadFromIpodTrack ()
        {
            var track = IpodTrack;
            try {
                Uri = new SafeUri (System.IO.Path.Combine (track.ITDB.Mountpoint, track.IpodPath.Replace (":", System.IO.Path.DirectorySeparatorChar.ToString ()).Substring (1)));
            } catch (Exception ex) {
                Log.Error (ex);
                Uri = null;
            }

            ExternalId = (long) track.DBID;

            IsCompilation = track.Compilation;
            AlbumArtist = track.AlbumArtist;
            AlbumTitle = String.IsNullOrEmpty (track.Album) ? null : track.Album;
            ArtistName = String.IsNullOrEmpty (track.Artist) ? null : track.Artist;
            BitRate = track.Bitrate;
            SampleRate = track.Samplerate;
            Bpm = (int)track.BPM;
            Comment = track.Comment;
            Composer = track.Composer;
            DateAdded = track.TimeAdded;
            DiscCount = track.CDs;
            DiscNumber = track.CDNumber;
            Duration = TimeSpan.FromMilliseconds (track.TrackLength);
            FileSize = track.Size;
            Genre = String.IsNullOrEmpty (track.Genre) ? null : track.Genre;
            Grouping = track.Grouping;
            LastPlayed = track.TimePlayed;
            PlayCount = (int) track.PlayCount;
            TrackCount = track.Tracks;
            TrackNumber = track.TrackNumber;
            TrackTitle = String.IsNullOrEmpty (track.Title) ? null : track.Title;
            Year = track.Year;
            description = track.Description;
            ReleaseDate = track.TimeReleased;
            ArtistNameSort = track.SortArtist;
            AlbumTitleSort = track.SortAlbum;
            AlbumArtistSort = track.SortAlbumArtist;
            TrackTitleSort = track.SortTitle;
            rating = track.Rating > 100 ? 0 : (int) track.Rating / ITDB_RATING_STEP;

            if (track.DRMUserID > 0) {
                PlaybackError = StreamPlaybackError.Drm;
            }

            MediaAttributes = TrackMediaAttributes.AudioStream;
            switch (track.MediaType) {
            case GPod.MediaType.Audio:
                MediaAttributes |= TrackMediaAttributes.Music;
                break;
            // This seems to cause audio files to show up in Banshee in the Videos section
            /*case GPod.MediaType.AudioVideo:
                MediaAttributes |= TrackMediaAttributes.VideoStream;
                break;*/
            case GPod.MediaType.MusicVideo:
                MediaAttributes |= TrackMediaAttributes.Music | TrackMediaAttributes.VideoStream;
                break;
            case GPod.MediaType.Movie:
                MediaAttributes |= TrackMediaAttributes.VideoStream | TrackMediaAttributes.Movie;
                break;
            case GPod.MediaType.TVShow:
                MediaAttributes |= TrackMediaAttributes.VideoStream | TrackMediaAttributes.TvShow;
                break;
            case GPod.MediaType.Podcast:
                MediaAttributes |= TrackMediaAttributes.Podcast;
                // FIXME: persist URL on the track (track.PodcastUrl)
                break;
            case GPod.MediaType.Audiobook:
                MediaAttributes |= TrackMediaAttributes.AudioBook;
                break;
            case GPod.MediaType.MusicTVShow:
                MediaAttributes |= TrackMediaAttributes.Music | TrackMediaAttributes.VideoStream | TrackMediaAttributes.TvShow;
                break;
            }

            // If it's just AudioStream, add Music to it as well so it'll show up in Music
            if (MediaAttributes == TrackMediaAttributes.AudioStream) {
                MediaAttributes |= TrackMediaAttributes.Music;
            }
        }

        public void CommitToIpod (GPod.ITDB database)
        {
            bool addTrack = IpodTrack == null;
            if (IpodTrack == null) {
                IpodTrack = new GPod.Track ();
            }

            var track = IpodTrack;
            track.Compilation = IsCompilation;
            track.AlbumArtist = AlbumArtist;
            track.Bitrate = BitRate;
            track.Samplerate= (ushort)SampleRate;
            track.BPM = (short)Bpm;
            track.Comment = Comment;
            track.Composer = Composer;
            track.TimeAdded = DateTime.Now;
            track.CDs = DiscCount;
            track.CDNumber = DiscNumber;
            track.TrackLength = (int) Duration.TotalMilliseconds;
            track.Size = (uint)FileSize;
            track.Grouping = Grouping;
            try {
                track.TimePlayed = LastPlayed;
            } catch {
                Hyena.Log.InformationFormat ("Couldn't set TimePlayed to '{0}'", LastPlayed);
            }
            track.PlayCount = (uint) PlayCount;
            track.Tracks = TrackCount;
            track.TrackNumber = TrackNumber;
            track.Year = Year;
            try {
                track.TimeReleased = ReleaseDate;
            } catch {
                Hyena.Log.InformationFormat ("Couldn't set TimeReleased to '{0}'", ReleaseDate);
            }
            track.Album = AlbumTitle;
            track.Artist = ArtistName;
            track.Title = TrackTitle;
            track.Genre = Genre;

            track.SortArtist = ArtistNameSort;
            track.SortAlbum = AlbumTitleSort;
            track.SortAlbumArtist = AlbumArtistSort;
            track.SortTitle = TrackTitleSort;
            track.Rating = ((Rating >= 1) && (Rating <= 5)) ? (uint)Rating * ITDB_RATING_STEP : 0;

            if (HasAttribute (TrackMediaAttributes.Podcast)) {
                track.Description = description;
                track.RememberPlaybackPosition = true;
                track.SkipWhenShuffling = true;
                track.Flag4 = (byte)1;
                track.MarkUnplayed = (track.PlayCount == 0);
            }

            track.MediaType = GPod.MediaType.Audio;
            if (HasAttribute (TrackMediaAttributes.VideoStream)) {
                if (HasAttribute (TrackMediaAttributes.Podcast)) {
                    track.MediaType = GPod.MediaType.Podcast | GPod.MediaType.Movie;
                } else if (HasAttribute (TrackMediaAttributes.Music)) {
                    if (HasAttribute (TrackMediaAttributes.TvShow)) {
                        track.MediaType = GPod.MediaType.MusicTVShow;
                    } else {
                        track.MediaType = GPod.MediaType.MusicVideo;
                    }
                } else if (HasAttribute (TrackMediaAttributes.Movie)) {
                    track.MediaType = GPod.MediaType.Movie;
                } else if (HasAttribute (TrackMediaAttributes.TvShow)) {
                    track.MediaType = GPod.MediaType.TVShow;
                } else {
                    // I dont' think AudioVideo should be used here; upon loading the tracks
                    // into Banshee, audio files often have AudioVideo (aka MediaType == 0) too.
                    //track.MediaType = GPod.MediaType.AudioVideo;
                    track.MediaType = GPod.MediaType.Movie;
                }
            } else {
                if (HasAttribute (TrackMediaAttributes.Podcast)) {
                    track.MediaType = GPod.MediaType.Podcast;
                } else if (HasAttribute (TrackMediaAttributes.AudioBook)) {
                    track.MediaType = GPod.MediaType.Audiobook;
                } else if (HasAttribute (TrackMediaAttributes.Music)) {
                    track.MediaType = GPod.MediaType.Audio;
                } else {
                    track.MediaType = GPod.MediaType.Audio;
                }
            }

            if (addTrack) {
                track.Filetype = mimetype;

                database.Tracks.Add (IpodTrack);
                database.MasterPlaylist.Tracks.Add (IpodTrack);

                if (HasAttribute (TrackMediaAttributes.Podcast) && database.Device.SupportsPodcast) {
                    database.PodcastsPlaylist.Tracks.Add (IpodTrack);
                }

                database.CopyTrackToIPod (track, Uri.LocalPath);
                Uri = new SafeUri (GPod.ITDB.GetLocalPath (database.Device, track));
                ExternalId = (long) IpodTrack.DBID;
            }

            if (CoverArtSpec.CoverExists (ArtworkId)) {
                string path = CoverArtSpec.GetPath (ArtworkId);
                if (!track.ThumbnailsSet (path)) {
                    Log.Error (String.Format ("Could not set cover art for {0}.", path));
                }
            } else {
                track.ThumbnailsRemoveAll ();
            }
        }
    }
}
