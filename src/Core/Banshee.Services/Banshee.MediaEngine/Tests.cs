//
// Tests.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
//   Andrés G. Aragoneses <knocte@gmail.com>
//
// Copyright (C) 2010 Novell, Inc.
// Copyright (C) 2014 Andrés G. Aragoneses
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

#if ENABLE_TESTS

using System;
using System.Linq;
using System.Threading;

using NUnit.Framework;

using Hyena;
using Hyena.Tests;

using Banshee.Collection.Database;
using Banshee.Collection;
using Banshee.ServiceStack;

namespace Banshee.MediaEngine
{
    [TestFixture]
    public class Tests : TestBase
    {
        PlayerEngineService service;
        Random rand = new Random ();

        [Test]
        public void TestMediaEngineService ()
        {
            AssertTransition (null, () => service.Volume = 5, PlayerEvent.Volume);

            for (int i = 0; i < 3; i++) {
                WaitFor (PlayerState.Idle);

                // Assert the default, just-started-up idle state
                Assert.IsFalse (service.IsPlaying ());
                Assert.AreEqual (null, service.CurrentTrack);
                Assert.AreEqual (null, service.CurrentSafeUri);

                LoadAndPlay ("A_boy.ogg");
                Assert.AreEqual (0, service.CurrentTrack.PlayCount);

                for (int j = 0; j < 4; j++) {
                    AssertTransition (() => service.Pause (), PlayerState.Paused);
                    AssertTransition (() => service.Play (), PlayerState.Playing);
                    Assert.IsTrue (service.IsPlaying ());
                    Thread.Sleep ((int) (rand.NextDouble () * 100));
                }

                AssertTransition (() => service.Position = service.Length - 200, PlayerEvent.Seek);

                WaitFor (PlayerState.Idle, PlayerEvent.EndOfStream);
                Assert.AreEqual (1, service.CurrentTrack.PlayCount);

                service.Close (true);
            }

            play_when_idles = 0;
            Assert.AreEqual (PlayerState.Idle, service.CurrentState);
            service.Play ();
            Thread.Sleep (50);
            Assert.AreEqual (1, play_when_idles);
            Assert.AreEqual (PlayerState.Idle, service.CurrentState);

            LoadAndPlay ("A_boy.ogg");
            AssertTransition (() => service.TrackInfoUpdated (), PlayerEvent.TrackInfoUpdated);
            LoadAndPlay ("A_girl.ogg");
            AssertTransition (() => service.TrackInfoUpdated (), PlayerEvent.TrackInfoUpdated);
        }

        [Test] // https://bugzilla.gnome.org/show_bug.cgi?id=722731
        public void TestThatInvalidTracksShouldNotBecomeCurrentTrack ()
        {
            var a_valid_track = "A_boy.ogg";
            var a_valid_uri = new SafeUri (Paths.Combine (TestsDir, "data", a_valid_track));

            var a_track_that_doesnt_exit = "this_does_not_exist_in_the_data_folder.ogg";
            var an_invalid_track = new SafeUri (Paths.Combine (TestsDir, "data", a_track_that_doesnt_exit));

            service.Open (a_valid_uri);
            var current_track = service.CurrentTrack;
            Assert.IsNotNull (current_track);
            Assert.IsTrue (current_track.Uri.AbsolutePath.EndsWith (a_valid_track));

            service.Play ();
            current_track = service.CurrentTrack;
            Assert.IsNotNull (current_track);
            Assert.IsTrue (current_track.Uri.AbsolutePath.EndsWith (a_valid_track));

            service.SetNextTrack (an_invalid_track);
            WaitUntil (PlayerState.Idle);
            Assert.IsNull (service.CurrentTrack);

            service.Open (a_valid_uri);
            service.Play ();
            WaitUntil (PlayerEvent.StartOfStream);

            current_track = service.CurrentTrack;
            Assert.IsNotNull (current_track);
            var actual_filename = System.IO.Path.GetFileName (SafeUri.UriToFilename (current_track.Uri));
            Assert.AreEqual (a_valid_track, actual_filename);
        }

