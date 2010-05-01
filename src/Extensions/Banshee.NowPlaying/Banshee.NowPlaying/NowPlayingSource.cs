//
// NowPlayingSource.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
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
using Mono.Unix;
using Gtk;

using Banshee.Sources;
using Banshee.ServiceStack;
using Banshee.MediaEngine;
using Banshee.Collection;

using Banshee.Gui;
using Banshee.Sources.Gui;

namespace Banshee.NowPlaying
{
    public class NowPlayingSource : Source, IDisposable
    {
        private TrackInfo transitioned_track;
        private NowPlayingInterface now_playing_interface;
        
        private Widget substitute_audio_display;

        public NowPlayingSource () : base ("now-playing", Catalog.GetString ("Now Playing"), 10, "now-playing")
        {
            Properties.SetString ("Icon.Name", "applications-multimedia");
            Properties.Set<bool> ("Nereid.SourceContents.HeaderVisible", false);
            Properties.SetString ("ActiveSourceUIResource", "ActiveSourceUI.xml");

            ServiceManager.SourceManager.AddSource (this);

            ServiceManager.PlaybackController.Transition += OnPlaybackControllerTransition;
            ServiceManager.PlaybackController.TrackStarted += OnPlaybackControllerTrackStarted;
            ServiceManager.PlayerEngine.ConnectEvent (OnTrackInfoUpdated, PlayerEvent.TrackInfoUpdated);
            ServiceManager.PlayerEngine.ConnectEvent (OnCreateVideoWindow, PlayerEvent.PrepareVideoWindow);
        }

        private void OnCreateVideoWindow (PlayerEventArgs args)
        {
            ServiceManager.PlayerEngine.DisconnectEvent (OnCreateVideoWindow);
            NowPlayingContents.CreateVideoDisplay ();
        }

        public void Dispose ()
        {
            ServiceManager.PlaybackController.Transition -= OnPlaybackControllerTransition;
            ServiceManager.PlaybackController.TrackStarted -= OnPlaybackControllerTrackStarted;
            ServiceManager.PlayerEngine.DisconnectEvent (OnTrackInfoUpdated);

            if (now_playing_interface != null) {
                now_playing_interface.Destroy ();
                now_playing_interface.Dispose ();
                now_playing_interface = null;
            }
        }

        private void OnTrackInfoUpdated (PlayerEventArgs args)
        {
            CheckForSwitch ();
        }

        private void OnPlaybackControllerTrackStarted (object o, EventArgs args)
        {
            CheckForSwitch ();
        }

        private void OnPlaybackControllerTransition (object o, EventArgs args)
        {
            transitioned_track = ServiceManager.PlaybackController.CurrentTrack;
        }

        private void CheckForSwitch ()
        {
            TrackInfo current_track = ServiceManager.PlaybackController.CurrentTrack;
            if (current_track != null && transitioned_track != current_track &&
                (current_track.MediaAttributes & TrackMediaAttributes.VideoStream) != 0) {
                ServiceManager.SourceManager.SetActiveSource (this);
            }

            if (current_track != null &&
                (current_track.MediaAttributes & TrackMediaAttributes.VideoStream) != 0) {
                OnUserNotifyUpdated ();
            }
        }
        
        public void SetSubstituteAudioDisplay (Widget widget)
        {
            if (now_playing_interface != null) {
                now_playing_interface.Contents.SetSubstituteAudioDisplay (widget);
            } else {
                substitute_audio_display = widget;
            }
        }

        public override void Activate ()
        {
            if (now_playing_interface == null) {
                now_playing_interface = new NowPlayingInterface ();
                Properties.Set<ISourceContents> ("Nereid.SourceContents", now_playing_interface);
                
                if (substitute_audio_display != null) {
                    now_playing_interface.Contents.SetSubstituteAudioDisplay (substitute_audio_display);
                    substitute_audio_display = null;
                }
            }

            if (now_playing_interface != null) {
                now_playing_interface.OverrideFullscreen ();
            }
        }

        public override void Deactivate ()
        {
            if (now_playing_interface != null) {
                now_playing_interface.RelinquishFullscreen ();
            }
        }
    }
}
