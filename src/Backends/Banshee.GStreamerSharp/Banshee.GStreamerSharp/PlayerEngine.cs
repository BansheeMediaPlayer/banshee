//
// PlayerEngine.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//   Andrés G. Aragoneses <knocte@gmail.com>
//   Stephan Sundermann <stephansundermann@gmail.com>
//
// Copyright (C) 2010 Novell, Inc.
// Copyright (C) 2013 Andrés G. Aragoneses
// Copyright (C) 2013 Stephan Sundermann
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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

using Mono.Unix;

using Gst;
using Gst.PbUtils;

using Hyena;
using Hyena.Data;

using Banshee.Base;
using Banshee.Collection;
using Banshee.Streaming;
using Banshee.MediaEngine;
using Banshee.ServiceStack;
using Banshee.Configuration;
using Banshee.Preferences;
using Gst.Video;

namespace Banshee.GStreamerSharp
{
    public class PlayerEngine : Banshee.MediaEngine.PlayerEngine, IEqualizer, IVisualizationDataSource
    {
        internal class AudioSinkBin : Bin
        {
            Element hw_audio_sink;
            Element volume;
            Element rgvolume;
            Element equalizer;
            Element preamp;
            Element first;
            GhostPad visible_sink;
            Element audiotee;
            object pipeline_lock = new object ();

            public AudioSinkBin (IntPtr o) : base(o)
            {
                Name = "audiobin";
            }

            public AudioSinkBin (string elementName) : base(elementName)
            {
                hw_audio_sink = SelectAudioSink ();
                Add (hw_audio_sink);
                first = hw_audio_sink;

                // Our audio sink is a tee, so plugins can attach their own pipelines
                audiotee = ElementFactory.Make ("tee", "audiotee");
                if (audiotee == null) {
                    Log.Error ("Can not create audio tee!");
                } else {
                    Add (audiotee);
                }

                volume = FindVolumeProvider (hw_audio_sink);
                if (volume != null) {
                    // If the sink provides its own volume property we assume that it will
                    // also save that value across program runs.  Pulsesink has this behaviour.
                    VolumeNeedsSaving = false;
                } else {
                    volume = ElementFactory.Make ("volume", "volume");
                    VolumeNeedsSaving = true;
                    Add (volume);
                    volume.Link (hw_audio_sink);
                    first = volume;
                }

                equalizer = ElementFactory.Make ("equalizer-10bands", "equalizer-10bands");

                if (equalizer != null) {
                    Element eq_audioconvert = ElementFactory.Make ("audioconvert", "audioconvert");
                    Element eq_audioconvert2 = ElementFactory.Make ("audioconvert", "audioconvert2");
                    preamp = ElementFactory.Make ("volume", "preamp");

                    this.Add (eq_audioconvert, preamp, equalizer, eq_audioconvert2);

                    Element.Link (eq_audioconvert, preamp, equalizer, eq_audioconvert2, first);

                    first = eq_audioconvert;
                    Log.Debug ("Built and linked Equalizer");
                }

                // Link the first tee pad to the primary audio sink queue
                Pad sinkpad = first.GetStaticPad ("sink");
                Pad pad = RequestTeePad ();
                audiotee["alloc-pad"] = pad;
                pad.Link (sinkpad);
                first = audiotee;

                visible_sink = new GhostPad ("sink", first.GetStaticPad ("sink"));
                AddPad (visible_sink);
            }

            static Element FindVolumeProvider (Element sink)
            {
                Element volumeProvider = null;
                // Sinks which automatically select between a number of possibilities
                // (such as autoaudiosink and gconfaudiosink) need to be at least in
                // the Ready state before they'll contain an actual sink.
                sink.SetState (State.Ready);

                try {
                    var volume = sink ["volume"];
                    volumeProvider = sink;
                    Log.DebugFormat ("Sink {0} has native volume: {1}", volumeProvider.Name, volume);
                } catch (Gst.PropertyNotFoundException) {
                    var sinkBin = sink as Bin;
                    if (sinkBin != null) {
                        foreach (Element e in sinkBin.IterateRecurse ()) {
                            try {
                                var volume = e ["volume"];
                                volumeProvider = e;
                                Log.DebugFormat ("Found volume provider {0} in {1}: {2}",
                                    volumeProvider.Name, sink.Name, volume);
                                break;
                            } catch (Gst.PropertyNotFoundException) { }
                        }
                    }
                }
                return volumeProvider;
            }

