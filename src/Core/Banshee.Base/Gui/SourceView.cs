/***************************************************************************
 *  SourceView.cs
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@abock.org>
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */
 
using System;
using System.Collections;
using System.Collections.Generic;
using Mono.Unix;
using Gtk;
using Gdk;
using Pango;

using Banshee.Base;
using Banshee.Dap;
using Banshee.Sources;
using Banshee.Gui.DragDrop;

namespace Banshee.Gui
{
    public class CellEdit : Entry, CellEditable
    {
        public string path;
    
        public CellEdit() : base()
        {
            MaxLength = 256;
        }
        
        protected CellEdit(System.IntPtr ptr) : base(ptr)
        {
        }
        
        protected override bool OnFocusOutEvent(Gdk.EventFocus focus)
        {
            FinishEditing();
            RemoveWidget();
            return base.OnFocusOutEvent(focus);
        }
    }
    
    public class EditedEventArgs : EventArgs
    {
        public TreePath path;
        public string text;
    }
    
    public delegate void EditedEventHandler(object o, EditedEventArgs args);

    public class SourceView : TreeView
    {
        private Source newPlaylistSource = new PlaylistSource(-1);
        private TreeIter newPlaylistIter = TreeIter.Zero;
        private bool newPlaylistVisible = false;
        
        private TreeStore store;
        private TreeViewColumn focus_column;
        private SourceRowRenderer renderer;
        private int currentTimeout = -1;
        
        private static TargetEntry [] dnd_source_entries = new TargetEntry [] {
            Banshee.Gui.DragDrop.DragDropTarget.Source
        };
            
        private static TargetEntry [] dnd_dest_entries = new TargetEntry [] {
            Banshee.Gui.DragDrop.DragDropTarget.TrackInfoObjects,
            Banshee.Gui.DragDrop.DragDropTarget.Source
        };
    
        public event EventHandler SourceDoubleClicked;
    
        public SourceView()
        {
            // Hidden expander column
            TreeViewColumn col = new TreeViewColumn();
            col.Visible = false;
            AppendColumn(col);
            ExpanderColumn = col;
        
            focus_column = new TreeViewColumn();
            renderer = new SourceRowRenderer();
            focus_column.Title = Catalog.GetString("Source");
            focus_column.PackStart(renderer, true);
            focus_column.SetCellDataFunc(renderer, new TreeCellDataFunc(SourceCellDataFunc));
            AppendColumn(focus_column);
            
            store = new TreeStore(typeof(Source), typeof(int));
            store.SetSortColumnId (1, SortType.Ascending);
            store.ChangeSortColumn ();
            Model = store;
            HeadersVisible = false;
            
            EnableModelDragSource(Gdk.ModifierType.Button1Mask | Gdk.ModifierType.Button3Mask,
                dnd_source_entries, DragAction.Copy | DragAction.Move);
        
            EnableModelDragDest(dnd_dest_entries, DragAction.Copy | DragAction.Move);
            
            RefreshList();

            SourceManager.SourceAdded += delegate(SourceAddedArgs args) {
                AddSource(args.Source);
            };
            
            SourceManager.SourceRemoved += delegate(SourceEventArgs args) {
                RemoveSource(args.Source);
            };
            
            SourceManager.ActiveSourceChanged += delegate(SourceEventArgs args) {
                ResetHighlight();
            };
            
            SourceManager.SourceUpdated += delegate(SourceEventArgs args) {
                TreeIter iter = FindSource (args.Source);
                store.SetValue (iter, 1, args.Source.Order);
                QueueDraw();
            };
        }

        private TreeIter FindSource(Source source)
        {
            TreeIter iter = TreeIter.Zero;
            store.GetIterFirst(out iter);
            return FindSource(source, iter);
        }
        
