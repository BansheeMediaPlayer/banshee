/***************************************************************************
 *  RemotePlayer.cs
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
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
using DBus;

using Banshee.Base;
using Banshee.MediaEngine;

namespace Banshee
{   
    [Interface("org.gnome.Banshee.Core")]
    public class RemotePlayer
    {
        private Gtk.Window mainWindow;
        private PlayerUI PlayerUI;
        
        public static RemotePlayer FindInstance()
        {
            Connection connection = Bus.GetSessionBus();
            Service service = Service.Get(connection, "org.gnome.Banshee");        
            return service.GetObject(typeof(RemotePlayer), "/org/gnome/Banshee/Player") as RemotePlayer;
        }
        
        public RemotePlayer(Gtk.Window mainWindow, PlayerUI ui)
        {
            this.mainWindow = mainWindow;
            this.PlayerUI = ui;
        }
        
        [Method]
        public virtual void PresentWindow()
        {
            if(mainWindow != null) {
                mainWindow.Present();
            }
        }
        
        [Method]
        public virtual void ShowWindow()
        {
            if(mainWindow != null) {
                mainWindow.Show();
            }
        }
        
        [Method]
        public virtual void HideWindow()
        {
            if(mainWindow != null) {
                mainWindow.Hide();
            }
        }
        
        [Method]
        public virtual void TogglePlaying()
        {
            if(PlayerUI != null) {
                PlayerUI.PlayPause();
            }
        }
        
        [Method]
        public virtual void Play()
        {
            if(PlayerUI == null) {
                return;
            }
            
            if(PlayerUI != null && !HaveTrack) {
                PlayerUI.PlayPause();
            }
            
            if(PlayerEngineCore.CurrentState != PlayerEngineState.Playing) {
                PlayerUI.PlayPause();
            }
        }
        
        [Method]
        public virtual void Pause()
        {
            if(HaveTrack && PlayerEngineCore.CurrentState == PlayerEngineState.Playing) {
                PlayerUI.PlayPause();
            }
        }
        
        [Method]
        public virtual void Next()
        {
            if(PlayerUI != null) {
                PlayerUI.Next();
            }
        }
        
        [Method]
        public virtual void Previous()
        {
            if(PlayerUI != null) {
                PlayerUI.Previous();
            }
        }

        [Method]
        public virtual void SelectAudioCd(string device)
        {
            if(PlayerUI != null) {
                PlayerUI.SelectAudioCd(device);
            }
        }
        
        [Method]
        public virtual void SelectDap(string device)
        {
            if(PlayerUI != null) {
                PlayerUI.SelectDap(device);
            }
        }
        
        [Method]
        public virtual void EnqueueFiles(string [] files)
        {
            Banshee.Sources.LocalQueueSource.Instance.Enqueue(files, true);
            Banshee.Sources.SourceManager.SetActiveSource(Banshee.Sources.LocalQueueSource.Instance);
        }
        
        private bool HaveTrack {
            get {
                return PlayerUI != null && PlayerEngineCore.CurrentTrack != null;
            }
        }
        
        [Method]
        public virtual string GetPlayingArtist()
        {
            return HaveTrack ? PlayerEngineCore.CurrentTrack.Artist : String.Empty;
        }
        
        [Method]
        public virtual string GetPlayingAlbum()
        {
            return HaveTrack ? PlayerEngineCore.CurrentTrack.Album : String.Empty;
        }
        
        [Method]
        public virtual string GetPlayingTitle()
        {
            return HaveTrack ? PlayerEngineCore.CurrentTrack.Title : String.Empty;
        }
        
        [Method]
        public virtual string GetPlayingGenre()
        {
            return HaveTrack ? PlayerEngineCore.CurrentTrack.Genre : String.Empty;
        }
        
        [Method]
        public virtual string GetPlayingUri()
        {
            return HaveTrack ? PlayerEngineCore.CurrentTrack.Uri.AbsoluteUri : String.Empty;
        }

        [Method]
        public virtual string GetPlayingCoverUri()
        {
            string cover = HaveTrack ? PlayerEngineCore.CurrentTrack.CoverArtFileName : String.Empty;
            return cover == null ? String.Empty : cover;
        }
        
        [Method]
        public virtual int GetPlayingDuration()
        {
            return HaveTrack ? (int)PlayerEngineCore.Length : -1;
        }
        
        [Method]
        public virtual int GetPlayingPosition()
        {
            return HaveTrack ? (int)PlayerEngineCore.Position : -1;
        }
        
        [Method]
        public virtual int GetPlayingStatus()
        {
            return PlayerEngineCore.CurrentState == PlayerEngineState.Playing ? 1 :
                (PlayerEngineCore.CurrentState != PlayerEngineState.Idle ? 0 : -1);
        }
        
        [Method]
        public virtual void SetVolume(int volume)
        {
            PlayerEngineCore.Volume = (ushort)volume;
        }

        [Method]
        public virtual void IncreaseVolume()
        {
            if(PlayerUI != null) {
                PlayerEngineCore.Volume += (ushort)PlayerUI.VolumeDelta;
            }
        }
        
        [Method]
        public virtual void DecreaseVolume()
        {
            if(PlayerUI != null) {
                PlayerEngineCore.Volume -= (ushort)PlayerUI.VolumeDelta;
            }
        }
        
        [Method]
        public virtual void SetPlayingPosition(int position)
        {
            PlayerEngineCore.Position = (uint)position;
        }
        
        [Method]
        public virtual void SkipForward()
        {
            PlayerEngineCore.Position += PlayerUI.SkipDelta;
        }
        
        [Method]
        public virtual void SkipBackward()
        {
            PlayerEngineCore.Position -= PlayerUI.SkipDelta;
        }
    }
}