            static Element SelectAudioSink ()
            {
                Element audiosink = null;

                // Default to GConfAudioSink, which should Do The Right Thing.
                audiosink = ElementFactory.Make ("gconfaudiosink", "audiosink");
                if (audiosink == null) {
                    // Try DirectSoundSink, which should work on Windows
                    audiosink = ElementFactory.Make ("directsoundsink", "audiosink");
                    if (audiosink != null) {
                        // The unmanaged code sets the volume on the directsoundsink here.
                        // Presumably this fixes a problem, but there's no reference as to what it is.
                        audiosink["volume"] = 1.0;
                    } else {
                        audiosink = ElementFactory.Make ("autoaudiosink", "audiosink");
                        if (audiosink == null) {
                            // As a last-ditch effort try ALSA.
                            audiosink = ElementFactory.Make ("alsasink", "audiosink");
                        }
                    }
                }
                return audiosink;
            }

            public bool ReplayGainEnabled {
                get { return rgvolume != null; }
                set {
                    if (value && rgvolume == null) {
                        visible_sink.AddProbe (PadProbeType.Idle, InsertReplayGain);
                        Log.Debug ("Enabled ReplayGain volume scaling.");
                    } else if (!value && rgvolume != null) {
                        visible_sink.AddProbe (PadProbeType.Idle, RemoveReplayGain);
                        Log.Debug ("Disabled ReplayGain volume scaling.");
                    }
                }
            }

            PadProbeReturn InsertReplayGain (Pad pad, PadProbeInfo info)
            {
                lock (pipeline_lock) {
                    if (rgvolume == null) {
                        rgvolume = ElementFactory.Make ("rgvolume", "rgvolume");
                        Add (rgvolume);
                        rgvolume.SyncStateWithParent ();
                        visible_sink.SetTarget (rgvolume.GetStaticPad ("sink"));
                        rgvolume.Link (first);
                        first = rgvolume;
                    }
                }
                return PadProbeReturn.Remove;
            }

            PadProbeReturn RemoveReplayGain (Pad pad, PadProbeInfo info)
            {
                lock (pipeline_lock) {
                    if (rgvolume != null) {
                        first = rgvolume.GetStaticPad ("src").Peer.Parent as Element;
                        rgvolume.Unlink (first);
                        rgvolume.SetState (State.Null);
                        Remove (rgvolume);
                        rgvolume = null;
                        visible_sink.SetTarget (first.GetStaticPad ("sink"));
                    }
                }
                return PadProbeReturn.Remove;
            }


            public bool VolumeNeedsSaving { get; private set; }
            public double Volume {
                get {
                    return (double)volume["volume"];
                }
                set {
                    if (value < 0 || value > 10.0) {
                        throw new ArgumentOutOfRangeException ("value", "Volume must be between 0 and 10.0");
                    }
                    Log.DebugFormat ("Setting volume to {0:0.00}", value);
                    volume["volume"] = value;
                }
            }

            public bool SupportsEqualizer { get {return preamp != null && equalizer != null;} }

            public double AmplifierLevel {
                set { preamp ["volume"] = Math.Pow (10.0, value / 20.0); }
            }

