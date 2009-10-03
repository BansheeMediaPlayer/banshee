// 
// PlayerEngine.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2005-2007 Novell, Inc.
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
using System.Runtime.InteropServices;
using System.Threading;
using Mono.Unix;
using Hyena;
using Hyena.Data;

using Banshee.Base;
using Banshee.Streaming;
using Banshee.MediaEngine;
using Banshee.ServiceStack;
using Banshee.Configuration;
using Banshee.Preferences;

namespace Banshee.GStreamer
{
    internal enum GstState
    {
        VoidPending = 0,
        Null = 1,
        Ready = 2,
        Paused = 3,
        Playing = 4
    }

    internal delegate void BansheePlayerEosCallback (IntPtr player);
    internal delegate void BansheePlayerErrorCallback (IntPtr player, uint domain, int code, IntPtr error, IntPtr debug);
    internal delegate void BansheePlayerStateChangedCallback (IntPtr player, GstState old_state, GstState new_state, GstState pending_state);
    internal delegate void BansheePlayerIterateCallback (IntPtr player);
    internal delegate void BansheePlayerBufferingCallback (IntPtr player, int buffering_progress);
    internal delegate void BansheePlayerVisDataCallback (IntPtr player, int channels, int samples, IntPtr data, int bands, IntPtr spectrum);
    internal delegate void BansheePlayerNextTrackStartingCallback (IntPtr player);
    internal delegate void BansheePlayerAboutToFinishCallback (IntPtr player);
    internal delegate IntPtr VideoPipelineSetupHandler (IntPtr player, IntPtr bus);

    internal delegate void GstTaggerTagFoundCallback (IntPtr player, string tagName, ref GLib.Value value);
    
