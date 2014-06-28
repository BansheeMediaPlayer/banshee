//
// SourceView.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2005-2008 Novell, Inc.
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
using System.Linq;
using System.Collections.Generic;

using Gtk;
using Cairo;
using Mono.Unix;

using Hyena;
using Hyena.Gui;
using Hyena.Gui.Theming;
using Hyena.Gui.Theatrics;

using Banshee.Configuration;
using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Playlist;

using Banshee.Gui;

namespace Banshee.Sources.Gui
{
    // Note: This is a partial class - the drag and drop code is split
    //       out into a separate file to make this class more manageable.
    //       See SourceView_DragAndDrop.cs for the DnD code.

    public partial class SourceView : TreeView
    {
        private TreeViewColumn source_column;
        private SourceRowRenderer source_renderer;
        private CellRendererText header_renderer;
        private Theme theme;
        private Cairo.Context cr;

        private Stage<TreeIter> notify_stage = new Stage<TreeIter> (2000);

        private TreeIter highlight_iter = TreeIter.Zero;
        private SourceModel store;
        private int current_timeout = -1;
        private bool editing_row = false;
        private bool need_resort = false;

        protected SourceView (IntPtr ptr) : base (ptr) {}

        public SourceView ()
        {
            FixedHeightMode = false;
            BuildColumns ();

            store = new SourceModel ();
            store.SourceRowInserted += OnSourceRowInserted;
            store.SourceRowRemoved += OnSourceRowRemoved;
            store.RowChanged += OnRowChanged;
            Model = store;
            EnableSearch = false;

            ShowExpanders = false;
            LevelIndentation = 6;

            ConfigureDragAndDrop ();
            store.Refresh ();
            ConnectEvents ();

            Selection.SelectFunction = (selection, model, path, selected) => {
                Source source = store.GetSource (path);
                if (source == null || source is SourceManager.GroupSource) {
                    return false;
                }
                return true;
            };

            ResetSelection ();
        }

#region Setup Methods

        private void BuildColumns ()
        {
            // Hidden expander column
            TreeViewColumn col = new TreeViewColumn ();
            col.Visible = false;
            AppendColumn (col);
            ExpanderColumn = col;

            source_column = new TreeViewColumn ();
            source_column.Sizing = TreeViewColumnSizing.Autosize;

            uint xpad = 2;

            // Special renderer for header rows; hidden for normal source rows
            header_renderer = new CellRendererText () {
                Xpad = xpad,
                Ypad = 4,
                Ellipsize = Pango.EllipsizeMode.End,
                Weight = (int)Pango.Weight.Bold,
                Variant = Pango.Variant.SmallCaps
            };

            // Renderer for source rows; hidden for header rows
            source_renderer = new SourceRowRenderer ();
            source_renderer.Xpad = xpad;

            source_column.PackStart (header_renderer, true);
            source_column.SetCellDataFunc (header_renderer, new Gtk.CellLayoutDataFunc ((layout, cell, model, iter) => {
                if (model == null) {
                    throw new ArgumentNullException ("model");
                }

                // be paranoid about the values returned from model.GetValue(), they may be null or have unexpected types, see bgo#683359
                var obj_type = model.GetValue (iter, (int)SourceModel.Columns.Type);
                if (obj_type == null || !(obj_type is SourceModel.EntryType)) {

                    var source = model.GetValue (iter, (int)SourceModel.Columns.Source) as Source;
                    var source_name = source == null ? "some source" : String.Format ("source {0}", source.Name);

                    Log.ErrorFormat (
                        "SourceView of {0} could not render its source column because its type value returned {1} from the iter",
                        source_name, obj_type == null ? "null" : String.Format ("an instance of {0}", obj_type.GetType ().FullName));

                    header_renderer.Visible = false;
                    source_renderer.Visible = false;

                    return;
                }

                var type = (SourceModel.EntryType) obj_type;
                header_renderer.Visible = type == SourceModel.EntryType.Group;
                source_renderer.Visible = type == SourceModel.EntryType.Source;
                if (type == SourceModel.EntryType.Group) {
                    var source = (Source) model.GetValue (iter, (int)SourceModel.Columns.Source);
                    header_renderer.Visible = true;
                    header_renderer.Text = source.Name;
                } else {
                    header_renderer.Visible = false;
                }
            }));

            int width, height;
            Gtk.Icon.SizeLookup (IconSize.Menu, out width, out height);
            source_renderer.RowHeight = RowHeight.Get ();
            source_renderer.RowHeight = height;

            source_renderer.Ypad = (uint)RowPadding.Get ();
            source_renderer.Ypad = 2;
            source_column.PackStart (source_renderer, true);
            source_column.SetCellDataFunc (source_renderer, new CellLayoutDataFunc (SourceRowRenderer.CellDataHandler));
            AppendColumn (source_column);

            HeadersVisible = false;
        }

