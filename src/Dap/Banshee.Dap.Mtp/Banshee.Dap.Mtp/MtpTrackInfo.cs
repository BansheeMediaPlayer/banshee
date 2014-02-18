/***************************************************************************
 *  MtpTrackInfo.cs
 *
 *  Copyright (C) 2006-2007 Novell and Patrick van Staveren
 *  Written by Patrick van Staveren (trick@vanstaveren.us)
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW:
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),
 *  to deal in the Software without restriction, including without limitation
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,
 *  and/or sell copies of the Software, and to permit persons to whom the
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 *  DEALINGS IN THE SOFTWARE.
 */

using System;
using System.IO;

using Hyena;

using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.ServiceStack;
using Banshee.MediaProfiles;

using Mtp;

namespace Banshee.Dap.Mtp
{
    public class MtpTrackInfo : DatabaseTrackInfo
    {
        private Track file;

        public Track OriginalFile {
            get { return file; }
        }

        public static string GetPathFromMtpTrack (Track track)
        {
            return String.Format ("mtp://{0}/{1}", track.FileId, track.FileName);
        }

        public MtpTrackInfo (MtpDevice device, Track file) : base()
        {
            this.file = file;
            ExternalId = file.FileId;

            AlbumTitle = file.Album;
            ArtistName = file.Artist;
            Duration = TimeSpan.FromMilliseconds (file.Duration);
            Genre = file.Genre;
            PlayCount = file.UseCount < 0 ? 0 : (int) file.UseCount;
            Rating = file.Rating < 0 ? 0 : (file.Rating / 20);
            TrackTitle = file.Title;
            TrackNumber = file.TrackNumber < 0 ? 0 : (int)file.TrackNumber;
            Year = file.Year;
            BitRate = (int)file.Bitrate;
            SampleRate = (int)file.SampleRate;
            FileSize = (long)file.FileSize;

            MediaAttributes = TrackMediaAttributes.AudioStream;
            if (device != null) {
                SetAttributeIf (file.InFolder (device.PodcastFolder, true) || Genre == "Podcast", TrackMediaAttributes.Podcast);
                SetAttributeIf (file.InFolder (device.MusicFolder, true), TrackMediaAttributes.Music);
                SetAttributeIf (file.InFolder (device.VideoFolder, true), TrackMediaAttributes.VideoStream);
            }

            // This can be implemented if there's enough people requesting it
            CanPlay = false;
            CanSaveToDatabase = true;
            //NeedSync = false;

            // TODO detect if this is a video file and set the MediaAttributes appropriately?
            /*Profile profile = ServiceManager.Get<MediaProfileManager> ().GetProfileForExtension (System.IO.Path.GetExtension (file.FileName));
            if (profile != null) {
                profile.
            }*/

            // Set a URI even though it's not actually accessible through normal API's.
            Uri = new SafeUri (GetPathFromMtpTrack (file));
        }

        internal static void ToMtpTrack (TrackInfo track, Track f)
        {
            f.Album = track.AlbumTitle;
            f.Artist = track.ArtistName;
            f.Duration = (uint)track.Duration.TotalMilliseconds;
            f.Genre = track.Genre;
            f.UseCount = (uint)track.PlayCount;
            f.Rating = (ushort)(track.Rating * 20);
            f.Title = track.TrackTitle;
            f.TrackNumber = (ushort)track.TrackNumber;
            f.Year = track.Year;
        }
    }
}
