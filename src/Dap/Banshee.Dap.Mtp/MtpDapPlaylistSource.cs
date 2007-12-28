// MtpPlaylistSource.cs created with MonoDevelop
// User: alan at 15:53 29/11/2007
//
// To change standard headers go to Edit->Preferences->Coding->Standard Headers
//
/*
using System;
using System.Collections.Generic;
using Banshee.Base;
using Banshee.Dap;
using Banshee.Sources;
using Gphoto2;

namespace Banshee.Dap.Mtp
{
	public class MtpPlaylistSource : DapPlaylistSource
	{
		MtpDap device;
		public MtpPlaylistSource(MtpDap device, string name)
			: base (device, name)
		{
			this.device = device;
		}
		
		public override bool AcceptsInput {
			get { return true; }
		}
		
		public override void Commit ()
		{/*
			MtpDapTrackInfo mtpTrack = null;
			List<MtpDapTrackInfo> uploads = new List<MtpDapTrackInfo>(tracks.Count);
			
			foreach (TrackInfo track in tracks)
			{
				mtpTrack = track as MtpDapTrackInfo;
				if (mtpTrack == null)
					mtpTrack = FindDapTrack(track);
				
				if (mtpTrack == null)
					continue;
				
				uploads.Add(mtpTrack);
			}
			
			Gphoto2.PlaylistFile playlist = new Gphoto2.PlaylistFile(Name);
			playlist.Files.AddRange(uploads);
			dap.Camera.FileSystems[0].Upload(playlist, dap.PlaylistDirectory);
		}
		
		private MtpDapTrackInfo FindDapTrack(TrackInfo info)
		{
			foreach (TrackInfo track in device.Tracks)
			{
				if(track.Artist == info.Artist
				   && track.Album == info.Album
				   && track.Title == info.Title)
					return (MtpDapTrackInfo)track;
			}
			
			return null;
		}
	}
}


        public override void AddTrack(TrackInfo track)
        {
            if (track == null) {
            	return;
            }
            
            IpodDapTrackInfo new_track = null;

            if(track is IpodDapTrackInfo) {
                new_track = track as IpodDapTrackInfo;
            } else {
                new_track = new IpodDapTrackInfo(track, device.Device.TrackDatabase);
            }
            
            base.AddTrack(new_track);            
        }
        
        public override void SourceDrop(Banshee.Sources.Source source)
        {
            if(source == this || source == null ) {
                return;
            }
            
            if (source.Tracks == null) {
            	return;
            }
            	
            LogCore.Instance.PushDebug("In IPodPlaylistSource.SourceDrop" , "");
            
            foreach(TrackInfo ti in source.Tracks) {
                LogCore.Instance.PushDebug("Adding track " + ti.ToString() , " to playlist " + source.Name);
                IpodDapTrackInfo idti = new IpodDapTrackInfo(ti, device.Device.TrackDatabase);
                AddTrack(idti);
            }
        }
        
        public override bool AcceptsInput {
            get { return true; }
        }
        
        public override bool Unmap()
        {
            LogCore.Instance.PushDebug("In IPodPlaylistSource.UnMap" , "");
            
            if(Count > 0 && !ConfirmUnmap(this)) {
                return false;
            }
                        
            foreach (IpodDapTrackInfo idti in Tracks) {
                LogCore.Instance.PushDebug("Trying to remove track from ipod source" , "Track: " + idti.ToString());
                device.RemoveTrackIfNotInPlaylists(idti, this);
            }
            tracks.Clear();
            
            //SourceManager.RemoveSource(this);
            device.Source.RemoveChildSource(this);
            
            IPod.Playlist p = device.Device.TrackDatabase.LookupPlaylist(Name);
            if (p != null) {
                LogCore.Instance.PushDebug("Removing playlist from ipod." , "");                    
                device.Device.TrackDatabase.RemovePlaylist(p);
               
                IPod.Playlist tempPlaylist = device.Device.TrackDatabase.LookupPlaylist(Name);
                if (tempPlaylist == null) {
                    LogCore.Instance.PushDebug("Removing playlist from ipod succeeded." , "");
                } else {
                    LogCore.Instance.PushDebug("Removing playlist from ipod failed." , "");
                }
            }
           
            return true;
        }
        
        /*
        // For use if we decide to prevent the user from removing the "podcast" playlist.
        protected void ShowPodcastPlaylistUnmapWarningMsg() 
        {
            HigMessageDialog dialog = new HigMessageDialog(null, Gtk.DialogFlags.Modal,
                Gtk.MessageType.Warning, Gtk.ButtonsType.Ok,
                Catalog.GetString("The Podcast Playlist Can Not Be Deleted"),
                Catalog.GetString("The Podcast Playlist is a special playlist, which can not be deleted.\n"));                                    
            try {
                dialog.Run();                
            } finally {
                dialog.Destroy();
            }
        }
        
        // For use if we decide to prevent the user from using the "podcast" name for a new playlist.
        protected void ShowPodcastPlaylistCreateWarningMsg() 
        {
            HigMessageDialog dialog = new HigMessageDialog(null, Gtk.DialogFlags.Modal,
                Gtk.MessageType.Warning, Gtk.ButtonsType.Ok,
                Catalog.GetString("The \"Podcast\" Name Can Not Be Used"),
                Catalog.GetString("The Podcast Playlist is a special playlist.  " +
                    "The \"Podcast\" playlist name can not be used as a name for a playlist.\n"));
            try {
                dialog.Run();                
            } finally {
                dialog.Destroy();
            }
        }
*/