        private TreeIter FindSource(Source source, TreeIter iter)
        {
            if(!store.IterIsValid(iter)) {
                return TreeIter.Zero;
            }
            
            do {
                if((store.GetValue(iter, 0) as Source) == source) {
                    return iter;
                }
                
                if(store.IterHasChild(iter)) {
                    TreeIter citer = TreeIter.Zero;
                    store.IterChildren(out citer, iter);
                    TreeIter result = FindSource(source, citer);
                    if(!result.Equals(TreeIter.Zero)) {
                        return result;
                    }
                }
            } while(store.IterNext(ref iter));
            
            return TreeIter.Zero;
        }
        
        private void AddSource(Source source)
        {
            AddSource(source, TreeIter.Zero);
        }

        private void AddSource(Source source, TreeIter parent)
        {
            // Don't add duplicates
            if(!FindSource(source).Equals(TreeIter.Zero))
                return;

            // Don't add a child source before its parent
            if(parent.Equals(TreeIter.Zero) && source is ChildSource)
                return;

            int position = source.Order;
            
            TreeIter iter = parent.Equals(TreeIter.Zero)
                ? store.InsertNode(position) 
                : store.InsertNode(parent, position);
            
            store.SetValue(iter, 0, source);
            store.SetValue(iter, 1, source.Order);

            lock(source.Children) {
                foreach(ChildSource s in source.Children) {
                    AddSource(s, iter);
                }
            }

            source.ChildSourceAdded += delegate(SourceEventArgs e) {
                AddSource(e.Source, iter);
            };

            source.ChildSourceRemoved += delegate(SourceEventArgs e) {
                RemoveSource(e.Source);
            };
           
            if(source.Expanded || (source.AutoExpand != null && source.AutoExpand.Value)) {
                Expand(iter);
            }
            
            if(source is ChildSource) {
                ChildSource child = source as ChildSource;
                if(child.Parent.AutoExpand) {
                    Expand(FindSource(child.Parent));
                }
            }
            
            UpdateView();
        }

        private void RemoveSource(Source source)
        {
            TreeIter iter = FindSource(source);
            if(!iter.Equals(TreeIter.Zero)) {
                store.Remove(ref iter);
            }

            UpdateView();
        }
    
        private void Expand(TreeIter iter)
        {
            TreePath path = store.GetPath(iter);
            ExpandRow(path, true);
        }
    
        private void RefreshList()
        {
            store.Clear();
            foreach(Source source in SourceManager.Sources) {
                AddSource(source);
            }
        }

        private bool UpdateView()
        {
            for(int i = 0, m = store.IterNChildren(); i < m; i++) {
                TreeIter iter = TreeIter.Zero;
                if(!store.IterNthChild(out iter, i)) {
                    continue;
                }
                
                if(store.IterNChildren(iter) > 0) {
                    ExpanderColumn = Columns[1];
                    return true;
                }
            }
        
            ExpanderColumn = Columns[0];
            return false;
        }
        
        protected override void OnRowExpanded(TreeIter iter, TreePath path)
        {
            base.OnRowExpanded(iter, path);
            GetSource(iter).Expanded = true;
        }
        
        protected override void OnRowCollapsed(TreeIter iter, TreePath path)
        {
            base.OnRowCollapsed(iter, path);
            GetSource(iter).Expanded = false;
        }
        
        protected void SourceCellDataFunc(TreeViewColumn tree_column,
            CellRenderer cell, TreeModel tree_model, TreeIter iter)
        {
            SourceRowRenderer renderer = (SourceRowRenderer)cell;
            renderer.view = this;
            renderer.source = (Source)store.GetValue(iter, 0);
            
            if(renderer.source == null) {
                return;
            }
            
            renderer.Selected = renderer.source.Equals(SourceManager.ActiveSource);
            renderer.Italicized = renderer.source.Equals(newPlaylistSource);
            renderer.Sensitive = renderer.source.CanActivate;
        }
        
        internal void UpdateRow(TreePath path, string text)
        {
            TreeIter iter;
            
            if(!store.GetIter(out iter, path)) {
                return;
            }
            
            Source source = store.GetValue(iter, 0) as Source;
            source.Rename(text);
        }
        