        private void LoadAndPlay (string filename)
        {
            track_intercepts = 0;
            var uri = new SafeUri (Paths.Combine (TestsDir, "data", filename));
            var states = service.IsPlaying () ? new object [] { PlayerState.Paused, PlayerState.Idle, PlayerState.Loading } : new object [] { PlayerState.Loading };
            //var states = service.IsPlaying () ? new object [] { PlayerState.Paused, PlayerState.Loading } : new object [] { PlayerState.Loading };
            Log.DebugFormat ("LoadAndPlaying {0}", filename);
            if (rand.NextDouble () > .5) {
                AssertTransition (() => service.Open (new TrackInfo () { Uri = uri }), states);
            } else {
                AssertTransition (() => service.Open (uri), states);
            }
            Assert.AreEqual (1, track_intercepts);

            // Sleep just a bit to ensure we didn't change from Loading
            Thread.Sleep (30);
            Assert.AreEqual (PlayerState.Loading, service.CurrentState);

            // Assert conditions after Opening (but not actually playing) a track
            Assert.AreEqual (uri, service.CurrentSafeUri);
            Assert.IsTrue (service.CanPause);
            Assert.IsTrue (service.IsPlaying ());
            Assert.IsTrue (service.Position == 0);
            Assert.IsTrue (service.IsPlaying (service.CurrentTrack));

            AssertTransition (() => service.Play (),
                PlayerState.Loaded, PlayerEvent.StartOfStream, PlayerState.Playing);
            Assert.IsTrue (service.Length > 0);
        }

        private void WaitFor (PlayerState state)
        {
            WaitFor (null, state);
        }

        private void WaitFor (System.Action action, PlayerState state)
        {
            WaitFor (default_ignore, action, state);
        }

        private void WaitFor (System.Func<PlayerState?, PlayerEvent?, bool> ignore, System.Action action, PlayerState state)
        {
            if (service.CurrentState != state) {
                AssertTransition (ignore, action, state);
            } else if (action != null) {
                Assert.Fail (String.Format ("Already in state {0} before invoking action", state));
            }
        }

        private void WaitFor (params object [] states)
        {
            WaitFor (default_ignore, states);
        }

        private void WaitFor (System.Func<PlayerState?, PlayerEvent?, bool> ignore, params object [] states)
        {
            AssertTransition (ignore, null, states);
        }

        private void AssertTransition (System.Action action, params object [] states)
        {
            // By default, ignore volume events b/c the system/stream volume stuff seems to raise them at random times
            AssertTransition (default_ignore, action, states);
        }

        public System.Func<PlayerState?, PlayerEvent?, bool> default_ignore = new System.Func<PlayerState?, PlayerEvent?, bool> ((s, e) =>
            e != null && (e.Value == PlayerEvent.Volume || e.Value == PlayerEvent.RequestNextTrack)
        );

