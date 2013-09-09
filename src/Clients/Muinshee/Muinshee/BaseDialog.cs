//
// BaseDialog.cs
//
// Authors:
//   Brad Taylor <brad@getcoded.net>
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
using Hyena.Data;
using Hyena.Widgets;

using Banshee.Gui;
using Banshee.Widgets;
using Banshee.Gui.Dialogs;
using Banshee.Playlist;
using Banshee.Collection;
using Banshee.Collection.Gui;
using Banshee.ServiceStack;
using Banshee.Configuration;
using Banshee.Collection.Database;
using Banshee.PlaybackController;
using Banshee.MediaEngine;

namespace Muinshee
{
    internal class MuinsheeSearchEntry : SearchEntry
    {
        protected override bool OnKeyPressEvent (Gdk.EventKey evnt)
        {
            // The default behavior is to have Esc clear the search entry
            // Close the dialog if there is already nothing in the search entry
            if (evnt.Key == Gdk.Key.Escape && String.IsNullOrEmpty (Query)) {
                return false;
            }
            return base.OnKeyPressEvent (evnt);
        }
    }

    public abstract class BaseDialog : BansheeDialog
    {
        protected const string CONFIG_NAMESPACE_PREFIX = "muinshee";
        protected const int DEFAULT_WIDTH = 500;
        protected const int DEFAULT_HEIGHT = 475;

        private SearchEntry search_entry;
        private PlaylistSource queue;
        private PersistentWindowController window_controller;

        public BaseDialog (PlaylistSource queue, string title, WindowConfiguration windowConfig) : base (title)
        {
            this.queue = queue;
            VBox.Spacing = 6;

            HBox filter_box = new HBox ();
            filter_box.Spacing = 6;

            Label search_label = new Label ("_Search:");
            filter_box.PackStart (search_label, false, false, 0);

            search_entry = new MuinsheeSearchEntry ();
            search_entry.Show ();
            search_entry.Changed += OnFilterChanged;
            search_entry.Ready = true;
            OnFilterChanged (null, null);
            filter_box.PackStart (search_entry, true, true, 0);

            VBox.PackStart (filter_box, false, false, 0);

            Hyena.Widgets.ScrolledWindow sw = new Hyena.Widgets.ScrolledWindow ();
            sw.Add (GetItemWidget ());
            VBox.PackStart (sw, true, true, 0);

            AddDefaultCloseButton ();

            Button queue_button = new ImageButton (Catalog.GetString ("En_queue"), "gtk-add");
            AddActionWidget (queue_button, Gtk.ResponseType.Apply);

            Button play_button = new ImageButton (Catalog.GetString ("_Play"), "media-playback-start");
            AddButton (play_button, Gtk.ResponseType.Ok, true);

            window_controller = new PersistentWindowController (this, windowConfig, WindowPersistOptions.Size);
            window_controller.Restore ();
            ShowAll ();

            Response += OnResponse;
        }

        private void OnResponse (object o, ResponseArgs args)
        {
            ResponseType response = args.ResponseId;

            if (response == ResponseType.Apply) {
                Queue ();
                return;
            }

            if (response == ResponseType.Ok) {
                Play ();
            }

            Destroy ();
        }

        private void OnFilterChanged (object o, EventArgs args)
        {
            Music.FilterQuery = search_entry.Query;
        }

        protected void Play ()
        {
            TrackInfo to_play = FirstTrack;
            Hyena.Log.InformationFormat  ("first to play is {0}", to_play);
            Queue ();
            if (to_play != null) {
                int i = QueueSource.DatabaseTrackModel.IndexOfFirst (to_play);
                Hyena.Log.InformationFormat ("but in queue is index {0}", i);
                if (i != -1) {
                    ServiceManager.PlayerEngine.OpenPlay (QueueSource.TrackModel[i]);
                }
            }
        }

        protected abstract void Queue ();
        protected abstract Widget GetItemWidget ();
        protected abstract TrackInfo FirstTrack { get; }

        protected PlaylistSource QueueSource { get { return queue; } }

        protected static Banshee.Library.MusicLibrarySource Music { get { return ServiceManager.SourceManager.MusicLibrary; } }

        public override void Destroy ()
        {
            OnFilterChanged (null, null);
            base.Destroy ();
        }
    }
}
