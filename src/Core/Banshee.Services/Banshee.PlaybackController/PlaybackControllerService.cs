//
// PlaybackControllerService.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2007-2008 Novell, Inc.
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

using Hyena;
using Hyena.Collections;

using Banshee.Sources;
using Banshee.ServiceStack;
using Banshee.Collection;
using Banshee.MediaEngine;

namespace Banshee.PlaybackController
{
    public class PlaybackControllerService : IRequiredService, ICanonicalPlaybackController,
        IPlaybackController, IPlaybackControllerService
    {
        private enum Direction
        {
            Next,
            Previous
        }

        private IStackProvider<TrackInfo> previous_stack;
        private IStackProvider<TrackInfo> next_stack;

        private TrackInfo current_track;
        private TrackInfo prior_track;
        private TrackInfo changing_to_track;
        private bool raise_started_after_transition = false;
        private bool transition_track_started = false;
        private bool last_was_skipped = true;
        private int consecutive_errors;
        private uint error_transition_id;
        private DateTime source_auto_set_at = DateTime.MinValue;

        private string shuffle_mode;
        private PlaybackRepeatMode repeat_mode;
        private bool stop_when_finished = false;

        private PlayerEngineService player_engine;
        private ITrackModelSource source;
        private ITrackModelSource next_source;

        private event PlaybackControllerStoppedHandler dbus_stopped;
        event PlaybackControllerStoppedHandler IPlaybackControllerService.Stopped {
            add { dbus_stopped += value; }
            remove { dbus_stopped -= value; }
        }

        public event EventHandler Stopped;
        public event EventHandler SourceChanged;
        public event EventHandler NextSourceChanged;
        public event EventHandler TrackStarted;
        public event EventHandler Transition;
        public event EventHandler<EventArgs<string>> ShuffleModeChanged;
        public event EventHandler<EventArgs<PlaybackRepeatMode>> RepeatModeChanged;

        public PlaybackControllerService ()
        {
            InstantiateStacks ();

            player_engine = ServiceManager.PlayerEngine;
            player_engine.PlayWhenIdleRequest += OnPlayerEnginePlayWhenIdleRequest;
            player_engine.ConnectEvent (OnPlayerEvent,
                PlayerEvent.RequestNextTrack |
                PlayerEvent.EndOfStream |
                PlayerEvent.StartOfStream |
                PlayerEvent.StateChange |
                PlayerEvent.Error,
                true);

            ServiceManager.SourceManager.ActiveSourceChanged += delegate {
                ITrackModelSource active_source = ServiceManager.SourceManager.ActiveSource as ITrackModelSource;
                if (active_source != null && source_auto_set_at == source_set_at && !player_engine.IsPlaying ()) {
                    Source = active_source;
                    source_auto_set_at = source_set_at;
                }
            };
        }

        protected virtual void InstantiateStacks ()
        {
            previous_stack = new PlaybackControllerDatabaseStack ();
            next_stack = new PlaybackControllerDatabaseStack ();
        }

        private void OnPlayerEnginePlayWhenIdleRequest (object o, EventArgs args)
        {
            ITrackModelSource next_source = NextSource;
            if (next_source != null && next_source.TrackModel.Selection.Count > 0) {
                Source = NextSource;
                CancelErrorTransition ();
                CurrentTrack = next_source.TrackModel[next_source.TrackModel.Selection.FirstIndex];
                QueuePlayTrack ();
            } else {
                Next ();
            }
        }

        private void OnPlayerEvent (PlayerEventArgs args)
        {
            switch (args.Event) {
                case PlayerEvent.StartOfStream:
                    CurrentTrack = player_engine.CurrentTrack;
                    consecutive_errors = 0;
                    break;
                case PlayerEvent.EndOfStream:
                    EosTransition ();
                    break;
                case PlayerEvent.Error:
                    if (++consecutive_errors >= 5) {
                        consecutive_errors = 0;
                        player_engine.Close (false);
                        OnStopped ();
                        break;
                    }

                    CancelErrorTransition ();
                    // TODO why is this so long? any reason not to be instantaneous?
                    error_transition_id = Application.RunTimeout (250, delegate {
                        EosTransition ();
                        RequestTrackHandler ();
                        return true;
                    });
                    break;
                case PlayerEvent.StateChange:
                    if (((PlayerEventStateChangeArgs)args).Current != PlayerState.Loading) {
                        break;
                    }

                    TrackInfo track = player_engine.CurrentTrack;
                    if (changing_to_track != track && track != null) {
                        CurrentTrack = track;
                    }

                    changing_to_track = null;

                    if (!raise_started_after_transition) {
                        transition_track_started = false;
                        OnTrackStarted ();
                    } else {
                        transition_track_started = true;
                    }
                    break;
                case PlayerEvent.RequestNextTrack:
                    RequestTrackHandler ();
                    break;
            }
        }

        private void CancelErrorTransition ()
        {
            if (error_transition_id > 0) {
                Application.IdleTimeoutRemove (error_transition_id);
                error_transition_id = 0;
            }
        }

        private bool EosTransition ()
        {
            player_engine.IncrementLastPlayed ();
            return true;
        }

        private bool RequestTrackHandler ()
        {
            if (!StopWhenFinished) {
                if (RepeatMode == PlaybackRepeatMode.RepeatSingle) {
                    RepeatCurrentAsNext ();
                } else {
                    last_was_skipped = false;
                    Next (RepeatMode == PlaybackRepeatMode.RepeatAll, false);
                }
            } else {
                OnStopped ();
            }

            StopWhenFinished = false;
            return false;
        }

        private void RepeatCurrentAsNext ()
        {
            raise_started_after_transition = true;

            player_engine.SetNextTrack (CurrentTrack);

            OnTransition ();
        }

        public void First ()
        {
            CancelErrorTransition ();

            Source = NextSource;

            // This and OnTransition() below commented out b/c of BGO #524556
            //raise_started_after_transition = true;

            if (Source is IBasicPlaybackController && ((IBasicPlaybackController)Source).First ()) {
            } else {
                ((IBasicPlaybackController)this).First ();
            }

            //OnTransition ();
        }

        public void Next ()
        {
            Next (RepeatMode == PlaybackRepeatMode.RepeatAll, true);
        }

        public void Next (bool restart)
        {
            Next (restart, true);
        }

        public void Next (bool restart, bool changeImmediately)
        {
            CancelErrorTransition ();

            Source = NextSource;
            raise_started_after_transition = true;

            if (changeImmediately) {
                player_engine.IncrementLastPlayed ();
            }

            if (Source is IBasicPlaybackController && ((IBasicPlaybackController)Source).Next (restart, changeImmediately)) {
            } else {
                ((IBasicPlaybackController)this).Next (restart, changeImmediately);
            }

            OnTransition ();
        }

        public void Previous ()
        {
            Previous (RepeatMode == PlaybackRepeatMode.RepeatAll);
        }

        public void Previous (bool restart)
        {
            CancelErrorTransition ();

            Source = NextSource;
            raise_started_after_transition = true;

            player_engine.IncrementLastPlayed ();

            if (Source is IBasicPlaybackController && ((IBasicPlaybackController)Source).Previous (restart)) {
            } else {
                ((IBasicPlaybackController)this).Previous (restart);
            }

            OnTransition ();
        }

        public void RestartOrPrevious ()
        {
            RestartOrPrevious (RepeatMode == PlaybackRepeatMode.RepeatAll);
        }

        public void RestartOrPrevious (bool restart)
        {
            const int delay = 4000; // ms
            if (player_engine.Position < delay) {
                Previous ();
            } else {
                Restart ();
            }
        }

        public void Restart ()
        {
            player_engine.RestartCurrentTrack ();
        }

        bool IBasicPlaybackController.First ()
        {
            if (Source.Count > 0) {
                if (ShuffleMode == "off") {
                    CurrentTrack = Source.TrackModel[0];
                    player_engine.OpenPlay (CurrentTrack);
                } else {
                    ((IBasicPlaybackController)this).Next (false, true);
                }
            }
            return true;
        }

        bool IBasicPlaybackController.Next (bool restart, bool changeImmediately)
        {
            if (CurrentTrack != null) {
                previous_stack.Push (CurrentTrack);
            }

            CurrentTrack = CalcNextTrack (Direction.Next, restart);
            if (!changeImmediately) {
                // A RequestNextTrack event should always result in SetNextTrack being called.  null is acceptable.
                player_engine.SetNextTrack (CurrentTrack);
            } else if (CurrentTrack != null) {
                QueuePlayTrack ();
            }
            return true;
        }

        bool IBasicPlaybackController.Previous (bool restart)
        {
            if (CurrentTrack != null && previous_stack.Count > 0) {
                next_stack.Push (current_track);
            }

            CurrentTrack = CalcNextTrack (Direction.Previous, restart);
            if (CurrentTrack != null) {
                QueuePlayTrack ();
            }

            return true;
        }

        private TrackInfo CalcNextTrack (Direction direction, bool restart)
        {
            if (direction == Direction.Previous) {
                if (previous_stack.Count > 0) {
                    return previous_stack.Pop ();
                }
            } else if (direction == Direction.Next) {
                if (next_stack.Count > 0) {
                    return next_stack.Pop ();
                }
            }
            return QueryTrack (direction, restart);
        }

        private TrackInfo QueryTrack (Direction direction, bool restart)
        {
            Log.DebugFormat ("Querying model for track to play in {0}:{1} mode", ShuffleMode, direction);
            return ShuffleMode == "off"
                ? QueryTrackLinear (direction, restart)
                : QueryTrackRandom (ShuffleMode, restart);
        }

        private TrackInfo QueryTrackLinear (Direction direction, bool restart)
        {
            if (Source.TrackModel.Count == 0)
                return null;

            int index = Source.TrackModel.IndexOf (PriorTrack);

            // Clear the PriorTrack after using it, it's only meant to be used for a single Query
            PriorTrack = null;

            if (index == -1) {
                return Source.TrackModel[0];
            } else {
                index += (direction == Direction.Next ? 1 : -1);
                if (index >= 0 && index < Source.TrackModel.Count) {
                    return Source.TrackModel[index];
                } else if (!restart) {
                    return null;
                } else if (index < 0) {
                    return Source.TrackModel[Source.TrackModel.Count - 1];
                } else {
                    return Source.TrackModel[0];
                }
            }
        }

        private TrackInfo QueryTrackRandom (string shuffle_mode, bool restart)
        {
            var track_shuffler = Source.TrackModel as Banshee.Collection.Database.DatabaseTrackListModel;
            TrackInfo track = track_shuffler == null
                ? Source.TrackModel.GetRandom (source_set_at)
                : track_shuffler.GetRandom (source_set_at, shuffle_mode, restart, last_was_skipped, Banshee.Collection.Database.Shuffler.Playback);
            // Reset to default of true, only ever set to false by EosTransition
            last_was_skipped = true;
            return track;
        }

        private void QueuePlayTrack ()
        {
            changing_to_track = CurrentTrack;
            player_engine.OpenPlay (CurrentTrack);
        }

        protected virtual void OnStopped ()
        {
            player_engine.IncrementLastPlayed ();

            EventHandler handler = Stopped;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }

            PlaybackControllerStoppedHandler dbus_handler = dbus_stopped;
            if (dbus_handler != null) {
                dbus_handler ();
            }
        }

