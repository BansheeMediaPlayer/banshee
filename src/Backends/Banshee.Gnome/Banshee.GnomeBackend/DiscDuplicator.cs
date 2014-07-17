//
// GnomeService.cs
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
using Mono.Unix;

using Hyena;
using Banshee.ServiceStack;
using Banshee.Hardware;

namespace Banshee.GnomeBackend
{
    public class DiscDuplicator : IDiscDuplicator
    {
        public void Duplicate (IDiscVolume disc)
        {
            if (!RunBrasero (disc.DeviceNode) && !RunNcb (disc.DeviceNode)) {
                throw new ApplicationException (Catalog.GetString (
                    "Neither Brasero nor Nautilus CD Burner could be found to duplicate this disc."));
            }
        }

        private bool RunBrasero (string device)
        {
            GnomeService gnome = ServiceManager.Get<GnomeService> ();
            if (gnome == null || gnome.Brasero == null) {
                return false;
            }

            try {
                gnome.Brasero.Run (String.Format ("-c {0}", device));
            } catch (Exception e) {
                Log.Error (e);
                return false;
            }

            return true;
        }

        private bool RunNcb (string device)
        {
            try {
                System.Diagnostics.Process.Start ("nautilus-cd-burner",
                    String.Format ("--source-device={0}", device));
            } catch (Exception e) {
                Log.Error (e);
                return false;
            }

            return false;
        }
    }
}
