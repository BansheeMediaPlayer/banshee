//
// PlayerEngineService.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2006-2008 Novell, Inc.
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
using System.IO;
using System.Collections.Generic;
using System.Reflection;

using Mono.Unix;
using Mono.Addins;

using Hyena;
using Banshee.Base;
using Banshee.Streaming;
using Banshee.ServiceStack;
using Banshee.Metadata;
using Banshee.Configuration;
using Banshee.Collection;
using Banshee.Equalizer;

namespace Banshee.MediaEngine
{
    public delegate bool TrackInterceptHandler (TrackInfo track);

    public class PlayerEngineService : IInitializeService, IDelayedInitializeService, 
        IRequiredService, IPlayerEngineService, IDisposable
    {   
        private List<PlayerEngine> engines = new List<PlayerEngine> ();
        private PlayerEngine active_engine;
        private PlayerEngine default_engine;
        private PlayerEngine pending_engine;
        private object pending_playback_for_not_ready;
        private bool pending_playback_for_not_ready_play;
        private TrackInfo synthesized_contacting_track;

        private string preferred_engine_id = null;

        public event EventHandler PlayWhenIdleRequest;
        public event TrackInterceptHandler TrackIntercept;
        public event Action<PlayerEngine> EngineBeforeInitialize;
        public event Action<PlayerEngine> EngineAfterInitialize;
        
        private event DBusPlayerEventHandler dbus_event_changed;
        event DBusPlayerEventHandler IPlayerEngineService.EventChanged {
            add { dbus_event_changed += value; }
            remove { dbus_event_changed -= value; }
        }

        private event DBusPlayerStateHandler dbus_state_changed;
        event DBusPlayerStateHandler IPlayerEngineService.StateChanged {
            add { dbus_state_changed += value; }
            remove { dbus_state_changed -= value; }
        }
        
        public PlayerEngineService ()
        {
        }
        
        void IInitializeService.Initialize ()
        {
            preferred_engine_id = EngineSchema.Get();
            
            if (default_engine == null && engines.Count > 0) {
                default_engine = engines[0];
            }
            
            foreach (TypeExtensionNode node in AddinManager.GetExtensionNodes ("/Banshee/MediaEngine/PlayerEngine")) {
                LoadEngine (node);
            }
            
            if (default_engine != null) {
                active_engine = default_engine;
                Log.Debug (Catalog.GetString ("Default player engine"), active_engine.Name);
            } else {
                default_engine = active_engine;
            }
            
            if (default_engine == null || active_engine == null || engines == null || engines.Count == 0) {
                Log.Warning (Catalog.GetString (
                    "No player engines were found. Please ensure Banshee has been cleanly installed."),
                    "Using the featureless NullPlayerEngine.");
                PlayerEngine null_engine = new NullPlayerEngine ();
                LoadEngine (null_engine);
                active_engine = null_engine;
                default_engine = null_engine;
            }
            
            MetadataService.Instance.HaveResult += OnMetadataServiceHaveResult;
            
            TrackInfo.IsPlayingMethod = track => IsPlaying (track) &&
                track.CacheModelId == CurrentTrack.CacheModelId &&
                (track.CacheEntryId == null || track.CacheEntryId.Equals (CurrentTrack.CacheEntryId));
        }

        private void InitializeEngine (PlayerEngine engine)
        {
            var handler = EngineBeforeInitialize;
            if (handler != null) {
                handler (engine);
            }
            
            engine.Initialize ();
            engine.IsInitialized = true;
            
            handler = EngineAfterInitialize;
            if (handler != null) {
                handler (engine);
            }
        }
        
        void IDelayedInitializeService.DelayedInitialize ()
        {
            foreach (var engine in Engines) {
                if (engine.DelayedInitialize) {
                    InitializeEngine (engine);
                }
            }
        }
        
        private void LoadEngine (TypeExtensionNode node)
        {
            LoadEngine ((PlayerEngine) node.CreateInstance (typeof (PlayerEngine)));
        }
        
        private void LoadEngine (PlayerEngine engine)
        {
            if (!engine.DelayedInitialize) {
                InitializeEngine (engine);
            }
            
            engine.EventChanged += OnEngineEventChanged;

            if (engine.Id == preferred_engine_id) {
                DefaultEngine = engine;
            } else {
                if (active_engine == null) {
                    active_engine = engine;
                }
                engines.Add (engine);
            }
        }

        public void Dispose ()
        {
            MetadataService.Instance.HaveResult -= OnMetadataServiceHaveResult;
            
            foreach (PlayerEngine engine in engines) {
                engine.Dispose ();
            }
            
            active_engine = null;
            default_engine = null;
            pending_engine = null;
            
            preferred_engine_id = null;
            
            engines.Clear ();
        }
        
        private void OnMetadataServiceHaveResult (object o, MetadataLookupResultArgs args)
        {
            if (CurrentTrack != null && args.Track == CurrentTrack) {
                foreach (StreamTag tag in args.ResultTags) {
                    StreamTagger.TrackInfoMerge (CurrentTrack, tag);
                }
                
                OnEngineEventChanged (new PlayerEventArgs (PlayerEvent.TrackInfoUpdated));
            }
        }
        
        private void HandleStateChange (PlayerEventStateChangeArgs args)
        {
            if (args.Current == PlayerState.Loaded && CurrentTrack != null) {
                active_engine.Volume = (ushort) VolumeSchema.Get ();
                MetadataService.Instance.Lookup (CurrentTrack);
            } else if (args.Current == PlayerState.Ready) {
                // Enable our preferred equalizer if it exists and was enabled last time.
                if (SupportsEqualizer && EqualizerSetting.EnabledSchema.Get ()) {
                    string name = EqualizerSetting.PresetSchema.Get();
                    if (!String.IsNullOrEmpty (name)) {
                        // Don't use EqualizerManager.Instance - used by the eq dialog window.
                        EqualizerManager manager = new EqualizerManager (EqualizerManager.Instance.Path);
                        manager.Load ();
                        EqualizerSetting equalizer = null;
                        foreach (EqualizerSetting eq in manager) {
                            if (eq.Name == name) {
                                equalizer = eq;
                                break;
                            }
                        }
                        
                        if (equalizer != null) {
                            Log.DebugFormat ("Enabling equalizer preset: {0}", equalizer.Name);
                            manager.Enable (equalizer);
                        }
                    }
                }

                if (pending_playback_for_not_ready != null) {
                    OpenCheck (pending_playback_for_not_ready, pending_playback_for_not_ready_play);
                    pending_playback_for_not_ready = null;
                    pending_playback_for_not_ready_play = false;
                }
            }
            
            DBusPlayerStateHandler dbus_handler = dbus_state_changed;
            if (dbus_handler != null) {
                dbus_handler (args.Current.ToString ().ToLower ());
            }
        }

        private void OnEngineEventChanged (PlayerEventArgs args)
        {
            if (CurrentTrack != null) {
                if (args.Event == PlayerEvent.Error 
                    && CurrentTrack.PlaybackError == StreamPlaybackError.None) {
                    CurrentTrack.SavePlaybackError (StreamPlaybackError.Unknown);
                } else if (args.Event == PlayerEvent.Iterate 
                    && CurrentTrack.PlaybackError != StreamPlaybackError.None) {
                    CurrentTrack.SavePlaybackError (StreamPlaybackError.None);
                }
            }
            
            RaiseEvent (args);
            
            // Do not raise iterate across DBus to avoid so many calls;
            // DBus clients should do their own iterating and 
            // event/state checking locally
            if (args.Event == PlayerEvent.Iterate) {
                return;
            }
            
            DBusPlayerEventHandler dbus_handler = dbus_event_changed;
            if (dbus_handler != null) {
                dbus_handler (args.Event.ToString ().ToLower (), 
                    args is PlayerEventErrorArgs ? ((PlayerEventErrorArgs)args).Message : String.Empty, 
                    args is PlayerEventBufferingArgs ? ((PlayerEventBufferingArgs)args).Progress : 0
                );
            }
        }
        
        private void OnPlayWhenIdleRequest ()
        {
            EventHandler handler = PlayWhenIdleRequest;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }
        
        private bool OnTrackIntercept (TrackInfo track)
        {
            TrackInterceptHandler handler = TrackIntercept;
            if (handler == null) {
                return false;
            }
            
            bool handled = false;
            
            foreach (TrackInterceptHandler single_handler in handler.GetInvocationList ()) {
                handled |= single_handler (track);
            }
            
            return handled;
        }
        
        public void Open (TrackInfo track)
        {
            OpenPlay (track, false);
        }
        
        public void Open (SafeUri uri)
        {
            OpenCheck (uri);
        }
        
        void IPlayerEngineService.Open (string uri)
        {
            OpenCheck (new SafeUri (uri));
        }
        
        public void OpenPlay (TrackInfo track)
        {
            OpenPlay (track, true);
        }
        
        private void OpenPlay (TrackInfo track, bool play)
        {
            if (track == null || !track.CanPlay || OnTrackIntercept (track)) {
                return;
            }
        
            try {
                OpenCheck (track, true);
            } catch (Exception e) {
                Log.Exception (e);
                Log.Error (Catalog.GetString ("Problem with Player Engine"), e.Message, true);
                Close ();
                ActiveEngine = default_engine;
            }
        }

        private void OpenCheck (object o)
        {
            OpenCheck (o, false);
        }
        
        private void OpenCheck (object o, bool play)
        {
            if (CurrentState == PlayerState.NotReady) {
                pending_playback_for_not_ready = o;
                pending_playback_for_not_ready_play = play;
                return;
            }
        
            SafeUri uri = null;
            TrackInfo track = null;
        
            if (o is SafeUri) {
                uri = (SafeUri)o;
            } else if (o is TrackInfo) {
                track = (TrackInfo)o;
                uri = track.Uri;
            } else {
                return;
            }

            IncrementLastPlayed ();
            
            FindSupportingEngine (uri);
            CheckPending ();
            
            if (track != null) {
                active_engine.Open (track);
                incremented_last_played = false;
            } else if (uri != null) {
                active_engine.Open (uri);
                incremented_last_played = false;
            }

            if (play) {
                active_engine.Play ();
            }
        }

        private bool incremented_last_played = true;
        public void IncrementLastPlayed ()
        {
            if (!incremented_last_played && CurrentTrack != null && CurrentTrack.PlaybackError == StreamPlaybackError.None) {
                //if Length <= 0 assume 100% completion:
                if (active_engine.Length <= 0) {
                    CurrentTrack.OnPlaybackFinished (1);
                } else {
                    CurrentTrack.OnPlaybackFinished ((double)active_engine.Position / (double)active_engine.Length);
                }
                incremented_last_played = true;
            }
        }
        
        private void FindSupportingEngine (SafeUri uri)
        {
            foreach (PlayerEngine engine in engines) {
                foreach (string extension in engine.ExplicitDecoderCapabilities) {
                    if (!uri.AbsoluteUri.EndsWith (extension)) {
                        continue;
                    } else if (active_engine != engine) {
                        Close ();
                        pending_engine = engine;
                        Log.DebugFormat ("Switching engine to: {0}", engine.GetType ());
                    }
                    return;
                }
            }
        
            foreach (PlayerEngine engine in engines) {
                foreach (string scheme in engine.SourceCapabilities) {
                    bool supported = scheme == uri.Scheme;
                    if (supported && active_engine != engine) {
                        Close ();
                        pending_engine = engine;
                        Log.DebugFormat ("Switching engine to: {0}", engine.GetType ());
                        return;
                    } else if (supported) {
                        return;
                    }
                }
            }
        }
        
        public void Close ()
        {
            Close (false);
        }
        
        public void Close (bool fullShutdown)
        {
            IncrementLastPlayed ();
            active_engine.Reset ();
            active_engine.Close (fullShutdown);
        }
        
        public void Play ()
        {
            if (CurrentState == PlayerState.Idle) {
                OnPlayWhenIdleRequest ();
            } else {
                active_engine.Play ();
            }
        }
        
        public void Pause ()
        {
            if (!CanPause) {
                Close ();
            } else {
                active_engine.Pause ();
            }
        }

        // For use by RadioTrackInfo
        // TODO remove this method once RadioTrackInfo playlist downloading/parsing logic moved here?
        internal void StartSynthesizeContacting (TrackInfo track)
        {
            //OnStateChanged (PlayerState.Contacting);
            RaiseEvent (new PlayerEventStateChangeArgs (CurrentState, PlayerState.Contacting));
            synthesized_contacting_track = track;
        }

        internal void EndSynthesizeContacting (TrackInfo track, bool idle)
        {
            if (track == synthesized_contacting_track) {
                synthesized_contacting_track = null;

                if (idle) {
                    RaiseEvent (new PlayerEventStateChangeArgs (PlayerState.Contacting, PlayerState.Idle));
                }
            }
        }
        
        public void TogglePlaying ()
        {
            if (IsPlaying () && CurrentState != PlayerState.Paused) {
                Pause ();
            } else if (CurrentState != PlayerState.NotReady) {
                Play ();
            }
        }
        
        public void VideoExpose (IntPtr displayContext, bool direct)
        {
            active_engine.VideoExpose (displayContext, direct);
        }
        
        public IntPtr VideoDisplayContext {
            set { active_engine.VideoDisplayContext = value; }
            get { return active_engine.VideoDisplayContext; }
        }
        
        public void TrackInfoUpdated ()
        {
            active_engine.TrackInfoUpdated ();
        }
        
        public bool IsPlaying (TrackInfo track)
        {
            return IsPlaying () && track != null && track.TrackEqual (CurrentTrack);
        }
        
        public bool IsPlaying ()
        {
            return CurrentState == PlayerState.Playing || 
                CurrentState == PlayerState.Paused || 
                CurrentState == PlayerState.Loaded ||
                CurrentState == PlayerState.Loading ||
                CurrentState == PlayerState.Contacting;
        }

        private void CheckPending ()
        {
            if(pending_engine != null && pending_engine != active_engine) {
                if(active_engine.CurrentState == PlayerState.Idle) {
                    Close ();
                }
                
                active_engine = pending_engine;
                pending_engine = null;
            } 
        }
    
        public TrackInfo CurrentTrack {
            get { return active_engine.CurrentTrack ?? synthesized_contacting_track; }
        }
        
        private Dictionary<string, object> dbus_sucks;
        IDictionary<string, object> IPlayerEngineService.CurrentTrack {
            get { 
                // FIXME: Managed DBus sucks - it explodes if you transport null
                // or even an empty dictionary (a{sv} in our case). Piece of shit.
                if (dbus_sucks == null) {
                    dbus_sucks = new Dictionary<string, object> ();
                    dbus_sucks.Add (String.Empty, String.Empty);
                }
                
                return CurrentTrack == null ? dbus_sucks : CurrentTrack.GenerateExportable ();
            }
        }
        
        public SafeUri CurrentSafeUri {
            get { return active_engine.CurrentUri; }
        }
        
        string IPlayerEngineService.CurrentUri {
            get { return CurrentSafeUri == null ? String.Empty : CurrentSafeUri.AbsoluteUri; }
        }
        
        public PlayerState CurrentState {
            get { return synthesized_contacting_track != null ? PlayerState.Contacting : active_engine.CurrentState; }
        }
        
        string IPlayerEngineService.CurrentState {
            get { return CurrentState.ToString ().ToLower (); }
        }
        
        public PlayerState LastState {
            get { return active_engine.LastState; }
        }
        
        string IPlayerEngineService.LastState {
            get { return LastState.ToString ().ToLower (); }
        }
        
        public ushort Volume {
            get { return active_engine.Volume; }
            set { 
                foreach (PlayerEngine engine in engines) {
                    engine.Volume = value;
                }
            }
        }
        
        public uint Position {
            get { return active_engine.Position; }
            set { active_engine.Position = value; }
        }
        
        public bool CanSeek {
            get { return active_engine.CanSeek; }
        }
        
        public bool CanPause {
            get { return CurrentTrack != null && !CurrentTrack.IsLive; }
        }
        
        public bool SupportsEqualizer {
            get { return ((active_engine is IEqualizer) && active_engine.SupportsEqualizer); }
        }
        
        public VideoDisplayContextType VideoDisplayContextType {
            get { return active_engine.VideoDisplayContextType; }
        }
        
        public uint Length {
            get { 
                uint length = active_engine.Length;
                if (length > 0) {
                    return length;
                } else if (CurrentTrack == null) {
                    return 0;
                }
                
                return (uint) CurrentTrack.Duration.TotalSeconds;
            }
        }
    
        public PlayerEngine ActiveEngine {
            get { return active_engine; }
            set { pending_engine = value; }
        }
        
        public PlayerEngine DefaultEngine {
            get { return default_engine; }
            set { 
                if (engines.Contains (value)) {
                    engines.Remove (value);
                }
                
                engines.Insert (0, value);
            
                default_engine = value;
                EngineSchema.Set (value.Id);
            }
        }
        
        public IEnumerable<PlayerEngine> Engines {
            get { return engines; }
        }
        
#region Player Event System

        private LinkedList<PlayerEventHandlerSlot> event_handlers = new LinkedList<PlayerEventHandlerSlot> ();
        
        private struct PlayerEventHandlerSlot
        {
            public PlayerEvent EventMask;
            public PlayerEventHandler Handler;
            
            public PlayerEventHandlerSlot (PlayerEvent mask, PlayerEventHandler handler)
            {
                EventMask = mask;
                Handler = handler;
            }
        }
        
        private const PlayerEvent event_all_mask = PlayerEvent.Iterate
            | PlayerEvent.StateChange
            | PlayerEvent.StartOfStream
            | PlayerEvent.EndOfStream
            | PlayerEvent.Buffering
            | PlayerEvent.Seek
            | PlayerEvent.Error
            | PlayerEvent.Volume
            | PlayerEvent.Metadata
            | PlayerEvent.TrackInfoUpdated;
        
        private const PlayerEvent event_default_mask = event_all_mask & ~PlayerEvent.Iterate;
        
        private static void VerifyEventMask (PlayerEvent eventMask)
        {
            if (eventMask <= PlayerEvent.None || eventMask > event_all_mask) {
                throw new ArgumentOutOfRangeException ("eventMask", "A valid event mask must be provided");
            }
        }
        
        public void ConnectEvent (PlayerEventHandler handler)
        {
            ConnectEvent (handler, event_default_mask, false);
        }
        
        public void ConnectEvent (PlayerEventHandler handler, PlayerEvent eventMask)
        {
            ConnectEvent (handler, eventMask, false);
        }
        
        public void ConnectEvent (PlayerEventHandler handler, bool connectAfter)
        {
            ConnectEvent (handler, event_default_mask, connectAfter);
        }
        
        public void ConnectEvent (PlayerEventHandler handler, PlayerEvent eventMask, bool connectAfter)
        {
            lock (event_handlers) {
                VerifyEventMask (eventMask);
            
                PlayerEventHandlerSlot slot = new PlayerEventHandlerSlot (eventMask, handler);
                
                if (connectAfter) {
                    event_handlers.AddLast (slot);
                } else {
                    event_handlers.AddFirst (slot);
                }
            }
        }
        
        private LinkedListNode<PlayerEventHandlerSlot> FindEventNode (PlayerEventHandler handler)
        {
            LinkedListNode<PlayerEventHandlerSlot> node = event_handlers.First;
            while (node != null) {
                if (node.Value.Handler == handler) {
                    return node;
                }
                node = node.Next;
            }
            
            return null;
        }
        
        public void DisconnectEvent (PlayerEventHandler handler)
        {
            lock (event_handlers) {
                LinkedListNode<PlayerEventHandlerSlot> node = FindEventNode (handler);
                if (node != null) {
                    event_handlers.Remove (node);
                }
            }
        }
        
        public void ModifyEvent (PlayerEvent eventMask, PlayerEventHandler handler)
        {
            lock (event_handlers) {
                VerifyEventMask (eventMask);
                
                LinkedListNode<PlayerEventHandlerSlot> node = FindEventNode (handler);
                if (node != null) {
                    PlayerEventHandlerSlot slot = node.Value;
                    slot.EventMask = eventMask;
                    node.Value = slot;
                }
            }
        }
        
        private void RaiseEvent (PlayerEventArgs args)
        {
            lock (event_handlers) {
                if (args.Event == PlayerEvent.StateChange && args is PlayerEventStateChangeArgs) {
                    HandleStateChange ((PlayerEventStateChangeArgs)args);
                }
            
                LinkedListNode<PlayerEventHandlerSlot> node = event_handlers.First;
                while (node != null) {
                    if ((node.Value.EventMask & args.Event) == args.Event) {
                        node.Value.Handler (args);
                    }
                    node = node.Next;
                }
            }
        }

#endregion
        
        string IService.ServiceName {
            get { return "PlayerEngine"; }
        }
        
        IDBusExportable IDBusExportable.Parent { 
            get { return null; }
        }
        
        public static readonly SchemaEntry<int> VolumeSchema = new SchemaEntry<int> (
            "player_engine", "volume",
            80,
            "Volume",
            "Volume of playback relative to mixer output"
        );

        public static readonly SchemaEntry<string> EngineSchema = new SchemaEntry<string> (
            "player_engine", "backend",
            "helix-remote",
            "Backend",
            "Name of media playback engine backend"
        );
    }
}
