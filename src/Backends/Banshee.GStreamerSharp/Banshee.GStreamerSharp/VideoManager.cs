//
// VideoManager.cs
//
// Authors:
//  Olivier Dufour <olivier.duff@gmail.com>
//  Andrés G. Aragoneses <knocte@gmail.com>
//  Stephan Sundermann <stephansundermann@gmail.com>
//
// Copyright (C) 2011 Olivier Dufour
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
using Gst.Video;

namespace Banshee.GStreamerSharp
{
    public class VideoManager
    {
        Element playbin;
        VideoDisplayContextType video_display_context_type;
        IntPtr video_window;
        IntPtr? video_window_xid;
        VideoOverlayAdapter xoverlay;
        object video_mutex = new object ();

        public VideoManager (Element playbin)
        {
            this.playbin = playbin;
        }

        public void Initialize ()
        {
            Element videosink;
            video_display_context_type = VideoDisplayContextType.GdkWindow;

            //FIXME BEFORE PUSHING NEW GST# BACKEND: is gconfvideosink gone in 1.0? it is not there in the unmanaged backend 
            videosink = ElementFactory.Make ("gconfvideosink", "videosink");
            if (videosink == null) {
                videosink = ElementFactory.Make ("autovideosink", "videosink");
                if (videosink == null) {
                    video_display_context_type = VideoDisplayContextType.Unsupported;
                    videosink = ElementFactory.Make ("fakesink", "videosink");
                    if (videosink != null) {
                        videosink ["sync"] = true;
                    }
                }
            }

            playbin ["video-sink"] = videosink;

            // FIXME: the 2 lines below (SyncHandler and ElementAdded), if uncommented, cause hangs, and they
            //        don't seem to be useful at this point anyway, remove?
            //a) playbin.Bus.SyncHandler = (bus, message) => { return bus.SyncSignalHandler (message); };
            //b) playbin.Bus.EnableSyncMessageEmission ();
            playbin.Bus.SyncMessage += OnSyncMessage;
            //if (videosink is Bin) { ((Bin)videosink).ElementAdded += OnVideoSinkElementAdded; }

            RaisePrepareWindow ();
        }

        private void RaisePrepareWindow ()
        {
            var handler = PrepareWindow;
            if (handler != null) {
                handler ();
            }
        }

        public delegate void PrepareWindowHandler ();
        public event PrepareWindowHandler PrepareWindow;

        public delegate void VideoGeometryHandler (int width, int height, int fps_n, int fps_d, int par_n, int par_d);
        public event VideoGeometryHandler VideoGeometry;

        private void OnSyncMessage (object o, SyncMessageArgs args)
        {
            Message message = args.Message;

            var src = message.Src as Element;
            if (src == null) {
                return;
            }

            if (!Gst.Video.Global.IsVideoOverlayPrepareWindowHandleMessage (message)) {
                return;
            }

            bool found_xoverlay = FindXOverlay ();

            if (found_xoverlay) {
                xoverlay.WindowHandle = video_window_xid.Value;
                xoverlay.HandleEvents (true);
            }
        }

        private void OnVideoSinkElementAdded (object o, ElementAddedArgs args)
        {
            FindXOverlay ();
        }

        internal void InvalidateOverlay ()
        {
            lock (video_mutex) {
                xoverlay = null;
            }
        }

        internal bool MaybePrepareOverlay ()
        {
            if (!video_window_xid.HasValue) {
                //this is not a video
                return false;
            }

            if (xoverlay == null && !FindXOverlay ()) {
                return false;
            }

            xoverlay.WindowHandle = video_window_xid.Value;
            xoverlay.HandleEvents (true);
            return true;
        }

        private bool FindXOverlay ()
        {
            Element video_sink = null;
            Element xoverlay_element;
            bool    found_xoverlay;

            video_sink = (Element)playbin ["video-sink"];

            lock (video_mutex) {

                if (video_sink == null) {
                    xoverlay = null;
                    return false;
                }

                xoverlay_element = video_sink is Bin
                    ? ((Bin)video_sink).GetByInterface (VideoOverlayAdapter.GType)
                    : video_sink;

                if (xoverlay_element == null) {
                    return false;
                }

                xoverlay = new VideoOverlayAdapter (xoverlay_element.Handle);

                if (!PlatformDetection.IsWindows) {
                    // We can't rely on aspect ratio from dshowvideosink
                    if (xoverlay != null) {
                        try {
                            xoverlay_element ["force-aspect-ratio"] = true;
                        } catch (PropertyNotFoundException) { }
                    }
                }

                if (xoverlay != null) {
                    try {
                        xoverlay_element ["handle-events"] = false;
                    } catch (PropertyNotFoundException) { }
                }

                found_xoverlay = (xoverlay != null) ? true : false;

                return found_xoverlay;
            }
        }

