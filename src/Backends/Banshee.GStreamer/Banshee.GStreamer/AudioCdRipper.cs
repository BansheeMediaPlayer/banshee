//
// AudioCdRipper.cs
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
    public class AudioCdRipper : IAudioCdRipper
    {
        private HandleRef handle;
        private string encoder_pipeline;
        private string output_extension;
        private string output_path;
        private TrackInfo current_track;

        private RipperProgressHandler progress_handler;
        private RipperMimeTypeHandler mimetype_handler;
        private RipperFinishedHandler finished_handler;
        private RipperErrorHandler error_handler;

        public event AudioCdRipperProgressHandler Progress;
        public event AudioCdRipperTrackFinishedHandler TrackFinished;
        public event AudioCdRipperErrorHandler Error;

        public void Begin (string device, bool enableErrorCorrection)
        {
            try {
                Profile profile = null;
                ProfileConfiguration config = ServiceManager.MediaProfileManager.GetActiveProfileConfiguration ("cd-importing");

                if (config != null) {
                    profile = config.Profile;
                } else {
                    profile = ServiceManager.MediaProfileManager.GetProfileForMimeType ("audio/vorbis")
                        ?? ServiceManager.MediaProfileManager.GetProfileForMimeType ("audio/flac");
                    if (profile != null) {
                        Log.InformationFormat ("Using default/fallback encoding profile: {0}", profile.Name);
                        ProfileConfiguration.SaveActiveProfile (profile, "cd-importing");
                    }
                }

                if (profile != null) {
                    encoder_pipeline = profile.Pipeline.GetProcessById ("gstreamer");
                    output_extension = profile.OutputFileExtension;
                }

                if (String.IsNullOrEmpty (encoder_pipeline)) {
                    throw new ApplicationException ();
                }

                Hyena.Log.InformationFormat ("Ripping using encoder profile `{0}' with pipeline: {1}", profile.Name, encoder_pipeline);
            } catch (Exception e) {
                throw new ApplicationException (Catalog.GetString ("Could not find an encoder for ripping."), e);
            }

            try {
                int paranoia_mode = enableErrorCorrection ? 255 : 0;
                handle = new HandleRef (this, br_new (device, paranoia_mode, encoder_pipeline));

                progress_handler = new RipperProgressHandler (OnNativeProgress);
                br_set_progress_callback (handle, progress_handler);

                mimetype_handler = new RipperMimeTypeHandler (OnNativeMimeType);
                br_set_mimetype_callback (handle, mimetype_handler);

                finished_handler = new RipperFinishedHandler (OnNativeFinished);
                br_set_finished_callback (handle, finished_handler);

                error_handler = new RipperErrorHandler (OnNativeError);
                br_set_error_callback (handle, error_handler);
            } catch (Exception e) {
                throw new ApplicationException (Catalog.GetString ("Could not create CD ripping driver."), e);
            }
        }

        public void Finish ()
        {
            if (output_path != null) {
                Banshee.IO.File.Delete (new SafeUri (output_path));
            }

            TrackReset ();

            encoder_pipeline = null;
            output_extension = null;

            br_destroy (handle);
            handle = new HandleRef (this, IntPtr.Zero);
        }

        public void Cancel ()
        {
            Finish ();
        }

        private void TrackReset ()
        {
            current_track = null;
            output_path = null;
        }

        public void RipTrack (int trackIndex, TrackInfo track, SafeUri outputUri, out bool taggingSupported)
        {
            TrackReset ();
            current_track = track;

            using (TagList tags = new TagList (track)) {
                output_path = String.Format ("{0}.{1}", outputUri.LocalPath, output_extension);

                // Avoid overwriting an existing file
                int i = 1;
                while (Banshee.IO.File.Exists (new SafeUri (output_path))) {
                    output_path = String.Format ("{0} ({1}).{2}", outputUri.LocalPath, i++, output_extension);
                }

                Log.DebugFormat ("GStreamer ripping track {0} to {1}", trackIndex, output_path);

                br_rip_track (handle, trackIndex + 1, output_path, tags.Handle, out taggingSupported);
            }
        }

        protected virtual void OnProgress (TrackInfo track, TimeSpan ellapsedTime)
        {
            AudioCdRipperProgressHandler handler = Progress;
            if (handler != null) {
                handler (this, new AudioCdRipperProgressArgs (track, ellapsedTime, track.Duration));
            }
        }

        protected virtual void OnTrackFinished (TrackInfo track, SafeUri outputUri)
        {
            AudioCdRipperTrackFinishedHandler handler = TrackFinished;
            if (handler != null) {
                handler (this, new AudioCdRipperTrackFinishedArgs (track, outputUri));
            }
        }

        protected virtual void OnError (TrackInfo track, string message)
        {
            AudioCdRipperErrorHandler handler = Error;
            if (handler != null) {
                handler (this, new AudioCdRipperErrorArgs (track, message));
            }
        }

        private void OnNativeProgress (IntPtr ripper, int mseconds)
        {
            OnProgress (current_track, TimeSpan.FromMilliseconds (mseconds));
        }

        private void OnNativeMimeType (IntPtr ripper, IntPtr mimetype)
        {
            if (mimetype != IntPtr.Zero && current_track != null) {
                string type = GLib.Marshaller.Utf8PtrToString (mimetype);
                if (type != null) {
                    string [] split = type.Split (';', '.', ' ', '\t');
                    if (split != null && split.Length > 0) {
                        current_track.MimeType = split[0].Trim ();
                    } else {
                        current_track.MimeType = type.Trim ();
                    }
                }
            }
        }

        private void OnNativeFinished (IntPtr ripper)
        {
            SafeUri uri = new SafeUri (output_path);
            TrackInfo track = current_track;

            TrackReset ();

            OnTrackFinished (track, uri);
        }

        private void OnNativeError (IntPtr ripper, IntPtr error, IntPtr debug)
        {
            string error_message = GLib.Marshaller.Utf8PtrToString (error);

            if (debug != IntPtr.Zero) {
                string debug_string = GLib.Marshaller.Utf8PtrToString (debug);
                if (!String.IsNullOrEmpty (debug_string)) {
                    error_message = String.Format ("{0}: {1}", error_message, debug_string);
                }
            }

            OnError (current_track, error_message);
        }

        private delegate void RipperProgressHandler (IntPtr ripper, int mseconds);
        private delegate void RipperMimeTypeHandler (IntPtr ripper, IntPtr mimetype);
        private delegate void RipperFinishedHandler (IntPtr ripper);
        private delegate void RipperErrorHandler (IntPtr ripper, IntPtr error, IntPtr debug);

        [DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr br_new (string device, int paranoia_mode, string encoder_pipeline);

        [DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void br_destroy (HandleRef handle);

        [DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void br_rip_track (HandleRef handle, int track_number, string output_path,
            HandleRef tag_list, out bool tagging_supported);

        [DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void br_set_progress_callback (HandleRef handle, RipperProgressHandler callback);

        [DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void br_set_mimetype_callback (HandleRef handle, RipperMimeTypeHandler callback);

        [DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void br_set_finished_callback (HandleRef handle, RipperFinishedHandler callback);

        [DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void br_set_error_callback (HandleRef handle, RipperErrorHandler callback);
    }
}
