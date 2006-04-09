/***************************************************************************
 *  AudioCdRipper.cs
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
using System.IO;
using System.Collections;
using System.Runtime.InteropServices;
using System.Threading;
using Mono.Unix;

using Banshee.Widgets;
using Banshee.Base;

namespace Banshee.Base
{
    public delegate void AudioCdRipperProgressHandler(object o, AudioCdRipperProgressArgs args);
    public delegate void AudioCdRipperTrackFinishedHandler(object o, AudioCdRipperTrackFinishedArgs args);
        
    public class AudioCdRipperProgressArgs : EventArgs
    {
        public int SecondsEncoded;
        public int TotalSeconds;
        public AudioCdTrackInfo Track;
    }

    public class AudioCdRipperTrackFinishedArgs : EventArgs
    {
        public AudioCdTrackInfo Track;
        public int TrackNumber;
        public SafeUri Uri;
    }

    public class AudioCdTrackRipper : IDisposable
    {
        private delegate void GstCdRipperProgressCallback(IntPtr transcoder, int seconds, IntPtr user_info);
        private delegate void GstCdRipperFinishedCallback(IntPtr transcoder);
        private delegate void GstCdRipperErrorCallback(IntPtr transcoder, IntPtr error, IntPtr debug);

        private HandleRef handle;
        private GstCdRipperProgressCallback progress_callback;

#if GSTREAMER_0_10
        private GstCdRipperFinishedCallback finished_callback;
        private GstCdRipperErrorCallback error_callback;
        private string error_message;
        private SafeUri output_uri;
        private int track_number;
#endif

        private AudioCdTrackInfo current_track;
        
        public event AudioCdRipperProgressHandler Progress;
        public event AudioCdRipperTrackFinishedHandler TrackFinished;
        public event EventHandler Error;
        
        public AudioCdTrackRipper(string device, int paranoiaMode, string encoderPipeline)
        {
            IntPtr ptr = gst_cd_ripper_new(device, paranoiaMode, encoderPipeline);
            if(ptr == IntPtr.Zero) {
                throw new ApplicationException(Catalog.GetString("Could not create CD Ripper"));
            }
            
            handle = new HandleRef(this, ptr);
            
            progress_callback = new GstCdRipperProgressCallback(OnProgress);
            gst_cd_ripper_set_progress_callback(handle, progress_callback);

#if GSTREAMER_0_10
            finished_callback = new GstCdRipperFinishedCallback(OnFinished);
            gst_cd_ripper_set_finished_callback(handle, finished_callback);
            
            error_callback = new GstCdRipperErrorCallback(OnError);
            gst_cd_ripper_set_error_callback(handle, error_callback);
#endif
        }
        
        public void Dispose()
        {
            gst_cd_ripper_free(handle);
        }
        
        public void RipTrack(AudioCdTrackInfo track, int trackNumber, SafeUri outputUri)
        {
#if GSTREAMER_0_10
            error_message = null;
            RipTrack_010(track, trackNumber, outputUri);
#else
            ThreadAssist.Spawn(delegate {
                RipTrack_08(track, trackNumber, outputUri);
            });
#endif
        }
  
#if GSTREAMER_0_10
        private void RipTrack_010(AudioCdTrackInfo track, int trackNumber, SafeUri outputUri)
        {
            current_track = track;
            track_number = trackNumber;
            output_uri = outputUri;
            
            gst_cd_ripper_rip_track(handle, outputUri.AbsoluteUri, trackNumber, 
                track.Artist, track.Album, track.Title, track.Genre, 
                (int)track.TrackNumber, (int)track.TrackCount, IntPtr.Zero);
                
            track = null;
        }
#else        
        private void RipTrack_08(AudioCdTrackInfo track, int trackNumber, SafeUri outputUri)
        {
            current_track = track;
            
            bool result = gst_cd_ripper_rip_track(handle, outputUri.AbsoluteUri, trackNumber, 
                track.Artist, track.Album, track.Title, track.Genre, 
                (int)track.TrackNumber, (int)track.TrackCount, IntPtr.Zero);
                
            if(result) {
                ThreadAssist.ProxyToMain(delegate {
                    OnTrackFinished(track, trackNumber, outputUri);
                });
            }
            
            track = null;
        }
#endif   
        
        private void OnTrackFinished(AudioCdTrackInfo track, int trackNumber, SafeUri outputUri)
        {
            track.IsRipped = true;
            track.Uri = outputUri;
            
            AudioCdRipperTrackFinishedHandler handler = TrackFinished;
            if(handler != null) {
                AudioCdRipperTrackFinishedArgs args = new AudioCdRipperTrackFinishedArgs();
                args.Track = track;
                args.TrackNumber = trackNumber;
                args.Uri = outputUri;
                handler(this, args);
            }
        }
        
        public void Cancel()
        {
            gst_cd_ripper_cancel(handle);
        }
        
        public string ErrorMessage {
            get {
#if GSTREAMER_0_10
                return error_message;
#else
                IntPtr errPtr = gst_cd_ripper_get_error(handle);
                if(errPtr == IntPtr.Zero) {
                    return null;
                }
                
                return GLib.Marshaller.Utf8PtrToString(errPtr);
#endif
            }
        }
        
        private void OnProgress(IntPtr ripper, int seconds, IntPtr user_info)
        {
            AudioCdRipperProgressHandler handler = Progress;
            if(handler == null)
                return;
                
            AudioCdRipperProgressArgs args = new AudioCdRipperProgressArgs();
            args.TotalSeconds = (int)current_track.Duration.TotalSeconds;
            args.SecondsEncoded = seconds;
            args.Track = current_track;
            
            handler(this, args); 
        }
        
#if GSTREAMER_0_10        
        private void OnFinished(IntPtr ripper)
        {
            OnTrackFinished(current_track, track_number, output_uri);
        }
        
        private void OnError(IntPtr ripper, IntPtr error, IntPtr debug)
        {
            error_message = GLib.Marshaller.Utf8PtrToString(error);
            
            if(debug != IntPtr.Zero) {
                string debug_string = GLib.Marshaller.Utf8PtrToString(debug);
                if(debug_string != null && debug_string != String.Empty) {
                    error_message += ": " + debug_string;
                }
            }
            
            if(Error != null) {
                Error(this, new EventArgs());
            }
        }
#endif

        [DllImport("libbanshee")]
        private static extern IntPtr gst_cd_ripper_new(string device,
            int paranoia_mode, string encoder_pipeline);
            
        [DllImport("libbanshee")]
        private static extern void gst_cd_ripper_free(HandleRef ripper);
        
        [DllImport("libbanshee")]
        private static extern bool gst_cd_ripper_rip_track(HandleRef ripper, 
            string uri, int track_number, string md_artist, string md_album, 
            string md_title, string md_genre, int md_track_number, 
            int md_track_count, IntPtr user_info);
            
        [DllImport("libbanshee")]
        private static extern void gst_cd_ripper_set_progress_callback(
            HandleRef ripper, GstCdRipperProgressCallback cb);
        
        [DllImport("libbanshee")]
        private static extern void gst_cd_ripper_cancel(HandleRef ripper);
                    
#if GSTREAMER_0_10 
        [DllImport("libbanshee")]
        private static extern void gst_cd_ripper_set_error_callback(
            HandleRef ripper, GstCdRipperErrorCallback cb);
            
        [DllImport("libbanshee")]
        private static extern void gst_cd_ripper_set_finished_callback(
            HandleRef ripper, GstCdRipperFinishedCallback cb);
#else
        [DllImport("libbanshee")]
        private static extern IntPtr gst_cd_ripper_get_error(HandleRef ripper);
#endif
    }

    public class AudioCdRipper
    {
        private Queue tracks = new Queue();
        private AudioCdTrackRipper ripper;
        private string device;
        private int currentIndex = 0;
        private int overallProgress = 0;
        private string status;
        private int total_tracks;
        
        // speed calculations
        private int currentSeconds = 0;
        private int lastPollSeconds = 0;
        private uint pollDelay = 1000;
        private long totalCount;
        
        private uint timeout_id; 
        private int current = 1;
        private PipelineProfile profile;
        
        public event HaveTrackInfoHandler HaveTrackInfo;
        public event EventHandler Finished;
        
        private ActiveUserEvent user_event;
        
        public AudioCdRipper()
        {
            user_event = new ActiveUserEvent("Importing CD");
            user_event.Icon = IconThemeUtils.LoadIcon("cd-action-rip-24", 22);
            user_event.CancelRequested += OnCancelRequested;
        }

        public void QueueTrack(AudioCdTrackInfo track)
        {
            if(device == null) {
                device = track.Device;
            } else if(device != track.Device) {
                throw new ApplicationException(String.Format(Catalog.GetString(
                    "The device node '{0}' differs from the device node " + 
                    "already set for previously queued tracks ({1})"),
                    track.Device, device));
            }
            
            tracks.Enqueue(track);
            totalCount += (int)track.Duration.TotalSeconds;
        }
        
        public void Start()
        {
            current = 1;
            
            user_event.Header = Catalog.GetString("Importing Audio CD");
            user_event.Message = Catalog.GetString("Initializing Drive");
        
            profile = PipelineProfile.GetConfiguredProfile("Ripping");
            
            try {
                string encodePipeline = profile.Pipeline;
        
                LogCore.Instance.PushDebug("Ripping CD and Encoding with Pipeline", encodePipeline);
            
                ripper = new AudioCdTrackRipper(device, 0, encodePipeline);
                ripper.Progress += OnRipperProgress;
                ripper.TrackFinished += OnTrackRipped;
                ripper.Error += OnRipperError;
                
                timeout_id = GLib.Timeout.Add(pollDelay, OnTimeout);
                
                total_tracks = tracks.Count;
                
                RipNextTrack();
            } catch(PipelineProfileException e) {
                LogCore.Instance.PushError(Catalog.GetString("Cannot Import CD"), e.Message);
            }
        }
        
        private void RipNextTrack()
        {
            if(tracks.Count <= 0) {
                return;
            }
            
            AudioCdTrackInfo track = tracks.Dequeue() as AudioCdTrackInfo;

            user_event.Header = String.Format(Catalog.GetString("Importing {0} of {1}"), current++, QueueSize);
            status = String.Format("{0} - {1}", track.Artist, track.Title);
            user_event.Message = status;

            SafeUri uri = new SafeUri(FileNamePattern.BuildFull(track, profile.Extension));

            ripper.RipTrack(track, track.TrackIndex, uri);
        }
        
        private void OnTrackRipped(object o, AudioCdRipperTrackFinishedArgs args)
        {
            overallProgress += (int)args.Track.Duration.TotalSeconds;

            if(!user_event.IsCancelRequested) {
                TrackInfo lti;
                try {
                    lti = new LibraryTrackInfo(args.Uri, args.Track);
                } catch(ApplicationException) {
                    lti = Globals.Library.TracksFnKeyed[Library.MakeFilenameKey(args.Uri)] as TrackInfo;
                }
            
                if(lti != null) {                       
                    HaveTrackInfoHandler handler = HaveTrackInfo;
                    if(handler != null) {
                        HaveTrackInfoArgs hargs = new HaveTrackInfoArgs();
                        hargs.TrackInfo = lti;
                        handler(this, hargs);
                    }
                }
            }

            currentIndex++;
            
            if(tracks.Count > 0 && !user_event.IsCancelRequested) {
                RipNextTrack();
                return;
            }
            
            if(timeout_id > 0) {
                GLib.Source.Remove(timeout_id);
            }
                
            ripper.Dispose();
            user_event.Dispose();
            OnFinished();
        }
        
        private void OnRipperError(object o, EventArgs args)
        {
            ripper.Dispose();
            user_event.Dispose();
            OnFinished();
            LogCore.Instance.PushError(Catalog.GetString("Cannot Import CD"), ripper.ErrorMessage); 
        }
        
        private void OnFinished()
        {
            EventHandler handler = Finished;
            if(handler != null) {
                handler(this, new EventArgs());
            }
        }
        
        private bool OnTimeout()
        {
            int diff = currentSeconds - lastPollSeconds;  
            lastPollSeconds = currentSeconds;
            
            if(diff <= 0) {
                user_event.Message = status;
                return true;
            }
            
            user_event.Message = status + String.Format(" ({0}x)", diff);
            return true;
        }
        
        private void OnRipperProgress(object o, AudioCdRipperProgressArgs args)
        {
            if(args.SecondsEncoded == 0) {
                return;
            }
            
            user_event.Progress = (double)(args.SecondsEncoded + overallProgress) / (double)(totalCount);
            currentSeconds = args.SecondsEncoded;
        }
        
        private void OnCancelRequested(object o, EventArgs args)
        {
            if(ripper != null) {
                ripper.Cancel();
                ripper.Dispose();
            }
            
            user_event.Dispose();
            OnFinished();
        }
        
        public int QueueSize {
            get {
                return total_tracks;
            }
        }
    }
}
