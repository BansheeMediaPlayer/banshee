//
// Brasero.cs
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
using System.IO;
using System.Text;
using System.Diagnostics;
using Mono.Unix;

using Gtk;

using Hyena;

using Banshee.Base;
using Banshee.Collection;
using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Library;
using Banshee.Gui;

namespace Banshee.GnomeBackend
{
    public class Brasero : IDisposable
    {
        private string brasero_exec;

        public Brasero ()
        {
            brasero_exec = Paths.FindProgramInPath ("brasero");
            if (brasero_exec == null) {
                throw new FileNotFoundException ("brasero");
            }
        }

        public void Initialize ()
        {
            InterfaceActionService uia_service = ServiceManager.Get<InterfaceActionService> ();
            uia_service.TrackActions.Add (new ActionEntry [] {
                new ActionEntry ("BurnDiscAction", null,
                    Catalog.GetString ("Write CD..."), null,
                    Catalog.GetString ("Write selected tracks to an audio CD"),
                    OnBurnDisc)
            });

            Gtk.Action action = uia_service.TrackActions["BurnDiscAction"];
            action.IconName = "media-write-cd";

            uia_service.UIManager.AddUiFromResource ("GlobalUI.xml");

            UpdateActions ();
            uia_service.TrackActions.SelectionChanged += delegate { ThreadAssist.ProxyToMain (UpdateActions); };
            ServiceManager.SourceManager.ActiveSourceChanged += delegate { ThreadAssist.ProxyToMain (UpdateActions); };
        }

        public void Dispose ()
        {
        }

        private void OnBurnDisc (object o, EventArgs args)
        {
            DatabaseSource source = ServiceManager.SourceManager.ActiveSource as DatabaseSource;
            if (source == null) {
                return;
            }

            var track_actions = ServiceManager.Get<InterfaceActionService> ().TrackActions;
            StringBuilder file_args = new StringBuilder ();
            file_args.Append ("-a");

            foreach (TrackInfo track in track_actions.SelectedTracks) {
                if (track.Uri.IsLocalPath) {
                    file_args.AppendFormat (" \"{0}\"", track.Uri.AbsolutePath.Replace ("\"", "\\\""));
                }
            }

            try {
                Run (file_args.ToString ());
            } catch (Exception e) {
                Log.Error ("Problem starting Brasero", e);

                Log.Error (Catalog.GetString ("Could not write CD"),
                    Catalog.GetString ("Brasero could not be started"), true);
            }
        }

        internal void Run (string args)
        {
            ProcessStartInfo psi = new ProcessStartInfo (brasero_exec, args);
            psi.UseShellExecute = false;
            Process.Start (psi);
        }

        private void UpdateActions ()
        {
            var track_actions = ServiceManager.Get<InterfaceActionService> ().TrackActions;
            Gtk.Action action = track_actions["BurnDiscAction"];

            bool visible = false;
            bool sensitive = false;

            DatabaseSource source = ServiceManager.SourceManager.ActiveSource as DatabaseSource;
            // FIXME: Figure out how to handle non-music-library sources
            if (source != null) {
                if (source is MusicLibrarySource || source.Parent is MusicLibrarySource) {
                    visible = true;
                }

                sensitive = track_actions.Selection.Count > 0;
            }

            action.Sensitive = sensitive;
            action.Visible = visible;
        }
    }
}
