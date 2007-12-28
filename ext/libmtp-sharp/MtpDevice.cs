/***************************************************************************
 *  MtpDevice.cs
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
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace libmtpsharp
{
	public class MtpDevice : IDisposable
	{
		internal MtpDeviceHandle handle;
		private MtpDeviceStruct device;
		private string name;
		private Folder albumFolder;
		private Folder musicFolder;
		private Folder organizerFolder;
		private Folder pictureFolder;
		private Folder playlistFolder;
		private Folder podcastFolder;
		private Folder textFolder;
		private Folder videoFolder;
		
		public Folder AlbumFolder
		{
			get { return albumFolder; }
		}
		
		public int BatteryLevel
		{
			get
			{
				ushort level, maxLevel;
				LibMtp.GetBatteryLevel (handle, out maxLevel, out level);
				return (int)((level * 100.0) / maxLevel);
			}
		}

		public Folder MusicFolder
		{
			get { return musicFolder;}
		}
		
		public string Name
		{
			get { return name; }
		}

		public Folder OrganizerFolder
		{
			get { return organizerFolder; }
		}

		public Folder PictureFolder
		{
			get { return pictureFolder; }
		}

		public Folder PlaylistFolder
		{
			get { return playlistFolder; }
		}

		public Folder PodcastFolder
		{
			get { return podcastFolder; }
		}

		public Folder TextFolder
		{
			get { return textFolder; }
		}
		
		public Folder VideoFolder
		{
			get { return videoFolder; }
		}
		
		static MtpDevice()
		{
			LibMtp.Init();
		}
		
		internal MtpDevice (MtpDeviceHandle handle, MtpDeviceStruct device)
		{
			this.device = device;
			this.handle = handle;
			this.name = LibMtp.GetFriendlyName(handle);
			SetDefaultFolders ();
		}
		
		internal MtpDevice(IntPtr handle, bool ownsHandle, MtpDeviceStruct device)
			: this(new MtpDeviceHandle(handle, ownsHandle), device)
		{
			
		}
		
		/// <summary>
		/// This function scans the top level directories and stores the relevant ones so they are readily
		/// accessible
		/// </summary>
		private void SetDefaultFolders ()
		{
			List<Folder> folders = new List<Folder>();
			
			foreach (Folder f in folders)
			{
				if (f.FolderId == this.device.default_album_folder)
					albumFolder = f;
				else if (f.FolderId == device.default_music_folder)
					musicFolder = f;
				else if (f.FolderId == device.default_organizer_folder)
					organizerFolder = f;
				else if (f.FolderId == device.default_picture_folder)
					pictureFolder = f;
				else if (f.FolderId == device.default_playlist_folder)
					playlistFolder = f;
				else if (f.FolderId == device.default_text_folder)
					textFolder = f;
				else if (f.FolderId == device.default_video_folder)
					videoFolder = f;
				else if (f.FolderId == device.default_zencast_folder)
					podcastFolder = f;
			}
		}
		
		public void Dispose ()
		{
			if (!handle.IsClosed)
				handle.Close();
		}
		
		public List<Folder> GetRootFolders()
		{
			return Folder.GetRootFolders(this);
		}
		
		public List<Track> GetAllTracks()
		{
			return GetAllTracks(null);
		}
		
		public List<Track> GetAllTracks(ProgressFunction callback)
		{
			IntPtr ptr = TrackManagement.GetTrackListing(handle, callback, IntPtr.Zero);

			if (ptr == IntPtr.Zero)
				throw new LibMtpException(ErrorCode.LIBMTP_ERROR_PTP_LAYER);
			
			List<Track> tracks = new List<Track>();
			while (ptr != IntPtr.Zero)
			{
				TrackStruct track = (TrackStruct)Marshal.PtrToStructure(ptr, typeof(TrackStruct));
				TrackManagement.DestroyTrack (ptr);
				tracks.Add(new Track(track, this));
				ptr = track.next;
			}
			
			return tracks;
		}
		
		
		public List<DeviceStorage> GetStorage ()
		{
			List<DeviceStorage> storages = new List<DeviceStorage>();
			IntPtr ptr = device.storage;
			while (ptr != IntPtr.Zero)
			{
				DeviceStorage storage = (DeviceStorage)Marshal.PtrToStructure(ptr, typeof(DeviceStorage));
				storages.Add(storage);
				ptr = storage.next;
			}
			return storages;
		}
		
		public void Remove (Track track)
		{
			Console.WriteLine("Removing: {0}", track.FileId);
			LibMtp.DeleteObject(handle, track.FileId);
		}
		
		public void UploadTrack (string path, Track track, Folder folder)
		{
			UploadTrack (path, track, folder);
		}
		
		public void UploadTrack (string path, Track track)
		{
			this.UploadTrack(path, track, new Folder(0, 0, "", this), null);
		}
		
		public void UploadTrack (string path, Track track, ProgressFunction callback)
		{
			this.UploadTrack(path, track, new Folder(0, 0, "", this), callback);
		}
		
		public void UploadTrack (string path, Track track, Folder folder, ProgressFunction callback)
		{
			if (string.IsNullOrEmpty(path))
				throw new ArgumentNullException("path");
			if (track == null)
				throw new ArgumentNullException("track");
			
			// We send the trackstruct by ref so that when the file_id gets filled in, our copy is updated
			TrackManagement.SendTrack (handle, path, ref track.trackStruct, callback, IntPtr.Zero, folder.FolderId);
			// LibMtp.GetStorage (handle, 0);
		}
		
		public static List<MtpDevice> Detect ()
		{
			IntPtr ptr;
			LibMtp.GetConnectedDevices(out ptr);
			
			List<MtpDevice> devices = new List<MtpDevice>();
			while (ptr != IntPtr.Zero)
			{
				MtpDeviceStruct d = (MtpDeviceStruct)Marshal.PtrToStructure(ptr, typeof(MtpDeviceStruct));
				devices.Add(new MtpDevice(ptr, true, d));
				ptr = d.next;
			}
			
			return devices;
		}
	}
}
