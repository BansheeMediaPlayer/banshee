//
// Transcoder.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
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
using System.Threading;
using System.Runtime.InteropServices;
using Mono.Unix;

using Hyena;
using Banshee.Base;
using Banshee.Collection;
using Banshee.ServiceStack;
using Banshee.MediaEngine;
using Banshee.MediaProfiles;
using Banshee.Configuration.Schema;

namespace Banshee.GStreamer
{
    public class Transcoder : ITranscoder
    {
        public event TranscoderProgressHandler Progress;
        public event TranscoderTrackFinishedHandler TrackFinished;
        public event TranscoderErrorHandler Error;

        private HandleRef handle;
        private GstTranscoderProgressCallback ProgressCallback;
        private GstTranscoderFinishedCallback FinishedCallback;
        private GstTranscoderErrorCallback ErrorCallback;
        private TrackInfo current_track;
        private string error_message;
        private SafeUri managed_output_uri;

        public Transcoder ()
        {
            IntPtr ptr = gst_transcoder_new();

            if(ptr == IntPtr.Zero) {
                throw new NullReferenceException(Catalog.GetString("Could not create transcoder"));
            }

            handle = new HandleRef(this, ptr);

            ProgressCallback = new GstTranscoderProgressCallback(OnNativeProgress);
            FinishedCallback = new GstTranscoderFinishedCallback(OnNativeFinished);
            ErrorCallback = new GstTranscoderErrorCallback(OnNativeError);

            gst_transcoder_set_progress_callback(handle, ProgressCallback);
            gst_transcoder_set_finished_callback(handle, FinishedCallback);
            gst_transcoder_set_error_callback(handle, ErrorCallback);
        }

        public void Finish ()
        {
            gst_transcoder_free(handle);
            handle = new HandleRef (this, IntPtr.Zero);
        }

        public void Cancel ()
        {
            gst_transcoder_cancel(handle);
            handle = new HandleRef (this, IntPtr.Zero);
        }

        public void TranscodeTrack (TrackInfo track, SafeUri outputUri, ProfileConfiguration config)
        {
            if(IsTranscoding) {
                throw new ApplicationException("Transcoder is busy");
            }

            Log.DebugFormat ("Transcoding {0} to {1}", track.Uri, outputUri);
            SafeUri inputUri = track.Uri;
            managed_output_uri = outputUri;
            IntPtr input_uri = GLib.Marshaller.StringToPtrGStrdup(inputUri.AbsoluteUri);
            IntPtr output_uri = GLib.Marshaller.StringToPtrGStrdup(outputUri.AbsoluteUri);

            error_message = null;

            current_track = track;
            gst_transcoder_transcode(handle, input_uri, output_uri, config.Profile.Pipeline.GetProcessById("gstreamer"));

            GLib.Marshaller.Free(input_uri);
            GLib.Marshaller.Free(output_uri);
        }

        private void OnNativeProgress(IntPtr transcoder, double fraction)
        {
            OnProgress (current_track, fraction);
        }

        private void OnNativeFinished(IntPtr transcoder)
        {
            OnTrackFinished (current_track, managed_output_uri);
        }

        private void OnNativeError(IntPtr transcoder, IntPtr error, IntPtr debug)
        {
            error_message = GLib.Marshaller.Utf8PtrToString(error);

            if(debug != IntPtr.Zero) {
                string debug_string = GLib.Marshaller.Utf8PtrToString(debug);
                if(!String.IsNullOrEmpty (debug_string)) {
                    error_message = String.Format ("{0}: {1}", error_message, debug_string);
                }
            }

            try {
                Banshee.IO.File.Delete (managed_output_uri);
            } catch {}

            OnError (current_track, error_message);
        }

        protected virtual void OnProgress (TrackInfo track, double fraction)
        {
            TranscoderProgressHandler handler = Progress;
            if (handler != null) {
                handler (this, new TranscoderProgressArgs (track, fraction, track.Duration));
            }
        }

        protected virtual void OnTrackFinished (TrackInfo track, SafeUri outputUri)
        {
            TranscoderTrackFinishedHandler handler = TrackFinished;
            if (handler != null) {
                handler (this, new TranscoderTrackFinishedArgs (track, outputUri));
            }
        }

        protected virtual void OnError (TrackInfo track, string message)
        {
            TranscoderErrorHandler handler = Error;
            if (handler != null) {
                handler (this, new TranscoderErrorArgs (track, message));
            }
        }

        public bool IsTranscoding {
            get { return gst_transcoder_get_is_transcoding(handle); }
        }

        public string ErrorMessage {
            get { return error_message; }
        }

        private delegate void GstTranscoderProgressCallback(IntPtr transcoder, double progress);
        private delegate void GstTranscoderFinishedCallback(IntPtr transcoder);
        private delegate void GstTranscoderErrorCallback(IntPtr transcoder, IntPtr error, IntPtr debug);

        [DllImport(PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr gst_transcoder_new();

        [DllImport(PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void gst_transcoder_free(HandleRef handle);

        [DllImport(PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void gst_transcoder_transcode(HandleRef handle, IntPtr input_uri,
            IntPtr output_uri, string encoder_pipeline);

        [DllImport(PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void gst_transcoder_cancel(HandleRef handle);

        [DllImport(PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void gst_transcoder_set_progress_callback(HandleRef handle,
            GstTranscoderProgressCallback cb);

        [DllImport(PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void gst_transcoder_set_finished_callback(HandleRef handle,
            GstTranscoderFinishedCallback cb);

        [DllImport(PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void gst_transcoder_set_error_callback(HandleRef handle,
            GstTranscoderErrorCallback cb);

        [DllImport(PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool gst_transcoder_get_is_transcoding(HandleRef handle);
    }
}
