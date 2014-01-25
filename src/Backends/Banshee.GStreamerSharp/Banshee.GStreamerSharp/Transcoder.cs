//
// Transcoder.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//   Olivier Dufour <olivier.duff@gmail.com>
//   Andrés G. Aragoneses <knocte@gmail.com>
//   Stephan Sundermann <stephansundermann@gmail.com>
//
// Copyright (C) 2008 Novell, Inc.
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
using System.Runtime.InteropServices;
using Mono.Unix;

using Hyena;
using Banshee.Base;
using Banshee.Collection;
using Banshee.ServiceStack;
using Banshee.MediaEngine;
using Banshee.MediaProfiles;
using Banshee.Configuration.Schema;

using Gst;

namespace Banshee.GStreamerSharp
{
    public class Transcoder : ITranscoder
    {
        private class AudioSinkBin : Bin
        {
            public AudioSinkBin (IntPtr o) : base (o)
            {}

            public AudioSinkBin (string elementName, string encoder_pipeline, string output_uri) : base (elementName)
            {
                Element encoder_elem = null;
                Element sink_elem;
                Element conv_elem;
                Element resample_elem;
                Pad encoder_pad;

                sink_elem = Element.MakeFromUri (URIType.Sink, output_uri, "sink");
                if (sink_elem == null) {
                    throw new Exception (Catalog.GetString ("Could not create sink element"));
                }

                conv_elem = ElementFactory.Make ("audioconvert", "audioconvert");
                if (conv_elem == null) {
                    throw new Exception (Catalog.GetString ("Could not create audioconvert plugin"));
                }

                resample_elem = ElementFactory.Make ("audioresample", "audioresample");
                if (resample_elem == null) {
                    throw new Exception (Catalog.GetString ("Could not create audioresample plugin"));
                }

                try {
                    encoder_elem = Parse.BinFromDescription (encoder_pipeline, true);
                } catch (Exception e) {
                    string err = Catalog.GetString ("Could not create encoding pipeline : {0}");
                    throw new Exception (String.Format (err, e.Message));
                }

                encoder_pad = conv_elem.GetStaticPad ("sink");
                if (encoder_pad == null) {
                    throw new Exception (Catalog.GetString ("Could not get sink pad from encoder"));
                }

                this.Add (conv_elem, resample_elem, encoder_elem, sink_elem);
                Element.Link (conv_elem, resample_elem, encoder_elem, sink_elem);

                AddPad (new GhostPad ("sink", encoder_pad));
            }
        }

        public event TranscoderProgressHandler Progress;
        public event TranscoderTrackFinishedHandler TrackFinished;
        public event TranscoderErrorHandler Error;

        private TrackInfo current_track;
        private string error_message;
        private SafeUri output_uri;

        bool is_transcoding;
        Gst.Pipeline pipeline;
        AudioSinkBin sink_bin;
        System.Timers.Timer timer;
    
        public Transcoder ()
        {
            timer = new System.Timers.Timer ();
            timer.Interval = 200;
            timer.AutoReset = true;
            timer.Elapsed += OnTick;
        }

        void OnTick (object o, System.Timers.ElapsedEventArgs args)
        {
            Format format = Format.Time;
            long position, duration;

            if (!pipeline.QueryDuration (format, out duration) ||
                !sink_bin.QueryPosition (format, out position)) {
                return;
            }

            RaiseProgress (current_track, (double)position / (double)duration);
        }

        public void Finish ()
        {
            timer.Stop ();

            if(pipeline != null) {
                pipeline.SetState (State.Null);
                pipeline = null;
            }

            if(output_uri != null) {
                output_uri = null;
            }
        }

        public void Cancel ()
        {
            timer.Stop ();
            is_transcoding = false;
    
            if(pipeline != null) {
                pipeline.SetState (State.Null);
                pipeline = null;
            }

            output_uri = null;
        }

