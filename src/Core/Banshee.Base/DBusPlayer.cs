/***************************************************************************
 *  DBusPlayer.cs
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
using NDesk.DBus;

using Banshee.Sources;
using Banshee.Burner;
using Banshee.MediaEngine;

namespace Banshee.Base
{
    [Interface("org.gnome.Banshee.Core")]
    public interface IDBusPlayer
    {
        void Shutdown();
        void PresentWindow();
        void ShowWindow();
        void HideWindow();
        void TogglePlaying();
        void Play();
        void Pause();
        void Next();
        void Previous();
        void SelectAudioCd(string device);
        void SelectDap(string device);
        void SelectBlankCd(string device);
        void EnqueueFiles(string [] files);
        string GetPlayingArtist();
        string GetPlayingAlbum();
        string GetPlayingTitle();
        string GetPlayingGenre();
        string GetPlayingUri();
        string GetPlayingCoverUri();
        int GetPlayingDuration();
        int GetPlayingPosition();
        int GetPlayingRating();
        int GetMaxRating();
        int SetPlayingRating(int rating);
        int GetPlayingStatus();
        void SetVolume(int volume);
        void IncreaseVolume();
        void DecreaseVolume();
        void SetPlayingPosition(int position);
        void SkipForward();
        void SkipBackward();
    }

    public class DBusPlayer : IDBusPlayer
    {
        public static IDBusPlayer FindInstance()
        {
            if(!Bus.Session.NameHasOwner(DBusRemote.BusName)) {
                return null;
            }

            return Bus.Session.GetObject<IDBusPlayer>(
                DBusRemote.BusName, new ObjectPath(DBusRemote.ObjectRoot + "/Player"));
        }
        
        public class UICommandArgs : EventArgs
        {
            private UICommand command;
            
            public UICommandArgs(UICommand command)
            {
                this.command = command;
            }
            
            public UICommand Command {
                get { return command; }
            }
        }

        public delegate void UICommandHandler(object o, UICommandArgs args);

        public enum UICommand
        {
            PresentWindow,
            ShowWindow,
            HideWindow
        }
        
        public event UICommandHandler UIAction;
        
        public DBusPlayer()
        {
        }
        
        private void OnUIAction(UICommand command)
        {
            if(!Available) {
                return;
            }
        
            UICommandHandler handler = UIAction;
            if(handler != null) {
                handler(this, new UICommandArgs(command));
            }
        }
        
        private bool Available {
            get { return Globals.StartupInitializer.IsRunFinished; }
        }
        
        private bool HaveTrack {
            get { return Available && PlayerEngineCore.CurrentTrack != null; }
        }
        
        public void Shutdown()
        {
            Globals.Shutdown();
        }
        
        public void PresentWindow()
        {
            OnUIAction(UICommand.PresentWindow);
        }
        
        public void ShowWindow()
        {
            OnUIAction(UICommand.ShowWindow);
        }
        
        public void HideWindow()
        {
            OnUIAction(UICommand.HideWindow);
        }
        
        public void TogglePlaying()
        {
            if(Available) {
                Globals.ActionManager["PlayPauseAction"].Activate();
            }
        }
        
        public void Play()
        {    
            if(HaveTrack && PlayerEngineCore.CurrentState != PlayerEngineState.Playing) {
                Globals.ActionManager["PlayPauseAction"].Activate();
            }
        }
        
        public void Pause()
        {
            if(HaveTrack && PlayerEngineCore.CurrentState == PlayerEngineState.Playing) {
                Globals.ActionManager["PlayPauseAction"].Activate();
            }
        }
        
        public void Next()
        {
            if(Available) {
                Globals.ActionManager["NextAction"].Activate();
            }
        }
        
        public void Previous()
        {
            if(Available) {
                Globals.ActionManager["PreviousAction"].Activate();
            }
        }

        public void SelectDeviceSource(string device, string hint)
        {
            if(!Available) {
                return;
            }
            
            Source single_source = null;
        
            foreach(Source source in SourceManager.Sources) {
                if(source is DapSource) {
                    if(single_source == null && hint == "dap") {
                        single_source = source;
                    }

                    if(((DapSource)source).Device.HalUdi == device) {
                        SourceManager.SetActiveSource(source);
                        return;
                    }
                } else if(source is BurnerSource) {
                    if(single_source == null && (hint == "blank-cd" || hint == "burn-cd")) {
                        Console.WriteLine ("Selecting single");
                        single_source = source;
                    }

                    BurnerSource blank_source = (BurnerSource)source;
                    if (blank_source.Session.Recorder != null && 
                        blank_source.Session.Recorder.Device == device) {
                        SourceManager.SetActiveSource(source);
                        return;
                    }
                } else if(source is AudioCdSource) {
                    if(single_source == null && hint == "audio-cd") {
                        single_source = source;
                    }

                    AudioCdSource cd_source = (AudioCdSource)source;
                    if(cd_source.Disk.DeviceNode == device || cd_source.Disk.Udi == device) {
                        SourceManager.SetActiveSource(source);
                        return;
                    }
                }
            }

            if(single_source != null) {
                SourceManager.SetActiveSource(single_source);
            }
        } 
        
        public void SelectDap(string device)
        {
            SelectDeviceSource(device, "dap");
        }

        public void SelectBlankCd(string device)
        {
            SelectDeviceSource(device, "blank-cd");
        }

        public void SelectAudioCd(string device)
        {
            SelectDeviceSource(device, "audio-cd");
        }
        
        public void EnqueueFiles(string [] files)
        {
            if(Available) {
                Banshee.Sources.LocalQueueSource.Instance.Enqueue(files, true);
                Banshee.Sources.SourceManager.SetActiveSource(Banshee.Sources.LocalQueueSource.Instance);
            }
        }

        private string TrackStringResult(string s)
        {
            return s == null ? String.Empty : s;
        }
        
        public string GetPlayingArtist()
        {
            return HaveTrack ? TrackStringResult(PlayerEngineCore.CurrentTrack.Artist) : String.Empty;
        }
        
        public string GetPlayingAlbum()
        {
            return HaveTrack ? TrackStringResult(PlayerEngineCore.CurrentTrack.Album) : String.Empty;
        }
        
        public string GetPlayingTitle()
        {
            return HaveTrack ? TrackStringResult(PlayerEngineCore.CurrentTrack.Title) : String.Empty;
        }
        
        public string GetPlayingGenre()
        {
            return HaveTrack ? TrackStringResult(PlayerEngineCore.CurrentTrack.Genre) : String.Empty;
        }
        
        public string GetPlayingUri()
        {
            return HaveTrack ? TrackStringResult(PlayerEngineCore.CurrentTrack.Uri.AbsoluteUri) : String.Empty;
        }

        public string GetPlayingCoverUri()
        {
            return HaveTrack ? TrackStringResult(PlayerEngineCore.CurrentTrack.CoverArtFileName) : String.Empty;
        }
        
        public int GetPlayingDuration()
        {
            return HaveTrack ? (int)PlayerEngineCore.Length : -1;
        }
        
        public int GetPlayingPosition()
        {
            return HaveTrack ? (int)PlayerEngineCore.Position : -1;
        }
        
        public int GetPlayingRating()
        {
            return HaveTrack ? (int)PlayerEngineCore.CurrentTrack.Rating : -1;
        }
        
        public int GetMaxRating()
        {
            return 5;
        }
        
        public int SetPlayingRating(int rating)
        {
            try {
                if(HaveTrack) {
                    PlayerEngineCore.CurrentTrack.Rating = (uint)Math.Max(0, Math.Min(rating, 5));
                    PlayerEngineCore.TrackInfoUpdated();
                    return (int)PlayerEngineCore.CurrentTrack.Rating;
                }
            } catch {
            }
            
            return -1;
        }
        
        public int GetPlayingStatus()
        {
            if(!Available) {
                return -1;
            }
            
            return PlayerEngineCore.CurrentState == PlayerEngineState.Playing ? 1 :
                (PlayerEngineCore.CurrentState != PlayerEngineState.Idle ? 0 : -1);
        }
        
        public void SetVolume(int volume)
        {
            if(Available) {
                PlayerEngineCore.Volume = (ushort)volume;
            }
        }

        public void IncreaseVolume()
        {
            if(Available) {
                PlayerEngineCore.Volume += 10;
            }
        }
        
        public void DecreaseVolume()
        {
            if(Available) {
                PlayerEngineCore.Volume -= 10;
            }
        }
        
        public void SetPlayingPosition(int position)
        {
            if(Available) {
                PlayerEngineCore.Position = (uint)position;
            }
        }
        
        public void SkipForward()
        {
            if(Available) {
                PlayerEngineCore.Position += 10;
            }
        }
        
        public void SkipBackward()
        {
            if(Available) {
                PlayerEngineCore.Position -= 10;
            }
        }
    }
}

