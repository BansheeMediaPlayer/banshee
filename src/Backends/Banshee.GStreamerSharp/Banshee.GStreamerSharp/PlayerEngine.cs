//
// PlayerEngine.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2010 Novell, Inc.
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
using Gst.BasePlugins;
using Gst.CorePlugins;
using Gst.Interfaces;

using Hyena;
using Hyena.Data;

using Banshee.Base;
using Banshee.Collection;
using Banshee.Streaming;
using Banshee.MediaEngine;
using Banshee.ServiceStack;
using Banshee.Configuration;
using Banshee.Preferences;

namespace Banshee.GStreamerSharp
{
    public class PlayerEngine : Banshee.MediaEngine.PlayerEngine, IEqualizer, IVisualizationDataSource
    {
        private class AudioSinkBin : Bin
        {
            Element hw_audio_sink;
            Element volume;
            Element rgvolume;
            Element equalizer;
            Element preamp;
            Element first;
            GhostPad visible_sink;
            Tee audiotee;
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
                audiotee = ElementFactory.Make ("tee", "audiotee") as Tee;
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

                    Add (eq_audioconvert, preamp, equalizer, eq_audioconvert2);
                    Element.Link (eq_audioconvert, preamp, equalizer, eq_audioconvert2, first);

                    first = eq_audioconvert;
                    Log.Debug ("Built and linked Equalizer");
                }

                // Link the first tee pad to the primary audio sink queue
                Pad sinkpad = first.GetStaticPad ("sink");
                Pad pad = audiotee.GetRequestPad ("src%d");
                audiotee.AllocPad = pad;
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

                if (sink.HasProperty ("volume")) {
                    volumeProvider = sink;
                    Log.DebugFormat ("Sink {0} has native volume.", volumeProvider.Name);
                } else {
                    var sinkBin = sink as Bin;
                    if (sinkBin != null) {
                        foreach (Element e in sinkBin.ElementsRecurse) {
                            if (e.HasProperty ("volume")) {
                                volumeProvider = e;
                                Log.DebugFormat ("Found volume provider {0} in {1}.",
                                    volumeProvider.Name, sink.Name);
                            }
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
                        visible_sink.SetBlocked (true, InsertReplayGain);
                        Log.Debug ("Enabled ReplayGain volume scaling.");
                    } else if (!value && rgvolume != null) {
                        visible_sink.SetBlocked (false, RemoveReplayGain);
                        Log.Debug ("Disabled ReplayGain volume scaling.");
                    }
                }
            }

            void InsertReplayGain (Pad pad, bool blocked)
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
                visible_sink.SetBlocked (false, (_, __) => { });
            }

            void RemoveReplayGain (Pad pad, bool blocked)
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
                visible_sink.SetBlocked (false, (_, __) => { });
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
                    int min = -1;
                    int max = -1;

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