            public int [] BandRange {
                get {
                    // http://gstreamer.freedesktop.org/data/doc/gstreamer/head/gst-plugins-good-plugins/html/gst-plugins-good-plugins-equalizer-10bands.html#GstIirEqualizer10Bands--band0
                    return new [] { -24, 12 };

                    /* TODO: should we check for band0::gain like it was done before? if yes, use paramspec.min|max retrieval
                    int min = -1;
                    int max = -1;

                    //http://gstreamer.freedesktop.org/data/doc/gstreamer/head/gst-plugins-good-plugins/html/gst-plugins-good-plugins-equalizer-10bands.html
                    PropertyInfo pspec = new PropertyInfo();


                    if (equalizer.HasProperty ("band0::gain")) {
                        pspec = equalizer.GetPropertyInfo ("band0::gain");
                    } else if (equalizer.HasProperty ("band0")) {
                        pspec = equalizer.GetPropertyInfo ("band0");
                    }

                    if (pspec.Name != null) {
                        min = (int)((double)pspec.Min);
                        max = (int)((double)pspec.Max);
                    }
                    */
                }
            }

            private uint GetNBands ()
            {
                if (equalizer == null) {
                    return 0;
                }

                return ChildProxyAdapter.GetObject (equalizer).ChildrenCount;
            }

            public uint [] EqualizerFrequencies {
                get {
                    uint count = GetNBands ();
                    uint[] ret = new uint[count];

                    if (equalizer != null) {
                        IChildProxy equalizer_child_proxy = ChildProxyAdapter.GetObject (equalizer);
                        for (uint i = 0; i < count; i++) {
                            Gst.Object band = (Gst.Object)equalizer_child_proxy.GetChildByIndex (i);
                            ret [i] = (uint)(double)band ["freq"];
                        }
                    }

                    return ret;
                }
            }

            public void SetEqualizerGain (uint band, double value)
            {
                if (equalizer != null) {
                    if (band >= GetNBands ()) {
                        throw new ArgumentOutOfRangeException ("band", "Attempt to set out-of-range equalizer band");
                    }
                    Gst.Object the_band = (Gst.Object)ChildProxyAdapter.GetObject (equalizer).GetChildByIndex (band);
                    the_band ["gain"] = value;
                }
            }

            public Pad RequestTeePad ()
            {
                var pad = audiotee.GetRequestPad ("src_%u");
                if (pad == null) {
                    throw new InvalidOperationException ("Could not retrieve tee pad");
                }
                return pad;
            }
        }


        Element playbin;
        AudioSinkBin audio_sink;
        uint iterate_timeout_id = 0;
        List<string> missing_details = new List<string> ();
        ManualResetEvent next_track_set;
        CddaManager cdda_manager;
        VideoManager video_manager = null;
        DvdManager dvd_manager = null;
        Visualization visualization;

        public PlayerEngine ()
        {
            Log.InformationFormat ("GStreamer# {0} Initializing; {1}.{2}",
                typeof (Gst.Version).Assembly.GetName ().Version, Gst.Version.Description, Gst.Version.Nano);

            // Setup the gst plugins/registry paths if running Windows
            if (PlatformDetection.IsWindows) {
                var gst_paths = new string [] { Hyena.Paths.Combine (Hyena.Paths.InstalledApplicationPrefix, "gst-plugins") };
                Environment.SetEnvironmentVariable ("GST_PLUGIN_PATH", String.Join (";", gst_paths));
                Environment.SetEnvironmentVariable ("GST_PLUGIN_SYSTEM_PATH", "");
                Environment.SetEnvironmentVariable ("GST_DEBUG", "1");

                string registry = Hyena.Paths.Combine (Hyena.Paths.ApplicationData, "registry.bin");
                if (!System.IO.File.Exists (registry)) {
                    System.IO.File.Create (registry).Close ();
                }

                Environment.SetEnvironmentVariable ("GST_REGISTRY", registry);

                //System.Environment.SetEnvironmentVariable ("GST_REGISTRY_FORK", "no");
                Log.DebugFormat ("GST_PLUGIN_PATH = {0}", Environment.GetEnvironmentVariable ("GST_PLUGIN_PATH"));
            }

            Gst.Application.Init ();
            playbin = ElementFactory.Make ("playbin", "the playbin");

            next_track_set = new ManualResetEvent (false);

            audio_sink = new AudioSinkBin ("audiobin");

            playbin["audio-sink"] = audio_sink;

            if (audio_sink.VolumeNeedsSaving) {
                // Remember the volume from last time
                Volume = (ushort)PlayerEngineService.VolumeSchema.Get ();
            }

            visualization = new Visualization (audio_sink);

            playbin.AddNotification ("volume", OnVolumeChanged);
            playbin.Bus.AddWatch (OnBusMessage);
            playbin.Connect ("about-to-finish", OnAboutToFinish);

            cdda_manager = new CddaManager (playbin);
            dvd_manager = new DvdManager (playbin);

            video_manager = new VideoManager (playbin);
            video_manager.PrepareWindow += OnVideoPrepareWindow;
            video_manager.Initialize ();

            dvd_manager.FindNavigation (playbin);
            OnStateChanged (PlayerState.Ready);
        }