        public void ParseStreamInfo ()
        {
            int audios_streams;
            int video_streams;
            int text_streams;
            Pad vpad = null;

            audios_streams = (int)playbin["n-audio"];
            video_streams = (int)playbin ["n-video"];
            text_streams = (int)playbin["n-text"];

            if (video_streams > 0) {
                int i;
                /* Try to obtain a video pad */
                for (i = 0; i < video_streams; i++) {
                    vpad = (Pad)playbin.Emit ("get-video-pad", new object [] { i });
                    if (vpad != null) {
                        break;
                    }
                }
            }

            if (vpad != null) {
                Caps caps = vpad.CurrentCaps;
                if (caps != null) {
                    OnCapsSet (vpad, null);
                }
                vpad.AddNotification ("caps", OnCapsSet);
            }
        }

        private void OnCapsSet (object o, GLib.NotifyArgs args)
        {
            Structure s = null;
            int width, height, fps_n, fps_d, par_n, par_d;
            Caps caps = ((Pad)o).CurrentCaps;

            width = height = fps_n = fps_d = 0;
            if (caps == null) {
                return;
            }

            /* Get video decoder caps */
            s = caps.GetStructure (0);
            if (s != null) {
                /* We need at least width/height and framerate */
                if (!(s.HasField ("framerate") && s.HasField ("width") && s.HasField ("height"))) {
                    return;
                }
                Fraction f = new Fraction (s.GetValue ("framerate"));
                fps_n = f.Numerator;
                fps_d = f.Denominator;
                GLib.Value val;
                width = (int)s.GetValue ("width");
                height = (int)s.GetValue ("height");
                /* Get the PAR if available */
                val = s.GetValue ("pixel-aspect-ratio");
                if (!val.Equals (GLib.Value.Empty)) {
                    Fraction par = new Fraction (val);
                    par_n = par.Numerator;
                    par_d = par.Denominator;
                }
                else { /* Square pixels */
                    par_n = 1;
                    par_d = 1;
                }

                /* Notify PlayerEngine if a callback was set */
                RaiseVideoGeometry (width, height, fps_n, fps_d, par_n, par_d);
            }
        }

        private void RaiseVideoGeometry (int width, int height, int fps_n, int fps_d, int par_n, int par_d)
        {
            var handler = VideoGeometry;
            if (handler != null) {
                handler (width, height, fps_n, fps_d, par_n, par_d);
            }
        }

        public void WindowExpose (IntPtr window, bool direct)
        {
            if (direct && xoverlay != null) {
                xoverlay.Expose ();
                return;
            }

            if (MaybePrepareOverlay () && xoverlay != null) {
                xoverlay.Expose ();
            }
        }

        public void WindowRealize (IntPtr window)
        {
            if (PlatformDetection.IsWindows) {
                video_window_xid = gdk_win32_drawable_get_handle (window);
            } else if (PlatformDetection.IsMac) {
                // On OSX, gdk can either be used with the quartz backend or the x11 one.
                // There is no function in gdk which returns which backend is currently used.
                // Therefore try to get the handle from the quartz backend first (which is more common)
                // A failure to find that function will result in a EntryPointNotFoundException
                try {
                    video_window_xid = gdk_quartz_window_get_nsview (window);
                } catch (EntryPointNotFoundException ex) {
                    // The bockbuild package always builds the quartz backend so this exception
                    // would never happen for a bockbuild build. The fallback is used when someone is still
                    // using XQuartz and did compile all the dependencies and stuff on their own.

                    Hyena.Log.Warning (ex);

                    video_window_xid = gdk_x11_window_get_xid (window);
                }
            } else if (PlatformDetection.IsUnix) {
                    //FIXME: we should maybe stop relying on x11 http://gstreamer.freedesktop.org/data/doc/gstreamer/head/gst-plugins-base-libs/html/gst-plugins-base-libs-gstvideooverlay.html#GstVideoOverlay
                video_window_xid = gdk_x11_window_get_xid (window);
            }
        }

        [DllImport ("libgdk-3-0.dll")]
        private static extern IntPtr gdk_quartz_window_get_nsview (IntPtr drawable);

        [DllImport ("libgdk-3-0.dll")]
        private static extern IntPtr gdk_x11_window_get_xid (IntPtr drawable);

        [DllImport ("libgdk-3-0.dll")]
        private static extern IntPtr gdk_win32_drawable_get_handle (IntPtr drawable);

        public VideoDisplayContextType VideoDisplayContextType {
            get { return video_display_context_type; }
        }

        public IntPtr VideoDisplayContext {
            set { 
                if (VideoDisplayContextType == VideoDisplayContextType.GdkWindow) {
                    video_window = value;
                }
            }
            get { 
                if (VideoDisplayContextType == VideoDisplayContextType.GdkWindow) {
                    return video_window;
                }
                return IntPtr.Zero;
            }
        }
    }
}