        public void BeginRenameSource(Source source)
        {
            TreeIter iter = FindSource(source);
            if(iter.Equals(TreeIter.Zero)) {
                return;
            }
            renderer.Editable = true;
            SetCursor(store.GetPath(iter), focus_column, true);
            renderer.Editable = false;
        }

        protected override bool OnButtonPressEvent(Gdk.EventButton evnt)
        {
            TreePath path;
            
            if(!GetPathAtPos((int)evnt.X, (int)evnt.Y, out path)) {
                return true;
            }

            Source source = GetSource(path);
            if(evnt.Button == 1) {
                if(!source.CanActivate) {
                    if(!source.Expanded) {
                        ExpandRow(path, false);
                    } else {
                        CollapseRow(path);
                    }
                    return false;
                }
                
                if(SourceManager.ActiveSource != source) {
                    SourceManager.SetActiveSource(source);
                }
                
                if(evnt.Type == EventType.TwoButtonPress) {
                    OnSourceDoubleClicked();
                }
                
            } else if(evnt.Button == 3) {
                HighlightPath(path);

                SourceManager.SensitizeActions(source);

                string group_name = source.ActionPath == null ? "/SourceMenu" : source.ActionPath;
                Menu source_menu = Globals.ActionManager.GetWidget(group_name) as Menu;
                source_menu.SelectionDone += delegate {
                    SourceManager.SensitizeActions(SourceManager.ActiveSource);
                    ResetHighlight();
                };
            
                source_menu.Popup(null, null, null, 0, evnt.Time);
                source_menu.Show();
                
                return true;
            }
            
            return base.OnButtonPressEvent(evnt);
        }

        protected override void OnCursorChanged()
        {
            if(currentTimeout < 0) {
                currentTimeout = (int)GLib.Timeout.Add(200, OnCursorChangedTimeout);
            }
        }
        
        private bool OnCursorChangedTimeout()
        {
            TreeIter iter;
            TreeModel model;
            
            currentTimeout = -1;
            
            if(!Selection.GetSelected(out model, out iter)) {
                return false;
            }
            
            Source new_source = store.GetValue(iter, 0) as Source;
            if(SourceManager.ActiveSource == new_source) {
                return false;
            }
            
            SourceManager.SetActiveSource(new_source);
            
            QueueDraw();

            return false;
        }
        
        protected virtual void OnSourceDoubleClicked()
        {
            EventHandler handler = SourceDoubleClicked;
            if(handler != null) {
                handler(this, new EventArgs());
            }
        }

        protected override void OnDragBegin(Gdk.DragContext context)
        {
            if(HighlightedSource.IsDragSource || HighlightedSource is IImportSource) {
                base.OnDragBegin(context);
            }
        }
        
        protected override void OnDragDataGet(Gdk.DragContext context, SelectionData selectionData,
            uint info, uint time)
        {
            switch((DragDropTargetType)info) {
                case DragDropTargetType.Source:
                    new DragDropList<Source>(HighlightedSource, selectionData, context.Targets[0]);
                    break;
                default:
                    return;
            }
            
            base.OnDragDataGet(context, selectionData, info, time);
        }
        
