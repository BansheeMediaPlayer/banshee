//
// GtkWindowThumbnailToolbarManager.cs
//
// Authors:
//   Pete Johanson <peter@peterjohanson.com>
//
// Copyright (C) 2010 Pete Johanson
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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using Gtk;

using Windows7Support;

namespace Banshee.Windows
{
    public static class GtkWindowThumbnailToolbarManager
    {
        public static void Register (Window window, Action<ThumbnailToolbar> toolbar_creation_callback)
        {
            callback_dict[window] = toolbar_creation_callback;

            if (window.IsRealized) {
                CreateForWindow (window);
            } else {
                window.Realized += OnWindowRealized;
            }
        }

        static Dictionary<Window, Action<ThumbnailToolbar>> callback_dict = new Dictionary<Window, Action<ThumbnailToolbar>> ();

        static void OnWindowRealized (object o, EventArgs args)
        {
            CreateForWindow ((Window)o);
        }

        static void CreateForWindow (Window window)
        {
            Action<ThumbnailToolbar> cb = null;

            if (!callback_dict.TryGetValue (window, out cb)) {
                return;
            }

            ThumbnailToolbarManager.Register (gdk_win32_drawable_get_handle (window.Window.Handle), cb);
        }

        [DllImport ("libgdk-win32-2.0-0.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr gdk_win32_drawable_get_handle (IntPtr drawable);
    }
}