        protected override bool DelayedInitialize {
            get {
                return true;
            }
        }

        protected override void Initialize ()
        {
            base.Initialize ();
            InstallPreferences ();
            audio_sink.ReplayGainEnabled = ReplayGainEnabledSchema.Get ();
        }

        public override void Dispose ()
        {
            UninstallPreferences ();
            base.Dispose ();
        }

        public event VisualizationDataHandler DataAvailable {
            add {
                visualization.DataAvailable += value;
            }

            remove {
                visualization.DataAvailable -= value;
            }
        }

        public override void VideoExpose (IntPtr window, bool direct)
        {
            video_manager.WindowExpose (window, direct);
        }

        public override void VideoWindowRealize (IntPtr window)
        {
            video_manager.WindowRealize (window);
        }

        private void OnVideoPrepareWindow ()
        {
            OnEventChanged (PlayerEvent.PrepareVideoWindow);
        }

        void OnAboutToFinish (object o, GLib.SignalArgs args)
        {
            // This is needed to make Shuffle-by-* work.
            // Shuffle-by-* uses the LastPlayed field to determine what track in the grouping to play next.
            // Therefore, we need to update this before requesting the next track.
            //
            // This will be overridden by IncrementLastPlayed () called by
            // PlaybackControllerService's EndOfStream handler.
            CurrentTrack.UpdateLastPlayed ();

            next_track_set.Reset ();
            OnEventChanged (PlayerEvent.RequestNextTrack);

            if (!next_track_set.WaitOne (1000, false)) {
                Log.Warning ("[Gapless]: Timed out while waiting for next track to be set.");
                next_track_set.Set ();
            }
        }

        public override void SetNextTrackUri (SafeUri uri, bool maybeVideo)
        {
            if (next_track_set.WaitOne (0, false)) {
                // We've been asked to set the next track, but have taken too
                // long to get here.  Bail for now, and the EoS handling will
                // pick up the pieces.
                return;
            }

            // If there isn't a next track for us, release the block on the about-to-finish callback.
            if (uri == null) {
                next_track_set.Set ();
                return;
            }

            playbin ["uri"] = uri.AbsoluteUri;

            if (maybeVideo) {
                LookupForSubtitle (uri);
            }

            next_track_set.Set ();
        }

        public override void Seek (uint position, bool accurate_seek)
        {
            SeekFlags seek_flags = SeekFlags.Flush;
            if (accurate_seek) {
                seek_flags |= SeekFlags.Accurate;
            }
            playbin.SeekSimple (Format.Time, seek_flags, (((long)position) * Constants.MSECOND));
            OnEventChanged (PlayerEvent.Seek);
        }