        protected override bool OnDragMotion(Gdk.DragContext context, int x, int y, uint time)
        {
            if(Gtk.Drag.GetSourceWidget(context) == this 
                && !HighlightedSource.IsDragSource && !(HighlightedSource is IImportSource)) {
                return false;
            }
        
            base.OnDragMotion(context, x, y, time);
            SetDragDestRow(null, TreeViewDropPosition.IntoOrAfter);
            Gdk.Drag.Status(context, Gdk.DragAction.Copy, time);

            // FIXME: We need to handle this nicer
            if(Gtk.Drag.GetSourceWidget(context) != this && 
                !(SourceManager.ActiveSource is LibrarySource ||
                SourceManager.ActiveSource is PlaylistSource ||
                SourceManager.ActiveSource is Banshee.SmartPlaylist.SmartPlaylistSource ||
                SourceManager.ActiveSource is IImportable)) {
                return false;
            }
        
            if(!newPlaylistVisible && Gtk.Drag.GetSourceWidget(context) != this) {
                TreeIter library = FindSource(LibrarySource.Instance);
                newPlaylistIter = store.AppendNode(library);
                store.SetValue(newPlaylistIter, 0, newPlaylistSource);
                store.SetValue(newPlaylistIter, 1, 999);
                newPlaylistVisible = true;

                UpdateView();
                Expand(library);
            }
        
            TreePath path;
            TreeViewDropPosition pos;
            
            if(GetDestRowAtPos(x, y, out path, out pos)) {
                Source source = GetSource(path);
                
                if(source == SourceManager.ActiveSource) {
                    return false;
                }
                
                SetDragDestRow(path, TreeViewDropPosition.IntoOrAfter);
                
                if((source is LibrarySource && (SourceManager.ActiveSource is IImportable 
                    || SourceManager.ActiveSource is IImportSource)) ||
                    (source is PlaylistSource) || (source is DapSource) || source.AcceptsInput) {
                    return true;
                }

                Gdk.Drag.Status(context, 0, time);
                return true;
            }
            
            return true;
        }
        
        private Source final_drag_source = null;
        private uint final_drag_start_time = 0;
    
        protected override void OnDragLeave(Gdk.DragContext context, uint time)
        {
            TreePath path;
            TreeViewDropPosition pos;
            GetDragDestRow (out path, out pos);

            if(path == null) {
                path = store.GetPath(newPlaylistIter);
            }
            
            final_drag_source = GetSource (path);
            final_drag_start_time = context.StartTime;
        
            if(newPlaylistVisible) {
                store.Remove(ref newPlaylistIter);
                newPlaylistVisible = false;
                UpdateView();
            }
        }

        protected override void OnDragDataReceived(Gdk.DragContext context, int x, int y,
            Gtk.SelectionData selectionData, uint info, uint time)
        {
            if(Gtk.Drag.GetSourceWidget(context) == this) {
                DragDropList<Source> sources = selectionData;
                if(sources.Count <= 0) { 
                    return;
                }
                
                Source source = sources[0];
                
                if(source is IImportSource && final_drag_source is LibrarySource) {
                    (source as IImportSource).Import();
                    Gtk.Drag.Finish(context, true, false, time);
                } else if(final_drag_source != null && source.IsDragSource && 
                    final_drag_source.AcceptsSourceDrop) {
                    final_drag_source.SourceDrop(source);
                    Gtk.Drag.Finish(context, true, false, time);
                } else {
                    Gtk.Drag.Finish(context, false, false, time);
                }
                
                return;
            }
            
            if(final_drag_start_time == context.StartTime) {
                PlaylistSource playlist_remove_on_failure = null;
                try {
                    DragDropList<TrackInfo> dnd_transfer = selectionData;
                    TrackDropOperation(final_drag_source, dnd_transfer, out playlist_remove_on_failure);
                } catch(Exception e) {
                    if(playlist_remove_on_failure != null) {
                        playlist_remove_on_failure.Unmap();
                        playlist_remove_on_failure = null;
                    }
                    
                    LogCore.Instance.PushError(Catalog.GetString("Could not import tracks"), e.Message);
                }
            }
            
            if(newPlaylistVisible) {
                store.Remove(ref newPlaylistIter);
                newPlaylistVisible = false;
                UpdateView();
            }
        
            Gtk.Drag.Finish(context, true, false, time);
        }
        