        public void TranscodeTrack (TrackInfo track, SafeUri outputUri, ProfileConfiguration config)
        {
            if(IsTranscoding) {
                throw new ApplicationException("Transcoder is busy");
            }

            Log.DebugFormat ("Transcoding {0} to {1}", track.Uri, outputUri);
            output_uri = outputUri;

            error_message = null;

            current_track = track;
            Transcode (config.Profile.Pipeline.GetProcessById("gstreamer"));

        }

        private bool ConstructPipeline (string encoder_pipeline)
        {
            Element source_elem;
            Element decoder_elem;

            pipeline = new Gst.Pipeline ("pipeline");

            source_elem = Element.MakeFromUri (URIType.Src, current_track.Uri.AbsoluteUri, "source");
            if (source_elem == null) {
                RaiseError (current_track, Catalog.GetString ("Could not create source element"));
                return false;
            }

            decoder_elem = ElementFactory.Make ("decodebin", "the decodebin");
            if (decoder_elem == null) {
                RaiseError (current_track, Catalog.GetString ("Could not create decodebin plugin"));
                return false;
            }

            try {
                sink_bin = new AudioSinkBin ("sinkbin", encoder_pipeline, output_uri.AbsoluteUri);
            } catch (Exception e) {
                RaiseError (current_track, e.Message);
            }

            if (sink_bin == null) {
                RaiseError (current_track, Catalog.GetString ("Could not create sinkbin plugin"));
                return false;
            }

            pipeline.Add (source_elem, decoder_elem, sink_bin);

            source_elem.Link (decoder_elem);

            decoder_elem.PadAdded += OnPadAdded;

            pipeline.Bus.AddWatch (OnBusMessage);

            return true;
        }

        private void OnPadAdded (object sender, PadAddedArgs args)
        {
            Structure str;
            Pad audiopad;

            audiopad = sink_bin.GetStaticPad ("sink");

            if (audiopad.IsLinked) {
                return;
            }

            Caps caps = args.NewPad.QueryCaps ();
            str = caps [0];

            if(!str.Name.Contains ("audio")) {
                return;
            }

            args.NewPad.Link (audiopad);
        }

        private bool OnBusMessage (Bus bus, Message msg)
        {
            switch (msg.Type) {
                case MessageType.Eos:
                    pipeline.SetState (State.Null);
                    pipeline = null;
                    is_transcoding = false;
                    timer.Stop ();
                    RaiseTrackFinished (current_track, output_uri);
                    break;

                case MessageType.Error:
                    GLib.GException ex;
                    string debug;
                    is_transcoding = false;
                    timer.Stop ();
                    msg.ParseError (out ex, out debug);
                    RaiseError (current_track, String.Format ("{0} : {1}", ex.Message, debug));
                    timer.Stop ();
                    break;

                default:
                    break;
            }

            return true;
        }

        private void Transcode (string encoder_pipeline)
        {
            if (IsTranscoding) {
                return;
            }
            if(!ConstructPipeline (encoder_pipeline)) {
                RaiseError (current_track, Catalog.GetString ("Could not construct pipeline")); 
                return;
            }

            is_transcoding = true;
            pipeline.SetState (State.Playing);
            timer.Start ();
        }

        protected virtual void RaiseProgress (TrackInfo track, double fraction)
        {
            TranscoderProgressHandler handler = Progress;
            if (handler != null) {
                handler (this, new TranscoderProgressArgs (track, fraction, track.Duration));
            }
        }

        protected virtual void RaiseTrackFinished (TrackInfo track, SafeUri outputUri)
        {
            TranscoderTrackFinishedHandler handler = TrackFinished;
            if (handler != null) {
                handler (this, new TranscoderTrackFinishedArgs (track, outputUri));
            }
        }

        protected virtual void RaiseError (TrackInfo track, string message)
        {
            try {
                Banshee.IO.File.Delete (output_uri);
            } catch {}
            
            TranscoderErrorHandler handler = Error;
            if (handler != null) {
                handler (this, new TranscoderErrorArgs (track, message));
            }
        }

        public bool IsTranscoding {
            get { return is_transcoding; }
        }

        public string ErrorMessage {
            get { return error_message; }
        }
    }
}