        private bool OnBusMessage (Bus bus, Message msg)
        {
            switch (msg.Type) {
                case MessageType.Eos:
                    StopIterating ();
                    Close (false);
                    OnEventChanged (PlayerEvent.EndOfStream);
                    OnEventChanged (PlayerEvent.RequestNextTrack);
                    break;

                case MessageType.StateChanged:
                    if (msg.Src == playbin) {
                        State old_state, new_state, pending_state;
                        msg.ParseStateChanged (out old_state, out new_state, out pending_state);
                        HandleStateChange (old_state, new_state, pending_state);
                    }
                    break;

                case MessageType.Buffering:
                    int buffer_percent = msg.ParseBuffering ();
                    HandleBuffering (buffer_percent);
                    break;

                case MessageType.Tag:
                    TagList tag_list = msg.ParseTag ();
                    tag_list.Foreach (HandleTag);
                    break;

                case MessageType.Error:
                    GLib.GException err;
                    string debug;
                    msg.ParseError (out err, out debug);

                    HandleError (err);
                    break;

                case MessageType.Element:

                    if (GlobalPbUtil.IsMissingPluginMessage (msg)) {
                        string detail = GlobalPbUtil.MissingPluginMessageGetInstallerDetail (msg);

                        if (detail == null)
                            return false;

                        if (missing_details.Contains (detail)) {
                            Log.DebugFormat ("Ignoring missing element details, already prompted ('{0}')", detail);
                            return false;
                        }

                        Log.DebugFormat ("Saving missing element details ('{0}')", detail);
                        missing_details.Add (detail);

                        Log.Error ("Missing GStreamer Plugin", GlobalPbUtil.MissingPluginMessageGetDescription (msg), true);

                        InstallPluginsContext install_context = new InstallPluginsContext ();
                        GlobalPbUtil.InstallPluginsAsync (missing_details.ToArray (), install_context, OnInstallPluginsReturn);
                    } else if (NavigationAdapter.MessageGetType (msg) == NavigationMessageType.CommandsChanged) {
                        dvd_manager.HandleCommandsChanged (playbin);
                    }
                    break;

                case MessageType.StreamStart:
                    HandleStreamStart ();
                    break;

                case MessageType.Application:
                    string name;
                    Structure s = msg.Structure;
                    name = s.Name;
                    if (String.IsNullOrEmpty (name) && name == "stream-changed") {
                        video_manager.ParseStreamInfo ();
                    }
                    break;
            }

            return true;
        }

        private void OnInstallPluginsReturn (InstallPluginsReturn status)
        {
            Log.InformationFormat ("GStreamer plugin installer returned: {0}", status);
            if (status == InstallPluginsReturn.Success || status == InstallPluginsReturn.InstallInProgress) {
            }
        }

        private void OnVolumeChanged (object o, GLib.NotifyArgs args)
        {
            OnEventChanged (PlayerEvent.Volume);
        }

        private void HandleStreamStart ()
        {
            // Set the current track as fully played before signaling EndOfStream.
            ServiceManager.PlayerEngine.IncrementLastPlayed (1.0);
            OnEventChanged (PlayerEvent.EndOfStream);
            OnEventChanged (PlayerEvent.StartOfStream);
        }

        private void HandleError (GLib.GException ex)
        {
            TrackInfo failed_track = CurrentTrack;
            Close (true);

            var error_message = String.IsNullOrEmpty (ex.Message) ? Catalog.GetString ("Unknown Error") : ex.Message;

            if (ex.Domain == Global.ResourceErrorQuark ()) {
                ResourceError domain_code = (ResourceError)ex.Code;
                if (failed_track != null) {
                    switch (domain_code) {
                    case ResourceError.NotFound:
                        failed_track.SavePlaybackError (StreamPlaybackError.ResourceNotFound);
                        break;
                    default:
                        break;
                    }
                }
                Log.Error (String.Format ("GStreamer resource error: {0}", domain_code), false);
            } else if (ex.Domain == Global.StreamErrorQuark ()) {
                StreamError domain_code = (StreamError)ex.Code;
                if (failed_track != null) {
                    switch (domain_code) {
                    case StreamError.CodecNotFound:
                        failed_track.SavePlaybackError (StreamPlaybackError.CodecNotFound);
                        break;
                    default:
                        break;
                    }
                }

                Log.Error (String.Format ("GStreamer stream error: {0}", domain_code), false);
            } else if (ex.Domain == Global.CoreErrorQuark ()) {
                CoreError domain_code = (CoreError)ex.Code;
                if (failed_track != null) {
                    switch (domain_code) {
                    case CoreError.MissingPlugin:
                        failed_track.SavePlaybackError (StreamPlaybackError.CodecNotFound);
                        break;
                    default:
                        break;
                    }
                }

                if (domain_code != CoreError.MissingPlugin) {
                    Log.Error (String.Format ("GStreamer core error: {0}", domain_code), false);
                }
            } else if (ex.Domain == Global.LibraryErrorQuark ()) {
                Log.Error (String.Format ("GStreamer library error: {0}", ex.Code), false);
            }

            OnEventChanged (new PlayerEventErrorArgs (error_message));
        }