                    return new int [] { min, max };
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
                        ChildProxy equalizer_child_proxy = ChildProxyAdapter.GetObject (equalizer);
                        for (uint i = 0; i < count; i++) {
                            Gst.Object band = equalizer_child_proxy.GetChildByIndex (i);
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
                    Gst.Object the_band = ChildProxyAdapter.GetObject (equalizer).GetChildByIndex (band);
                    the_band ["gain"] = value;
                }
            }

            public Pad RequestTeePad ()
            {
                return audiotee.GetRequestPad ("src%d");
            }
        }


        PlayBin2 playbin;
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
            playbin = new PlayBin2 ();

            next_track_set = new ManualResetEvent (false);

            audio_sink = new AudioSinkBin ("audiobin");

            playbin["audio-sink"] = audio_sink;

            if (audio_sink.VolumeNeedsSaving) {
                // Remember the volume from last time
                Volume = (ushort)PlayerEngineService.VolumeSchema.Get ();
            }

            Pad teepad = audio_sink.RequestTeePad ();
            visualization = new Visualization (audio_sink, teepad);

            playbin.AddNotification ("volume", OnVolumeChanged);
            playbin.Bus.AddWatch (OnBusMessage);
            playbin.AboutToFinish += OnAboutToFinish;

            cdda_manager = new CddaManager (playbin);
            dvd_manager = new DvdManager (playbin);
            // FIXME: Disable video stuff until GLib# 3 is used instead of the sopy bundled in GStreamerSharp
            //video_manager = new VideoManager (playbin);
            //video_manager.PrepareWindow += OnVideoPrepareWindow;
            //video_manager.Initialize ();

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

        private System.DateTime about_to_finish_time_stamp;

        void OnAboutToFinish (object o, Gst.GLib.SignalArgs args)
        {
            // HACK: ugly workaround for GStreamer's bug http://bugzilla.gnome.org/722769
            // long story short, AboutToFinish signal firing twice for the same play of the same track
            // causes problems when Gapless Enabled because of RequestNextTrack event being fired twice
            if (about_to_finish_time_stamp == CurrentTrackTimeStamp) {
                return;
            }
            about_to_finish_time_stamp = CurrentTrackTimeStamp;


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
            playbin.Uri = uri.AbsoluteUri;

            if (maybeVideo) {
                LookupForSubtitle (uri);
            }

            next_track_set.Set ();
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
                    int buffer_percent;
                    msg.ParseBuffering (out buffer_percent);
                    HandleBuffering (buffer_percent);
                    break;

                case MessageType.Tag:
                    Pad pad;
                    TagList tag_list;
                    msg.ParseTag (out pad, out tag_list);

                    HandleTag (pad, tag_list);
                    break;

                case MessageType.Error:
                    Enum error_type;
                    string err_msg, debug;
                    msg.ParseError (out error_type, out err_msg, out debug);

                    HandleError (error_type, err_msg, debug);
                    break;

                case MessageType.Element:
                    if (MissingPluginMessage.IsMissingPluginMessage (msg)) {
                        string detail = MissingPluginMessage.GetInstallerDetail (msg);

                        if (detail == null)
                            return false;

                        if (missing_details.Contains (detail)) {
                            Log.DebugFormat ("Ignoring missing element details, already prompted ('{0}')", detail);
                            return false;
                        }

                        Log.DebugFormat ("Saving missing element details ('{0}')", detail);
                        missing_details.Add (detail);

                        Log.Error ("Missing GStreamer Plugin", MissingPluginMessage.GetDescription (msg), true);

                        InstallPluginsContext install_context = new InstallPluginsContext ();
                        Install.InstallPlugins (missing_details.ToArray (), install_context, OnInstallPluginsReturn);
                    } else if (msg.Src == playbin && msg.Structure.Name == "playbin2-stream-changed") {
                        HandleStreamChanged ();
                    } else if (NavigationMessage.MessageGetType (msg) == NavigationMessageType.CommandsChanged) {
                        dvd_manager.HandleCommandsChanged (playbin);
                    }
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

        private void OnVolumeChanged (object o, Gst.GLib.NotifyArgs args)
        {
            OnEventChanged (PlayerEvent.Volume);
        }

        private void HandleStreamChanged ()
        {
            // Set the current track as fully played before signaling EndOfStream.
            ServiceManager.PlayerEngine.IncrementLastPlayed (1.0);
            OnEventChanged (PlayerEvent.EndOfStream);
            OnEventChanged (PlayerEvent.StartOfStream);
        }

        private void HandleError (Enum domain, string error_message, string debug)
        {
            TrackInfo failed_track = CurrentTrack;
            Close (true);

            error_message = error_message ?? Catalog.GetString ("Unknown Error");

            if (domain is ResourceError) {
                ResourceError domain_code = (ResourceError)domain;
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
            } else if (domain is StreamError) {
                StreamError domain_code = (StreamError)domain;
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
            } else if (domain is CoreError) {
                CoreError domain_code = (CoreError)domain;
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
                    Log.Error (String.Format ("GStreamer core error: {0}", (CoreError)domain), false);
                }
            } else if (domain is LibraryError) {
                Log.Error (String.Format ("GStreamer library error: {0}", (LibraryError)domain), false);
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

        private void HandleTag (Pad pad, TagList tag_list)
        {
            foreach (string tag in tag_list.Tags) {
                if (String.IsNullOrEmpty (tag)) {
                    continue;
                }

                if (tag_list.GetTagSize (tag) < 1) {
                    continue;
                }

                List tags = tag_list.GetTag (tag);

                foreach (object o in tags) {
                    OnTagFound (new StreamTag () { Name = tag, Value = o });
                }
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

            playbin.Uri = uri.AbsoluteUri;
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
            int flags;
            flags = (int)playbin.Flags;
            flags |= (1 << 2);//GST_PLAY_FLAG_TEXT
            playbin.Flags = (ObjectFlags)flags;

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
                    playbin.Suburi = subfile;
                    return;
                }
            }
        }

        public override void Play ()
        {
            playbin.SetState (Gst.State.Playing);
            OnStateChanged (PlayerState.Playing);
        }

        public override void Pause ()
        {
            playbin.SetState (Gst.State.Paused);
            OnStateChanged (PlayerState.Paused);
        }

        public override void Close (bool fullShutdown)
        {
            playbin.SetState (State.Null);
            base.Close (fullShutdown);
        }

        public override string GetSubtitleDescription (int index)
        {
            return playbin.GetTextTags (index)
             .GetTag (Gst.Tag.LanguageCode)
             .Cast<string> ()
             .FirstOrDefault (t => t != null);
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
                playbin.QueryPosition (ref query_format, out pos);
                return (uint) ((ulong)pos / Gst.Clock.MSecond);
            }
            set {
                playbin.Seek (Format.Time, SeekFlags.Accurate, (long)(value * Gst.Clock.MSecond));
            }
        }

        public override uint Length {
            get {
                long duration;
                playbin.QueryDuration (ref query_format, out duration);
                return (uint) ((ulong)duration / Gst.Clock.MSecond);
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
            get { return playbin.NText; }
        }

        public override int SubtitleIndex {
            set {
                if (SubtitleCount == 0 || value < -1 || value >= SubtitleCount)
                    return;
                int flags = (int)playbin.Flags;

                if (value == -1) {
                    flags &= ~(1 << 2);//GST_PLAY_FLAG_TEXT
                    playbin.Flags = (ObjectFlags)flags;
                } else {
                    flags |= (1 << 2);//GST_PLAY_FLAG_TEXT
                    playbin.Flags = (ObjectFlags)flags;
                    playbin.CurrentText = value;
                }
            }
        }

        public override SafeUri SubtitleUri {
            set {
                long pos = -1;
                State state;
                Format format = Format.Bytes;
                bool paused = false;

                // GStreamer playbin does not support setting the suburi during playback
                // so we have to stop/play and seek
                playbin.GetState (out state, 0);
                paused = (state == State.Paused);
                if (state >= State.Paused) {
                    playbin.QueryPosition (ref format, out pos);
                    playbin.SetState (State.Ready);
                    // Wait for the state change to complete
                    playbin.GetState (out state, 0);
                }

                playbin.Suburi = value.AbsoluteUri;
                playbin.SetState (paused ? State.Paused : State.Playing);

                // Wait for the state change to complete
                playbin.GetState (out state, 0);

                if (pos != -1) {
                    playbin.Seek (format, SeekFlags.Flush | SeekFlags.KeyUnit, pos);
                }
            }
            get { return new SafeUri (playbin.Suburi); }
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
