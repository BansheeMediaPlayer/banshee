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

namespace Mtp
{
	public delegate int ProgressFunction(ulong sent, ulong total, IntPtr data);

	public class MtpDevice : IDisposable
	{
		internal MtpDeviceHandle Handle;
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

		static MtpDevice() {
			LIBMTP_Init ();
		}
		
		public int BatteryLevel {
			get {
				ushort level, maxLevel;
				GetBatteryLevel (Handle, out maxLevel, out level);
				return (int)((level * 100.0) / maxLevel);
			}
		}

        public string SerialNumber {
            get { return GetSerialnumber (Handle); }
        }

        public string Version {
            get { return GetDeviceversion (Handle); }
        }

		public string Name {
			get { return name; }
            set {
                if (SetFriendlyName (Handle, value)) {
                    name = value;
                }
            }
		}

		public Folder AlbumFolder {
			get { return albumFolder; }
		}

		public Folder MusicFolder {
			get { return musicFolder; }
		}

		public Folder OrganizerFolder {
			get { return organizerFolder; }
		}

		public Folder PictureFolder {
			get { return pictureFolder; }
		}

		public Folder PlaylistFolder {
			get { return playlistFolder; }
		}

		public Folder PodcastFolder {
			get { return podcastFolder; }
		}

		public Folder TextFolder {
			get { return textFolder; }
		}
		
		public Folder VideoFolder {
			get { return videoFolder; }
		}
		
		internal MtpDevice (MtpDeviceHandle handle, MtpDeviceStruct device)
		{
			this.device = device;
			this.Handle = handle;
			this.name = GetFriendlyName(Handle);
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
			List<Folder> folders = GetRootFolders();
			
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
			if (!Handle.IsClosed)
				Handle.Close();
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
			IntPtr ptr = Track.GetTrackListing(Handle, callback, IntPtr.Zero);

			List<Track> tracks = new List<Track>();
			
			while (ptr != IntPtr.Zero) {
				TrackStruct track = (TrackStruct)Marshal.PtrToStructure(ptr, typeof(TrackStruct));
				Track.DestroyTrack (ptr);
				tracks.Add (new Track (track, this));
				ptr = track.next;
			}
			
			return tracks;
		}

        public List<Playlist> GetPlaylists ()
        {
            List<Playlist> playlists = new List<Playlist> ();

			IntPtr ptr = Playlist.LIBMTP_Get_Playlist_List (Handle);
			while (ptr != IntPtr.Zero) {
				PlaylistStruct d = (PlaylistStruct)Marshal.PtrToStructure(ptr, typeof(PlaylistStruct));
				playlists.Add (new Playlist (this, d));
				ptr = d.next;
			}
			
            return playlists;
        }

        public List<Album> GetAlbums ()
        {
            List<Album> albums = new List<Album> ();

			IntPtr ptr = Album.LIBMTP_Get_Album_List (Handle);
			while (ptr != IntPtr.Zero) {
				AlbumStruct d = (AlbumStruct)Marshal.PtrToStructure(ptr, typeof(AlbumStruct));
				albums.Add (new Album (this, d));
				ptr = d.next;
			}
			
            return albums;
        }
		
		public List<DeviceStorage> GetStorage ()
		{
			List<DeviceStorage> storages = new List<DeviceStorage>();
			IntPtr ptr = device.storage;
			while (ptr != IntPtr.Zero) {
				DeviceStorage storage = (DeviceStorage)Marshal.PtrToStructure(ptr, typeof(DeviceStorage));
				storages.Add (storage);
				ptr = storage.Next;
			}
			return storages;
		}
		
		public void Remove (Track track)
		{
			DeleteObject(Handle, track.FileId);
		}
		
		public void UploadTrack (string path, Track track, Folder folder)
		{
			UploadTrack (path, track, folder, null);
		}
		
		public void UploadTrack (string path, Track track, Folder folder, ProgressFunction callback)
		{
			if (string.IsNullOrEmpty(path))
				throw new ArgumentNullException("path");
			if (track == null)
				throw new ArgumentNullException("track");

            folder = folder ?? MusicFolder;
            if (folder != null) {
                track.trackStruct.parent_id = folder.FolderId;
            }
			
			// We send the trackstruct by ref so that when the file_id gets filled in, our copy is updated
			Track.SendTrack (Handle, path, ref track.trackStruct, callback, IntPtr.Zero);
			// LibMtp.GetStorage (Handle, 0);
		}

        public FileType [] GetFileTypes ()
        {
            Int16 [] ints = GetFileTypes (Handle);
            FileType [] file_types = new FileType [ints.Length];
            for (int i = 0; i < ints.Length; i++) {
                file_types[i] = (FileType) ints[i];
            }

            return file_types;
        }
		
		public static List<MtpDevice> Detect ()
		{
			IntPtr ptr;
			GetConnectedDevices(out ptr);
			
			List<MtpDevice> devices = new List<MtpDevice>();
			while (ptr != IntPtr.Zero)
			{
				MtpDeviceStruct d = (MtpDeviceStruct)Marshal.PtrToStructure(ptr, typeof(MtpDeviceStruct));
				devices.Add(new MtpDevice(ptr, true, d));
				ptr = d.next;
			}
			
			return devices;
		}

