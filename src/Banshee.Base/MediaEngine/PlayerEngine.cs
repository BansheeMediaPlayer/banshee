/***************************************************************************
 *  PlayerEngine.cs
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
using System.Collections;
using Banshee.Base;

namespace Banshee.MediaEngine
{
    public delegate void PlayerEngineStateHandler(object o, PlayerEngineStateArgs args);
    public delegate void PlayerEngineEventHandler(object o, PlayerEngineEventArgs args);
    
    public sealed class PlayerEngineStateArgs : EventArgs
    {
        public PlayerEngineState State;
    }
    
    public sealed class PlayerEngineEventArgs : EventArgs
    {
        public PlayerEngineEvent Event;
        public string Message;
        public double BufferingPercent;
    }
    
    public enum PlayerEngineState {
        Idle,
        Loaded,
        Playing,
        Paused
    }
    
    public enum PlayerEngineEvent {
        Iterate,
        Seek,
        Error,
        Metadata,
        StartOfStream,
        EndOfStream,
        Volume,
        Buffering,
        TrackInfoUpdated
    }

    public abstract class PlayerEngine
    {
        public event PlayerEngineStateHandler StateChanged;
        public event PlayerEngineEventHandler EventChanged;
        
        private TrackInfo current_track;
        private Uri current_uri;
        private PlayerEngineState current_state = PlayerEngineState.Idle;
        private PlayerEngineState last_state = PlayerEngineState.Idle;
        
        protected abstract void OpenUri(Uri uri);
        
        public void Reset()
        {
            current_track = null;
            current_uri = null;
            OnStateChanged(PlayerEngineState.Idle);
        }
        
        public virtual void Close()
        {
            OnStateChanged(PlayerEngineState.Idle);
        }
        
        public virtual void Dispose()
        {
            Close();
        }
        
        public void Open(TrackInfo track)
        {
            current_uri = track.Uri;
            current_track = track;
            
            HandleOpen(track.Uri);
        }
        
        public void Open(Uri uri)
        {
            current_uri = uri;
            current_track = new UnknownTrackInfo(uri);
            
            HandleOpen(uri);
        }

        private void HandleOpen(Uri uri)
        {
            if(current_state != PlayerEngineState.Idle) {
                Close();
            }
        
            try {
                OpenUri(uri);
                OnEventChanged(PlayerEngineEvent.StartOfStream);
                OnStateChanged(PlayerEngineState.Loaded);
            } catch(Exception e) {
                Console.WriteLine(e);
                OnEventChanged(PlayerEngineEvent.Error, e.Message);
            }
        }
        
        public virtual void Play()
        {
            OnStateChanged(PlayerEngineState.Playing);
        }

        public virtual void Pause()
        {
            OnStateChanged(PlayerEngineState.Paused);
        }
        
        public virtual IntPtr [] GetBaseElements()
        {
            return null;
        }
        
        protected virtual void OnStateChanged(PlayerEngineState state)
        {
            if(current_state == state) {
                return;
            }
        
            if(ThreadAssist.InMainThread) {
                RaiseStateChanged(state);
            } else {
                ThreadAssist.ProxyToMain(delegate {
                    RaiseStateChanged(state);
                });
            }
        }
        
        private void RaiseStateChanged(PlayerEngineState state)
        {
            last_state = current_state;
            current_state = state;
            
            PlayerEngineStateHandler handler = StateChanged;
            if(handler != null) {
                PlayerEngineStateArgs args = new PlayerEngineStateArgs();
                args.State = state;
                handler(this, args);
            }
        }
        
        protected void OnEventChanged(PlayerEngineEvent evnt)
        {
            OnEventChanged(evnt, null, 0.0);
        }
        
        protected void OnEventChanged(PlayerEngineEvent evnt, string message)
        {
            OnEventChanged(evnt, message, 0.0);
        }
        
        protected virtual void OnEventChanged(PlayerEngineEvent evnt, string message, double bufferingPercent)
        {
            if(ThreadAssist.InMainThread) {
                RaiseEventChanged(evnt, message, bufferingPercent);
            } else {
                ThreadAssist.ProxyToMain(delegate {
                    RaiseEventChanged(evnt, message, bufferingPercent);
                });
            }
        }
        
        private void RaiseEventChanged(PlayerEngineEvent evnt, string message, double bufferingPercent)
        {
            PlayerEngineEventHandler handler = EventChanged;
            if(handler != null) {
                PlayerEngineEventArgs args = new PlayerEngineEventArgs();
                args.Event = evnt;
                args.Message = message;
                args.BufferingPercent = bufferingPercent;
                handler(this, args);
            }
        }
        
        private uint track_info_updated_timeout = 0;
        
        protected void OnTagFound(StreamTag tag)
        {
            if(tag.Equals(StreamTag.Zero) || current_track == null) {
                return;
            }
            
            StreamTagger.TrackInfoMerge(current_track, tag);
            
            if(track_info_updated_timeout <= 0) {
                track_info_updated_timeout = GLib.Timeout.Add(500, OnTrackInfoUpdated);
            }
        }
        
        private bool OnTrackInfoUpdated()
        {
            OnEventChanged(PlayerEngineEvent.TrackInfoUpdated);
            track_info_updated_timeout = 0;
            return false;
        }
        
        public TrackInfo CurrentTrack {
            get { return current_track; }
        }
        
        public Uri CurrentUri {
            get { return current_uri; }
        }
        
        public PlayerEngineState CurrentState {
            get { return current_state; }
        }
        
        public PlayerEngineState LastState {
            get { return last_state; }
        }
        
        public abstract ushort Volume {
            get;
            set;
        }
        
        public virtual bool CanSeek {
            get { return true; }
        }
        
        public abstract uint Position {
            get;
            set;
        }
        
        public abstract uint Length {
            get;
        }
        
        public abstract IEnumerable SourceCapabilities {
            get;
        }
        
        public abstract IEnumerable ExplicitDecoderCapabilities {
            get;
        }
        
        public abstract string Id {
            get;
        }
        
        public abstract string Name {
            get;
        }
    }
}
