//
// BpmDetector.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//   Andrés G. Aragoneses <knocte@gmail.com>
//   Stephan Sundermann <stephansundermann@gmail.com>
//
// Copyright (C) 2008-2011 Novell, Inc.
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
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Mono.Unix;

using Gst;

using Hyena;
using Hyena.Data;

using Banshee.Base;
using Banshee.Streaming;
using Banshee.MediaEngine;
using Banshee.ServiceStack;
using Banshee.Configuration;
using Banshee.Preferences;

namespace Banshee.GStreamerSharp
{
    public class BpmDetector : IBpmDetector
    {
        SafeUri current_uri;
        Dictionary<int, int> bpm_histogram = new Dictionary<int, int> ();

        Pipeline pipeline;
        Element filesrc;
        Element fakesink;

        public event BpmEventHandler FileFinished;

        public BpmDetector ()
        {
            try {
                pipeline = new Pipeline ("the pipeline");
                filesrc          = ElementFactory.Make ("filesrc", "the filesrc");
                var decodebin    = ElementFactory.Make ("decodebin", "the decodebin");
                var audioconvert = Make ("audioconvert");
                var bpmdetect    = Make ("bpmdetect");
                fakesink         = ElementFactory.Make ("fakesink", "the fakesink");

                pipeline.Add (filesrc, decodebin, audioconvert, bpmdetect, fakesink);

                if (!filesrc.Link (decodebin)) {
                    Log.Error ("Could not link pipeline elements");
                    throw new Exception ();
                }

                // decodebin and audioconvert are linked dynamically when the decodebin creates a new pad
                decodebin.PadAdded += (o,args) => {
                    var audiopad = audioconvert.GetStaticPad ("sink");
                    if (audiopad.IsLinked) {
                        return;
                    }

                    var caps = args.NewPad.QueryCaps ();
                    if (caps == null)
                        return;

                    var str = caps[0];
                    if (!str.Name.Contains ("audio"))
                        return;

                    args.NewPad.Link (audiopad);
                };

                if (!Element.Link (audioconvert, bpmdetect, fakesink)) {
                    Log.Error ("Could not link pipeline elements");
                    throw new Exception ();
                }
                pipeline.Bus.AddWatch (OnBusMessage);
            } catch (Exception e) {
                Log.Error (e);
                throw new ApplicationException (Catalog.GetString ("Could not create BPM detection driver."), e);
            }
        }

        private bool OnBusMessage (Bus bus, Message msg)
        {
            switch (msg.Type) {
            case MessageType.Tag:
                TagList tag_list = msg.ParseTag ();

                foreach (var name in tag_list.Tags) {
                    if (name == "beats-per-minute") {
                        if (tag_list.GetTagSize (name) < 1) {
                            continue;
                        }
                        tag_list.Foreach (delegate(TagList list, string tagname) {
                            for (uint i = 0; i < tag_list.GetTagSize (tagname); i++) {
                                GLib.Value val = tag_list.GetValueIndex (tagname, i);
                                if (val.Val is double) {
                                    double bpm = (double)val;
                                    int rounded = (int)Math.Round (bpm);
                                    if (!bpm_histogram.ContainsKey (rounded)) {
                                        bpm_histogram [rounded] = 1;
                                    } else {
                                        bpm_histogram [rounded]++;
                                    }
                                }
                                val.Dispose ();
                            }
                        });
                    }
                }
                break;

            case MessageType.Error:
                string debug;
                GLib.GException error;
                msg.ParseError (out error, out debug);

                IsDetecting = false;
                Log.ErrorFormat ("BPM Detection error: {0}", error.Message);
                break;

            case MessageType.Eos:
                IsDetecting = false;
                pipeline.SetState (State.Null);

                SafeUri uri = current_uri;
                int best_bpm = -1, best_bpm_count = 0;
                foreach (int bpm in bpm_histogram.Keys) {
                    int count = bpm_histogram[bpm];
                    if (count > best_bpm_count) {
                        best_bpm_count = count;
                        best_bpm = bpm;
                    }
                }

                Reset ();

                var handler = FileFinished;
                if (handler != null) {
                    handler (this, new BpmEventArgs (uri, best_bpm));
                }

                break;
            }

            return true;
        }

        public void Dispose ()
        {
            Reset ();

            if (pipeline != null) {
                pipeline.SetState (State.Null);
                pipeline.Dispose ();
                pipeline = null;
            }
        }

        public bool IsDetecting { get; private set; }

        public void ProcessFile (SafeUri uri)
        {
            Reset ();
            current_uri = uri;
            string path = uri.LocalPath;

            try {
                Log.DebugFormat ("GStreamer running beat detection on {0}", path);
                IsDetecting = true;
                fakesink.SetState (State.Null);
                filesrc ["location"] = path;
                pipeline.SetState (State.Playing);
            } catch (Exception e) {
                Log.Error (e);
            }
        }

        private void Reset ()
        {
            current_uri = null;
            bpm_histogram.Clear ();
        }

        private static Gst.Element Make (string name)
        {
            var e = ElementFactory.Make (name, "the " + name);
            if (e == null) {
                Log.ErrorFormat ("BPM Detector unable to make element '{0}'", name);
                throw new Exception ();
            }
            return e;
        }
    }
}