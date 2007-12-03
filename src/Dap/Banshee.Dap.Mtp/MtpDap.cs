/***************************************************************************
 *  MtpDap.cs
 *
 *  Copyright (C) 2006-2007 Novell and Patrick van Staveren
 *  Authors:
 *  Patrick van Staveren (trick@vanstaveren.us)
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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Hal;
using Gphoto2;
using Banshee.Dap;
using Banshee.Base;
using Banshee.Widgets;
using Banshee.Sources;
using Mono;
using Mono.Unix;
using Gtk;

public static class PluginModuleEntry
{
	public static Type [] GetTypes()
	{
		return new Type [] { typeof(Banshee.Dap.Mtp.MtpDap) };
	}
}

namespace Banshee.Dap.Mtp
{
	[DapProperties(DapType = DapType.NonGeneric)]
	[SupportedCodec(CodecType.Mp3)]
	[SupportedCodec(CodecType.Wma)]
//	[SupportedCodec(CodecType.Wav)] // for some reason, files get sent to the device as Wav's when this is enabled.  wtf?
	
	public sealed class MtpDap : DapDevice, IImportable//, IPlaylistCapable
	{
		private Camera camera;
		private List<MtpDapTrackInfo> metadataChangedQueue;
		private Queue<MtpDapTrackInfo> removeQueue;
		private List<MtpDapTrackInfo> allTracks;

		
		internal Camera Camera
		{
			get { return camera; }
		}

		public override bool CanSynchronize
		{
			get { return true; }
		}
		
		public MtpDap()
		{
			allTracks = new List<MtpDapTrackInfo>();
			metadataChangedQueue = new List<MtpDapTrackInfo>();
			removeQueue = new Queue<MtpDapTrackInfo>();			
		}
		
		
		public override void Eject ()
		{
			if (camera.Connected)
				camera.Disconnect();
		}
		
		public override InitializeResult Initialize(Hal.Device halDevice)
		{
			HalDevice = halDevice;
			if(!halDevice.PropertyExists("usb.vendor_id") ||
			   !halDevice.PropertyExists("usb.product_id") ||
			   !halDevice.PropertyExists("portable_audio_player.type") ||
			   !halDevice.PropertyExists("camera.libgphoto2.support")) {
				return InitializeResult.Invalid;
			}
			
			short product_id = (short) halDevice.GetPropertyInteger("usb.product_id");
			short vendor_id  = (short) halDevice.GetPropertyInteger("usb.vendor_id");
			string type = halDevice.GetPropertyString("portable_audio_player.type");
			string name = halDevice.GetPropertyString("camera.libgphoto2.name");
			int deviceNumber = halDevice.GetPropertyInteger("usb.linux.device_number");
			int busNumber = halDevice.GetPropertyInteger("usb.bus_number");

			if (type != "mtp")
			{			
				LogCore.Instance.PushDebug("MTP: Unsupported Device",
				                           "The device's portable_audio_player.type IS NOT mtp");
				return InitializeResult.Invalid;
			}
			
			if (!halDevice.GetPropertyBoolean("camera.libgphoto2.support"))
			{
				LogCore.Instance.PushDebug("MTP: Unsupported Device",
				                           "The device has camera.libgphoto2.support = false");
				return InitializeResult.Invalid;
			}
			
			LogCore.Instance.PushDebug("MTP: Starting initialization",
			                           string.Format("Name: {0}, Device: {1}, Bus:{2}",
			                                         name, deviceNumber, busNumber));
			
			List<Camera> cameras = Gphoto2.Camera.Detect();
			camera = cameras.Find(delegate (Camera c) { return c.UsbBusNumber == busNumber && c.UsbDeviceNumber == deviceNumber; });
				
			if(camera == null)
			{
				LogCore.Instance.PushDebug("Connection failed", string.Format("MTP: found {0} devices, but not the one we're looking for.", cameras.Count));
				foreach (Camera cam in cameras)
					LogCore.Instance.PushDebug("Found", string.Format("name={2}, vendor={0}, prod={1}", cam.Vendor, cam.Product, cam.Name));
				
				return Banshee.Dap.InitializeResult.Invalid;
			}
			
			LogCore.Instance.PushDebug("MTP: device found", String.Format("vendor={0}, prod={1}", vendor_id, product_id));

			base.Initialize(halDevice);
			
			InstallProperty("Model", camera.Name);
			InstallProperty("Vendor", halDevice["usb.vendor"]);
			InstallProperty("Serial Number", halDevice["usb.serial"]);
			ThreadAssist.Spawn(InitializeBackgroundThread);

			CanCancelSave = false;
			return InitializeResult.Valid;
		}

		public void InitializeBackgroundThread()
		{
			ActiveUserEvent userEvent = new ActiveUserEvent("MTP Initialization");
			try
			{
				userEvent.CanCancel = true;
				userEvent.Header = Catalog.GetString(string.Format("{0}: Found", camera.Name));
				userEvent.Message = Catalog.GetString("Connecting...");
				try
				{
					camera.Connect();
					ReloadDatabase(userEvent);
				}
				catch (Exception e)
				{
					LogCore.Instance.PushDebug("Connection failed", e.ToString());
					LogCore.Instance.PushWarning(String.Format("Initialization of your {0} failed. Try plugging the device out and back in again. If that fails, run banshee from a terminal and file a bug report on bugzilla.gnome.org with the terminal output attached", camera.Name), "");
					Dispose();
					return;
				}
			}
			finally
			{
				GLib.Timeout.Add(4000, delegate {
					userEvent.Dispose();
					return false;
				});
			}
		}

		public override void Dispose() {
			camera.Dispose();
			base.Dispose();
		}
		
		private void LoadFiles(ActiveUserEvent userEvent, int total, FileSystem fs, string directory, List<Gphoto2.File> files)
		{
			if(userEvent.IsCancelRequested)
				return;
			
			files.AddRange(fs.GetFiles(directory));
			userEvent.Progress = (double)files.Count / total;

			foreach(string folder in fs.GetFolders(directory))
				LoadFiles(userEvent, total, fs, FileSystem.CombinePath(directory, folder), files);
		}
		
		private void OnMetadataChanged(object sender, EventArgs e)
		{
			MtpDapTrackInfo info = (MtpDapTrackInfo)sender;
			if (!metadataChangedQueue.Contains(info))
				metadataChangedQueue.Add(info);
		}
		
		private void ReloadDatabase(bool reconnect)
		{
			ActiveUserEvent userEvent = new ActiveUserEvent("MTP Initialization");
			try
			{
				if(reconnect)
				{
					userEvent.Message = "Syncing device library...";
					camera.Reconnect();
				}
				ReloadDatabase(userEvent);
			}
			finally
			{
				GLib.Timeout.Add(4000, delegate {
					userEvent.Dispose();
					return false;
				});
			}
		}
		
		// FIXME: Try/catch this entire block?
		private void ReloadDatabase(ActiveUserEvent userEvent)
		{
			double startTime = Environment.TickCount;
			
			// Clear the list of tracks that banshee keeps
			ClearTracks(false);
			
			userEvent.Message = Catalog.GetString("Counting tracks...");	
			
			int fileCount = 0;
			foreach (FileSystem fs in camera.FileSystems)
				fileCount += fs.Count(null, true);
			
			userEvent.Message = string.Format(Catalog.GetString("Loading database... {0}"), fileCount);
			
			List<Gphoto2.File> files = new List<Gphoto2.File>(fileCount);
			foreach(FileSystem fs in camera.FileSystems)
				LoadFiles(userEvent, fileCount, fs, "", files);
			
			if(userEvent.IsCancelRequested)
			{
				userEvent.Message = Catalog.GetString("Cancelled...");
				return;
			}
			
			allTracks = new List<MtpDapTrackInfo>(files.Count + 50);
			foreach (Gphoto2.File f in files)
			{
				if(!(f is MusicFile))
					continue;
					
				MtpDapTrackInfo track = new MtpDapTrackInfo(camera, (MusicFile)f);
				track.Changed += new EventHandler(OnMetadataChanged);
				AddTrack(track);
				allTracks.Add(track);
			}
			
			startTime = (Environment.TickCount - startTime) / 1000.0;
			userEvent.Message = string.Format(Catalog.GetString("Loaded {0} of {1} files in {2:0.00}sec"), this.tracks.Count, fileCount, startTime);
			userEvent.Header = Catalog.GetString(string.Format("{0}: Ready", camera.Name));
		}
		
		protected override void OnTrackRemoved(TrackInfo track)
		{
			base.OnTrackRemoved(track);
			
			MtpDapTrackInfo t = track as MtpDapTrackInfo;
			if (IsReadOnly || t == null || !t.OnCamera(camera))
				return;

			// This means we have write access and the file is on the camera.
			removeQueue.Enqueue((MtpDapTrackInfo)track);
		}
		
		public override void AddTrack(TrackInfo track)
		{
			//FIXME: DO i need to check if i already have the track in the list?
			//if ((mtpTrack != null && mtpTrack.OnCamera(camera)))
			//	return;
			
			base.AddTrack(track);
		}
		
		/*PL*
		private void AddDevicePlaylist(MtpDapPlaylistSource playlist) {
			this.Source.AddChildSource(playlist);
			playlists.Add(playlist);
		}

		public DapPlaylistSource AddPlaylist(Source source) {
			ArrayList playlist_tracks = new ArrayList();

			foreach (TrackInfo track in source.Tracks) {
				if (!TrackExistsInList(track, Tracks)) {
					AddTrack(track);
					playlist_tracks.Add(track);
				} else {
					playlist_tracks.Add(find_existing_track(track) as TrackInfo);
				}
			}

			MtpDapPlaylistSource playlist = new MtpDapPlaylistSource(this, source.Name, playlist_tracks);
			playlists.Add(playlist);
			dev.Playlists.Add(playlist.GetDevicePlaylist());
			
			this.Source.AddChildSource(playlist); // fixme: this should happen automatically in DapDevice or DapSource or something.
			return playlist;
		}
		*/

		private MusicFile ToMusicFile(TrackInfo track, string path, string filename, long length)
		{
			// FIXME: Set the length properly
			// Fixme: update the reference i'm holding to the original music file?
			// Why am i holding it anyway?
			MusicFile f =  new MusicFile(path, filename);
			f.Album = track.Album;
			f.Artist = track.Artist;
			f.Duration = (int)track.Duration.TotalMilliseconds;
			f.Genre = track.Genre;
			f.Rating = (int)track.Rating;
			f.Title = track.Title;
			f.Track = (int)track.TrackNumber;
			f.UseCount = (int)track.TrackCount;
			f.Year = track.Year > 0 ? track.Year : 0;
			return f;
		}
		
		private void RemoveTracks()
		{
			int count = removeQueue.Count;
			while(removeQueue.Count > 0)
			{
				MtpDapTrackInfo track = removeQueue.Dequeue();
				string message = string.Format("Removing {0}/{1}: {2} - {3}", count - removeQueue.Count,
				                               count, track.Artist, track.Title);
				UpdateSaveProgress("Synchronising...", message, ((double)count - removeQueue.Count) / count);
				
				// Quick check to see if the track is on this camera - possibly needed in
				// case we have multiple MTP devices connected simultaenously
				if(!track.OnCamera(camera))
					continue;
				
				foreach (FileSystem fs in camera.FileSystems)
				{
					if (fs.Contains(track.OriginalFile.Path, track.OriginalFile.Filename))
					{
						fs.DeleteFile(track.OriginalFile);
						allTracks.Remove(track);
						
						if (metadataChangedQueue.Contains(track))
							metadataChangedQueue.Remove(track);
						
						if (fs.Count(track.OriginalFile.Path) == 0)
							fs.DeleteAll(track.OriginalFile.Path, true);
						
						track.Changed -= new EventHandler(OnMetadataChanged);
					}
				}
			}
		}
		
		private void UploadTracks()
		{
			string dapDirectory = "";
			string dapFilename = "";

			// For all the tracks that are listed to upload, find only the ones
			// which exist and can be read.
			// FIXME: I can upload 'MtpDapTrackInfo' types. Just make sure they dont
			// exist on *this* device already
			List<TrackInfo> tracks = new List<TrackInfo>(Tracks);
			tracks = tracks.FindAll(delegate (TrackInfo t) {
				if (t == null || t is MtpDapTrackInfo || t.Uri == null)
					return false;
				return System.IO.File.Exists(t.Uri.LocalPath);
			});
			
			for (int i = 0; i < tracks.Count; i++)
			{
				string path = Path.GetDirectoryName(tracks[i].Uri.LocalPath);
				string name = Path.GetFileName(tracks[i].Uri.LocalPath);
				MusicFile f = ToMusicFile(tracks[i], path, name, 0);
				
				CreateDapDirectory(f, out dapDirectory, out dapFilename);
				if (!camera.Abilities.CanCreateDirectory)
					dapDirectory = "";
				
				// When we upload a file to the camera, we get returned a new file
				// representing the file in it's location on the camera. We should
				// store this instead of the old representation we had
				FileSystem fs = camera.FileSystems.Find(delegate (FileSystem system) { return system.CanUpload(f); });
				
				if (fs == null)
				{
					LogCore.Instance.PushError("Device full", "There was not enough free space on your device to upload more files");
					return;
				}
				
				string message = string.Format("Adding {0}/{1}: {2} - {3}",
				                               i + 1, tracks.Count, f.Artist, f.Title);
				UpdateSaveProgress("Synchronising...", message, (double)(i + 1) / tracks.Count);
				
				// When a file is uploaded to the device, we are given a reference to it
				// This reference is the one we add to 'allTracks'
				f = (MusicFile)fs.Upload(f, dapDirectory, dapFilename);
				MtpDapTrackInfo newTrackInfo = new MtpDapTrackInfo(camera, f);
				allTracks.Add(newTrackInfo);
				AddTrack(newTrackInfo);
				newTrackInfo.Changed += new EventHandler(OnMetadataChanged);
			}
		}
		
		private void CreateDapDirectory(MusicFile track, out string dapDirectory, out string dapFilename)
		{
			char sep = Gphoto2.Camera.DirectorySeperator;
			StringBuilder sb = new StringBuilder(32);
			sb.Append(camera.MusicFolder);
			sb.Append(sep);
			
			if(!string.IsNullOrEmpty(track.Artist))
			{
				sb.Append(track.Artist);
				sb.Append(sep);
			}
			
			sb.Append('(');
			sb.Append(track.Year);
			sb.Append(") ");
			
			if(!string.IsNullOrEmpty(track.Album))
			{
				sb.Append(track.Album);
				sb.Append(sep);
			}
			
			int count=0;
			string filename = Path.GetFileNameWithoutExtension(track.Filename);
			string ext = Path.GetExtension(track.Filename);
			string fulldirectory = sb.ToString();
			string fullname = filename + ext;
			
			while (camera.FileSystems.Find (delegate (FileSystem f) { return f.Contains(fulldirectory, fullname); }) != null)
				fullname = filename + (++count).ToString() + ext;
			
			dapDirectory = fulldirectory;
			dapFilename = fullname;
		}
		
		private void UpdateMetadata()
		{
			try
			{
				for (int i = 0; i < metadataChangedQueue.Count; i++)
				{
					MtpDapTrackInfo info = metadataChangedQueue[i];
					MusicFile file = (MusicFile) info.OriginalFile;
					file.Album = info.Album;
					file.Artist = info.Artist;
					//file.DateAdded = info.DateAdded;
					file.Duration = (int) info.Duration.TotalMilliseconds;
					file.Genre = info.Genre;
					//file.LastPlayed = info.LastPlayed;
					file.Rating = (int) info.Rating;
					file.Title = info.Title;
					file.Track = (int) info.TrackNumber;
					file.UseCount = (int) info.PlayCount;
				
					if (file.IsDirty)
						file.Update();
				}
			}
			finally
			{
				metadataChangedQueue.Clear();
			}
		}
		
		public override void Synchronize()
		{
			// 1. remove everything in the remove queue if it's on the device
			// 2. Add everything in the tracklist that isn't on the device
			// 3. Sync playlists?
			try
			{
				RemoveTracks();
				UpdateMetadata();
				UploadTracks();
			}
			catch (Exception e)
			{
				LogCore.Instance.PushWarning("There was an error while synchronizing the current track.  Please file a bug report.", e.ToString());
			}
			finally
			{
				ClearTracks(false);

				for (int i = 0; i < allTracks.Count; i++)
					AddTrack(allTracks[i]);

				FinishSave();
			}
		}

		public void Import(IEnumerable<TrackInfo> tracks, PlaylistSource playlist) 
		{
			LogCore.Instance.PushDebug("MTP: importing tracks", "");
			if (playlist != null)
				LogCore.Instance.PushDebug("Playlist importing not supported",
				                           "Banshee does not support importing playlists from MTP devices yet...");
			
			QueuedOperationManager importer = new QueuedOperationManager ();
			
			importer = new QueuedOperationManager();
			importer.HandleActveUserEvent = false;
			importer.UserEvent.Icon = GetIcon(22);
			importer.UserEvent.Header = String.Format(Catalog.GetString("Importing from {0}"), Name);
			importer.UserEvent.Message = Catalog.GetString("Scanning...");
			importer.OperationRequested += OnImportOperationRequested;
			importer.Finished += delegate {
				importer.UserEvent.Message = "Import complete...";
				importer.UserEvent.Dispose();
			};
			
			// For each track in the list, check to make sure it is on this MTP
			// device and then add it to the import queue.
			foreach (TrackInfo track in tracks)
			{
				if (!(track is MtpDapTrackInfo))
					LogCore.Instance.PushDebug("Not MTP track", "Tried to import a non-mtp track");
				
				if(!((MtpDapTrackInfo)track).OnCamera(this.camera))
					LogCore.Instance.PushDebug("Track not on this device", "The track to import did not come from this camera");
				
				importer.Enqueue(track);
			}
		}
		
		private void OnImportOperationRequested(object o, QueuedOperationArgs args) 
		{
			if (!(args.Object is MtpDapTrackInfo))
			{
				LogCore.Instance.PushDebug("Import failure", string.Format("An attempt to import a '{0}' was detected. Can only import MtpDapTrackInfo objects", args.Object.GetType().Name));
				return;
			}
			
			QueuedOperationManager importer = (QueuedOperationManager)o;
			MtpDapTrackInfo track = (MtpDapTrackInfo)args.Object;

			if (importer.UserEvent.IsCancelRequested)
			{
				importer.UserEvent.Message = "Cancelled";
				return;
			}
			
			importer.UserEvent.Progress = importer.ProcessedCount / (double)importer.TotalCount;
			importer.UserEvent.Message = string.Format("{0}/{1}: {2} - {3}", importer.ProcessedCount, importer.TotalCount, track.Artist, track.Title);
			
			// This is the path where the file will be saved on-disk
			string destination = FileNamePattern.BuildFull(track, Path.GetExtension(track.OriginalFile.Filename));
			
			try
			{
				if (System.IO.File.Exists(destination))
				{
					FileInfo to_info = new FileInfo(destination);
					
					// FIXME: Probably already the same file. Is this ok?
					if (track.OriginalFile.Size == to_info.Length)
					{
						try
						{
							new LibraryTrackInfo(new SafeUri(destination, false), track);
						}
						catch
						{
							// was already in the library
						}
						LogCore.Instance.PushDebug("Import warning",
						                           string.Format("Track {0} - {1} - {2} already exists in the library",
						                                         track.Artist, track.Album, track.Title));
						return;
					}
				}
			}
			catch (Exception ex)
			{
				LogCore.Instance.PushDebug("Import Warning", "Could not check if the file already exists, skipping");
				LogCore.Instance.PushDebug("Exception", ex.ToString());
				return;
			}
			
			try
			{
				using (FileStream to_stream = new FileStream(destination, FileMode.Create, FileAccess.ReadWrite))
					track.OriginalFile.Download(to_stream);
				
				// Add the track to the library
				new LibraryTrackInfo(new SafeUri(destination, false), track);
			}
			
			catch(Exception e)
			{
				try
				{
					LogCore.Instance.PushDebug("Critical error", "Could not import tracks");
					LogCore.Instance.PushDebug("Exception", e.ToString());
					// FIXME: Is this ok?
					System.IO.File.Delete(destination);
				}
				catch
				{
					// Do nothing
				}
			}
		}
		
		public void Import(IEnumerable<TrackInfo> tracks) {
			Import(tracks, null);
		}

		public override Gdk.Pixbuf GetIcon(int size) {
			string prefix = "multimedia-player-";
			string id = "dell-pocket-dj";
			Gdk.Pixbuf icon = IconThemeUtils.LoadIcon(prefix + id, size);
			return icon == null? base.GetIcon(size) : icon;
		}
