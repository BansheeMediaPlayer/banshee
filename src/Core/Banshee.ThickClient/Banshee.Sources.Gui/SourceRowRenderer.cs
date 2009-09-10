//
// SourceRowRenderer.cs
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

using Gtk;
using Gdk;
using Pango;

using Hyena.Gui.Theming;
using Hyena.Gui.Theatrics;

using Banshee.ServiceStack;

namespace Banshee.Sources.Gui
{
    public class SourceRowRenderer : CellRendererText
    {
        public static void CellDataHandler (CellLayout layout, CellRenderer cell, TreeModel model, TreeIter iter)
        {
            SourceRowRenderer renderer = cell as SourceRowRenderer;
            Source source = model.GetValue (iter, 0) as Source;
            
            if (renderer == null) {
                return;
            }
            
            renderer.Source = source;
            renderer.Path = model.GetPath (iter);
            
            if (source == null) {
                return;
            }
            
            renderer.Text = source.Name;
            renderer.Sensitive = source.CanActivate;
        }
        
        private Source source;
        public Source Source {
            get { return source; }
            set { source = value; }
        }
        
        private SourceView view;
        
        private Widget parent_widget;
        public Widget ParentWidget {
            get { return parent_widget; }
            set { parent_widget = value; }
        }
        
        private TreePath path;
        public TreePath Path {
            get { return path; }
            set { path = value; }
        }
        
        private int padding;
        public int Padding {
            get { return padding; }
            set { padding = value; }
        }

        public SourceRowRenderer ()
        {
        }
        
        private StateType RendererStateToWidgetState (Widget widget, CellRendererState flags)
        {
            if (!Sensitive) {
                return StateType.Insensitive;
            } else if ((flags & CellRendererState.Selected) == CellRendererState.Selected) {
                return widget.HasFocus ? StateType.Selected : StateType.Active;
            } else if ((flags & CellRendererState.Prelit) == CellRendererState.Prelit) {
                ComboBox box = parent_widget as ComboBox;
                return box != null && box.PopupShown ? StateType.Prelight : StateType.Normal;
            } else if (widget.State == StateType.Insensitive) {
                return StateType.Insensitive;
            } else {
                return StateType.Normal;
            }
        }
        
        public override void GetSize (Widget widget, ref Gdk.Rectangle cell_area,
            out int x_offset, out int y_offset, out int width, out int height)
        {        
            int text_x, text_y, text_w, text_h;
   
            base.GetSize (widget, ref cell_area, out text_x, out text_y, out text_w, out text_h);
                
            x_offset = 0;
            y_offset = 0;
            
            if (!(widget is TreeView)) {
                width = 200;
            } else {
                width = 0;
            }
            
            height = (int)Math.Max (22, text_h) + Padding;
        }
        
