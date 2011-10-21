//
// BaseTrackListView.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2007-2008 Novell, Inc.
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

using Hyena.Data;
using Hyena.Data.Gui;
using Hyena.Gui;

using Banshee.Collection.Database;
using Banshee.Sources;
using Banshee.ServiceStack;
using Banshee.MediaEngine;
using Banshee.Playlist;

using Banshee.Gui;

namespace Banshee.Collection.Gui
{
    public class BaseTrackListView : SearchableListView<TrackInfo>
    {
        public BaseTrackListView () : base ()
        {
            RulesHint = true;
            RowOpaquePropertyName = "Enabled";
            RowBoldPropertyName = "IsPlaying";

            ServiceManager.PlayerEngine.ConnectEvent (
                OnPlayerEvent, PlayerEvent.StartOfStream | PlayerEvent.StateChange);

            ForceDragSourceSet = true;
            IsEverReorderable = true;

            RowActivated += (o, a) => {
                var source = ServiceManager.SourceManager.ActiveSource as ITrackModelSource;
                if (source != null && source.TrackModel == Model) {
                    ServiceManager.Get<InterfaceActionService> ().TrackActions["PlayTrack"].Activate ();
                }
            };

            DragFailed += (o, a) => {
                int x, y;
                GetPointer (out x, out y);
                bool inside_list = (x >= 0 && y >= 0) && (x < Allocation.Width && y < Allocation.Height);
                if (inside_list && a.DragResult == DragResult.NoTarget) {
                    PlaylistSource playlist = ServiceManager.SourceManager.ActiveSource as PlaylistSource;
                    if (playlist != null && !IsReorderable) {
                        Hyena.Log.Information (
                            Catalog.GetString ("Cannot Reorder While Sorted"),
                            Catalog.GetString ("To put the playlist in manual sort mode, click the currently sorted column header until the sort arrow goes away."),
                            true
                        );
                    }
                }
            };
        }

        protected BaseTrackListView (IntPtr raw) : base (raw)
        {
        }

        public override bool SelectOnRowFound {
            get { return true; }
        }

        private static TargetEntry [] source_targets = new TargetEntry [] {
            ListViewDragDropTarget.ModelSelection,
            Banshee.Gui.DragDrop.DragDropTarget.UriList
        };

        protected override TargetEntry [] DragDropSourceEntries {
            get { return source_targets; }
        }

        protected override bool OnKeyPressEvent (Gdk.EventKey press)
        {
            // Have o act the same as enter - activate the selection
            if (GtkUtilities.NoImportantModifiersAreSet () && press.Key == Gdk.Key.o && ActivateSelection ()) {
                return true;
            }
            return base.OnKeyPressEvent (press);
        }

        protected override bool OnPopupMenu ()
        {
            ServiceManager.Get<InterfaceActionService> ().TrackActions["TrackContextMenuAction"].Activate ();
            return true;
        }

        private string user_query;
        protected override void OnModelReloaded ()
        {
            base.OnModelReloaded ();

            var model = Model as IFilterable;
            if (model != null && user_query != model.UserQuery) {
                // Make sure selected tracks are visible as the user edits the query.
                CenterOnSelection ();
                user_query = model.UserQuery;
            }
        }

        private void OnPlayerEvent (PlayerEventArgs args)
        {
            if (args.Event == PlayerEvent.StartOfStream) {
                UpdateSelection ();
            } else if (args.Event == PlayerEvent.StateChange) {
                QueueDraw ();
            }
        }

        private TrackInfo current_track;
        private void UpdateSelection ()
        {
            TrackInfo old_track = current_track;
            current_track = ServiceManager.PlayerEngine.CurrentTrack;

            var track_model = Model as TrackListModel;
            if (track_model == null) {
                return;
            }

            if (Selection.Count > 1) {
                return;
            }

            int old_index = Selection.FirstIndex;
            TrackInfo selected_track = Selection.Count == 1 ? track_model[old_index] : null;
            if (selected_track != null && !selected_track.TrackEqual (old_track)) {
                return;
            }

            int current_index = track_model.IndexOf (current_track);
            if (current_index == -1) {
                return;
            }

            Selection.Clear (false);
            Selection.QuietSelect (current_index);
            Selection.FocusedIndex = current_index;

            if (old_index == -1 || IsRowVisible (old_index)) {
                CenterOn (current_index);
            }
        }

#region Drag and Drop

        protected override void OnDragSourceSet ()
        {
            base.OnDragSourceSet ();
            Drag.SourceSetIconName (this, "audio-x-generic");
        }

        protected override bool OnDragDrop (Gdk.DragContext context, int x, int y, uint time_)
        {
            y = TranslateToListY (y);
            if (Gtk.Drag.GetSourceWidget (context) == this) {
                PlaylistSource playlist = ServiceManager.SourceManager.ActiveSource as PlaylistSource;
                if (playlist != null) {
                    //Gtk.Drag.
                    int row = GetModelRowAt (0, y);
                    if (row != GetModelRowAt (0, y + ChildSize.Height / 2)) {
                        row += 1;
                    }

                    if (playlist.TrackModel.Selection.Contains (row)) {
                        // can't drop within the selection
                        return false;
                    }

                    playlist.ReorderSelectedTracks (row);
                    return true;
                }
            }

            return false;
        }

        protected override void OnDragDataGet (Gdk.DragContext context, SelectionData selection_data, uint info, uint time)
        {
            if (info == Banshee.Gui.DragDrop.DragDropTarget.UriList.Info) {
                ITrackModelSource track_source = ServiceManager.SourceManager.ActiveSource as ITrackModelSource;
                if (track_source != null) {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder ();
                    foreach (TrackInfo track in track_source.TrackModel.SelectedItems) {
                        sb.Append (track.Uri);
                        sb.Append ("\r\n");
                    }
                    byte [] data = System.Text.Encoding.UTF8.GetBytes (sb.ToString ());
                    selection_data.Set (context.Targets[0], 8, data, data.Length);
                }
            }
        }

#endregion
    }
}