        private void HandleBuffering (int buffer_percent)
        {
            OnEventChanged (new PlayerEventBufferingArgs (buffer_percent / 100.0));
        }

        private void HandleStateChange (State old_state, State new_state, State pending_state)
        {
            StopIterating ();
            if (CurrentState != PlayerState.Loaded && old_state == State.Ready && new_state == State.Paused && pending_state == State.Playing) {
                OnStateChanged (PlayerState.Loaded);
            } else if (old_state == State.Paused && new_state == State.Playing && pending_state == State.VoidPending) {
                if (CurrentState == PlayerState.Loaded) {
                    OnEventChanged (PlayerEvent.StartOfStream);
                }
                OnStateChanged (PlayerState.Playing);
                StartIterating ();
            } else if (CurrentState == PlayerState.Playing && old_state == State.Playing && new_state == State.Paused) {
                OnStateChanged (PlayerState.Paused);
            }
        }

        private void HandleTag (TagList tag_list, string tagname)
        {
            for (uint i = 0; i < tag_list.GetTagSize (tagname); i++) {
                GLib.Value val = tag_list.GetValueIndex (tagname, i);
                Log.Debug ("Found Tag: " + tagname + " Value: " + val.Val);
                OnTagFound (new StreamTag () { Name = tagname, Value = val.Val });
                val.Dispose ();
            }
        }

        private bool OnIterate ()
        {
            // Actual iteration.
            OnEventChanged (PlayerEvent.Iterate);
            // Run forever until we are stopped
            return true;
        }

        private void StartIterating ()
        {
            if (iterate_timeout_id > 0) {
                GLib.Source.Remove (iterate_timeout_id);
                iterate_timeout_id = 0;
            }

            iterate_timeout_id = GLib.Timeout.Add (200, OnIterate);
        }

        private void StopIterating ()
        {
            if (iterate_timeout_id > 0) {
                GLib.Source.Remove (iterate_timeout_id);
                iterate_timeout_id = 0;
            }
        }

        protected override void OpenUri (SafeUri uri, bool maybeVideo)
        {
            if (cdda_manager.HandleURI (playbin, uri.AbsoluteUri)) {
                return;
            } else if (dvd_manager.HandleURI (playbin, uri.AbsoluteUri)) {
                return;
            } else if (playbin == null) {
                throw new ApplicationException ("Could not open resource");
            }

            if (playbin.CurrentState == State.Playing || playbin.CurrentState == State.Paused) {
                playbin.SetState (Gst.State.Ready);
            }

            playbin ["uri"] = uri.AbsoluteUri;
            if (maybeVideo) {
                // Lookup for subtitle files with same name/folder
                LookupForSubtitle (uri);
            }
        }

        private void LookupForSubtitle (SafeUri uri)
        {
            string scheme, filename, subfile;
            SafeUri suburi;
            int dot;
            // Always enable rendering of subtitles
            uint flags;
            flags = (uint)playbin["flags"];
            flags |= (1 << 2);//GST_PLAY_FLAG_TEXT
            playbin ["flags"] = flags;

            Log.Debug ("[subtitle]: looking for subtitles for video file");
            scheme = uri.Scheme;
            string[] subtitle_extensions = { ".srt", ".sub", ".smi", ".txt", ".mpl", ".dks", ".qtx" };
            if (scheme == null || scheme == "file") {
                return;
            }

            dot = uri.AbsoluteUri.LastIndexOf ('.');
            if (dot == -1) {
                return;
            }
            filename = uri.AbsoluteUri.Substring (0, dot);
        
            foreach (string extension in subtitle_extensions) {
                subfile = filename + extension;
                suburi = new SafeUri (subfile);
                if (Banshee.IO.File.Exists (suburi)) {
                    Log.DebugFormat ("[subtitle]: Found subtitle file: {0}", subfile);
                    playbin ["suburi"] = subfile;
                    return;
                }
            }
        }

