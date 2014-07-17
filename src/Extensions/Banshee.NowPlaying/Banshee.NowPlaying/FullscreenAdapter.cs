//
// FullscreenAdapter.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright 2008-2010 Novell, Inc.
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
using Gtk;
using Mono.Addins;

using Hyena;

namespace Banshee.NowPlaying
{
    public class FullscreenAdapter : IFullscreenAdapter
    {
        private IFullscreenAdapter adapter;
        private bool probed = false;
        private bool first_fullscreen = false;

        public event EventHandler SuggestUnfullscreen;

        public void Fullscreen (Window window, bool fullscreen)
        {
            if (!first_fullscreen && !fullscreen) {
                return;
            } else if (fullscreen) {
                first_fullscreen = true;
            }

            if (adapter != null) {
                try {
                    adapter.Fullscreen (window, fullscreen);
                } catch (Exception e) {
                    Log.Error ("IFullscreenAdapter extension failed, so disabling", e);
                    DisposeAdapter ();
                }

                return;
            } else if (probed) {
                DefaultFullscreen (window, fullscreen);
                return;
            }

            foreach (TypeExtensionNode node in AddinManager.GetExtensionNodes ("/Banshee/NowPlaying/FullscreenAdapter")) {
                try {
                    adapter = (IFullscreenAdapter)node.CreateInstance (typeof (IFullscreenAdapter));
                    adapter.SuggestUnfullscreen += OnSuggestUnfullscreen;
                    Log.DebugFormat ("Loaded IFullscreenAdapter: {0}", adapter.GetType ().FullName);
                    break;
                } catch (Exception e) {
                    Log.Error ("IFullscreenAdapter extension failed to load", e);
                }
            }

            probed = true;
            Fullscreen (window, fullscreen);
        }

        private void OnSuggestUnfullscreen (object o, EventArgs args)
        {
            var handler = SuggestUnfullscreen;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }

        public void Dispose ()
        {
            DisposeAdapter ();
            probed = false;
        }

        private void DisposeAdapter ()
        {
            if (adapter != null) {
                try {
                    adapter.SuggestUnfullscreen -= OnSuggestUnfullscreen;
                    adapter.Dispose ();
                } catch (Exception e) {
                    Log.Error ("IFullscreenAdapter failed to dispose", e);
                }

                adapter = null;
            }
        }

        private void DefaultFullscreen (Window window, bool fullscreen)
        {
            if (fullscreen) {
                window.Fullscreen ();
            } else {
                window.Unfullscreen ();
            }
        }
    }
}