        private void TrackDropOperation(Source source, IList<TrackInfo> tracks, 
            out PlaylistSource newPlaylist)
        {
            newPlaylist = null;
            
            if(source is LibrarySource && SourceManager.ActiveSource is IImportable) {
                IImportable import_source = SourceManager.ActiveSource as IImportable;
                import_source.Import(tracks);
            } else if(source is PlaylistSource && SourceManager.ActiveSource is IImportable) {
                IImportable import_source = SourceManager.ActiveSource as IImportable;
                PlaylistSource playlist = null;
                    
                if(source == newPlaylistSource) {
                    playlist = new PlaylistSource();
                    LibrarySource.Instance.AddChildSource(playlist);
                    newPlaylist = playlist;
                } else {
                    playlist = source as PlaylistSource;
                }
                    
                import_source.Import(tracks, playlist);
            } else if(source == newPlaylistSource) {
                PlaylistSource playlist = new PlaylistSource();
                playlist.AddTrack(tracks);
                playlist.Rename(PlaylistUtil.GoodUniqueName(playlist.Tracks));
                playlist.Commit();
                LibrarySource.Instance.AddChildSource(playlist);
                UpdateView();
            } else if(source is PlaylistSource || source is DapSource || source.AcceptsInput) {
                source.AddTrack(tracks);
                source.Commit();
            }
        }
        
        public void HighlightPath(TreePath path)
        {
            Selection.SelectPath(path);
        }
        
        public Source GetSource(TreeIter iter)
        {
            return store.GetValue(iter, 0) as Source;
        }
        
        public Source GetSource(TreePath path)
        {
            TreeIter iter;
        
            if(store.GetIter(out iter, path)) {
                return GetSource(iter);
            }
        
            return null;
        }
        
        public void ResetHighlight()
        {
            TreeIter iter = FindSource (SourceManager.ActiveSource);
            
            if(!iter.Equals(TreeIter.Zero)){
                Selection.SelectIter(iter);
            }
         }
        
        public Source HighlightedSource {
            get {
                TreeModel model;
                TreeIter iter;
                
                if(!Selection.GetSelected(out model, out iter)) {
                    return null;
                }
                    
                return store.GetValue(iter, 0) as Source;
            }
        }
        
        public void ThreadedQueueDraw()
        {
            Application.Invoke(delegate {
                QueueDraw();
            });
        }
        
        public bool EditingRow = false;
    }

    public class SourceRowRenderer : CellRendererText
    {
        public bool Selected = false;
        public bool Italicized = false;
        public Source source;
        public SourceView view;

        public SourceRowRenderer()
        {
        }
        
        protected SourceRowRenderer(System.IntPtr ptr) : base(ptr)
        {
        
        }
        
        private StateType RendererStateToWidgetState(CellRendererState flags)
        {
            StateType state = StateType.Normal;
            if((CellRendererState.Selected & flags).Equals(
                CellRendererState.Selected))
                state = StateType.Selected;
            return state;
        }
        
        public override void GetSize(Widget widget, ref Gdk.Rectangle cell_area,
            out int x_offset, out int y_offset, out int width, out int height)
        {        
               int text_x, text_y, text_w, text_h;
   
               base.GetSize(widget, ref cell_area, out text_x, out text_y, 
                   out text_w, out text_h);
                
            x_offset = 0;
            y_offset = 0;
            width = text_w;
            height = text_h + 5;
        }
        
