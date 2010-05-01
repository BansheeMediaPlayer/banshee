//
// PlayerEngine.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2006-2007 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections;

using Hyena;

using Banshee.Base;
using Banshee.Streaming;
using Banshee.Collection;
using Banshee.ServiceStack;

namespace Banshee.MediaEngine
{
    public abstract class PlayerEngine
    {
        public const int VolumeDelta = 10;
        public const int SkipDelta = 10;

        public event PlayerEventHandler EventChanged;

        private TrackInfo current_track;
        private SafeUri current_uri;
        private TrackInfo pending_track;
        private SafeUri pending_uri;
        private PlayerState current_state = PlayerState.NotReady;
        private PlayerState last_state = PlayerState.NotReady;

        // will be changed to PlayerState.Idle after going to PlayerState.Ready
        private PlayerState idle_state = PlayerState.NotReady;

        protected abstract void OpenUri (SafeUri uri);

        internal protected virtual bool DelayedInitialize {
            get { return false; }
        }

        public bool IsInitialized { get; internal set; }

        internal protected virtual void Initialize ()
        {
        }

        public void Reset ()
        {
            current_track = null;
            current_uri = null;
            OnStateChanged (idle_state);
        }

        public virtual void Close (bool fullShutdown)
        {
            OnStateChanged (idle_state);
        }

        public virtual void Dispose ()
        {
            Close (true);
        }

        public void Open (TrackInfo track)
        {
            current_uri = track.Uri;
            current_track = track;

            HandleOpen (track.Uri);
        }

        public void Open (SafeUri uri)
        {
            current_uri = uri;
            current_track = new UnknownTrackInfo (uri);

            HandleOpen (uri);
        }

        public void SetNextTrack (TrackInfo track)
        {
            pending_track = track;
            pending_uri = track != null ? track.Uri : null;

            HandleNextTrack (pending_uri);
        }

        public void SetNextTrack (SafeUri uri)
        {
            pending_uri = uri;
            pending_track = new UnknownTrackInfo (uri);

            HandleNextTrack (uri);
        }

        private void HandleNextTrack (SafeUri uri)
        {
            if (current_state != PlayerState.Playing) {
                // Pre-buffering the next track only makes sense when we're currently playing
                // Instead, just open.
                if (uri != null) {
                    HandleOpen (uri);
                    Play ();
                }
                return;
            }

            try {
                // Setting the next track doesn't change the player state.
                SetNextTrackUri (uri);
            } catch (Exception e) {
                Log.Exception ("Failed to pre-buffer next track", e);
            }
        }

        private void HandleOpen (SafeUri uri)
        {
            if (current_state != PlayerState.Idle && current_state != PlayerState.NotReady && current_state != PlayerState.Contacting) {
                Close (false);
            }

            try {
                OnStateChanged (PlayerState.Loading);
                OpenUri (uri);
            } catch (Exception e) {
                Close (true);
                OnEventChanged (new PlayerEventErrorArgs (e.Message));
            }
        }

        public abstract void Play ();

        public abstract void Pause ();

        public virtual void SetNextTrackUri (SafeUri uri)
        {
            // Opening files on SetNextTrack is a sane default behaviour.
            // This only wants to be overridden if the PlayerEngine sends out RequestNextTrack signals before EoS
            OpenUri (uri);
        }

        public virtual void VideoExpose (IntPtr displayContext, bool direct)
        {
            throw new NotImplementedException ("Engine must implement VideoExpose since this method only gets called when SupportsVideo is true");
        }

        public virtual void VideoWindowRealize (IntPtr displayContext)
        {
            throw new NotImplementedException ("Engine must implement VideoWindowRealize since this method only gets called when SupportsVideo is true");
        }

        public virtual IntPtr [] GetBaseElements ()
        {
            return null;
        }

        protected virtual void OnStateChanged (PlayerState state)
        {
            if (current_state == state) {
                return;
            }

            if (idle_state == PlayerState.NotReady && state != PlayerState.Ready) {
                Hyena.Log.Warning ("Engine must transition to the ready state before other states can be entered", false);
                return;
            } else if (idle_state == PlayerState.NotReady && state == PlayerState.Ready) {
                idle_state = PlayerState.Idle;
            }

            last_state = current_state;
            current_state = state;

            Log.DebugFormat ("Player state change: {0} -> {1}", last_state, current_state);

            OnEventChanged (new PlayerEventStateChangeArgs (last_state, current_state));

            // Going to the Ready state automatically transitions to the Idle state
            // The Ready state is advertised so one-time startup processes can easily
            // happen outside of the engine itself

            if (state == PlayerState.Ready) {
                OnStateChanged (PlayerState.Idle);
            }
        }

        protected void OnEventChanged (PlayerEvent evnt)
        {
            OnEventChanged (new PlayerEventArgs (evnt));
        }

        protected virtual void OnEventChanged (PlayerEventArgs args)
        {
            if (args.Event == PlayerEvent.StartOfStream && pending_track != null) {
                Log.DebugFormat ("OnEventChanged called with StartOfStream.  Replacing current_track: \"{0}\" with pending_track: \"{1}\"", current_track.DisplayTrackTitle, pending_track.DisplayTrackTitle);
                current_track = pending_track;
                current_uri = pending_uri;
                pending_track = null;
                pending_uri = null;
            }

            if (ThreadAssist.InMainThread) {
                RaiseEventChanged (args);
            } else {
                ThreadAssist.ProxyToMain (delegate {
                    RaiseEventChanged (args);
                });
            }
        }

        private void RaiseEventChanged (PlayerEventArgs args)
        {
            PlayerEventHandler handler = EventChanged;
            if (handler != null) {
                handler (args);
            }
        }

        private uint track_info_updated_timeout = 0;

        protected void OnTagFound (StreamTag tag)
        {
            if (tag.Equals (StreamTag.Zero) || current_track == null || current_track.Uri.IsFile) {
                return;
            }

            StreamTagger.TrackInfoMerge (current_track, tag);

            if (track_info_updated_timeout <= 0) {
                track_info_updated_timeout = Application.RunTimeout (250, OnTrackInfoUpdated);
            }
        }

        private bool OnTrackInfoUpdated ()
        {
            TrackInfoUpdated ();
            track_info_updated_timeout = 0;
            return false;
        }

        public void TrackInfoUpdated ()
        {
            OnEventChanged (PlayerEvent.TrackInfoUpdated);
        }

        public TrackInfo CurrentTrack {
            get { return current_track; }
        }

        public SafeUri CurrentUri {
            get { return current_uri; }
        }

        public PlayerState CurrentState {
            get { return current_state; }
        }

        public PlayerState LastState {
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

        public abstract bool SupportsEqualizer {
            get;
        }

        public abstract VideoDisplayContextType VideoDisplayContextType {
            get;
        }

        public virtual IntPtr VideoDisplayContext {
            set { }
            get { return IntPtr.Zero; }
        }
    }
}