		internal static void ClearErrorStack(MtpDeviceHandle handle)
		{
			LIBMTP_Clear_Errorstack (handle);
		}
		
		internal static void DeleteObject(MtpDeviceHandle handle, uint object_id)
		{
			if (LIBMTP_Delete_Object(handle, object_id) != 0)
			{
				LibMtpException.CheckErrorStack(handle);
				throw new LibMtpException(ErrorCode.General, "Could not delete the track");
			}
		}

		internal static void GetBatteryLevel (MtpDeviceHandle handle, out ushort maxLevel, out ushort currentLevel)
		{
			int result = LIBMTP_Get_Batterylevel (handle, out maxLevel, out currentLevel);
			if (result != 0)
				throw new LibMtpException (ErrorCode.General, "Could not retrieve battery stats");
		}

		internal static void GetConnectedDevices (out IntPtr list)
		{
			Error.CheckError (LIBMTP_Get_Connected_Devices (out list));
		}

		internal static IntPtr GetErrorStack (MtpDeviceHandle handle)
		{
			return LIBMTP_Get_Errorstack(handle);
		}

		internal static string GetDeviceversion(MtpDeviceHandle handle)
		{
			IntPtr ptr = LIBMTP_Get_Deviceversion(handle);
			if (ptr == IntPtr.Zero)
				return null;
			
            return StringFromIntPtr (ptr);
		}

		
		internal static string GetFriendlyName(MtpDeviceHandle handle)
		{
			IntPtr ptr = LIBMTP_Get_Friendlyname(handle);
			if (ptr == IntPtr.Zero)
				return null;
			
            return StringFromIntPtr (ptr);
		}

		internal static bool SetFriendlyName(MtpDeviceHandle handle, string name)
        {
            bool success = LIBMTP_Set_Friendlyname (handle, name) == 0;
            return success;
        }

		internal static string GetSerialnumber(MtpDeviceHandle handle)
		{
			IntPtr ptr = LIBMTP_Get_Serialnumber(handle);
			if (ptr == IntPtr.Zero)
				return null;

            return StringFromIntPtr (ptr);
        }
		
		internal static void GetStorage (MtpDeviceHandle handle, int sortMode)
		{
			LIBMTP_Get_Storage (handle, sortMode);
		}

        internal static Int16 [] GetFileTypes (MtpDeviceHandle handle)
        {
            IntPtr types = IntPtr.Zero;
            ushort count = 0;
            if (LIBMTP_Get_Supported_Filetypes (handle, ref types, ref count) == 0) {
                Int16 [] type_ary = new Int16 [count];
                Marshal.Copy (types, type_ary, 0, (int)count);
                Marshal.FreeHGlobal (types);
                return type_ary;
            }

            return new Int16[0];
        }
		
		internal static void ReleaseDevice (IntPtr handle)
		{
			LIBMTP_Release_Device(handle);
		}

        private static string StringFromIntPtr (IntPtr ptr)
        {
			int i = 0;
			while (Marshal.ReadByte (ptr, i) != (byte) 0) ++i;
			byte[] s_buf = new byte [i];
			Marshal.Copy (ptr, s_buf, 0, s_buf.Length);
			string s = System.Text.Encoding.UTF8.GetString (s_buf);
			Marshal.FreeCoTaskMem(ptr);
			return s;
        }

        // Device Management
		[DllImport("libmtp.dll")]
		private static extern void LIBMTP_Init ();
			
		// Clears out the error stack and frees any allocated memory.
		[DllImport("libmtp.dll")]
		private static extern void LIBMTP_Clear_Errorstack (MtpDeviceHandle handle);
		
		[DllImport("libmtp.dll")]
		internal static extern int LIBMTP_Delete_Object (MtpDeviceHandle handle, uint object_id); 	
			
		// Gets the first connected device:
		//[DllImport("libmtp.dll")]
		//private static extern IntPtr LIBMTP_Get_First_Device (); // LIBMTP_mtpdevice_t *
		
		// Gets the storage information
		[DllImportAttribute("libmtp.dll")]
		private static extern int LIBMTP_Get_Storage (MtpDeviceHandle handle, int sortMode);
		
		// Formats the supplied storage device attached to the device
		//[DllImportAttribute("libmtp.dll")]
		//private static extern int LIBMTP_Format_Storage (MtpDeviceHandle handle, ref DeviceStorage storage);
		
		// Counts the devices in the list
		//[DllImportAttribute("libmtp.dll")]
		//private static extern uint LIBMTP_Number_Devices_In_List (MtpDeviceHandle handle);
		
		[DllImportAttribute("libmtp.dll")]
		private static extern ErrorCode LIBMTP_Get_Connected_Devices (out IntPtr list); //LIBMTP_mtpdevice_t **
		
		// Deallocates the memory for the device
		[DllImportAttribute("libmtp.dll")]
		private static extern void LIBMTP_Release_Device (IntPtr device);

		//[DllImportAttribute("libmtp.dll")]
		//private static extern int LIBMTP_Reset_Device (MtpDeviceHandle handle);
		