        private void ConnectEvents ()
        {
            ServiceManager.SourceManager.ActiveSourceChanged += delegate (SourceEventArgs args) {
                ThreadAssist.ProxyToMain (ResetSelection);
            };

            ServiceManager.SourceManager.SourceUpdated += delegate (SourceEventArgs args) {
                ThreadAssist.ProxyToMain (delegate {
                    lock (args.Source) {
                        TreeIter iter = store.FindSource (args.Source);
                        if (!TreeIter.Zero.Equals (iter)) {
                            if (args.Source.Expanded) {
                                Expand (args.Source);
                            }

                            need_resort = true;
                            QueueDraw ();
                        }
                    }
                });
            };

            ServiceManager.PlaybackController.NextSourceChanged += delegate {
                ThreadAssist.ProxyToMain (QueueDraw);
            };

            notify_stage.ActorStep += delegate (Actor<TreeIter> actor) {
                ThreadAssist.AssertInMainThread ();
                if (!store.IterIsValid (actor.Target)) {
                    return false;
                }

                using (var path = store.GetPath (actor.Target) ) {
                    Gdk.Rectangle rect = GetBackgroundArea (path, source_column);
                    QueueDrawArea (rect.X, rect.Y, rect.Width, rect.Height);
                }
                return true;
            };

            ServiceManager.Get<InterfaceActionService> ().SourceActions["OpenSourceSwitcher"].Activated += delegate {
                new SourceSwitcherEntry (this);
            };
        }

#endregion

#region Gtk.Widget Overrides

        protected override void OnStyleSet (Style old_style)
        {
            base.OnStyleSet (old_style);
            theme = Hyena.Gui.Theming.ThemeEngine.CreateTheme (this);

            var light_text = Hyena.Gui.Theming.GtkTheme.GetCairoTextMidColor (this);
            header_renderer.Foreground = CairoExtensions.ColorGetHex (light_text, false);
        }

        // While scrolling the source view with the keyboard, we want to
        // just skip group sources and jump to the next source in the view.
        protected override bool OnKeyPressEvent (Gdk.EventKey press)
        {
            TreeIter iter;
            bool movedCursor = false;

            Selection.GetSelected (out iter);
            TreePath path = store.GetPath (iter);

            // Move the path to the next source in line as we need to check if it's a group
            IncrementPathForKeyPress (press, path);

            Source source = store.GetSource (path);
            while (source is SourceManager.GroupSource && IncrementPathForKeyPress (press, path)) {
                source = store.GetSource (path);
                SetCursor (path, source_column, false);
                movedCursor = true;
            }

            return movedCursor ? true : base.OnKeyPressEvent (press);
        }

        protected override bool OnButtonPressEvent (Gdk.EventButton press)
        {
            TreePath path;
            TreeViewColumn column;

            if (press.Button == 1) {
                ResetHighlight ();
            }

            // If there is not a row at the click position let the base handler take care of the press
            if (!GetPathAtPos ((int)press.X, (int)press.Y, out path, out column)) {
                return base.OnButtonPressEvent (press);
            }

            Source source = store.GetSource (path);

            if (source == null || source is SourceManager.GroupSource) {
                return false;
            }

            // From F-Spot's SaneTreeView class
            if (source_renderer.InExpander ((int)press.X)) {
                if (!source.Expanded) {
                    ExpandRow (path, false);
                } else {
                    CollapseRow (path);
                }

                // If the active source is a child of this source, and we are about to collapse it, switch
                // the active source to the parent.
                if (source == ServiceManager.SourceManager.ActiveSource.Parent && GetRowExpanded (path)) {
                    ServiceManager.SourceManager.SetActiveSource (source);
                }
                return true;
            }

            // For Sources that can't be activated, when they're clicked just
            // expand or collapse them and return.
            if (press.Button == 1 && !source.CanActivate) {
                if (!source.Expanded) {
                    ExpandRow (path, false);
                } else {
                    CollapseRow (path);
                }
                return false;
            }

            if (press.Button == 3) {
                TreeIter iter;
                if (Model.GetIter (out iter, path)) {
                    HighlightIter (iter);
                    OnPopupMenu ();
                    return true;
                }
            }

            if (!source.CanActivate) {
                return false;
            }


            if (press.Button == 1) {
                if (ServiceManager.SourceManager.ActiveSource != source) {
                    ServiceManager.SourceManager.SetActiveSource (source);
                }
            }

            if ((press.State & Gdk.ModifierType.ControlMask) != 0) {
                if (press.Type == Gdk.EventType.TwoButtonPress && press.Button == 1) {
                    ActivateRow (path, null);
                }
                return true;
            }

            return base.OnButtonPressEvent (press);
        }

