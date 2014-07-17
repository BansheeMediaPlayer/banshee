//
// ScreensaverManager.cs
//
// Author:
//   Christopher James Halse Rogers <raof@ubuntu.com>
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
using Hyena;
using Mono.Addins;

namespace Banshee.PlatformServices
{
    public class ScreensaverManager : IScreensaverManager, IDisposable
    {
        private IScreensaverManager manager;
        bool inhibited = false;

        public ScreensaverManager ()
        {
            foreach (TypeExtensionNode node in AddinManager.GetExtensionNodes ("/Banshee/PlatformServices/ScreensaverManager")) {
                try {
                    manager = (IScreensaverManager)node.CreateInstance (typeof (IScreensaverManager));
                    Log.DebugFormat ("Loaded IScreensaverManager: {0}", manager.GetType ().FullName);
                    break;
                } catch (Exception e) {
                    Log.Error ("IScreensaverManager extension failed to load", e);
                }
            }
        }

        public void Dispose ()
        {
            UnInhibit ();
        }

        public void Inhibit ()
        {
            if (manager != null && !inhibited) {
                Log.Information ("Inhibiting screensaver during fullscreen playback");
                manager.Inhibit ();
                inhibited = true;
            }
        }

        public void UnInhibit ()
        {
            if (manager != null && inhibited) {
                Log.Information ("Uninhibiting screensaver");
                manager.UnInhibit ();
                inhibited = false;
            }
        }
    }
}
