//
// ConnectedVolumeButton.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2007 Novell, Inc.
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

using Banshee.MediaEngine;
using Banshee.ServiceStack;

namespace Banshee.Gui.Widgets
{
    public class ConnectedVolumeButton : Bacon.VolumeButton
    {
        private bool emit_lock = false;

        public ConnectedVolumeButton () : base()
        {
            var player = ServiceManager.PlayerEngine;

            if (player.ActiveEngine != null && player.ActiveEngine.IsInitialized) {
                SetVolume ();
            } else {
                Sensitive = false;
                player.EngineAfterInitialize += (e) => {
                    Hyena.ThreadAssist.ProxyToMain (delegate {
                        SetVolume ();
                        Sensitive = true;
                    });
                };
            }

            player.ConnectEvent (OnPlayerEvent, PlayerEvent.Volume);
        }

        public ConnectedVolumeButton (bool classic) : this ()
        {
            Classic = classic;
        }

        private void OnPlayerEvent (PlayerEventArgs args)
        {
            SetVolume ();
        }

        private void SetVolume ()
        {
            emit_lock = true;
            Volume = ServiceManager.PlayerEngine.Volume;
            emit_lock = false;
        }

        protected override void OnVolumeChanged ()
        {
            if (emit_lock) {
                return;
            }

            ServiceManager.PlayerEngine.Volume = (ushort)Volume;

            base.OnVolumeChanged ();
        }
    }
}