        protected virtual void OnTransition ()
        {
            EventHandler handler = Transition;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }

            if (raise_started_after_transition && transition_track_started) {
                OnTrackStarted ();
            }

            raise_started_after_transition = false;
            transition_track_started = false;
        }

        protected virtual void OnSourceChanged ()
        {
            EventHandler handler = SourceChanged;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }

        protected void OnNextSourceChanged ()
        {
            EventHandler handler = NextSourceChanged;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }

        protected virtual void OnTrackStarted ()
        {
            EventHandler handler = TrackStarted;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }

        public TrackInfo CurrentTrack {
            get { return current_track; }
            protected set { current_track = value; }
        }

        public TrackInfo PriorTrack {
            get { return prior_track ?? CurrentTrack; }
            set { prior_track = value; }
        }

        protected DateTime source_set_at = DateTime.MinValue;
        public ITrackModelSource Source {
            get {
                if (source == null && ServiceManager.SourceManager.DefaultSource is ITrackModelSource) {
                    return (ITrackModelSource)ServiceManager.SourceManager.DefaultSource;
                }
                return source;
            }

            set {
                if (source != value) {
                    NextSource = value;
                    source = value;
                    source_set_at = DateTime.Now;
                    OnSourceChanged ();
                }
            }
        }

        public ITrackModelSource NextSource {
            get { return next_source ?? Source; }
            set {
                if (next_source != value) {
                    next_source = value;
                    OnNextSourceChanged ();

                    if (!player_engine.IsPlaying ()) {
                        Source = next_source;
                    }
                }
            }
        }

        public string ShuffleMode {
            get { return shuffle_mode; }
            set {
                shuffle_mode = value;

                // If the user changes the shuffle mode, she expects the "Next"
                // button to behave according to the new selection. See bgo#528809
                next_stack.Clear ();

                var handler = ShuffleModeChanged;
                if (handler != null) {
                    handler (this, new EventArgs<string> (shuffle_mode));
                }
            }
        }

        string prev_shuffle;
        public void ToggleShuffle ()
        {
            if (ShuffleMode == "off") {
                ShuffleMode = prev_shuffle ?? "song";
            } else {
                prev_shuffle = ShuffleMode;
                ShuffleMode = "off";
            }
        }

        public PlaybackRepeatMode RepeatMode {
            get { return repeat_mode; }
            set {
                repeat_mode = value;
                EventHandler<EventArgs<PlaybackRepeatMode>> handler = RepeatModeChanged;
                if (handler != null) {
                    handler (this, new EventArgs<PlaybackRepeatMode> (repeat_mode));
                }
            }
        }

        PlaybackRepeatMode? prev_repeat;
        public void ToggleRepeat ()
        {
            if (RepeatMode == PlaybackRepeatMode.None) {
                RepeatMode = prev_repeat != null ? prev_repeat.Value : PlaybackRepeatMode.RepeatAll;
            } else {
                prev_repeat = RepeatMode;
                RepeatMode = PlaybackRepeatMode.None;
            }
        }

        public bool StopWhenFinished {
            get { return stop_when_finished; }
            set { stop_when_finished = value; }
        }

        string IService.ServiceName {
            get { return "PlaybackController"; }
        }

        IDBusExportable IDBusExportable.Parent {
            get { return null; }
        }
    }
}