        private void AssertTransition (System.Func<PlayerState?, PlayerEvent?, bool> ignore, System.Action action, params object [] states)
        {
            Log.DebugFormat ("AssertTransition: {0}", String.Join (", ", states.Select (s => s.ToString ()).ToArray ()));
            int result_count = 0;
            var reset_event = new ManualResetEvent (false);
            var handler = new Action<PlayerEventArgs> (a => {
                lock (states) {
                    if (result_count < states.Length) {
                        var sca = a as PlayerEventStateChangeArgs;

                        var last_state = sca != null ? sca.Current : service.CurrentState;
                        var last_event = a.Event;

                        if (ignore != null && ignore (last_state, last_event)) {
                            Log.DebugFormat ("   > ignoring {0}/{1}", last_event, last_state);
                            return;
                        }

                        if (sca == null) {
                            Log.DebugFormat ("   > {0}", a.Event);
                        } else {
                            Log.DebugFormat ("   > {0}", last_state);
                        }

                        var evnt = (states[result_count] as PlayerEvent?) ?? PlayerEvent.StateChange;
                        var state = states[result_count] as PlayerState?;

                        var evnt_or_state_desc = (evnt == PlayerEvent.StateChange ?
                            evnt.ToString() + ">" + states[result_count].ToString () : evnt.ToString ());
                        var last_evnt_or_state_desc = (last_event == PlayerEvent.StateChange ?
                            last_event.ToString () + ">" + sca.Current.ToString () : last_event.ToString ());

                        Assert.That (last_event, Is.EqualTo (evnt),
                            String.Format ("Event[{0}] doesn't match (should be {1}, it was {2})",
                                           result_count, evnt_or_state_desc, last_evnt_or_state_desc));
                        if (state != null) {
                            Assert.That (last_state, Is.EqualTo (state), String.Format ("State[{0}] doesn't match", result_count));
                        }

                        result_count++;
                    }
                }
                reset_event.Set ();
            });

            AssertionException exception = null;
            var guarded_handler = new PlayerEventHandler (args => {
                try {
                    handler (args);
                } catch (AssertionException ae) {
                    exception = ae;
                }
            });
            service.ConnectEvent (guarded_handler);

            if (action != null) action ();

            const int seconds = 3;
            while (result_count < states.Length) {
                reset_event.Reset ();
                if (!reset_event.WaitOne (TimeSpan.FromSeconds (seconds))) {
                    Assert.Fail (String.Format ("Waited {0}s for state/event, didn't happen", seconds));
                    break;
                }
                if (exception != null) {
                    throw new AssertionException (exception.Message, exception);
                }
            }

            service.DisconnectEvent (guarded_handler);
        }

        //[Test]
        //public void TestMediaEngines ()
        //{
            //  * TrackInfoUpdated ()
            //  * CurrentTrack
            //  * CurrentState
            //  * LastState
            //  * Volume
            //  * CanSeek
            //  * Position
            //  * Length
        //}


        /*public void AssertEvent (string evnt)
        {
            var evnt_obj = service.
        }*/

            /* TODO: test:
                public event EventHandler PlayWhenIdleRequest;
                public event TrackInterceptHandler TrackIntercept;
                public event Action<PlayerEngine> EngineBeforeInitialize;
                public event Action<PlayerEngine> EngineAfterInitialize;
                public PlayerEngineService ()
        public void Dispose ()
        public void Open (TrackInfo track)
        public void Open (SafeUri uri)
        public void SetNextTrack (TrackInfo track)
        public void SetNextTrack (SafeUri uri)
        public void OpenPlay (TrackInfo track)
        public void IncrementLastPlayed ()
        public void IncrementLastPlayed (double completed)
        public void Close ()
        public void Close (bool fullShutdown)
        public void Play ()
        public void Pause ()
        public void TogglePlaying ()
        public void TrackInfoUpdated ()
        public bool IsPlaying (TrackInfo track)
        public bool IsPlaying ()
        public TrackInfo CurrentTrack {
        public SafeUri CurrentSafeUri {
        public PlayerState CurrentState {
        public PlayerState LastState {
        public ushort Volume {
        public uint Position {
        public byte Rating {
        public bool CanSeek {
        public bool CanPause {
        public bool SupportsEqualizer {
        public uint Length {
        public PlayerEngine ActiveEngine {
        public PlayerEngine DefaultEngine {
        public IEnumerable<PlayerEngine> Engines {
        public void ConnectEvent (PlayerEventHandler handler)
        public void ConnectEvent (PlayerEventHandler handler, PlayerEvent eventMask)
        public void ConnectEvent (PlayerEventHandler handler, bool connectAfter)
        public void ConnectEvent (PlayerEventHandler handler, PlayerEvent eventMask, bool connectAfter)
        public void DisconnectEvent (PlayerEventHandler handler)
        public void ModifyEvent (PlayerEvent eventMask, PlayerEventHandler handler)
        */

        private void WaitUntil (PlayerEvent @event)
        {
            WaitUntil (@event, null);
        }

