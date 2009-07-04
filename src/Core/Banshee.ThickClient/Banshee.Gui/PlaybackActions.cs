//
// PlaybackActions.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2007 Novell, Inc.
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

using Hyena.Data.Gui;

using Banshee.Base;
using Banshee.Collection;
using Banshee.Sources;
using Banshee.Configuration;
using Banshee.ServiceStack;
using Banshee.MediaEngine;
using Banshee.PlaybackController;
using Banshee.Gui.Dialogs;

namespace Banshee.Gui
{
    public class PlaybackActions : BansheeActionGroup
    {
        private Gtk.Action play_pause_action;
        private PlaybackRepeatActions repeat_actions;
        private PlaybackShuffleActions shuffle_actions;

        public PlaybackRepeatActions RepeatActions {
            get { return repeat_actions; }
        }
        
        public PlaybackShuffleActions ShuffleActions {
            get { return shuffle_actions; }
        }
        
        public PlaybackActions () : base ("Playback")
        {
            ImportantByDefault = false;

            Add (new ActionEntry [] {
                new ActionEntry ("PlayPauseAction", null,
                    Catalog.GetString ("_Play"), "space",
                    Catalog.GetString ("Play or pause the current item"), OnPlayPauseAction),
                    
                new ActionEntry ("NextAction", null,
                    Catalog.GetString ("_Next"), "N",
                    Catalog.GetString ("Play the next item"), OnNextAction),
                    
                new ActionEntry ("PreviousAction", null,
                    Catalog.GetString ("Pre_vious"), "B",
                    Catalog.GetString ("Play the previous item"), OnPreviousAction),

                new ActionEntry ("SeekToAction", null,
                    Catalog.GetString ("Seek _to..."), "T",
                    Catalog.GetString ("Seek to a specific location in current item"), OnSeekToAction),

                new ActionEntry ("JumpToPlayingTrackAction", null,
                    Catalog.GetString("_Jump to Playing Song"), "<control>J",
                    Catalog.GetString ("Jump to the currently playing item"), OnJumpToPlayingTrack),
                
                new ActionEntry ("RestartSongAction", null,
                    Catalog.GetString ("_Restart Song"), "R",
                    Catalog.GetString ("Restart the current item"), OnRestartSongAction)
            });
            
            Add (new ToggleActionEntry [] {
                new ToggleActionEntry ("StopWhenFinishedAction", null,
                    Catalog.GetString ("_Stop When Finished"), "<Shift>space",
                    Catalog.GetString ("Stop playback after the current item finishes playing"), 
                    OnStopWhenFinishedAction, false)
            });
            
            Actions.GlobalActions.Add (new ActionEntry [] {
                new ActionEntry ("PlaybackMenuAction", null,
                    Catalog.GetString ("_Playback"), null, null, null),
            });

            this["JumpToPlayingTrackAction"].Sensitive = false;
            this["RestartSongAction"].Sensitive = false;
            this["SeekToAction"].Sensitive = false;
            
            this["PlayPauseAction"].IconName = "media-playback-start";
            this["NextAction"].IconName = "media-skip-forward";
            this["PreviousAction"].IconName = "media-skip-backward";
            
            ServiceManager.PlayerEngine.ConnectEvent (OnPlayerEvent, 
                PlayerEvent.Error | 
                PlayerEvent.EndOfStream | 
                PlayerEvent.StateChange);
            
            repeat_actions = new PlaybackRepeatActions (Actions);
            shuffle_actions = new PlaybackShuffleActions (Actions, this);
        }
        
        private void OnPlayerEvent (PlayerEventArgs args)
        {
            switch (args.Event) {
                case PlayerEvent.Error:
                case PlayerEvent.EndOfStream:
                    ToggleAction stop_action = (ToggleAction) this["StopWhenFinishedAction"];
                    // Kinda lame, but we don't want to actually reset StopWhenFinished inside the controller
                    // since it is also listening to EOS and needs to actually stop playback; we listen here
                    // just to keep the UI in sync.
                    stop_action.Activated -= OnStopWhenFinishedAction;
                    stop_action.Active = false;
                    stop_action.Activated += OnStopWhenFinishedAction;
                    break;
                case PlayerEvent.StateChange:
                    OnPlayerStateChange ((PlayerEventStateChangeArgs)args);
                    break;
            }
        }
        
