//
// MeeGoClient.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright 2010 Novell, Inc.
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

// The MeeGo client is just a wrapper around the Nereid client.
// This is done to ensure we can create the MeeGo panel instance as
// soon as possible. The problem with loading the panel through the
// /Banshee/ThickClient/GtkBaseClient/PostInitializeGtk extension
// point is that Mono.Addins.AddinManager.Initialize is extremely
// slow. Here's the scenario:
//
//   (a) Banshee is not running at all
//   (b) User clicks "Media" icon in the MeeGo toolbar
//   (c) Banshee is started via its MeeGo toolbar DBus service
//   (d) Meanwhile, the MeeGo toolbar is waiting for someone to
//       actually acquire the DBus name as specified in the
//       service file
//   (e) Banshee is taking forever to load (stuck in Mono.Addins), so
//       the MeeGo extension has not yet run (it is an addin), and
//       thus its /Banshee/ThickClient/GtkBaseClient/PostInitializeGtk
//       extension is never instantiated (which is an instance of
//       Banshee.MeeGo.MeeGoPanel), and in turn, mpl_panel_gtk_new
//       is not invoked in time (ultimately not acquiring the DBus
//       name in time), and so the MeeGo toolbar gives up, and stops
//       showing the panel (even in its "loading" state)
//
// So to work around this, we provide a separate entry point assembly
// for MeeGo, which can acquire the DBus name immediately after
// gtk_init is called. This satisfies the MeeGo toolbar, and we then
// can load the rest of Banshee as usual. When Mono.Addins loads the
// MeeGo extension, the actual panel contents are created, and the
// panel is populated (meanwhile the MeeGo panel UI is in 'startup'
// state, showing a spinner). When the GTK main loop finally runs,
// the panel UI shows up on screen.
//

using System;
using System.IO;
using System.Reflection;

using Banshee.Base;

namespace Banshee.MeeGo.Client
{
    public class MeeGoClient : Nereid.Client
    {
        public new static void Main (string [] args)
        {
            // Normally Mono.Addins would load the MeeGo extension from the
            // Extensions directory, so we need to load this reference manually
            Assembly.LoadFile (Paths.Combine (Path.GetDirectoryName (
                Assembly.GetEntryAssembly ().Location), "Extensions", "Banshee.MeeGo.dll"));
            Startup<MeeGoClient> (args);
        }

        protected override void InitializeGtk ()
        {
            base.InitializeGtk ();
            new Banshee.MeeGo.MeeGoPanel ();
        }
    }
}
