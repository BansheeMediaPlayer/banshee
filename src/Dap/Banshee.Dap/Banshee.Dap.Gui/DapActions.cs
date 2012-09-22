//
// DapActions.cs
//
// Authors:
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
using Mono.Unix;
using Gtk;

using Hyena;
using Hyena.Widgets;

using Banshee.Dap;
using Banshee.Gui;
using Banshee.ServiceStack;

namespace Banshee.Dap.Gui
{
    public class DapActions : BansheeActionGroup
    {
        private DapSource previous_dap = null;
        private DapSource Dap {
            get { return Actions.SourceActions.ActionSource as DapSource; }
        }

        public DapActions () : base ("dap")
        {
            AddImportant (
                new ActionEntry ("SyncDapAction", null,
                    Catalog.GetString ("Sync"), null,
                    String.Empty, OnSyncDap)
            );

            AddUiFromFile ("GlobalUI.xml");

            this["SyncDapAction"].IconName = Stock.Refresh;
            ServiceManager.SourceManager.ActiveSourceChanged += OnActiveSourceChanged;
            Actions.SourceActions.Updated += delegate { UpdateActions (); };
            OnActiveSourceChanged (null);

            Register ();
        }

        private void OnActiveSourceChanged (EventArgs args)
        {
            if (previous_dap != null) {
                previous_dap.Sync.Updated -= OnSyncUpdated;
            }

            previous_dap = ActiveSource as DapSource;

            if (previous_dap != null) {
                previous_dap.Sync.Updated += OnSyncUpdated;
            }
        }

        private void OnSyncUpdated (DapSync sync)
        {
            ThreadAssist.ProxyToMain (UpdateActions);
        }

        private void UpdateActions ()
        {
            DapSource dap = Dap;
            if (dap != null) {
                UpdateAction ("SyncDapAction", dap.Sync.Enabled);
            }
        }

        private void OnSyncDap (object o, EventArgs args)
        {
            DapSource dap = Dap;
            if (dap != null) {
                dap.Sync.Sync ();
            }
        }

        internal static bool ConfirmUserAction (int tracks_to_remove)
        {
            string header = String.Format (
                Catalog.GetPluralString (
                    // singular form unused b/c we know it's > 1, but we still need GetPlural
                    "The sync operation will remove one track from your device.",
                    "The sync operation will remove {0} tracks from your device.",
                    tracks_to_remove),
                tracks_to_remove);
            string message = Catalog.GetString ("Are you sure you want to continue?");

            bool remove_tracks = false;
            ThreadAssist.BlockingProxyToMain (() => {

                var md = new HigMessageDialog (
                    ServiceManager.Get<GtkElementsService> ().PrimaryWindow,
                    DialogFlags.DestroyWithParent, MessageType.Warning,
                    ButtonsType.None, header, message
                );
                md.AddButton ("gtk-cancel", ResponseType.No, true);
                md.AddButton (Catalog.GetString ("Remove tracks"), ResponseType.Yes, false);

                try {
                    if (md.Run () == (int) ResponseType.Yes) {
                        remove_tracks = true;
                    }
                } finally {
                    md.Destroy ();
                }
            });
            return remove_tracks;
        }
    }
}
