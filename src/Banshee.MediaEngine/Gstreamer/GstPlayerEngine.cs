/***************************************************************************
 *  GstPlayerEngine.cs
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
using System.Collections;
using System.Runtime.InteropServices;
using Mono.Unix;

using Banshee.Base;
using Banshee.MediaEngine;
using Banshee.Gstreamer;

namespace Banshee.MediaEngine.Gstreamer
{
#if GSTREAMER_0_10
    internal delegate void GstPlaybackEosCallback(IntPtr engine);
    internal delegate void GstPlaybackErrorCallback(IntPtr engine, IntPtr error, IntPtr debug);
    internal delegate void GstPlaybackStateChangedCallback(IntPtr engine, int old_state, int new_state, int pending_state);
    internal delegate void GstPlaybackIterateCallback(IntPtr engine);
    internal delegate void GstPlaybackBufferingCallback(IntPtr engine, int buffering_progress);

    public class GstreamerPlayerEngine : PlayerEngine
    {
        private HandleRef handle;
        
        private GstPlaybackEosCallback eos_callback;
        private GstPlaybackErrorCallback error_callback;
        private GstPlaybackStateChangedCallback state_changed_callback;
        private GstPlaybackIterateCallback iterate_callback;
        private GstPlaybackBufferingCallback buffering_callback;
        private GstTaggerTagFoundCallback tag_found_callback;
        
        private bool buffering_finished;
        
        public GstreamerPlayerEngine()
        {
            IntPtr ptr = gst_playback_new();
            
            if(ptr == IntPtr.Zero) {
                throw new ApplicationException(Catalog.GetString("Could not initialize GStreamer library"));
            }
            
            handle = new HandleRef(this, ptr);
            
            eos_callback = new GstPlaybackEosCallback(OnEos);
            error_callback = new GstPlaybackErrorCallback(OnError);
            state_changed_callback = new GstPlaybackStateChangedCallback(OnStateChanged);
            iterate_callback = new GstPlaybackIterateCallback(OnIterate);
            buffering_callback = new GstPlaybackBufferingCallback(OnBuffering);
            tag_found_callback = new GstTaggerTagFoundCallback(OnTagFound);
            
            gst_playback_set_eos_callback(handle, eos_callback);
            gst_playback_set_iterate_callback(handle, iterate_callback);
            gst_playback_set_error_callback(handle, error_callback);
            gst_playback_set_state_changed_callback(handle, state_changed_callback);
            gst_playback_set_buffering_callback(handle, buffering_callback);
            gst_playback_set_tag_found_callback(handle, tag_found_callback);
        }
        
        public override void Dispose()
        {
            base.Dispose();
            gst_playback_free(handle);
        }
        
        public override void Close()
        {
            gst_playback_stop(handle);
            base.Close();
        }
        
        protected override void OpenUri(Uri uri)
        {
            IntPtr uri_ptr = GLib.Marshaller.StringToPtrGStrdup(uri.AbsoluteUri);
            gst_playback_open(handle, uri_ptr);
            GLib.Marshaller.Free(uri_ptr);
        }
        
        public override void Play()
        {
            gst_playback_play(handle);
            OnStateChanged(PlayerEngineState.Playing);
        }
        
        public override void Pause()
        {
            gst_playback_pause(handle);
            OnStateChanged(PlayerEngineState.Paused);
        }

        public override IntPtr [] GetBaseElements()
        {
            IntPtr [] elements = new IntPtr[3];
            
            if(gst_playback_get_pipeline_elements(handle, out elements[0], out elements[1], out elements[2])) {
                return elements;
            }
            
            return null;
        }

        private void OnEos(IntPtr engine)
        {
            Close();
            OnEventChanged(PlayerEngineEvent.EndOfStream);
        }
        
        private void OnIterate(IntPtr engine)
        {
            OnEventChanged(PlayerEngineEvent.Iterate);
        }
        
        private void OnError(IntPtr engine, IntPtr error, IntPtr debug)
        {
            Close();
            
            string error_message = error == IntPtr.Zero
                ? Catalog.GetString("Unknown Error")
                : GLib.Marshaller.Utf8PtrToString(error);
                
            if(debug != IntPtr.Zero) {
                Console.Error.WriteLine("GST-DEBUG: {0}: {1}", error_message, 
                    GLib.Marshaller.Utf8PtrToString(debug));
            } 
            
            OnEventChanged(PlayerEngineEvent.Error, error_message);
        }
        
        private void OnStateChanged(IntPtr engine, int new_state, int old_state, int pending_state)
        {
        }
        
        private void OnBuffering(IntPtr engine, int progress)
        {
            if(buffering_finished && progress >= 100) {
                return;
            }
            
            buffering_finished = progress >= 100;
            OnEventChanged(PlayerEngineEvent.Buffering, Catalog.GetString("Buffering"), (double)progress / 100.0);
        }
        
        private void OnTagFound(string tagName, ref GLib.Value value, IntPtr userData)
        {
            OnTagFound(GstTagger.ProcessNativeTagResult(tagName, ref value));
        }
        
        public override ushort Volume {
            get { return (ushort)gst_playback_get_volume(handle); }
            set { 
                gst_playback_set_volume(handle, (int)value);
                OnEventChanged(PlayerEngineEvent.Volume);
            }
        }
        
        public override uint Position {
            get { return (uint)gst_playback_get_position(handle) / 1000; }
            set { 
                gst_playback_set_position(handle, (ulong)value * 1000);
                OnEventChanged(PlayerEngineEvent.Seek);
            }
        }
        
        public override bool CanSeek {
            get { return gst_playback_can_seek(handle); }
        }
        
        public override uint Length {
            get { return (uint)gst_playback_get_duration(handle) / 1000; }
        }
        
        public override string Id {
            get { return "gstreamer"; }
        }
        
        public override string Name {
            get { return "GStreamer 0.10"; }
        }
        
        private static string [] source_capabilities = { "file", "http", "cdda" };
        public override IEnumerable SourceCapabilities {
            get { return source_capabilities; }
        }
                
        private static string [] decoder_capabilities = { "wma", "asf", "flac" };
        public override IEnumerable ExplicitDecoderCapabilities {
            get { return decoder_capabilities; }
        }
        
        [DllImport("libbanshee")]
        private static extern IntPtr gst_playback_new();
        
        [DllImport("libbanshee")]
        private static extern void gst_playback_free(HandleRef engine);
        
        [DllImport("libbanshee")]
        private static extern void gst_playback_set_eos_callback(HandleRef engine, GstPlaybackEosCallback cb);
        
        [DllImport("libbanshee")]
        private static extern void gst_playback_set_error_callback(HandleRef engine, GstPlaybackErrorCallback cb);
        
        [DllImport("libbanshee")]
        private static extern void gst_playback_set_state_changed_callback(HandleRef engine, 
            GstPlaybackStateChangedCallback cb);
            
        [DllImport("libbanshee")]
        private static extern void gst_playback_set_iterate_callback(HandleRef engine, GstPlaybackIterateCallback cb);
        
        [DllImport("libbanshee")]
        private static extern void gst_playback_set_buffering_callback(HandleRef engine, GstPlaybackBufferingCallback cb);
        
        [DllImport("libbanshee")]
        private static extern void gst_playback_set_tag_found_callback(HandleRef engine, GstTaggerTagFoundCallback cb);
        
        [DllImport("libbanshee")]
        private static extern void gst_playback_open(HandleRef engine, IntPtr uri);
        
        [DllImport("libbanshee")]
        private static extern void gst_playback_stop(HandleRef engine);
        
        [DllImport("libbanshee")]
        private static extern void gst_playback_pause(HandleRef engine);
        
        [DllImport("libbanshee")]
        private static extern void gst_playback_play(HandleRef engine);
        
        [DllImport("libbanshee")]
        private static extern void gst_playback_set_volume(HandleRef engine, int volume);
        
        [DllImport("libbanshee")]
        private static extern int gst_playback_get_volume(HandleRef engine);
        
        [DllImport("libbanshee")]
        private static extern bool gst_playback_can_seek(HandleRef engine);
        
        [DllImport("libbanshee")]
        private static extern void gst_playback_set_position(HandleRef engine, ulong time_ms);
        
        [DllImport("libbanshee")]
        private static extern ulong gst_playback_get_position(HandleRef engine);
        
        [DllImport("libbanshee")]
        private static extern ulong gst_playback_get_duration(HandleRef engine);
        
        [DllImport("libbanshee")]
        private static extern bool gst_playback_get_pipeline_elements(HandleRef engine, out IntPtr playbin,
            out IntPtr audiobin, out IntPtr audiotee);    
    }
    
    
#else


    internal delegate void GpeErrorCallback(IntPtr engine, IntPtr error);
    internal delegate void GpeIterateCallback(IntPtr engine, int position, int length);
    internal delegate void GpeEndOfStreamCallback(IntPtr engine);

    public class GstreamerPlayerEngine : PlayerEngine
    {
        private HandleRef handle;

        private GpeErrorCallback error_cb;
        private GpeIterateCallback iterate_cb;
        private GpeEndOfStreamCallback eos_cb;

        public GstreamerPlayerEngine()
        {
            IntPtr ptr = gpe_new();
            
            if(ptr == IntPtr.Zero) {
                throw new ApplicationException(Catalog.GetString("Could not initialize GStreamer library"));
            }
            
            handle = new HandleRef(this, ptr);
            
            error_cb = new GpeErrorCallback(OnError);
            iterate_cb = new GpeIterateCallback(OnIterate);
            eos_cb = new GpeEndOfStreamCallback(OnEndOfStream);
            
            gpe_set_end_of_stream_handler(handle, eos_cb);
            gpe_set_error_handler(handle, error_cb);
            gpe_set_iterate_handler(handle, iterate_cb);
        }
        
        public override void Dispose()
        {
            base.Dispose();
            gpe_free(handle);
        }
            
        protected override void OpenUri(Uri uri)
        {
            gpe_open(handle, uri.AbsoluteUri);
        }
        
        public override void Close()
        {
            gpe_stop(handle);
            base.Close();
        }
            
        public override void Play()
        {
            gpe_play(handle);
            OnStateChanged(PlayerEngineState.Playing);
        }
        
        public override void Pause()
        {
            gpe_pause(handle);
            OnStateChanged(PlayerEngineState.Paused);
        }
        
        private void OnEndOfStream(IntPtr engine)
        {
            OnEventChanged(PlayerEngineEvent.EndOfStream);
        }
        
        private void OnError(IntPtr engine, IntPtr messagePtr)
        {
            OnEventChanged(PlayerEngineEvent.Error, Marshal.PtrToStringAnsi(messagePtr));
        }
        
        private void OnIterate(IntPtr engine, int position, int total)
        {
            OnEventChanged(PlayerEngineEvent.Iterate);
        }
           
        public override ushort Volume {
            get { return (ushort)gpe_get_volume(handle); }
            set { 
                gpe_set_volume(handle, (int)value);
                OnEventChanged(PlayerEngineEvent.Volume);
            }
        }
           
        public override uint Position { 
            get { return (uint)gpe_get_position(handle); }
            set { 
                gpe_set_position(handle, (int)value);
                OnEventChanged(PlayerEngineEvent.Seek);
            }
        }
           
        public override uint Length {
            get { return (uint)gpe_get_length(handle); }
        }
        
        public override string Id {
            get { return "gstreamer"; }
        }
        
        public override string Name {
            get { return "GStreamer 0.8"; }
        }
        
        private static string [] source_capabilities = { "file", "http", "cdda" };
        public override IEnumerable SourceCapabilities {
            get { return source_capabilities; }
        }
                
        private static string [] decoder_capabilities = { "wma", "asf", "flac" };
        public override IEnumerable ExplicitDecoderCapabilities {
            get { return decoder_capabilities; }
        }
        
        [DllImport("libbanshee")]
        private static extern IntPtr gpe_new();
        
        [DllImport("libbanshee")]
        private static extern void gpe_free(HandleRef handle);
        
        [DllImport("libbanshee")]
        private static extern void gpe_set_end_of_stream_handler(HandleRef handle, GpeEndOfStreamCallback cb);
            
        [DllImport("libbanshee")]
        private static extern void gpe_set_error_handler(HandleRef handle, GpeErrorCallback cb);
            
        [DllImport("libbanshee")]
        private static extern void gpe_set_iterate_handler(HandleRef handle, GpeIterateCallback cb);

        [DllImport("libbanshee")]
        private static extern bool gpe_open(HandleRef handle, string file);
        
        [DllImport("libbanshee")]
        private static extern void gpe_play(HandleRef handle);
        
        [DllImport("libbanshee")]
        private static extern void gpe_pause(HandleRef handle);
    
        [DllImport("libbanshee")]
        private static extern void gpe_stop(HandleRef handle);
        
        [DllImport("libbanshee")]
        private static extern void gpe_set_volume(HandleRef handle,
            int volume);
        
        [DllImport("libbanshee")]
        private static extern int gpe_get_volume(HandleRef handle);
        
        [DllImport("libbanshee")]
        private static extern void gpe_set_position(HandleRef handle,
            int position);
            
        [DllImport("libbanshee")]
        private static extern int gpe_get_position(HandleRef handle);
        
        [DllImport("libbanshee")]
        private static extern int gpe_get_length(HandleRef handle);
        
        [DllImport("libbanshee")]
        private static extern bool gpe_is_eos(HandleRef handle);
        
        [DllImport("libbanshee")]
        private static extern IntPtr gpe_get_error(HandleRef handle);
        
        [DllImport("libbanshee")]
        private static extern bool gpe_have_error(HandleRef handle);
    }
#endif
}
