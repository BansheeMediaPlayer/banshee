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
        private TrackInfo pending_track;
        private PlayerState current_state = PlayerState.NotReady;
        private PlayerState last_state = PlayerState.NotReady;

        public DateTime CurrentTrackTimeStamp {
            get;
            private set;
        }

        // will be changed to PlayerState.Idle after going to PlayerState.Ready
        private PlayerState idle_state = PlayerState.NotReady;

        protected abstract void OpenUri (SafeUri uri, bool maybeVideo);

        internal protected virtual bool DelayedInitialize {
            get { return false; }
        }

        public bool IsInitialized { get; internal set; }

        internal protected virtual void Initialize ()
        {
        }

        public void Reset ()
        {
            CurrentTrackTimeStamp = DateTime.Now;
            current_track = null;
            OnStateChanged (idle_state);
        }

        public virtual void Close (bool fullShutdown)
        {
            if (fullShutdown) {
                Reset ();
            } else {
                OnStateChanged (idle_state);
            }
        }

        public virtual void Dispose ()
        {
            Close (true);
        }

        public void Open (SafeUri uri)
        {
            HandleOpen (new UnknownTrackInfo (uri));
        }

        public void Open (TrackInfo track)
        {
            pending_track = null;
            HandleOpen (track);
        }

        public void SetNextTrack (SafeUri uri)
        {
            SetNextTrack (new UnknownTrackInfo (uri));
        }

        public void SetNextTrack (TrackInfo track)
        {
            HandleNextTrack (track);
        }

        private void HandleNextTrack (TrackInfo track)
        {
            pending_track = track;
            if (current_state != PlayerState.Playing) {
                // Pre-buffering the next track only makes sense when we're currently playing
                // Instead, just open.
                if (track != null && track.Uri != null) {
                    HandleOpen (track);
                    Play ();
                }
                return;
            }

            try {
                // Setting the next track doesn't change the player state.
                SetNextTrackUri (track == null ? null : track.Uri,
                    track == null || track.HasAttribute (TrackMediaAttributes.VideoStream) || track is UnknownTrackInfo);
            } catch (Exception e) {
                Log.Exception ("Failed to pre-buffer next track", e);
            }
        }

        private void HandleOpen (TrackInfo track)
        {
            var uri = track.Uri;
            if (current_state != PlayerState.Idle && current_state != PlayerState.NotReady && current_state != PlayerState.Contacting) {
                Close (false);
            }

            try {
                CurrentTrackTimeStamp = DateTime.Now;
                current_track = track;
                OnStateChanged (PlayerState.Loading);
                OpenUri (uri, track.HasAttribute (TrackMediaAttributes.VideoStream) || track is UnknownTrackInfo);
            } catch (Exception e) {
                Close (true);
                OnEventChanged (new PlayerEventErrorArgs (e.Message));
            }
        }

        public abstract void Play ();

        public abstract void Pause ();

        public virtual void SetNextTrackUri (SafeUri uri, bool maybeVideo)
        {
            // Opening files on SetNextTrack is a sane default behaviour.
            // This only wants to be overridden if the PlayerEngine sends out RequestNextTrack signals before EoS
            OpenUri (uri, maybeVideo);
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
                Log.DebugFormat ("OnEventChanged called with StartOfStream.  Replacing current_track with pending_track: \"{0}\"",
                                 pending_track.DisplayTrackTitle);
                CurrentTrackTimeStamp = DateTime.Now;
                current_track = pending_track;
                pending_track = null;
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
            if (tag.Equals (StreamTag.Zero) || current_track == null
                || (current_track.Uri != null && current_track.Uri.IsFile)) {
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

        public abstract string GetSubtitleDescription (int index);

        public TrackInfo CurrentTrack {
            get { return current_track; }
        }

        public SafeUri CurrentUri {
            get { return current_track == null ? null : current_track.Uri; }
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

        public abstract int SubtitleCount {
            get;
        }

        public abstract int SubtitleIndex {
            set;
        }

        public abstract SafeUri SubtitleUri {
            set;
            get;
        }

        public abstract bool InDvdMenu {
            get;
        }

        public abstract void NotifyMouseMove (double x, double y);
        public abstract void NotifyMouseButtonPressed (int button, double x, double y);
        public abstract void NotifyMouseButtonReleased (int button, double x, double y);

        public abstract void NavigateToLeftMenu ();
        public abstract void NavigateToRightMenu ();
        public abstract void NavigateToUpMenu ();
        public abstract void NavigateToDownMenu ();
        public abstract void NavigateToMenu ();

        public abstract void ActivateCurrentMenu ();

        public abstract void GoToNextChapter ();
        public abstract void GoToPreviousChapter ();

    }
}