		[DllImport("libmtp.dll")]
		private static extern int LIBMTP_Get_Batterylevel (MtpDeviceHandle handle, out ushort maxLevel, out ushort currentLevel);
		
		//[DllImportAttribute("libmtp.dll")]
		//private static extern IntPtr LIBMTP_Get_Modelname (MtpDeviceHandle handle); // char *
		
		[DllImportAttribute("libmtp.dll")]
		private static extern IntPtr LIBMTP_Get_Serialnumber (MtpDeviceHandle handle); // char *
		
		[DllImportAttribute("libmtp.dll")]
		private static extern IntPtr LIBMTP_Get_Deviceversion (MtpDeviceHandle handle); // char *
		
		[DllImportAttribute("libmtp.dll")]
		private static extern IntPtr LIBMTP_Get_Friendlyname (MtpDeviceHandle handle); // char *
		
		[DllImport("libmtp.dll")]
		private static extern int LIBMTP_Set_Friendlyname (MtpDeviceHandle handle, string name);
		
		[DllImportAttribute("libmtp.dll")]
		private static extern IntPtr LIBMTP_Get_Errorstack (MtpDeviceHandle handle); // LIBMTP_error_t *
		
		[DllImportAttribute("libmtp.dll")]
		private static extern int LIBMTP_Get_Supported_Filetypes (MtpDeviceHandle handle, ref IntPtr types, ref ushort count); // uint16_t **const
		
		
		// void LIBMTP_Release_Device_List (LIBMTP_mtpdevice_t *)
				

		// int LIBMTP_Detect_Descriptor (uint16_t *, uint16_t *); 
		/*
				void 	LIBMTP_Dump_Device_Info (LIBMTP_mtpdevice_t *)
				
				char * 	LIBMTP_Get_Syncpartner (LIBMTP_mtpdevice_t *)
				int 	LIBMTP_Set_Syncpartner (LIBMTP_mtpdevice_t *, char const *const)
				int 	LIBMTP_Get_Secure_Time (LIBMTP_mtpdevice_t *, char **const)
				int 	LIBMTP_Get_Device_Certificate (LIBMTP_mtpdevice_t *, char **const)
		 */
		
        public static string GetMimeTypeFor (FileType type)
        {
            switch (type) {
                case FileType.MP3:      return "audio/mpeg";
                case FileType.OGG:      return "audio/ogg";
                case FileType.WMA:      return "audio/x-ms-wma";
                case FileType.WMV:      return "video/x-ms-wmv";
                case FileType.ASF:      return "video/x-ms-asf";
                case FileType.AAC:      return "audio/x-aac";
                case FileType.MP4:      return "video/mp4";
                case FileType.AVI:      return "video/avi";
                case FileType.WAV:      return "audio/x-wav";
                case FileType.MPEG:     return "video/mpeg";
                case FileType.FLAC:     return "audio/flac";
                case FileType.QT:       return "video/quicktime";
                case FileType.M4A:      return "audio/mp4";
            }
            return null;
        }
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct DeviceEntry
	{
		[MarshalAs(UnmanagedType.LPStr)] public string vendor;
		public short vendor_id;
		[MarshalAs(UnmanagedType.LPStr)] public string product;
		public short product_id;
		public int device_flags;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct DeviceStorage
	{
		public uint Id;
		public ushort StorageType;
		public ushort FileSystemType;
		public ushort AccessCapability;
		public ulong MaxCapacity;
		public ulong FreeSpaceInBytes;
		public ulong FreeSpaceInObjects;
		[MarshalAs(UnmanagedType.LPStr)] public string StorageDescription;
		[MarshalAs(UnmanagedType.LPStr)] public string VolumeIdentifier;
		public IntPtr Next; // LIBMTP_devicestorage_t*
		public IntPtr Prev; // LIBMTP_devicestorage_t*
	}

	internal class MtpDeviceHandle : SafeHandle
	{
		private MtpDeviceHandle()
			: base(IntPtr.Zero, true)
		{
			
		}
		
		internal MtpDeviceHandle(IntPtr ptr, bool ownsHandle)
			: base (IntPtr.Zero, ownsHandle)
		{
			SetHandle (ptr);
		}
		
		public override bool IsInvalid
		{
			get { return handle == IntPtr.Zero; }
		}

		protected override bool ReleaseHandle ()
		{
			MtpDevice.ReleaseDevice(handle);
			return true;
		}
	}
	
	internal struct MtpDeviceStruct
	{
		public byte object_bitsize;
		public IntPtr parameters;  // void*
		public IntPtr usbinfo;     // void*
		public IntPtr storage;     // LIBMTP_devicestorage_t*
		public IntPtr errorstack;  // LIBMTP_error_t*
		public byte maximum_battery_level;
		public uint default_music_folder;
		public uint default_playlist_folder;
		public uint default_picture_folder;
		public uint default_video_folder;
		public uint default_organizer_folder;
		public uint default_zencast_folder;
		public uint default_album_folder;
		public uint default_text_folder;
		public IntPtr cd; // void*
		public IntPtr next; // LIBMTP_mtpdevice_t*
	}
}