        public override void Play ()
        {
            playbin.SetState (Gst.State.Playing);
            video_manager.InvalidateOverlay ();
            video_manager.MaybePrepareOverlay ();
        }

        public override void Pause ()
        {
            playbin.SetState (Gst.State.Paused);
        }

        public override void Close (bool fullShutdown)
        {
            playbin.SetState (State.Null);
            base.Close (fullShutdown);
        }

        public override string GetSubtitleDescription (int index)
        {
            var list = (TagList)playbin.Emit ("get-text-tags", new object[] { index });

            if (list == null)
                return String.Empty;

            string code;
            if (!list.GetString (Constants.TAG_LANGUAGE_CODE, out code))
                return String.Empty;

            var name = Gst.Tags.GlobalTag.TagGetLanguageName (code);
            Log.Debug ("Subtitle language code " + code + " resolved to " + name);

            return name;
        }

        public override ushort Volume {
            get { return (ushort) Math.Round (audio_sink.Volume * 100.0); }
            set {
                double volume = ((double)value) / 100.0;
                audio_sink.Volume = volume;
                if (audio_sink.VolumeNeedsSaving) {
                    PlayerEngineService.VolumeSchema.Set (value);
                }
            }
        }

        public override bool CanSeek {
            get { return true; }
        }

        private static Format query_format = Format.Time;
        public override uint Position {
            get {
                long pos;
                playbin.QueryPosition (query_format, out pos);
                return (uint) ((ulong)pos / Constants.MSECOND);
            }
            set { Seek (value); }
        }

        public override uint Length {
            get {
                long duration;
                playbin.QueryDuration (query_format, out duration);
                return (uint) ((ulong)duration / Constants.MSECOND);
            }
        }

        private static string [] source_capabilities = { "file", "http", "cdda", "dvd", "vcd" };
        public override IEnumerable SourceCapabilities {
            get { return source_capabilities; }
        }

        private static string [] decoder_capabilities = { "ogg", "wma", "asf", "flac" };
        public override IEnumerable ExplicitDecoderCapabilities {
            get { return decoder_capabilities; }
        }

        public override string Id {
            get { return "gstreamer-sharp"; }
        }

        public override string Name {
            get { return Catalog.GetString ("GStreamer# 0.10"); }
        }

        public override bool SupportsEqualizer {
            get { return audio_sink != null && audio_sink.SupportsEqualizer; }
        }

        public double AmplifierLevel {
            set {
                if (SupportsEqualizer) {
                    audio_sink.AmplifierLevel = value;
                }
            }
        }
        public int [] BandRange {
            get {
                if (SupportsEqualizer) {
                    return audio_sink.BandRange;
                }
                return new int [] {};
            }
        }

        public uint [] EqualizerFrequencies {
            get {
                if (SupportsEqualizer) {
                    return audio_sink.EqualizerFrequencies;
                }
                return new uint [] {};
            }
        }

        public void SetEqualizerGain (uint band, double gain)
        {
            if (SupportsEqualizer) {
                audio_sink.SetEqualizerGain (band, gain);
            }
        }

        public override VideoDisplayContextType VideoDisplayContextType {
            get { return video_manager != null ? video_manager.VideoDisplayContextType : VideoDisplayContextType.Unsupported; }
        }

        public override IntPtr VideoDisplayContext {
            set {
                if (video_manager != null)
                    video_manager.VideoDisplayContext = value;
            }
            get { return video_manager != null ? video_manager.VideoDisplayContext : IntPtr.Zero; }
        }

        public override int SubtitleCount {
            get { return (int)playbin ["n-text"]; }
        }

