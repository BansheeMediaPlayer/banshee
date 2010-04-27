//
// MeeGoPanel.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright 2009-2010 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;

using Gtk;
using MeeGo.Panel;

using Hyena;
using Banshee.Base;
using Banshee.ServiceStack;

namespace Banshee.MeeGo
{
    public class MeeGoPanel : IDisposable
    {
        public static MeeGoPanel Instance { get; private set; }

        private PanelGtk embedded_panel;
        private Window window_panel;

        public MediaPanelContents Contents { get; private set; }

        public MeeGoPanel ()
        {
            if (Instance != null) {
                throw new InvalidOperationException ("Only one MeeGoPanel instance should exist");
            }

            Instance = this;

            Hyena.Gui.Theming.ThemeEngine.SetCurrentTheme<MeeGoTheme> ();

            var timer = Log.DebugTimerStart ();

            try {
                Log.Debug ("Attempting to create MeeGo toolbar panel");
                embedded_panel = new PanelGtk ("banshee", "media", null, "media-button", true);
                embedded_panel.ShowBeginEvent += (o, e) =>
                    ServiceManager.SourceManager.SetActiveSource (ServiceManager.SourceManager.MusicLibrary);
                while (Gtk.Application.EventsPending ()) {
                    Gtk.Application.RunIteration ();
                }
            } catch (Exception e) {
                if (!(e is DllNotFoundException)) {
                    Log.Exception ("Could not bind to MeeGo panel", e);
                }
                window_panel = new Gtk.Window ("MeeGo Media Panel");
            }

            Log.DebugTimerPrint (timer, "MeeGo panel created: {0}");
        }

        public void Dispose ()
        {
        }

        public void BuildContents ()
        {
            var timer = Log.DebugTimerStart ();
            Contents = new MediaPanelContents ();
            Contents.ShowAll ();
            Log.DebugTimerPrint (timer, "MeeGo panel contents created: {0}");

            if (embedded_panel != null) {
                embedded_panel.SetChild (Contents);
            } else if (window_panel != null) {
                window_panel.Add (Contents);
                window_panel.SetDefaultSize (1000, 500);
                window_panel.WindowPosition = WindowPosition.Center;
                window_panel.Show ();
                GLib.Timeout.Add (1000, () => {
                    window_panel.Present ();
                    return false;
                });
            }
        }

        public void Hide ()
        {
            if (embedded_panel != null) {
                embedded_panel.Hide ();
            }
        }
    }
}
