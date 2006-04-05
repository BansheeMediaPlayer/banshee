/***************************************************************************
 *  GstTranscoder.cs
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@abock.org>
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Runtime.InteropServices;
using Mono.Unix;

namespace Banshee.Base
{    
    internal delegate void GstTranscoderProgressCallback(IntPtr transcoder, double progress);
    internal delegate void GstTranscoderFinishedCallback(IntPtr transcoder);
    internal delegate void GstTranscoderErrorCallback(IntPtr transcoder, IntPtr error, IntPtr debug);

    public class GstTranscoder : Transcoder
    {
#if GSTREAMER_0_10

        [DllImport("libbanshee")]
        private static extern IntPtr gst_transcoder_new();

        [DllImport("libbanshee")]
        private static extern void gst_transcoder_free(HandleRef handle);

        [DllImport("libbanshee")]
        private static extern void gst_transcoder_transcode(HandleRef handle, IntPtr input_uri, 
            IntPtr output_uri, string encoder_pipeline);

        [DllImport("libbanshee")]
        private static extern void gst_transcoder_cancel(HandleRef handle);

        [DllImport("libbanshee")]
        private static extern void gst_transcoder_set_progress_callback(HandleRef handle,
            GstTranscoderProgressCallback cb);

        [DllImport("libbanshee")]
        private static extern void gst_transcoder_set_finished_callback(HandleRef handle, 
            GstTranscoderFinishedCallback cb);

        [DllImport("libbanshee")]
        private static extern void gst_transcoder_set_error_callback(HandleRef handle, 
            GstTranscoderErrorCallback cb);
            
        [DllImport("libbanshee")]
        private static extern bool gst_transcoder_get_is_transcoding(HandleRef handle);

        private HandleRef handle;
        private GstTranscoderProgressCallback ProgressCallback;
        private GstTranscoderFinishedCallback FinishedCallback;
        private GstTranscoderErrorCallback ErrorCallback;
        private string error_message;
        
        public GstTranscoder()
        {
            IntPtr ptr = gst_transcoder_new();
            
            if(ptr == IntPtr.Zero) {
                throw new NullReferenceException(Catalog.GetString("Could not create transcoder"));
            }
            
            handle = new HandleRef(this, ptr);
            
            ProgressCallback = new GstTranscoderProgressCallback(OnTranscoderProgress);
            FinishedCallback = new GstTranscoderFinishedCallback(OnTranscoderFinished);
            ErrorCallback = new GstTranscoderErrorCallback(OnTranscoderError);
            
            gst_transcoder_set_progress_callback(handle, ProgressCallback);
            gst_transcoder_set_finished_callback(handle, FinishedCallback);
            gst_transcoder_set_error_callback(handle, ErrorCallback);
        }

        public override void Dispose()
        {
            gst_transcoder_free(handle);
        }
        
        public override void BeginTranscode(Uri inputUri, Uri outputUri, PipelineProfile profile)
        {
            if(IsTranscoding) {
                throw new ApplicationException("Transcoder is busy");
            }
        
            IntPtr input_uri = GLib.Marshaller.StringToPtrGStrdup(inputUri.AbsoluteUri);
            IntPtr output_uri = GLib.Marshaller.StringToPtrGStrdup(outputUri.AbsoluteUri);
            
            error_message = null;
            
            gst_transcoder_transcode(handle, input_uri, output_uri, profile.Pipeline);
            
            GLib.Marshaller.Free(input_uri);
            GLib.Marshaller.Free(output_uri);
        }
        
        public override void Cancel()
        {
            gst_transcoder_cancel(handle);
        }
        
        private void OnTranscoderProgress(IntPtr transcoder, double progress)
        {
            OnProgress(progress);
        }
        
        private void OnTranscoderFinished(IntPtr transcoder)
        {
            OnFinished();
        }
        
        private void OnTranscoderError(IntPtr transcoder, IntPtr error, IntPtr debug)
        {
            error_message = GLib.Marshaller.Utf8PtrToString(error);
            
            if(debug != IntPtr.Zero) {
                string debug_string = GLib.Marshaller.Utf8PtrToString(debug);
                if(debug_string != null && debug_string != String.Empty) {
                    error_message += ": " + debug_string;
                }
            }
            
            OnError();
        }
        
        public override bool IsTranscoding {
            get { return gst_transcoder_get_is_transcoding(handle); }
        }
        
        public override string ErrorMessage {
            get { return error_message; }
        }
#else
        [DllImport("libbanshee")]
        private static extern IntPtr gst_transcoder_new();
        
        [DllImport("libbanshee")]
        private static extern void gst_transcoder_free(HandleRef transcoder);
        
        [DllImport("libbanshee")]
        private static extern bool gst_transcoder_transcode(HandleRef encoder, IntPtr input_uri, 
            IntPtr output_uri, string encode_pipeline, GstTranscoderProgressCallback progress_cb);
    
        [DllImport("libbanshee")]
        private static extern IntPtr gst_transcoder_get_error(HandleRef transcoder);
        
        [DllImport("libbanshee")]
        private static extern void gst_transcoder_cancel(HandleRef transcoder);
        
        private HandleRef handle;
        private GstTranscoderProgressCallback ProgressCallback;
        private Uri input_uri;
        private Uri output_uri;
        private PipelineProfile profile;
        private bool is_transcoding;
        private bool canceled;
        private string error;
        
        public GstTranscoder()
        {
            IntPtr ptr = gst_transcoder_new();
            
            if(ptr == IntPtr.Zero) {
                throw new NullReferenceException(Catalog.GetString("Could not create transcoder"));
            }
            
            ProgressCallback = new GstTranscoderProgressCallback(OnTranscoderProgress);
            handle = new HandleRef(this, ptr);
        }
        
        public override void Dispose()
        {
            gst_transcoder_free(handle);
        }
        
        public override void BeginTranscode(Uri inputUri, Uri outputUri, PipelineProfile profile)
        {
            if(IsTranscoding) {
                throw new ApplicationException("Transcoder is busy");
            }
        
            input_uri = inputUri;
            output_uri = outputUri;
            this.profile = profile;
            
            ThreadAssist.Spawn(ThreadedTranscode);
        }
        
        private void ThreadedTranscode()
        {
            IntPtr input_uri = GLib.Marshaller.StringToPtrGStrdup(this.input_uri.AbsoluteUri);
            IntPtr output_uri = GLib.Marshaller.StringToPtrGStrdup(this.output_uri.AbsoluteUri);

            is_transcoding = true;
            canceled = false;
            error = null;

            bool have_error = !gst_transcoder_transcode(handle, input_uri, output_uri,
                profile.Pipeline, ProgressCallback);
            
            GLib.Marshaller.Free(input_uri);
            GLib.Marshaller.Free(output_uri);
            
            is_transcoding = false;            
            this.input_uri = null;
            this.output_uri = null;
            profile = null;

            if(have_error) {
                IntPtr errPtr = gst_transcoder_get_error(handle);
                error = Marshal.PtrToStringAnsi(errPtr);
                OnError();
            }
            
            if(!canceled) {
                OnFinished();
            }
        }
        
        public override void Cancel()
        {
            canceled = true;
            gst_transcoder_cancel(handle);
        }
        
        private void OnTranscoderProgress(IntPtr transcoder, double progress)
        {
            OnProgress(progress);
        }
        
        public override bool IsTranscoding {
            get { return is_transcoding; }
        }
        
        public override string ErrorMessage {
            get { return error; }
        }
#endif
    }
}