        public override int SubtitleIndex {
            set {
                if (SubtitleCount == 0 || value < -1 || value >= SubtitleCount)
                    return;
                int flags = (int)playbin.Flags;

                if (value == -1) {
                    flags &= ~(1 << 2);//GST_PLAY_FLAG_TEXT
                    playbin ["flags"] = flags;
                } else {
                    flags |= (1 << 2);//GST_PLAY_FLAG_TEXT
                    playbin ["flags"] = flags;
                    playbin ["current-text"] = value;
                }
            }
        }

        public override SafeUri SubtitleUri {
            set {
                long pos = -1;
                State state, pending;
                Format format = Format.Bytes;
                bool paused = false;

                // GStreamer playbin does not support setting the suburi during playback
                // so we have to stop/play and seek
                playbin.GetState (out state, out pending, 0);
                paused = (state == State.Paused);
                if (state >= State.Paused) {
                    playbin.QueryPosition (format, out pos);
                    playbin.SetState (State.Ready);
                    // Wait for the state change to complete
                    playbin.GetState (out state, out pending, 0);
                }

                playbin ["suburi"] = value.AbsoluteUri;
                playbin.SetState (paused ? State.Paused : State.Playing);

                // Wait for the state change to complete
                playbin.GetState (out state, out pending, 0);

                if (pos != -1) {
                    playbin.SeekSimple (format, SeekFlags.Flush | SeekFlags.KeyUnit, pos);
                }
            }
            get { return new SafeUri ((string)playbin ["suburi"]); }
        }

#region DVD support

        public override void NotifyMouseMove (double x, double y)
        {
            dvd_manager.NotifyMouseMove (playbin, x, y);
        }

        public override void NotifyMouseButtonPressed (int button, double x, double y)
        {
            dvd_manager.NotifyMouseButtonPressed (playbin, button, x, y);
        }

        public override void NotifyMouseButtonReleased (int button, double x, double y)
        {
            dvd_manager.NotifyMouseButtonReleased (playbin, button, x, y);
        }

        public override void NavigateToLeftMenu ()
        {
            dvd_manager.NavigateToLeftMenu (playbin);
        }

        public override void NavigateToRightMenu ()
        {
            dvd_manager.NavigateToRightMenu (playbin);
        }

        public override void NavigateToUpMenu ()
        {
            dvd_manager.NavigateToUpMenu (playbin);
        }

        public override void NavigateToDownMenu ()
        {
            dvd_manager.NavigateToDownMenu (playbin);
        }

        public override void NavigateToMenu ()
        {
            dvd_manager.NavigateToMenu (playbin);
        }

        public override void ActivateCurrentMenu ()
        {
            dvd_manager.ActivateCurrentMenu (playbin);
        }

        public override void GoToNextChapter ()
        {
            dvd_manager.GoToNextChapter (playbin);
        }

        public override void GoToPreviousChapter ()
        {
            dvd_manager.GoToPreviousChapter (playbin);
        }

        public override bool InDvdMenu {
            get { return dvd_manager.InDvdMenu; }
        }

#endregion

#region Preferences

        private PreferenceBase replaygain_preference;

        private void InstallPreferences ()
        {
            PreferenceService service = ServiceManager.Get<PreferenceService> ();
            if (service == null) {
                return;
            }

            replaygain_preference = service["general"]["misc"].Add (new SchemaPreference<bool> (ReplayGainEnabledSchema,
                Catalog.GetString ("_Enable ReplayGain correction"),
                Catalog.GetString ("For tracks that have ReplayGain data, automatically scale (normalize) playback volume"),
                delegate { audio_sink.ReplayGainEnabled = ReplayGainEnabledSchema.Get (); }
            ));
        }

        private void UninstallPreferences ()
        {
            PreferenceService service = ServiceManager.Get<PreferenceService> ();
            if (service == null) {
                return;
            }

            service["general"]["misc"].Remove (replaygain_preference);
            replaygain_preference = null;
        }

        public static readonly SchemaEntry<bool> ReplayGainEnabledSchema = new SchemaEntry<bool> (
            "player_engine", "replay_gain_enabled",
            false,
            "Enable ReplayGain",
            "If ReplayGain data is present on tracks when playing, allow volume scaling"
        );

#endregion
    }
}
