//
// BpmDetector.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
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
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Mono.Unix;

using Hyena;
using Hyena.Data;

using Banshee.Base;
using Banshee.Streaming;
using Banshee.MediaEngine;
using Banshee.ServiceStack;
using Banshee.Configuration;
using Banshee.Preferences;

namespace Banshee.GStreamer
{
    public class BpmDetector : IBpmDetector
    {
        private HandleRef handle;
        private SafeUri current_uri;
        private Dictionary<int, int> bpm_histogram = new Dictionary<int, int> ();

        private BpmDetectorProgressHandler progress_cb;
        private BpmDetectorFinishedHandler finished_cb;
        //private BpmDetectorErrorHandler error_cb;

        public event BpmEventHandler FileFinished;

        public BpmDetector ()
        {
            try {
                handle = new HandleRef (this, bbd_new ());

                progress_cb = new BpmDetectorProgressHandler (OnNativeProgress);
                bbd_set_progress_callback (handle, progress_cb);

                finished_cb = new BpmDetectorFinishedHandler (OnNativeFinished);
                bbd_set_finished_callback (handle, finished_cb);
            } catch (Exception e) {
                throw new ApplicationException (Catalog.GetString ("Could not create BPM detection driver."), e);
            }
        }

        public void Dispose ()
        {
            Reset ();

            bbd_destroy (handle);
            handle = new HandleRef (this, IntPtr.Zero);
        }

        public void Cancel ()
        {
            Dispose ();
        }

        public bool IsDetecting {
            get { return bbd_get_is_detecting (handle); }
        }

        private void Reset ()
        {
            current_uri = null;
            bpm_histogram.Clear ();
        }

        public void ProcessFile (SafeUri uri)
        {
            Reset ();
            current_uri = uri;

            string path = uri.LocalPath;
            IntPtr path_ptr = GLib.Marshaller.StringToPtrGStrdup (path);
            try {
                Log.DebugFormat ("GStreamer running beat detection on {0}", path);
                bbd_process_file (handle, path_ptr);
            } catch (Exception e) {
                Log.Error (e);
            } finally {
                GLib.Marshaller.Free (path_ptr);
            }
        }

        private void OnFileFinished (SafeUri uri, int bpm)
        {
            BpmEventHandler handler = FileFinished;
            if (handler != null) {
                handler (this, new BpmEventArgs (uri, bpm));
            }
        }

        private void OnNativeProgress (double bpm)
        {
            int rounded = (int) Math.Round (bpm);
            if (!bpm_histogram.ContainsKey(rounded)) {
                bpm_histogram[rounded] = 1;
            } else {
                bpm_histogram[rounded]++;
            }
        }

        private void OnNativeFinished ()
        {
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
            OnFileFinished (uri, best_bpm);
        }

        /*private void OnNativeError (IntPtr error, IntPtr debug)
        {
            string error_message = GLib.Marshaller.Utf8PtrToString (error);

            if (debug != IntPtr.Zero) {
                string debug_string = GLib.Marshaller.Utf8PtrToString (debug);
                if (!String.IsNullOrEmpty (debug_string)) {
                    error_message = String.Format ("{0}: {1}", error_message, debug_string);
                }
            }

            Log.Debug (error_message);
            SafeUri uri = current_uri;
            Reset ();
            OnFileFinished (uri, 0);
        }*/

        private delegate void BpmDetectorProgressHandler (double bpm);
        private delegate void BpmDetectorFinishedHandler ();
        //private delegate void BpmDetectorErrorHandler (IntPtr error, IntPtr debug);

        [DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr bbd_new ();

        [DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void bbd_destroy (HandleRef handle);

        [DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool bbd_get_is_detecting (HandleRef handle);

        [DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void bbd_process_file (HandleRef handle, IntPtr path);

        [DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void bbd_set_progress_callback (HandleRef handle, BpmDetectorProgressHandler callback);

        [DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void bbd_set_finished_callback (HandleRef handle, BpmDetectorFinishedHandler callback);

        //[DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        //private static extern void bbd_set_error_callback (HandleRef handle, BpmDetectorErrorHandler callback);
    }
}
