/***************************************************************************
 *  MtpDapTrackInfo.cs
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
using Banshee.Base;
using Banshee.Dap;
using libmtpsharp;

namespace Banshee.Dap.Mtp
{
    public sealed class MtpDapTrackInfo : DapTrackInfo
    {
        private MtpDevice camera;
        private Track file;
        
        public Track OriginalFile
        {
            get { return file; }
        }
        
        public MtpDapTrackInfo(MtpDevice camera, Track file) : base()
		{
            this.camera = camera;
            this.file = file;
			
			album = file.Album;
            artist = file.Artist;
            duration = TimeSpan.FromMilliseconds(file.Duration);
            genre = file.Genre;
            play_count = file.UseCount < 0 ? (uint)0 : (uint)file.UseCount;
            rating = file.Rating < 0 ? (uint)0 : (uint)file.Rating;
            title = file.Title;
            track_number = file.TrackNumber < 0 ? (uint)0 : file.TrackNumber;
            year = (file.Date != null && file.Date.Length >= 4) ? int.Parse(file.Date.Substring(0, 4)) : 0;
            can_play = false;             // This can be implemented if there's enough people requesting it
            can_save_to_database = true;
            NeedSync = false;
			// Set a URI even though it's not actually accessible through normal API's.
			uri = new SafeUri("mtp://invalid");
        }
        
        public override bool Equals (object o)
        {
            MtpDapTrackInfo dapInfo = o as MtpDapTrackInfo;
            return dapInfo == null ? false : Equals(dapInfo);
        }
        
        // FIXME: Is this enough? Does it matter if i just match metadata?
        public bool Equals(MtpDapTrackInfo info)
        {
			return this.file.Equals(info.file);
            return info == null ? false
             : this.album == info.album
             && this.artist == info.artist
             && this.title == info.title
             && this.track_number == info.track_number;
        }
        
        public override int GetHashCode ()
        {
            int result = 0;
            result ^= (int)track_number;
            if(album != null) result ^= album.GetHashCode();
            if(artist != null) result ^= artist.GetHashCode();
            if(title != null) result ^= title.GetHashCode();
            
            return result;
        }
        
        public bool OnCamera(MtpDevice camera)
        {
            return this.camera == camera;
        }
		
		protected override void WriteUpdate ()
		{
			OnChanged();
		}
    }
}