        protected override bool OnPopupMenu ()
        {
            ServiceManager.Get<InterfaceActionService> ().SourceActions["SourceContextMenuAction"].Activate ();
            return true;
        }

        protected override bool OnExposeEvent (Gdk.EventExpose evnt)
        {
            if (need_resort) {
                need_resort = false;

                // Resort the tree store. This is performed in an event handler
                // known not to conflict with gtk_tree_view_bin_expose() to prevent
                // errors about corrupting the TreeView's internal state.
                foreach (Source dsource in ServiceManager.SourceManager.Sources.ToArray ()) {
                    TreeIter iter = store.FindSource (dsource);
                    if (!TreeIter.Zero.Equals (iter) &&
                        (int)store.GetValue (iter, (int)SourceModel.Columns.Order) != dsource.Order)
                    {
                        store.SetValue (iter, (int)SourceModel.Columns.Order, dsource.Order);
                    }
                }
                QueueDraw ();
            }

            try {
                cr = Gdk.CairoHelper.Create (evnt.Window);
                base.OnExposeEvent (evnt);
                if (Hyena.PlatformDetection.IsMeeGo) {
                    theme.DrawFrameBorder (cr, new Gdk.Rectangle (0, 0,
                        Allocation.Width, Allocation.Height));
                }
                return true;
            } finally {
                CairoExtensions.DisposeContext (cr);
                cr = null;
            }
        }

        private bool IncrementPathForKeyPress (Gdk.EventKey press, TreePath path)
        {
            switch (press.Key) {
            case Gdk.Key.Up:
            case Gdk.Key.KP_Up:
                return path.Prev ();

            case Gdk.Key.Down:
            case Gdk.Key.KP_Down:
                path.Next ();
                return true;
            }

            return false;
        }

#endregion

#region Gtk.TreeView Overrides

        protected override void OnRowExpanded (TreeIter iter, TreePath path)
        {
            base.OnRowExpanded (iter, path);
            var source = store.GetSource (iter);
            if (source != null) {
                source.Expanded = true;
            }
        }

        protected override void OnRowCollapsed (TreeIter iter, TreePath path)
        {
            base.OnRowCollapsed (iter, path);
            var source = store.GetSource (iter);
            if (source != null) {
                source.Expanded = false;
            }
        }

        protected override void OnCursorChanged ()
        {
            if (current_timeout < 0) {
                current_timeout = (int)GLib.Timeout.Add (200, OnCursorChangedTimeout);
            }
        }

        private bool OnCursorChangedTimeout ()
        {
            TreeIter iter;
            TreeModel model;

            current_timeout = -1;

            if (!Selection.GetSelected (out model, out iter)) {
                return false;
            }

            Source new_source = store.GetValue (iter, (int)SourceModel.Columns.Source) as Source;

            if (ServiceManager.SourceManager.ActiveSource == new_source) {
                return false;
            }

            ServiceManager.SourceManager.SetActiveSource (new_source);

            QueueDraw ();

            return false;
        }

#endregion

#region Add/Remove Sources / SourceManager interaction

