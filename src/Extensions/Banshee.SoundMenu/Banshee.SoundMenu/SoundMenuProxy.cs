//
// SoundMenu.cs
//
// Author:
//   Bertrand Lorentz <bertrand.lorentz@gmail.com>
//
// Copyright 2011 Bertrand Lorentz
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

using DBus;

using Hyena;

namespace Banshee.SoundMenu
{
    public class SoundMenuProxy
    {
        private const string DBusInterface = "com.canonical.indicators.sound";
        private const string DBusPath = "/com/canonical/indicators/sound/service";
        private const string desktop_name = "banshee";

        private delegate void SoundStateUpdateHandler (int new_state);

        private ISoundMenu sound_menu;

        [Interface("com.canonical.indicators.sound")]
        private interface ISoundMenu
        {
            bool BlacklistMediaPlayer (string player_desktop_name, bool blacklist);
            bool IsBlacklisted (string player_desktop_name);
            int GetSoundState ();
            event SoundStateUpdateHandler SoundStateUpdate;
        }

        public SoundMenuProxy ()
        {
        }

        private ISoundMenu SoundMenu {
            get {
                if (sound_menu == null) {
                    if (!Bus.Session.NameHasOwner (DBusInterface)) {
                        return null;
                    }

                    sound_menu = Bus.Session.GetObject<ISoundMenu> (DBusInterface, new ObjectPath (DBusPath));

                    if (sound_menu == null) {
                        Log.WarningFormat ("The {0} object could not be located on the DBus interface {1}",
                            DBusPath, DBusInterface);
                    }
                }
                return sound_menu;
            }
        }


        public void Register (bool startup)
        {
            if (SoundMenu != null && !startup) {
                // We don't have to do anything to register on startup
                try {
                    Log.Debug ("Adding ourselves to the sound indicator");
                    SoundMenu.BlacklistMediaPlayer (desktop_name, false);
                } catch (Exception e) {
                    Log.Exception (e);
                }
            }
        }

        public void Unregister ()
        {
            if (SoundMenu != null) {
                try {
                    SoundMenu.BlacklistMediaPlayer (desktop_name, true);
                } catch (Exception e) {
                    Log.Exception (e);
                }
            }
        }
   }
}

