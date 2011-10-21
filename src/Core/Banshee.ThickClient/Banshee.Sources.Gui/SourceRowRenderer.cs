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

using Hyena.Gui;
using Hyena.Gui.Theming;
using Hyena.Gui.Theatrics;

using Banshee.ServiceStack;

namespace Banshee.Sources.Gui
{
    public class SourceRowRenderer : CellRendererText
    {
        protected SourceRowRenderer (IntPtr raw) : base (raw)
        {
        }

        public static void CellDataHandler (CellLayout layout, CellRenderer cell, TreeModel model, TreeIter iter)
        {
            SourceRowRenderer renderer = cell as SourceRowRenderer;
            if (renderer == null) {
                return;
            }

            var type = model.GetValue (iter, (int)SourceModel.Columns.Type);
            if (type == null || (SourceModel.EntryType) type != SourceModel.EntryType.Source) {
                renderer.Visible = false;
                return;
            }

            Source source = model.GetValue (iter, 0) as Source;
            renderer.Source = source;
            renderer.Iter = iter;

            if (source == null) {
                return;
            }

            renderer.Visible = true;
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

        private TreeIter iter = TreeIter.Zero;
        public TreeIter Iter {
            get { return iter; }
            set { iter = value; }
        }

        private int row_height = 22;
        public int RowHeight {
            get { return row_height; }
            set { row_height = value; }
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

        private int Depth {
            get {
                return Source.Parent != null ? 1 : 0;
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

            height = (int)Math.Max (RowHeight, text_h);
        }

        private int expander_right_x;
        public bool InExpander (int x)
        {
            return x < expander_right_x;
        }

        protected override void Render (Gdk.Drawable drawable, Widget widget, Gdk.Rectangle background_area,
            Gdk.Rectangle cell_area, Gdk.Rectangle expose_area, CellRendererState flags)
        {
            if (source == null || source is SourceManager.GroupSource) {
                return;
            }

            view = widget as SourceView;
            bool selected = view != null && view.Selection.IterIsSelected (iter);
            StateType state = RendererStateToWidgetState (widget, flags);

            RenderSelection (drawable, background_area, selected, state);

            int title_layout_width = 0, title_layout_height = 0;
            int count_layout_width = 0, count_layout_height = 0;
            int max_title_layout_width;

            int img_padding = 6;
            int expander_icon_spacing = 3;
            int x = cell_area.X;
            bool np_etc = (source.Order + Depth * 100) < 40;
            if (!np_etc) {
                x += Depth * img_padding + (int)Xpad;
            } else {
                // Don't indent NowPlaying and Play Queue as much
                x += Math.Max (0, (int)Xpad - 2);
            }

            Gdk.GC main_gc = widget.Style.TextGC (state);

            // Draw the expander if the source has children
            double exp_h = (cell_area.Height - 2.0*Ypad) / 3.2;
            double exp_w = exp_h * 1.6;
            if (view != null && view.Cr != null && source.Children != null && source.Children.Count > 0) {
                var r = new Gdk.Rectangle (x, cell_area.Y + (int)((cell_area.Height - exp_h) / 2.0), (int)exp_w, (int)exp_h);
                view.Theme.DrawArrow (view.Cr, r, source.Expanded ? Math.PI/2.0 : 0.0);
            }

            if (!np_etc) {
                x += (int) exp_w;
                x += 2; // a little spacing after the expander
                expander_right_x = x;
            }

            // Draw icon
            Pixbuf icon = SourceIconResolver.ResolveIcon (source, RowHeight);

            bool dispose_icon = false;
            if (state == StateType.Insensitive) {
                // Code ported from gtk_cell_renderer_pixbuf_render()
                var icon_source = new IconSource () {
                    Pixbuf = icon,
                    Size = IconSize.SmallToolbar,
                    SizeWildcarded = false
                };

                icon = widget.Style.RenderIcon (icon_source, widget.Direction, state,
                    (IconSize)(-1), widget, "SourceRowRenderer");

                dispose_icon = true;
                icon_source.Dispose ();
            }

            if (icon != null) {
                x += expander_icon_spacing;
                drawable.DrawPixbuf (main_gc, icon, 0, 0,
                    x, Middle (cell_area, icon.Height),
                    icon.Width, icon.Height, RgbDither.None, 0, 0);

                x += icon.Width;

                if (dispose_icon) {
                    icon.Dispose ();
                }
            }

            // Setup font info for the title/count, and see if we should show the count
            bool hide_count = source.EnabledCount <= 0 || source.Properties.Get<bool> ("SourceView.HideCount");
            FontDescription fd = widget.PangoContext.FontDescription.Copy ();
            fd.Weight = (ISource)ServiceManager.PlaybackController.NextSource == (ISource)source
                ? Pango.Weight.Bold
                : Pango.Weight.Normal;

            if (view != null && source == view.NewPlaylistSource) {
                fd.Style = Pango.Style.Italic;
                hide_count = true;
            }

            Pango.Layout title_layout = new Pango.Layout (widget.PangoContext);
            Pango.Layout count_layout = null;

            // If we have a count to draw, setup its fonts and see how wide it is to see if we have room
            if (!hide_count) {
                count_layout = new Pango.Layout (widget.PangoContext);
                count_layout.FontDescription = fd;
                count_layout.SetMarkup (String.Format ("<span size=\"small\">{0}</span>", source.EnabledCount));
                count_layout.GetPixelSize (out count_layout_width, out count_layout_height);
            }

            // Hide the count if the title has no space
            max_title_layout_width = cell_area.Width - x - count_layout_width;//(icon == null ? 0 : icon.Width) - count_layout_width - 10;
            if (!hide_count && max_title_layout_width <= 0) {
                hide_count = true;
            }

            // Draw the source Name
            title_layout.FontDescription = fd;
            title_layout.Width = (int)(max_title_layout_width * Pango.Scale.PangoScale);
            title_layout.Ellipsize = EllipsizeMode.End;
            title_layout.SetText (source.Name);
            title_layout.GetPixelSize (out title_layout_width, out title_layout_height);

            x += img_padding;
            drawable.DrawLayout (main_gc, x, Middle (cell_area, title_layout_height), title_layout);

            title_layout.Dispose ();

            // Draw the count
            if (!hide_count) {
                if (view != null && view.Cr != null) {
                    view.Cr.Color = state == StateType.Normal || (view != null && state == StateType.Prelight)
                        ? view.Theme.TextMidColor
                        : view.Theme.Colors.GetWidgetColor (GtkColorClass.Text, state);

                    view.Cr.MoveTo (
                        cell_area.X + cell_area.Width - count_layout_width - 2,
                        cell_area.Y + 0.5 + (double)(cell_area.Height - count_layout_height) / 2.0);
                    PangoCairoHelper.ShowLayout (view.Cr, count_layout);
                }

                count_layout.Dispose ();
            }

            fd.Dispose ();
        }

        private void RenderSelection (Gdk.Drawable drawable, Gdk.Rectangle background_area,
            bool selected, StateType state)
        {
            if (view == null) {
                return;
            }

            if (selected && view.Cr != null) {
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
            } else if (!TreeIter.Zero.Equals (iter) && iter.Equals (view.HighlightedIter) && view.Cr != null) {
                view.Theme.DrawRowSelection (view.Cr, background_area.X + 1, background_area.Y + 1,
                    background_area.Width - 2, background_area.Height - 2, false);
            } else if (view.NotifyStage.ActorCount > 0 && view.Cr != null) {
                if (!TreeIter.Zero.Equals (iter) && view.NotifyStage.Contains (iter)) {
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
            return area.Y + (int)Math.Round ((double)(area.Height - height) / 2.0, MidpointRounding.AwayFromZero);
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
            using (var tree_path = new TreePath (edit.path)) {
                view.UpdateRow (tree_path, edit.Text);
            }
        }
    }
}