        protected override void Render(Gdk.Drawable drawable, 
            Widget widget, Gdk.Rectangle background_area, 
            Gdk.Rectangle cell_area, Gdk.Rectangle expose_area, 
            CellRendererState flags)
        {
            int titleLayoutWidth, titleLayoutHeight;
            int countLayoutWidth, countLayoutHeight;
            int maxTitleLayoutWidth;
            
            if(source == null) {
                return;
            }
            
            bool hideCounts = source.Count <= 0;
            
            StateType state = RendererStateToWidgetState(flags);
            Pixbuf icon = null;
            
            if(source == null) {
                return;
            }

            icon = source.Icon;
            if(icon == null) {
                icon = IconThemeUtils.LoadIcon(22, "source-library");
            }
            
            Pango.Layout titleLayout = new Pango.Layout(widget.PangoContext);
            Pango.Layout countLayout = new Pango.Layout(widget.PangoContext);
            
            FontDescription fd = widget.PangoContext.FontDescription.Copy();
            fd.Weight = Selected ? Pango.Weight.Bold : Pango.Weight.Normal;
            if(Italicized || source.HasEmphasis) {
                fd.Style = Pango.Style.Italic;
                hideCounts = true;
            }

            titleLayout.FontDescription = fd;
            countLayout.FontDescription = fd;
            
            titleLayout.SetMarkup("...");
            titleLayout.GetPixelSize(out titleLayoutWidth, out titleLayoutHeight);
            int ellipsisSize = titleLayoutWidth;
            
            string titleText = source.Name;
            titleLayout.SetMarkup(GLib.Markup.EscapeText(titleText));
            countLayout.SetMarkup("<span size=\"small\">(" + source.Count + ")</span>");
            
            titleLayout.GetPixelSize(out titleLayoutWidth, out titleLayoutHeight);
            countLayout.GetPixelSize(out countLayoutWidth, out countLayoutHeight);
            
            maxTitleLayoutWidth = cell_area.Width - icon.Width - countLayoutWidth - 10;
            
            if(titleLayoutWidth > maxTitleLayoutWidth) {
                float ratio = (float)(maxTitleLayoutWidth - ellipsisSize) / (float)titleLayoutWidth;
                int characters = (int)(ratio * (float)titleText.Length);
                while(titleLayoutWidth > maxTitleLayoutWidth) {
                    if(characters > 0) {
                        titleLayout.SetMarkup(GLib.Markup.EscapeText(
                            titleText.Substring(0, characters--)).Trim() + "...");
                        titleLayout.GetPixelSize(out titleLayoutWidth, out titleLayoutHeight);
                    } else {
                        hideCounts = true;
                        titleLayout.SetMarkup(GLib.Markup.EscapeText(titleText.Trim()));
                        break;
                    }
                }
            }
            
            Gdk.GC mainGC = widget.Style.TextGC(state);

            drawable.DrawPixbuf(mainGC, icon, 0, 0, 
                cell_area.X + 0, 
                cell_area.Y + ((cell_area.Height - icon.Height) / 2),
                icon.Width, icon.Height,
                RgbDither.None, 0, 0);
        
            drawable.DrawLayout(mainGC, 
                cell_area.X + icon.Width + 6, 
                cell_area.Y + ((cell_area.Height - titleLayoutHeight) / 2) + 1, 
                titleLayout);
            
            if(hideCounts) {
                return;
            }
                
            Gdk.GC modGC = widget.Style.TextGC(state);
            if(!state.Equals(StateType.Selected)) {
                modGC = new Gdk.GC(drawable);
                modGC.Copy(widget.Style.TextGC(state));
                Gdk.Color fgcolor = widget.Style.Foreground(state);
                Gdk.Color bgcolor = widget.Style.Background(state);
                modGC.RgbFgColor = Utilities.ColorBlend(fgcolor, bgcolor);
            } 
            
            drawable.DrawLayout(modGC,
                (cell_area.X + cell_area.Width) - countLayoutWidth - 2,
                cell_area.Y + ((cell_area.Height - countLayoutHeight) / 2) + 1,
                countLayout);
        }
        
        public override CellEditable StartEditing(Gdk.Event evnt , Widget widget, 
            string path, Gdk.Rectangle background_area, Gdk.Rectangle cell_area, 
            CellRendererState flags)
        {
            CellEdit text = new CellEdit();
            text.EditingDone += OnEditDone;
            text.Text = source.Name;
            text.path = path;
            text.Show();
            
            view.EditingRow = true;
            
            return text;
        }
        
        private void OnEditDone(object o, EventArgs args)
        {
            CellEdit edit = o as CellEdit;
            if(view == null) {
                return;
            }
            
            view.EditingRow = false;
            view.UpdateRow(new TreePath(edit.path), edit.Text);
        }
    }
}