        protected override void Render (Gdk.Drawable drawable, Widget widget, Gdk.Rectangle background_area, 
            Gdk.Rectangle cell_area, Gdk.Rectangle expose_area, CellRendererState flags)
        {
            if (source == null) {
                return;
            }
            
            view = widget as SourceView;
            bool path_selected = view != null && view.Selection.PathIsSelected (path);            
            StateType state = RendererStateToWidgetState (widget, flags);
            
            RenderSelection (drawable, background_area, path_selected, state);
            
            int title_layout_width = 0, title_layout_height = 0;
            int count_layout_width = 0, count_layout_height = 0;
            int max_title_layout_width;
            
            bool hide_counts = source.Count <= 0;
            
            Pixbuf icon = SourceIconResolver.ResolveIcon (source);

            if (state == StateType.Insensitive) {
                // Code ported from gtk_cell_renderer_pixbuf_render()
                var icon_source = new IconSource () {
                    Pixbuf = icon,
                    Size = IconSize.SmallToolbar,
                    SizeWildcarded = false
                };

                icon = widget.Style.RenderIcon (icon_source, widget.Direction, state,
                    (IconSize)(-1), widget, "SourceRowRenderer");
            }
            
            FontDescription fd = widget.PangoContext.FontDescription.Copy ();
            fd.Weight = (ISource)ServiceManager.PlaybackController.NextSource == (ISource)source 
                ? Pango.Weight.Bold 
                : Pango.Weight.Normal;

            if (view != null && source == view.NewPlaylistSource) {
                fd.Style = Pango.Style.Italic;
                hide_counts = true;
            }
            
            Pango.Layout title_layout = new Pango.Layout (widget.PangoContext);
            Pango.Layout count_layout = null;
            
            if (!hide_counts) {
                count_layout = new Pango.Layout (widget.PangoContext);
                count_layout.FontDescription = fd;
                count_layout.SetMarkup (String.Format ("<span size=\"small\">{0}</span>", source.Count));
                count_layout.GetPixelSize (out count_layout_width, out count_layout_height);
            }

            max_title_layout_width = cell_area.Width - (icon == null ? 0 : icon.Width) - count_layout_width - 10;
            
            if (!hide_counts && max_title_layout_width < 0) {
                hide_counts = true;
            }
			
            title_layout.FontDescription = fd;
            title_layout.Width = (int)(max_title_layout_width * Pango.Scale.PangoScale);
            title_layout.Ellipsize = EllipsizeMode.End;
            title_layout.SetText (source.Name);
            title_layout.GetPixelSize (out title_layout_width, out title_layout_height);
            
            Gdk.GC main_gc = widget.Style.TextGC (state);
            
            drawable.DrawLayout (main_gc, 
                cell_area.X + (icon == null ? 0 : icon.Width) + 6, 
                Middle (cell_area, title_layout_height),
                title_layout);
            
            if (icon != null) {
                drawable.DrawPixbuf (main_gc, icon, 0, 0, 
                    cell_area.X, Middle (cell_area, icon.Height),
                    icon.Width, icon.Height, RgbDither.None, 0, 0);
            }
            
            if (hide_counts) {
                return;
            }
                
            Gdk.GC mod_gc = widget.Style.TextGC (state);
            if (state == StateType.Normal || (view != null && state == StateType.Prelight)) {
                Gdk.Color fgcolor = widget.Style.Base (state);
                Gdk.Color bgcolor = widget.Style.Text (state);
                
                mod_gc = new Gdk.GC (drawable);
                mod_gc.Copy (widget.Style.TextGC (state));
                mod_gc.RgbFgColor = Hyena.Gui.GtkUtilities.ColorBlend (fgcolor, bgcolor);
                mod_gc.RgbBgColor = fgcolor;
            } 
            
            drawable.DrawLayout (mod_gc,
                cell_area.X + cell_area.Width - count_layout_width - 2,
                Middle (cell_area, count_layout_height),
                count_layout);
        }
        
        private void RenderSelection (Gdk.Drawable drawable, Gdk.Rectangle background_area, 
            bool path_selected, StateType state)
        {
            if (view == null) {
                return;
            }
            
            if (path_selected && view.Cr != null) {
                Gdk.Rectangle rect = background_area;
                rect.X -= 2;
                rect.Width += 4;
                
                // clear the standard GTK selection and focus
                drawable.DrawRectangle (view.Style.BaseGC (StateType.Normal), true, rect);
                
                // draw the hot cairo selection
                if (!view.EditingRow) { 
                    view.Theme.DrawRowSelection (view.Cr, background_area.X + 1, background_area.Y + 1, 
                        background_area.Width - 2, background_area.Height - 2);
                }
            } else if (path != null && path.Equals (view.HighlightedPath) && view.Cr != null) {
                view.Theme.DrawRowSelection (view.Cr, background_area.X + 1, background_area.Y + 1, 
                    background_area.Width - 2, background_area.Height - 2, false);
            } else if (view.NotifyStage.ActorCount > 0 && view.Cr != null) {
                TreeIter iter;
                if (view.Model.GetIter (out iter, path) && view.NotifyStage.Contains (iter)) {
                    Actor<TreeIter> actor = view.NotifyStage[iter];
                    Cairo.Color color = view.Theme.Colors.GetWidgetColor (GtkColorClass.Background, StateType.Active);
                    color.A = Math.Sin (actor.Percent * Math.PI);
                        
                    view.Theme.DrawRowSelection (view.Cr, background_area.X + 1, background_area.Y + 1, 
                        background_area.Width - 2, background_area.Height - 2, true, true, color);
                }
            }
        }
        
        private int Middle (Gdk.Rectangle area, int height)
        {
            return area.Y + (int)Math.Round ((double)(area.Height - height) / 2.0) + 1;
        }
        
        public override CellEditable StartEditing (Gdk.Event evnt, Widget widget, string path, 
            Gdk.Rectangle background_area, Gdk.Rectangle cell_area, CellRendererState flags)
        {
            CellEditEntry text = new CellEditEntry ();
            text.EditingDone += OnEditDone;
            text.Text = source.Name;
            text.path = path;
            text.Show ();
            
            view.EditingRow = true;
            
            return text;
        }
        
        private void OnEditDone (object o, EventArgs args)
        {
            CellEditEntry edit = (CellEditEntry)o;
            if (view == null) {
                return;
            }
            
            view.EditingRow = false;
            view.UpdateRow (new TreePath (edit.path), edit.Text);
        }
    }
}