    public class PlayerEngine : Banshee.MediaEngine.PlayerEngine, 
        IEqualizer, IVisualizationDataSource, ISupportClutter
    {
        private uint GST_CORE_ERROR = 0;
        private uint GST_LIBRARY_ERROR = 0;
        private uint GST_RESOURCE_ERROR = 0;
        private uint GST_STREAM_ERROR = 0; 
    
        private HandleRef handle;
        
        private BansheePlayerEosCallback eos_callback;
        private BansheePlayerErrorCallback error_callback;
        private BansheePlayerStateChangedCallback state_changed_callback;
        private BansheePlayerIterateCallback iterate_callback;
        private BansheePlayerBufferingCallback buffering_callback;
        private BansheePlayerVisDataCallback vis_data_callback;
        private VideoPipelineSetupHandler video_pipeline_setup_callback;
        private GstTaggerTagFoundCallback tag_found_callback;
        private BansheePlayerNextTrackStartingCallback next_track_starting_callback;
        private BansheePlayerAboutToFinishCallback about_to_finish_callback;
        
        private bool buffering_finished;
        private int pending_volume = -1;
        private bool xid_is_set = false;

        private bool GaplessEnabled;
        private EventWaitHandle next_track_set;
        
        private event VisualizationDataHandler data_available = null;
        public event VisualizationDataHandler DataAvailable {
            add {
                if (value == null) {
                    return;
                } else if (data_available == null) {
                    bp_set_vis_data_callback (handle, vis_data_callback);
                }

                data_available += value;
            }
            
            remove {
                if (value == null) {
                    return;
                }

                data_available -= value;

                if (data_available == null) {
                    bp_set_vis_data_callback (handle, null);
                }
            }
        }

        protected override bool DelayedInitialize {
            get { return true; }
        }


        public PlayerEngine ()
        {
            IntPtr ptr = bp_new ();
            
            if (ptr == IntPtr.Zero) {
                throw new ApplicationException (Catalog.GetString ("Could not initialize GStreamer library"));
            }
            
            handle = new HandleRef (this, ptr);
            
            bp_get_error_quarks (out GST_CORE_ERROR, out GST_LIBRARY_ERROR, 
                out GST_RESOURCE_ERROR, out GST_STREAM_ERROR);
            
            eos_callback = new BansheePlayerEosCallback (OnEos);
            error_callback = new BansheePlayerErrorCallback (OnError);
            state_changed_callback = new BansheePlayerStateChangedCallback (OnStateChange);
            iterate_callback = new BansheePlayerIterateCallback (OnIterate);
            buffering_callback = new BansheePlayerBufferingCallback (OnBuffering);
            vis_data_callback = new BansheePlayerVisDataCallback (OnVisualizationData);
            video_pipeline_setup_callback = new VideoPipelineSetupHandler (OnVideoPipelineSetup);
            tag_found_callback = new GstTaggerTagFoundCallback (OnTagFound);
            next_track_starting_callback = new BansheePlayerNextTrackStartingCallback (OnNextTrackStarting);
            about_to_finish_callback = new BansheePlayerAboutToFinishCallback (OnAboutToFinish);

            bp_set_eos_callback (handle, eos_callback);
            bp_set_iterate_callback (handle, iterate_callback);
            bp_set_error_callback (handle, error_callback);
            bp_set_state_changed_callback (handle, state_changed_callback);
            bp_set_buffering_callback (handle, buffering_callback);
            bp_set_tag_found_callback (handle, tag_found_callback);
            bp_set_next_track_starting_callback (handle, next_track_starting_callback);
            bp_set_video_pipeline_setup_callback (handle, video_pipeline_setup_callback);

            next_track_set = new EventWaitHandle (false, EventResetMode.AutoReset);
        }
        
        protected override void Initialize ()
        {
            if (!bp_initialize_pipeline (handle)) {
                bp_destroy (handle);
                handle = new HandleRef (this, IntPtr.Zero);
                throw new ApplicationException (Catalog.GetString ("Could not initialize GStreamer library"));
            }

            OnStateChanged (PlayerState.Ready);
            
            if (pending_volume >= 0) {
                Volume = (ushort)pending_volume;
            }
            
            InstallPreferences ();
            ReplayGainEnabled = ReplayGainEnabledSchema.Get ();
            GaplessEnabled = GaplessEnabledSchema.Get ();

            if (GaplessEnabled) {
                bp_set_about_to_finish_callback (handle, about_to_finish_callback);
            }
        }
        
        public override void Dispose ()
        {
            UninstallPreferences ();
            base.Dispose ();
            bp_destroy (handle);
            handle = new HandleRef (this, IntPtr.Zero);
        }
        
        public override void Close (bool fullShutdown)
        {
            bp_stop (handle, fullShutdown);
            base.Close (fullShutdown);
        }
        
        protected override void OpenUri (SafeUri uri)
        {
            // The GStreamer engine can use the XID of the main window if it ever
            // needs to bring up the plugin installer so it can be transient to
            // the main window.
            if (!xid_is_set) {
                IPropertyStoreExpose service = ServiceManager.Get<IService> ("GtkElementsService") as IPropertyStoreExpose;
                if (service != null) {
                    bp_set_application_gdk_window (handle, service.PropertyStore.Get<IntPtr> ("PrimaryWindow.RawHandle"));
                }
                xid_is_set = true;
            }
                
            IntPtr uri_ptr = GLib.Marshaller.StringToPtrGStrdup (uri.AbsoluteUri);
            try {
                if (!bp_open (handle, uri_ptr)) {
                    throw new ApplicationException ("Could not open resource");
                }
            } finally {
                GLib.Marshaller.Free (uri_ptr);
            }
        }
        
        public override void Play ()
        {
            bp_play (handle);
        }
        
        public override void Pause ()
        {
            bp_pause (handle);
        }

        public override void SetNextTrackUri (SafeUri uri)
        {
            // If there isn't a next track for us, release the block on the about-to-finish callback.
            if (uri == null) {
                next_track_set.Set ();
                return;
            }            
            IntPtr uri_ptr = GLib.Marshaller.StringToPtrGStrdup (uri.AbsoluteUri);
            try {
                bp_set_next_track (handle, uri_ptr);
            } finally {
                GLib.Marshaller.Free (uri_ptr);
            }
            next_track_set.Set ();
        }
        
        public override void VideoExpose (IntPtr window, bool direct)
        {
            bp_video_window_expose (handle, window, direct);
        }

        public override IntPtr [] GetBaseElements ()
        {
            IntPtr [] elements = new IntPtr[3];
            
            if (bp_get_pipeline_elements (handle, out elements[0], out elements[1], out elements[2])) {
                return elements;
            }
            
            return null;
        }

        private void OnEos (IntPtr player)
        {
            Close (false);
            OnEventChanged (PlayerEvent.EndOfStream);
            OnEventChanged (PlayerEvent.RequestNextTrack);
        }

        private void OnNextTrackStarting (IntPtr player)
        {
            if (GaplessEnabled) {
                OnEventChanged (PlayerEvent.EndOfStream);
                OnEventChanged (PlayerEvent.StartOfStream);
            }
        }

        private void OnAboutToFinish (IntPtr player)
        {
            // This is needed to make Shuffle-by-* work.
            // Shuffle-by-* uses the LastPlayed field to determine what track in the grouping to play next.
            // Therefore, we need to update this before requesting the next track.
            //
            // This will be overridden by IncrementLastPlayed () called by 
            // PlaybackControllerService's EndOfStream handler.
            CurrentTrack.LastPlayed = DateTime.Now;
            CurrentTrack.Save ();
            
            OnEventChanged (PlayerEvent.RequestNextTrack);
            // Gapless playback with Playbin2 requires that the about-to-finish callback does not return until
            // the next uri has been set.  Block here for a second or until the RequestNextTrack event has 
            // finished triggering.
            if (!next_track_set.WaitOne (1000, false)) {
                Log.Debug ("[Gapless] Timed out while waiting for next_track_set to be raised");
            }
        }       
        
        private void OnIterate (IntPtr player)
        {
            OnEventChanged (PlayerEvent.Iterate);
        }
        
        private void OnStateChange (IntPtr player, GstState old_state, GstState new_state, GstState pending_state)
        {
            if (old_state == GstState.Ready && new_state == GstState.Paused && pending_state == GstState.Playing) {
                OnStateChanged (PlayerState.Loaded);
                return;
            } else if (old_state == GstState.Paused && new_state == GstState.Playing && pending_state == GstState.VoidPending) {
                if (CurrentState == PlayerState.Loaded) {
                    OnEventChanged (PlayerEvent.StartOfStream);
                }
                OnStateChanged (PlayerState.Playing);
                return;
            } else if (CurrentState == PlayerState.Playing && old_state == GstState.Playing && new_state == GstState.Paused) {
                OnStateChanged (PlayerState.Paused);
                return;
            }
        }
        
        private void OnError (IntPtr player, uint domain, int code, IntPtr error, IntPtr debug)
        {
            Close (true);
            
            string error_message = error == IntPtr.Zero
                ? Catalog.GetString ("Unknown Error")
                : GLib.Marshaller.Utf8PtrToString (error);

            if (domain == GST_RESOURCE_ERROR) {
                GstResourceError domain_code = (GstResourceError) code;
                if (CurrentTrack != null) {
                    switch (domain_code) {
                        case GstResourceError.NotFound:
                            CurrentTrack.SavePlaybackError (StreamPlaybackError.ResourceNotFound);
                            break;
                        default:
                            break;
                    }        
                }
                
                Log.Error (String.Format ("GStreamer resource error: {0}", domain_code), false);
            } else if (domain == GST_STREAM_ERROR) {
                GstStreamError domain_code = (GstStreamError) code;
                if (CurrentTrack != null) {
                    switch (domain_code) {
                        case GstStreamError.CodecNotFound:
                            CurrentTrack.SavePlaybackError (StreamPlaybackError.CodecNotFound);
                            break;
                        default:
                            break;
                    }
                }
                
                Log.Error (String.Format("GStreamer stream error: {0}", domain_code), false);
            } else if (domain == GST_CORE_ERROR) {
                GstCoreError domain_code = (GstCoreError) code;
                if (CurrentTrack != null) {
                    switch (domain_code) {
                        case GstCoreError.MissingPlugin:
                            CurrentTrack.SavePlaybackError (StreamPlaybackError.CodecNotFound);
                            break;
                        default:
                            break;
                    }
                }
                
                if (domain_code != GstCoreError.MissingPlugin) {
                    Log.Error (String.Format("GStreamer core error: {0}", (GstCoreError) code), false);
                }
            } else if (domain == GST_LIBRARY_ERROR) {
                Log.Error (String.Format("GStreamer library error: {0}", (GstLibraryError) code), false);
            }
            
            OnEventChanged (new PlayerEventErrorArgs (error_message));
        }
        
        private void OnBuffering (IntPtr player, int progress)
        {
            if (buffering_finished && progress >= 100) {
                return;
            }
            
            buffering_finished = progress >= 100;
            OnEventChanged (new PlayerEventBufferingArgs ((double) progress / 100.0));
        }
        
        private void OnTagFound (IntPtr player, string tagName, ref GLib.Value value)
        {
            OnTagFound (ProcessNativeTagResult (tagName, ref value));
        }
        
        private void OnVisualizationData (IntPtr player, int channels, int samples, IntPtr data, int bands, IntPtr spectrum)
        {
            VisualizationDataHandler handler = data_available;
            
            if (handler == null) {
                return;
            }
            
            float [] flat = new float[channels * samples];
            Marshal.Copy (data, flat, 0, flat.Length);
            
            float [][] cbd = new float[channels][];
            for (int i = 0; i < channels; i++) {
                float [] channel = new float[samples];
                Array.Copy (flat, i * samples, channel, 0, samples);
                cbd[i] = channel;
            }
            
            float [] spec = new float[bands];
            Marshal.Copy (spectrum, spec, 0, bands);
            
            try {
                handler (cbd, new float[][] { spec });
            } catch (Exception e) {
                Log.Exception ("Uncaught exception during visualization data post.", e);
            }
        }
        
        private static StreamTag ProcessNativeTagResult (string tagName, ref GLib.Value valueRaw)
        {
            if (tagName == String.Empty || tagName == null) {
                return StreamTag.Zero;
            }
        
            object value = null;
            
            try {
                value = valueRaw.Val;
            } catch {
                return StreamTag.Zero;
            }
            
            if (value == null) {
                return StreamTag.Zero;
            }
            
            StreamTag item;
            item.Name = tagName;
            item.Value = value;
            
            return item;
        }
        
        public override ushort Volume {
            get { return (ushort)Math.Round (bp_get_volume (handle) * 100.0); }
            set { 
                if ((IntPtr)handle == IntPtr.Zero) {
                    pending_volume = value;
                    return;
                }
                
                bp_set_volume (handle, value / 100.0);
                OnEventChanged (PlayerEvent.Volume);
            }
        }
        
        public override uint Position {
            get { return (uint)bp_get_position(handle); }
            set { 
                bp_set_position (handle, (ulong)value);
                OnEventChanged (PlayerEvent.Seek);
            }
        }
        
        public override bool CanSeek {
            get { return bp_can_seek (handle); }
        }
        
        public override uint Length {
            get { return (uint)bp_get_duration (handle); }
        }
        
        public override string Id {
            get { return "gstreamer"; }
        }
        
        public override string Name {
            get { return "GStreamer 0.10"; }
        }
        
        private bool? supports_equalizer = null;
        public override bool SupportsEqualizer {
            get { 
                if (supports_equalizer == null) {
                    supports_equalizer = bp_equalizer_is_supported (handle); 
                }
                
                return supports_equalizer.Value;
            }
        }
        
        public override VideoDisplayContextType VideoDisplayContextType {
            get { return bp_video_get_display_context_type (handle); }
        }
        
        public override IntPtr VideoDisplayContext {
            set { bp_video_set_display_context (handle, value); }
            get { return bp_video_get_display_context (handle); }
        }
        
        public double AmplifierLevel {
            set {
                double scale = Math.Pow (10.0, value / 20.0);
                bp_equalizer_set_preamp_level (handle, scale);
            }
        }
        
        public int [] BandRange {
            get {
                int min = -1;
                int max = -1;
                
                bp_equalizer_get_bandrange (handle, out min, out max);
                
                return new int [] { min, max };
            }
        }
        
        public uint [] EqualizerFrequencies {
            get {
                uint count = bp_equalizer_get_nbands (handle);
                double [] freq = new double[count];

                bp_equalizer_get_frequencies (handle, out freq);
                
                uint [] ret = new uint[count];
                for (int i = 0; i < count; i++) {
                    ret[i] = (uint)freq[i];
                }
                
                return ret;
            }
        }
        
        public void SetEqualizerGain (uint band, double gain)
        {
            bp_equalizer_set_gain (handle, band, gain);
        }
        
        private static string [] source_capabilities = { "file", "http", "cdda" };
        public override IEnumerable SourceCapabilities {
            get { return source_capabilities; }
        }
                
        private static string [] decoder_capabilities = { "ogg", "wma", "asf", "flac" };
        public override IEnumerable ExplicitDecoderCapabilities {
            get { return decoder_capabilities; }
        }
        
        private bool ReplayGainEnabled {
            get { return bp_replaygain_get_enabled (handle); }
            set { bp_replaygain_set_enabled (handle, value); }
        }

#region ISupportClutter

        private IntPtr clutter_video_sink;
        private IntPtr clutter_video_texture;
        private bool clutter_video_sink_enabled;

        public void EnableClutterVideoSink (IntPtr videoTexture)
        {
            clutter_video_sink_enabled = true;
            clutter_video_texture = videoTexture;
        }

        public void DisableClutterVideoSink ()
        {
            clutter_video_sink_enabled = false;
            clutter_video_texture = IntPtr.Zero;
        }

        public bool IsClutterVideoSinkInitialized {
            get { return 
                clutter_video_sink_enabled &&
                clutter_video_texture != IntPtr.Zero &&
                clutter_video_sink != IntPtr.Zero;
            }
        }

        private IntPtr OnVideoPipelineSetup (IntPtr player, IntPtr bus)
        {
            try {
                if (clutter_video_sink_enabled) {
                    if (clutter_video_sink != IntPtr.Zero) {
                        // FIXME: does this get unreffed by the pipeline?
                    }
                    
                    clutter_video_sink = clutter_gst_video_sink_new (clutter_video_texture);
                } else if (!clutter_video_sink_enabled && clutter_video_sink != IntPtr.Zero) {
                    clutter_video_sink = IntPtr.Zero;
                    clutter_video_texture = IntPtr.Zero;
                }
            } catch (Exception e) {
                Log.Exception ("Clutter support could not be initialized", e);
                clutter_video_sink = IntPtr.Zero;
                clutter_video_texture = IntPtr.Zero;
                clutter_video_sink_enabled = false;
            }

            return clutter_video_sink;
        }

#endregion
        
#region Preferences

        private PreferenceBase replaygain_preference;
        private PreferenceBase gapless_preference;

        private void InstallPreferences ()
        {
            PreferenceService service = ServiceManager.Get<PreferenceService> ();
            if (service == null) {
                return;
            }
            
            replaygain_preference = service["general"]["misc"].Add (new SchemaPreference<bool> (ReplayGainEnabledSchema, 
                Catalog.GetString ("_Enable ReplayGain correction"),
                Catalog.GetString ("For tracks that have ReplayGain data, automatically scale (normalize) playback volume."),
                delegate { ReplayGainEnabled = ReplayGainEnabledSchema.Get (); }
            ));
            gapless_preference = service["general"]["misc"].Add (new SchemaPreference<bool> (GaplessEnabledSchema,
                Catalog.GetString ("Enable _gapless playback (EXPERIMENTAL)"),
                Catalog.GetString ("Eliminate the small playback gap on track change.  Useful for concept albums & classical music."),
                delegate { GaplessEnabled = GaplessEnabledSchema.Get (); }
            ));                            
        }
        
        private void UninstallPreferences ()
        {
            PreferenceService service = ServiceManager.Get<PreferenceService> ();
            if (service == null) {
                return;
            }
            
            service["general"]["misc"].Remove (replaygain_preference);
            service["general"]["misc"].Remove (gapless_preference);
            replaygain_preference = null;
            gapless_preference = null;
        }
        
        public static readonly SchemaEntry<bool> ReplayGainEnabledSchema = new SchemaEntry<bool> (
            "player_engine", "replay_gain_enabled", 
            false,
            "Enable ReplayGain",
            "If ReplayGain data is present on tracks when playing, allow volume scaling"
        );

        public static readonly SchemaEntry<bool> GaplessEnabledSchema = new SchemaEntry<bool> (
            "player_engine", "gapless_playback_enabled",
            false,
            "Enable gapless playback (EXPERIMENTAL)",
            "Eliminate the small playback gap on track change.  Useful for concept albums & classical music.  EXPERIMENTAL"
        );
                                                                                              

#endregion
        
        [DllImport ("libbanshee.dll")]
        private static extern IntPtr bp_new ();
        
        [DllImport ("libbanshee.dll")]
        private static extern bool bp_initialize_pipeline (HandleRef player);

        [DllImport ("libbanshee.dll")]
        private static extern void bp_destroy (HandleRef player);
        
        [DllImport ("libbanshee.dll")]
        private static extern void bp_set_eos_callback (HandleRef player, BansheePlayerEosCallback cb);
        
        [DllImport ("libbanshee.dll")]
        private static extern void bp_set_error_callback (HandleRef player, BansheePlayerErrorCallback cb);
        
        [DllImport ("libbanshee.dll")]
        private static extern void bp_set_vis_data_callback (HandleRef player, BansheePlayerVisDataCallback cb);
        
        [DllImport ("libbanshee.dll")]
        private static extern void bp_set_state_changed_callback (HandleRef player, 
            BansheePlayerStateChangedCallback cb);
            
        [DllImport ("libbanshee.dll")]
        private static extern void bp_set_iterate_callback (HandleRef player,
            BansheePlayerIterateCallback cb);
        
        [DllImport ("libbanshee.dll")]
        private static extern void bp_set_buffering_callback (HandleRef player,
            BansheePlayerBufferingCallback cb);

        [DllImport ("libbanshee.dll")]
        private static extern void bp_set_video_pipeline_setup_callback (HandleRef player,
            VideoPipelineSetupHandler cb);
        
        [DllImport ("libbanshee.dll")]
        private static extern void bp_set_tag_found_callback (HandleRef player,
            GstTaggerTagFoundCallback cb);

        [DllImport ("libbanshee")]
        private static extern void bp_set_next_track_starting_callback (HandleRef player,
            BansheePlayerNextTrackStartingCallback cb);

        [DllImport ("libbanshee.dll")]
        private static extern void bp_set_about_to_finish_callback (HandleRef player,
            BansheePlayerAboutToFinishCallback cb);
        
        [DllImport ("libbanshee.dll")]
        private static extern bool bp_open (HandleRef player, IntPtr uri);
        
        [DllImport ("libbanshee.dll")]
        private static extern void bp_stop (HandleRef player, bool nullstate);
        
        [DllImport ("libbanshee.dll")]
        private static extern void bp_pause (HandleRef player);
        
        [DllImport ("libbanshee.dll")]
        private static extern void bp_play (HandleRef player);
        
        [DllImport ("libbanshee.dll")]
        private static extern bool bp_set_next_track (HandleRef player, IntPtr uri);

        [DllImport ("libbanshee.dll")]
        private static extern void bp_set_volume (HandleRef player, double volume);
        
        [DllImport("libbanshee.dll")]
        private static extern double bp_get_volume (HandleRef player);
        
        [DllImport ("libbanshee.dll")]
        private static extern bool bp_can_seek (HandleRef player);
        
        [DllImport ("libbanshee.dll")]
        private static extern bool bp_set_position (HandleRef player, ulong time_ms);
        
        [DllImport ("libbanshee.dll")]
        private static extern ulong bp_get_position (HandleRef player);
        
        [DllImport ("libbanshee.dll")]
        private static extern ulong bp_get_duration (HandleRef player);
        
        [DllImport ("libbanshee.dll")]
        private static extern bool bp_get_pipeline_elements (HandleRef player, out IntPtr playbin,
            out IntPtr audiobin, out IntPtr audiotee);
            
        [DllImport ("libbanshee.dll")]
        private static extern void bp_set_application_gdk_window (HandleRef player, IntPtr window);
        
        [DllImport ("libbanshee.dll")]
        private static extern VideoDisplayContextType bp_video_get_display_context_type (HandleRef player);
        
        [DllImport ("libbanshee.dll")]
        private static extern void bp_video_set_display_context (HandleRef player, IntPtr displayContext);
        
        [DllImport ("libbanshee.dll")]
        private static extern IntPtr bp_video_get_display_context (HandleRef player);
        
        [DllImport ("libbanshee.dll")]
        private static extern void bp_video_window_expose (HandleRef player, IntPtr displayContext, bool direct);
                                                                   
        [DllImport ("libbanshee.dll")]
        private static extern void bp_get_error_quarks (out uint core, out uint library, 
            out uint resource, out uint stream);
        
        [DllImport ("libbanshee.dll")]
        private static extern bool bp_equalizer_is_supported (HandleRef player);
        
        [DllImport ("libbanshee.dll")]
        private static extern void bp_equalizer_set_preamp_level (HandleRef player, double level);
        
        [DllImport ("libbanshee.dll")]
        private static extern void bp_equalizer_set_gain (HandleRef player, uint bandnum, double gain);
        
        [DllImport ("libbanshee.dll")]
        private static extern void bp_equalizer_get_bandrange (HandleRef player, out int min, out int max);
        
        [DllImport ("libbanshee.dll")]
        private static extern uint bp_equalizer_get_nbands (HandleRef player);
        
        [DllImport ("libbanshee.dll")]
        private static extern void bp_equalizer_get_frequencies (HandleRef player,
            [MarshalAs (UnmanagedType.LPArray)] out double [] freq);
            
        [DllImport ("libbanshee.dll")]
        private static extern void bp_replaygain_set_enabled (HandleRef player, bool enabled);
        
        [DllImport ("libbanshee.dll")]
        private static extern bool bp_replaygain_get_enabled (HandleRef player);

        [DllImport ("libbanshee.dll")]
        private static extern IntPtr clutter_gst_video_sink_new (IntPtr texture);
    }
}
