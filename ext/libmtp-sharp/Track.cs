/***************************************************************************
 *  Track.cs
 *
 *  Copyright (C) 2006-2007 Alan McGovern
 *  Authors:
 *  Alan McGovern (alan.mcgovern@gmail.com)
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

namespace libmtpsharp
{
	public class Track
	{
		internal TrackStruct trackStruct;
		private MtpDevice device;
		
		internal uint FileId
		{
			get { return trackStruct.item_id; }
		}
		
		public string Album
		{
			get { return trackStruct.album; }
			set { trackStruct.album = value;}
		}
		public string Artist
		{
			get { return trackStruct.artist; }
			set { trackStruct.artist = value; }
		}
		public uint Bitrate
		{
			get { return trackStruct.bitrate; }
		}
		public ushort BitrateType
		{
			get { return trackStruct.bitratetype; }
		}
		public string Date
		{
			get { return trackStruct.date; }
			set { trackStruct.date = value; }
		}
		public uint Duration
		{
			get { return trackStruct.duration; }
			set { trackStruct.duration = value; }
		}
		public string Filename
		{
			get { return trackStruct.filename; }
			set { trackStruct.filename = value; }
		}
		public ulong Filesize
		{
			get { return trackStruct.filesize; }
			set { trackStruct.filesize = value; }
		}
		public FileType Filetype
		{
			get { return trackStruct.filetype; }
			set { trackStruct.filetype = value; }
		}
		public string Genre
		{
			get { return trackStruct.genre; }
			set { trackStruct.genre = value; }
		}
		public ushort NoChannels
		{
			get { return trackStruct.nochannels; }
			set { trackStruct.nochannels = value; }
		}
		public ushort Rating  // 0 -> 100
		{
			get { return trackStruct.rating; }
			set
			{
				if (value < 0 || value > 100)
					throw new ArgumentOutOfRangeException("Rating", "Rating must be between zero and 100");
				trackStruct.rating = value;
			}
		}
		public uint SampleRate
		{
			get { return trackStruct.samplerate; }
			set { trackStruct.samplerate = value; }
		}
		public string Title
		{
			get { return trackStruct.title; }
			set { trackStruct.title = value; }
		}
		public ushort TrackNumber
		{
			get { return trackStruct.tracknumber; }
			set { trackStruct.tracknumber = value; }
		}
		public uint WaveCodec
		{
			get { return trackStruct.wavecodec; }
		}
		public uint UseCount
		{
			get { return trackStruct.usecount; }
			set { trackStruct.usecount = value; }
		}
		

		public Track (string filename, ulong filesize)
			: this(new TrackStruct(), null)
		{
			this.trackStruct.filename = filename;
			this.trackStruct.filesize = filesize;
			this.trackStruct.filetype = DetectFiletype(this);
		}
		
		internal Track (TrackStruct track, MtpDevice device)
		{
			this.device = device;
			this.trackStruct = track;
		}
		
		public void Download(string path)
		{
			Download(path, null);
		}
		
		public void Download (string path, ProgressFunction callback)
		{
			if (string.IsNullOrEmpty(path))
				throw new ArgumentException ("Cannot be null or empty", "path");
			
			TrackManagement.GetTrack (device.handle, trackStruct.item_id, path, callback, IntPtr.Zero);
		}
		
		public void UpdateMetadata()
		{
			TrackManagement.UpdateTrackMetadata(device.handle, ref trackStruct);
		}
		
		private static FileType DetectFiletype(Track track)
		{
			if(track.Filename.EndsWith(".asf", System.StringComparison.OrdinalIgnoreCase))
				return FileType.LIBMTP_FILETYPE_ASF;
			
			if(track.Filename.EndsWith(".avi", System.StringComparison.OrdinalIgnoreCase))
				return  FileType.LIBMTP_FILETYPE_AVI;
			
			if(track.Filename.EndsWith(".BMP", System.StringComparison.OrdinalIgnoreCase))
				return  FileType.LIBMTP_FILETYPE_BMP;
			
			if(track.Filename.EndsWith(".JPEG", System.StringComparison.OrdinalIgnoreCase)
			   || track.Filename.EndsWith(".JPG", System.StringComparison.OrdinalIgnoreCase))
				return FileType.LIBMTP_FILETYPE_JPEG;
			
			if(track.Filename.EndsWith(".MP3", System.StringComparison.OrdinalIgnoreCase))
				return FileType.LIBMTP_FILETYPE_MP3;
			
			if(track.Filename.EndsWith(".MPG", System.StringComparison.OrdinalIgnoreCase)
			   || track.Filename.EndsWith(".MPEG", System.StringComparison.OrdinalIgnoreCase))
				return FileType.LIBMTP_FILETYPE_MPEG;
			
			if(track.Filename.EndsWith(".OGG", System.StringComparison.OrdinalIgnoreCase)
			   || track.Filename.EndsWith(".OGM", System.StringComparison.OrdinalIgnoreCase))
				return  FileType.LIBMTP_FILETYPE_OGG;
						
			if(track.Filename.EndsWith(".PNG", System.StringComparison.OrdinalIgnoreCase))
				return  FileType.LIBMTP_FILETYPE_PNG;
			
			if(track.Filename.EndsWith(".WAV", System.StringComparison.OrdinalIgnoreCase))
				return FileType.LIBMTP_FILETYPE_WAV;
									
			if(track.Filename.EndsWith(".WMA", System.StringComparison.OrdinalIgnoreCase))
				return FileType.LIBMTP_FILETYPE_WMA;

			return  FileType.LIBMTP_FILETYPE_UNKNOWN;
		}
	}
}