/*
		public DapPlaylistSource AddPlaylist (Source playlist)
		{
			IPodPlaylistSource ips = new IPodPlaylistSource(this, playlist.Name);       		
			
			LogCore.Instance.PushDebug("In IPodDap.AddPlaylist" , "");
			
			foreach(TrackInfo ti in playlist.Tracks) {
				LogCore.Instance.PushDebug("Adding track " + ti.ToString() , " to new playlist " + ips.Name);
				IpodDapTrackInfo idti = new IpodDapTrackInfo(ti, device.TrackDatabase);
				ips.AddTrack(idti);
				AddTrack(idti);                
			}
			
			return (DapPlaylistSource) ips;
		}
*/
		public override string Name
		{
			get
			{
				if (camera == null)
					return "";
				
				return camera.Name;
			}			
		}

		public override string GenericName
		{
			get { return Name; }
		}

		public override ulong StorageCapacity
		{
			get
			{
				if (!camera.Connected)
					return 0;
				
				ulong count = 0;
				foreach(FileSystem fs in camera.FileSystems)
					count += (ulong)fs.Capacity;
				
				return count;
			}
		}

		public override ulong StorageUsed
		{
			get
			{
				if (!camera.Connected)
					return 0;
				
				ulong count = 0;
				foreach(FileSystem fs in camera.FileSystems)
					count += (ulong)fs.UsedSpace;
				
				return count;
			}
		}
		
		public override bool IsReadOnly
		{
			get
			{
				if (!camera.Connected)
					return true;
				
				foreach(FileSystem fs in camera.FileSystems)
					if(fs.CanDelete || fs.CanWrite)
						return false;
				
				return true;
			}
		}

		public override bool IsPlaybackSupported {
			get {
				return false;
			}
		}
	}
}