        private void OnSourceRowInserted (object o, SourceRowEventArgs args)
        {
            args.Source.UserNotifyUpdated += OnSourceUserNotifyUpdated;

            if (args.Source.Parent != null && args.Source.Parent.AutoExpand == true) {
                Expand (args.ParentIter);
            }

            if (args.Source.Expanded || args.Source.AutoExpand == true) {
                Expand (args.Iter);
            }

            UpdateView ();

            if (args.Source.Properties.Get<bool> ("NotifyWhenAdded")) {
                args.Source.NotifyUser ();
            }
        }

        private void OnSourceRowRemoved (object o, SourceRowEventArgs args)
        {
            args.Source.UserNotifyUpdated -= OnSourceUserNotifyUpdated;
            UpdateView ();
        }

        private void OnRowChanged (object o, RowChangedArgs args)
        {
            QueueDraw ();
        }

        internal void Expand (Source src)
        {
            Expand (store.FindSource (src));
            src.Expanded = true;
        }

        private void Expand (TreeIter iter)
        {
            using (var path = store.GetPath (iter)) {
                ExpandRow (path, true);
            }
        }

        private void OnSourceUserNotifyUpdated (object o, EventArgs args)
        {
            ThreadAssist.ProxyToMain (delegate {
                TreeIter iter = store.FindSource ((Source)o);
                if (iter.Equals (TreeIter.Zero)) {
                    return;
                }

                notify_stage.AddOrReset (iter);
            });
        }

#endregion

#region List/View Utility Methods

        private bool UpdateView ()
        {
            for (int i = 0, m = store.IterNChildren (); i < m; i++) {
                TreeIter iter = TreeIter.Zero;
                if (!store.IterNthChild (out iter, i)) {
                    continue;
                }

                if (store.IterNChildren (iter) > 0) {
                    ExpanderColumn = source_column;
                    return true;
                }
            }

            ExpanderColumn = Columns[0];
            return false;
        }

        internal void UpdateRow (TreePath path, string text)
        {
            TreeIter iter;

            if (!store.GetIter (out iter, path)) {
                return;
            }

            Source source = store.GetValue (iter, (int)SourceModel.Columns.Source) as Source;
            source.Rename (text);
        }

        public void BeginRenameSource (Source source)
        {
            TreeIter iter = store.FindSource (source);
            if (iter.Equals (TreeIter.Zero)) {
                return;
            }

            source_renderer.Editable = true;
            using (var path = store.GetPath (iter)) {
                SetCursor (path, source_column, true);
            }
            source_renderer.Editable = false;
        }

        private void ResetSelection ()
        {
            TreeIter iter = store.FindSource (ServiceManager.SourceManager.ActiveSource);

            if (!iter.Equals (TreeIter.Zero)){
                Selection.SelectIter (iter);
            }
        }

        public void HighlightIter (TreeIter iter)
        {
            highlight_iter = iter;
            QueueDraw ();
        }

        public void ResetHighlight ()
        {
            highlight_iter = TreeIter.Zero;
            QueueDraw ();
        }

#endregion

#region Public Properties

        public Source HighlightedSource {
            get {
                if (TreeIter.Zero.Equals (highlight_iter)) {
                    return null;
                }

                return store.GetValue (highlight_iter, (int)SourceModel.Columns.Source) as Source;
            }
        }

        public bool EditingRow {
            get { return editing_row; }
            set {
                editing_row = value;
                QueueDraw ();
            }
        }

#endregion

#region Internal Properties

        internal TreeIter HighlightedIter {
            get { return highlight_iter; }
        }

        internal Cairo.Context Cr {
            get { return cr; }
        }

        internal Theme Theme {
            get { return theme; }
        }

        internal Stage<TreeIter> NotifyStage {
            get { return notify_stage; }
        }

        internal Source NewPlaylistSource {
            get {
                return new_playlist_source ??
                    (new_playlist_source = new PlaylistSource (Catalog.GetString ("New Playlist"),
                        ServiceManager.SourceManager.MusicLibrary));
            }
        }

#endregion

#region Property Schemas

        private static SchemaEntry<int> RowHeight = new SchemaEntry<int> (
            "player_window", "source_view_row_height", 22, "The height of each source row in the SourceView.  22 is the default.", "");

        private static SchemaEntry<int> RowPadding = new SchemaEntry<int> (
            "player_window", "source_view_row_padding", 5, "The padding between sources in the SourceView.  5 is the default.", "");

#endregion
    }
}
