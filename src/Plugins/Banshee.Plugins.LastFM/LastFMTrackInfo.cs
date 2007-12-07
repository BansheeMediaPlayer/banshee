/***************************************************************************
 *  LastFMTrackInfo.cs
 *
 *  Copyright (C) 2007 Novell, Inc.
 *  Written by Gabriel Burt <gabriel.burt@gmail.com>
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
using System.Net;
using System.Web;

using Gdk;

using Banshee.Base;
using Banshee.Playlists.Formats.Xspf;

namespace Banshee.Plugins.LastFM
{
    public class LastFMTrackInfo : TrackInfo
    {
        private StationSource station;
        private Track track;
        private bool loved, hated;

        public LastFMTrackInfo ()
        {
            CanSaveToDatabase = false;
        }

        public Track XspfTrack {
            get { return track; }
            set { track = value; }
        }

        public bool Loved {
            get { return loved; }
        }

        public bool Hated {
            get { return hated; }
        }

        public LastFMTrackInfo (Track track, StationSource station)
        {
            this.station = station;
            Uri = new SafeUri (track.Locations[0]);
            Artist = track.Creator;
            Title = track.Title;
            Album = track.Album;
            Duration = track.Duration;
            TrackNumber = track.TrackNumber;
            XspfTrack = track;
            CanSaveToDatabase = false;
        }

        public override void IncrementPlayCount ()
        {
            station.PlayCount++;
            station.Commit ();
        }

		public void Love () 
		{
            loved = true; hated = false;
            ThreadAssist.Spawn (delegate {
                Connection.Instance.Love (this);
            });
		}

		public void Ban () 
		{
            loved = false; hated = true;
            ThreadAssist.Spawn (delegate {
                Connection.Instance.Ban (this);
            });
		}
    }
}
