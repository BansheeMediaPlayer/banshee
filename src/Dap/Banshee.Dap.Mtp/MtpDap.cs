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
using Banshee.Dap;
using Banshee.Base;
using Banshee.Widgets;
using Banshee.Sources;
using Mono;
using Mono.Unix;
using Gtk;
using libmtpsharp;

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
		private MtpDevice camera;
		private List<MtpDapTrackInfo> metadataChangedQueue;
		private Queue<MtpDapTrackInfo> removeQueue;
		private List<MtpDapTrackInfo> allTracks;

		
		internal MtpDevice Camera
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
			camera.Dispose();
			base.Eject();
		}
		
		public override InitializeResult Initialize(Hal.Device halDevice)
		{
			HalDevice = halDevice;
			if(!halDevice.PropertyExists("usb.vendor_id") ||
			   !halDevice.PropertyExists("usb.product_id") ||
			   !halDevice.PropertyExists("portable_audio_player.type")) {
				return InitializeResult.Invalid;
			}
			
			short product_id = (short) halDevice.GetPropertyInteger("usb.product_id");
			short vendor_id  = (short) halDevice.GetPropertyInteger("usb.vendor_id");
			string type = halDevice.GetPropertyString("portable_audio_player.type");
			string name = halDevice.PropertyExists("usb_device.product") ? halDevice.GetPropertyString("usb_device.product") : "Mtp Device";
			int deviceNumber = halDevice.GetPropertyInteger("usb.linux.device_number");
			int busNumber = halDevice.GetPropertyInteger("usb.bus_number");

			if (type != "mtp")
			{			
				LogCore.Instance.PushDebug("MTP: Unsupported Device",
				                           "The device's portable_audio_player.type IS NOT mtp");
				return InitializeResult.Invalid;
			}
			
			LogCore.Instance.PushDebug("MTP: Starting initialization",
			                           string.Format("Name: {0}, Device: {1}, Bus:{2}",
			                                         name, deviceNumber, busNumber));
			
			List<MtpDevice> cameras = null;
			try
			{
				cameras = MtpDevice.Detect();
			}
			catch(TypeInitializationException ex)
			{
				string message = "Required libraries could not be found. Read http://www.banshee-project.org/Guide/DAPs/MTP for more information. ";
				message += (Environment.NewLine + Environment.NewLine);
				message += ex.InnerException.Message;
				message += " could not be found";
				LogCore.Instance.PushError("Initialisation error", message);
				return InitializeResult.Invalid;
			}
			catch (Exception ex)
			{
				ShowGeneralExceptionDialog(ex);
				return InitializeResult.Invalid;
			}
			//camera = cameras.Find(delegate (Camera c) { return c.UsbBusNumber == busNumber && c.UsbDeviceNumber == deviceNumber; });
				
			if(cameras == null || cameras.Count != 1)
			{
				//LogCore.Instance.PushDebug("Connection failed", string.Format("MTP: found {0} devices, but not the one we're looking for.", cameras.Count));
				//foreach (MtpDap cam in cameras)
				//	LogCore.Instance.PushDebug("Found", string.Format("name={2}, vendor={0}, prod={1}", cam.Vendor, cam.Product, cam.Name));
				
				LogCore.Instance.PushDebug("Connection failed", "We can only handle 1 connected mtp device at a time.");
				return Banshee.Dap.InitializeResult.Invalid;
			}
			camera = cameras[0];
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
					ReloadDatabase(userEvent);
				}
				catch (Exception e)
				{
					ShowGeneralExceptionDialog(e);
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
				
			
			userEvent.Message = string.Format(Catalog.GetString("Loading database..."));
			
			List<Track> files = camera.GetAllTracks(delegate (ulong current, ulong total, IntPtr data) {
				userEvent.Progress = (double)current / total;
				return userEvent.IsCancelRequested ? 1 : 0;
			});
			
			if(userEvent.IsCancelRequested)
			{
				userEvent.Message = Catalog.GetString("Cancelled...");
				return;
			}
			
			allTracks = new List<MtpDapTrackInfo>(files.Count + 50);
			foreach (Track f in files)
			{
				MtpDapTrackInfo track = new MtpDapTrackInfo(camera, f);
				track.Changed += new EventHandler(OnMetadataChanged);
				AddTrack(track);
				allTracks.Add(track);
			}
			
			startTime = (Environment.TickCount - startTime) / 1000.0;
			userEvent.Message = string.Format(Catalog.GetString("Loaded {0} files in {1:0.00}sec"), this.tracks.Count, startTime);
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

		private Track ToMusicFile(TrackInfo track, string name, ulong length)
		{
			// FIXME: Set the length properly
			// Fixme: update the reference i'm holding to the original music file?
			// Why am i holding it anyway?
			Track f =  new Track(name, length);
			f.Album = track.Album;
			f.Artist = track.Artist;
			f.Duration = (uint)track.Duration.TotalMilliseconds;
			f.Genre = track.Genre;
			f.Rating = (ushort)track.Rating;
			f.Title = track.Title;
			f.TrackNumber = (ushort)track.TrackNumber;
			f.UseCount = track.TrackCount;
#warning FIX THIS
			//f.Year = track.Year > 0 ? track.Year : 0;
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
				
				camera.Remove (track.OriginalFile);
				allTracks.Remove(track);
				
				if (metadataChangedQueue.Contains(track))
					metadataChangedQueue.Remove(track);
				
				track.Changed -= new EventHandler(OnMetadataChanged);
				
				// Optimisation - Delete the folder if it's empty
			}
		}
		
		private void UploadTracks()
		{
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
				FileInfo info = new FileInfo(tracks[i].Uri.AbsolutePath);
				Track f = ToMusicFile(tracks[i], info.Name, (ulong)info.Length);
				
				string message = string.Format("Adding {0}/{1}: {2} - {3}",
				                               i + 1, tracks.Count, f.Artist, f.Title);
				UpdateSaveProgress("Synchronising...", message, (double)(i + 1) / tracks.Count);
				camera.UploadTrack(tracks[i].Uri.AbsolutePath, f);
				
				// Create an MtpDapTrackInfo for the new file and add it to our lists
				MtpDapTrackInfo newTrackInfo = new MtpDapTrackInfo(camera, f);
				newTrackInfo.Changed += new EventHandler(OnMetadataChanged);
				
				allTracks.Add(newTrackInfo);
				AddTrack(newTrackInfo);
			}
		}
		
		private void UpdateMetadata()
		{
			try
			{
				for (int i = 0; i < metadataChangedQueue.Count; i++)
				{
					MtpDapTrackInfo info = metadataChangedQueue[i];
					Track file = info.OriginalFile;
					file.Album = info.Album;
					file.Artist = info.Artist;
					//file.DateAdded = info.DateAdded;
					file.Duration = (uint) info.Duration.TotalMilliseconds;
					file.Genre = info.Genre;
					//file.LastPlayed = info.LastPlayed;
					file.Rating = (ushort) info.Rating;
					file.Title = info.Title;
					file.TrackNumber = (ushort) info.TrackNumber;
					file.UseCount = info.PlayCount;
					file.Date = info.Year + "0101T0000.0";
					file.UpdateMetadata();
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
		
				
		private void ShowGeneralExceptionDialog(Exception ex)
		{
			string message = "There was an error initializing the device. Read http://www.banshee-project.org/Guide/DAPs/MTP for more information. ";
			message += (Environment.NewLine + Environment.NewLine);
			message += ex.ToString();
			LogCore.Instance.PushError("Initialisation error", message);
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
					if (track.OriginalFile.Filesize == (ulong)to_info.Length)
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
				// Copy the track from the device to the destination file
				track.OriginalFile.Download(destination);
				
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
				if (camera == null)
					return 0;
				
				ulong count = 0;
				foreach (DeviceStorage s in camera.GetStorage())
					count += s.MaxCapacity;
				return count;
			}
		}

		public override ulong StorageUsed
		{
			get
			{
				if (camera == null)
					return 0;
				ulong count = 0;
				foreach (DeviceStorage s in this.camera.GetStorage())
					count += s.MaxCapacity - s.FreeSpace;
				return count;
			}
		}
		
		public override bool IsReadOnly
		{
			get { return false; }
		}

		public override bool IsPlaybackSupported {
			get {
				return false;
			}
		}
	}
}
