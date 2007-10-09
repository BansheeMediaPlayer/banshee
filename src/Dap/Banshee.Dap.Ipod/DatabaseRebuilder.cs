/***************************************************************************
 *  DatabaseRebuilder.cs
 *
 *  Copyright (C) 2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@abock.org>
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
using Mono.Unix;
using IPod;

using Banshee.Base;
using Banshee.Widgets;

namespace Banshee.Dap.Ipod
{
    public class DatabaseRebuilder
    {
        private IpodDap dap;
        private ActiveUserEvent user_event;
        private Queue song_queue = new Queue();
        private int discovery_count;

        public event EventHandler Finished;

        public DatabaseRebuilder(IpodDap dap)
        {
            this.dap = dap;
            
            user_event = new ActiveUserEvent(Catalog.GetString("Rebuilding Database"));
            user_event.Header = Catalog.GetString("Rebuilding Database");
            user_event.Message = Catalog.GetString("Scanning iPod...");
            user_event.Icon = dap.GetIcon(22);
            user_event.CanCancel = true;
            
            ThreadAssist.Spawn(RebuildDatabase);
        }
        
        private void RebuildDatabase()
        {
            DirectoryInfo music_dir = new DirectoryInfo(
                dap.Device.VolumeInfo.MountPoint +
                Path.DirectorySeparatorChar +
                "iPod_Control" +
                Path.DirectorySeparatorChar + 
                "Music");
                
            foreach(DirectoryInfo directory in music_dir.GetDirectories()) {
                ScanMusicDirectory(directory);
            }
            
            ProcessTrackQueue();
        }
        
        private void ScanMusicDirectory(DirectoryInfo directory)
        {
            foreach(FileInfo file in directory.GetFiles()) {
                song_queue.Enqueue(file);
            }
        }
        
        private void ProcessTrackQueue()
        {
            discovery_count = song_queue.Count;
            
            user_event.Message = Catalog.GetString("Processing Tracks...");
            
            while(song_queue.Count > 0) {
                user_event.Progress = (double)(discovery_count - song_queue.Count) 
                    / (double)discovery_count;
                
                try {
                    ProcessTrack(song_queue.Dequeue() as FileInfo);
                } catch {
                }
                
                if(user_event.IsCancelRequested) {
                    break;
                }
            }
            
            if(!user_event.IsCancelRequested) {
                SaveDatabase();
            }
            
            user_event.Dispose();
            user_event = null;
            
            OnFinished();
        }
        
        private void ProcessTrack(FileInfo file)
        {
            TagLib.File af = Banshee.IO.IOProxy.OpenFile(file.FullName);
            Track song = dap.Device.TrackDatabase.CreateTrack();

            song.FileName = file.FullName;
            song.Album = af.Tag.Album;
            song.Artist = af.Tag.FirstAlbumArtist;
            song.Title = af.Tag.Title;
            song.Genre = af.Tag.Genres[0];
            song.TrackNumber = (int)af.Tag.Track;
            song.TotalTracks = (int)af.Tag.TrackCount;
            song.Duration = af.Properties.Duration;
            song.Year = (int)af.Tag.Year;
            song.BitRate = af.Properties.AudioBitrate / 1024;
            song.SampleRate = (ushort)af.Properties.AudioSampleRate;
        }
        
        private void SaveDatabase()
        {
            user_event.CanCancel = false;
            user_event.Message = Catalog.GetString("Saving new database...");
            user_event.Progress = 0.0;
            
            try {
                dap.Device.TrackDatabase.Save();
            } catch(Exception e) {
                LogCore.Instance.PushError(
                    Catalog.GetString("Error rebuilding iPod database"),
                    e.Message);
            }
        }
        
        protected virtual void OnFinished()
        {
            ThreadAssist.ProxyToMain(delegate {
                EventHandler handler = Finished;
                if(handler != null) {
                    handler(this, new EventArgs());
                }
            });
        }
    }
}