        private void WaitUntil (PlayerState state)
        {
            WaitUntil (null, state);
        }

        private void WaitUntil (PlayerEvent? @event, PlayerState? state)
        {
            if (@event == null && state == null) {
                throw new ArgumentException ("Event or state must be non-null");
            }
            if (@event != null && state != null) {
                throw new ArgumentException ("Event and state cannot be both non-null");
            }

            Exception exception = null;
            PlayerEvent? last_event = null;
            PlayerState? last_state = null;

            var reset_event = new ManualResetEvent (false);
            var handler = new PlayerEventHandler (a => {
                try {
                    var sca = a as PlayerEventStateChangeArgs;
                    last_state = sca != null ? sca.Current : service.CurrentState;
                    last_event = a.Event;
                    reset_event.Set ();
                } catch (Exception ex) {
                    exception = ex;
                }
            });
            service.ConnectEvent (handler);

            const int seconds = 3;
            int max_count = 10;

            string event_or_state_desc = @event != null ? @event.ToString () : state.ToString ();

            Func<bool> matches = () =>
                (@event != null && @event.Value.Equals (last_event.Value))
                || (state != null && state.Value.Equals (last_state.Value));

            do {
                reset_event.Reset ();
                if (!reset_event.WaitOne (TimeSpan.FromSeconds (seconds))) {
                    Assert.Fail (String.Format ("Waited {0}s for state/event, didn't happen", seconds));
                    break;
                }
                if (exception != null) {
                    throw exception;
                }
                if (max_count == 0) {
                    Assert.Fail (String.Format ("Many events/states happened, but not {0}", event_or_state_desc));
                } else {
                    max_count--;
                }
            } while (!matches ());

            service.DisconnectEvent (handler);
        }


        Thread main_thread;
        GLib.MainLoop main_loop;
        bool started;

        [TestFixtureSetUp]
        public void Setup ()
        {
            GLib.GType.Init ();
            if (!GLib.Thread.Supported) {
                GLib.Thread.Init ();
            }

            ApplicationContext.Debugging = false;
            //Log.Debugging = true;
            Application.TimeoutHandler = RunTimeout;
            Application.IdleHandler = RunIdle;
            Application.IdleTimeoutRemoveHandler = IdleTimeoutRemove;
            Application.Initialize ();

            Mono.Addins.AddinManager.Initialize (BinDir);

            main_thread = new Thread (RunMainLoop);
            main_thread.Start ();
            while (!started) {}
        }

        [TestFixtureTearDown]
        public void Teardown ()
        {
            AssertTransition (() => service.Dispose (), PlayerState.Paused, PlayerState.Idle);

            GLib.Idle.Add (delegate { main_loop.Quit (); return false; });
            main_thread.Join ();
            main_thread = null;
        }

        int play_when_idles = 0;
        int track_intercepts = 0;

        private void RunMainLoop ()
        {
            ThreadAssist.InitializeMainThread ();
            ThreadAssist.ProxyToMainHandler = Banshee.ServiceStack.Application.Invoke;

            service = new PlayerEngineService ();

            service.PlayWhenIdleRequest += delegate { play_when_idles++; };
            service.TrackIntercept += delegate { track_intercepts++; return false; };

            // TODO call each test w/ permutations of Gapless enabled/disabled, RG enabled/disabled

            try {
                ServiceManager.RegisterService (service);
            } catch {}

            ((IInitializeService)service).Initialize ();
            ((IDelayedInitializeService)service).DelayedInitialize ();

            main_loop = new GLib.MainLoop ();
            started = true;
            main_loop.Run ();
        }

        protected uint RunTimeout (uint milliseconds, TimeoutHandler handler)
        {
            return GLib.Timeout.Add (milliseconds, delegate { return handler (); });
        }

        protected uint RunIdle (IdleHandler handler)
        {
            return GLib.Idle.Add (delegate { return handler (); });
        }

        protected bool IdleTimeoutRemove (uint id)
        {
            return GLib.Source.Remove (id);
        }
    }
}

#endif