        private void OnPlayerStateChange (PlayerEventStateChangeArgs args)
        {
            if (play_pause_action == null) {
                play_pause_action = Actions["Playback.PlayPauseAction"];
            }

            switch (args.Current) {
                case PlayerState.Contacting:
                case PlayerState.Loading:
                case PlayerState.Loaded:
                case PlayerState.Playing:
                    ShowStopAction ();
                    break;
                case PlayerState.Paused:
                    ShowPlay ();
                    break;
                case PlayerState.Idle:
                    ShowPlay ();
                    break;
                default:
                    break;
            }

            TrackInfo track = ServiceManager.PlayerEngine.CurrentTrack; 
            if (track != null) {
                this["SeekToAction"].Sensitive = !track.IsLive;
                this["RestartSongAction"].Sensitive = !track.IsLive;

                this["RestartSongAction"].Label = track.RestartLabel;
                this["JumpToPlayingTrackAction"].Label = track.JumpToLabel;
                this["JumpToPlayingTrackAction"].Sensitive = true;
            } else {
                this["JumpToPlayingTrackAction"].Sensitive = false;
                this["RestartSongAction"].Sensitive = false;
                this["SeekToAction"].Sensitive = false;
            }

            // Disable all actions while NotReady
            Sensitive = args.Current != PlayerState.NotReady;
        }
        
        private void ShowStopAction ()
        {
            if (ServiceManager.PlayerEngine.CanPause) {
                ShowPause ();
            } else {
                ShowStop ();
            }
        }
        
        private void ShowPause ()
        {
            play_pause_action.Label = Catalog.GetString ("_Pause");
            play_pause_action.IconName = "media-playback-pause";
        }
        
        private void ShowPlay ()
        {
            play_pause_action.Label = Catalog.GetString ("_Play");
            play_pause_action.IconName = "media-playback-start";
        }
                
        private void ShowStop ()
        {
            play_pause_action.Label = Catalog.GetString ("Sto_p");
            play_pause_action.IconName = "media-playback-stop";
        }
                
        private void OnPlayPauseAction (object o, EventArgs args)
        {
            ServiceManager.PlayerEngine.TogglePlaying ();
        }
        
        private void OnNextAction (object o, EventArgs args)
        {
            ServiceManager.PlaybackController.Next ();
        }
        
        private void OnPreviousAction (object o, EventArgs args)
        {
            const int delay = 4000; // ms
            if (ServiceManager.PlayerEngine.Position < delay) {
                ServiceManager.PlaybackController.Previous ();
            } else {
                OnRestartSongAction (o, args);
            }
        }

        private void OnSeekToAction (object o, EventArgs args)
        {
            GladeDialog dialog = new SeekDialog ();
            dialog.Run ();
            dialog.Destroy ();
        }
            
        private void OnRestartSongAction (object o, EventArgs args)
        {
            TrackInfo track = ServiceManager.PlayerEngine.CurrentTrack;
            if (track != null) {
                ServiceManager.PlayerEngine.Close ();
                ServiceManager.PlayerEngine.OpenPlay (track);
            }
        }
        
        private void OnStopWhenFinishedAction (object o, EventArgs args)
        {
            ServiceManager.PlaybackController.StopWhenFinished = ((ToggleAction)o).Active;
        }

        private void OnJumpToPlayingTrack (object o, EventArgs args)
        {
            ITrackModelSource track_src = ServiceManager.PlaybackController.Source;
            Source src = track_src as Source;

            if (track_src != null && src != null) {
                int i = track_src.TrackModel.IndexOf (ServiceManager.PlaybackController.CurrentTrack);
                if (i != -1) {
                    // TODO clear the search/filters if there are any, since they might be hiding the currently playing item?
                    // and/or switch to the track's primary source?  what if it's been removed from the library all together?
                    IListView<TrackInfo> track_list = src.Properties.Get<IListView<TrackInfo>> ("Track.IListView");
                    if (track_list != null) {
                        ServiceManager.SourceManager.SetActiveSource (src);
                        track_src.TrackModel.Selection.Clear (false);
                        track_src.TrackModel.Selection.Select (i);
                        track_src.TrackModel.Selection.FocusedIndex = i;
                        track_list.CenterOn (i);
                        track_list.GrabFocus ();
                    }
                }
            }
        }
    }
}
