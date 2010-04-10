//
// DataViewChildAlbum.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright 2007-2010 Novell, Inc.
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
using Cairo;

using Hyena.Gui;
using Hyena.Gui.Theming;
using Hyena.Gui.Canvas;
using Hyena.Data.Gui;
using Hyena.Data.Gui.Accessibility;
using Hyena.Gui.Theatrics;

using Banshee.Gui;
using Banshee.ServiceStack;

namespace Banshee.Collection.Gui
{
    public class DataViewChildAlbum : DataViewChild
    {
        private static Stage<DataViewChildAlbum> stage = new Stage<DataViewChildAlbum> (250);

        static DataViewChildAlbum ()
        {
            stage.ActorStep += actor => {
                var alpha = actor.Target.prelight_opacity;
                alpha += actor.Target.prelight_in
                    ? actor.StepDeltaPercent
                    : -actor.StepDeltaPercent;
                actor.Target.prelight_opacity = alpha = Math.Max (0.0, Math.Min (1.0, alpha));
                actor.Target.InvalidateImage ();
                return alpha > 0 && alpha < 1;
            };
        }

        private ArtworkManager artwork_manager;

        private bool prelight_in;
        private double prelight_opacity;

        private ImageSurface image_surface;
        private string [] lines;

        private Rect inner_allocation;
        private Rect image_allocation;
        private Rect first_line_allocation;
        private Rect second_line_allocation;

        public double ImageSize { get; set; }
        public double ImageSpacing { get; set; }
        public double TextSpacing { get; set; }

        private bool IsGridLayout {
            // FIXME: Cache this after implementing virtual notification
            // on ColumnCell that ViewLayout has changed ...
            get { return ParentLayout is DataViewLayoutGrid; }
        }

        public DataViewChildAlbum ()
        {
            artwork_manager = ServiceManager.Get<ArtworkManager> ();

            Padding = new Thickness (5);
            ImageSize = 90;
            ImageSpacing = 2;
            TextSpacing = 2;
        }

        public override void Arrange ()
        {
            if (!HandleBoundObject (out image_surface, out lines)) {
                return;
            }

            inner_allocation = new Rect () {
                X = Padding.Left,
                Y = Padding.Top,
                Width = Allocation.Width - Padding.X,
                Height = Allocation.Height - Padding.Y
            };

            double width = ImageSize;
            double height = ImageSize;

            if (image_surface != null) {
                width = image_surface.Width;
                height = image_surface.Height;
            }

            image_allocation = new Rect () {
                Width = Math.Min (inner_allocation.Width, width),
                Height = Math.Min (inner_allocation.Height, height)
            };

            if (IsGridLayout) {
                image_allocation.X = Math.Round ((inner_allocation.Width - width) / 2.0);
            } else {
                image_allocation.Y = Math.Round ((inner_allocation.Height - height) / 2.0);
            }

            if (IsGridLayout) {
                first_line_allocation.Y = image_allocation.Height + ImageSpacing;
                first_line_allocation.Width = second_line_allocation.Width = inner_allocation.Width;
            } else {
                first_line_allocation.X = second_line_allocation.X = image_allocation.Width + ImageSpacing;
                first_line_allocation.Width = second_line_allocation.Width =
                    inner_allocation.Width - image_allocation.Width - ImageSpacing;
            }

            second_line_allocation.Y = first_line_allocation.Bottom + TextSpacing;
        }

        public override void Render (CellContext context)
        {
            if (inner_allocation.IsEmpty) {
                return;
            }

            context.Context.Translate (inner_allocation.X, inner_allocation.Y);

            RenderImageSurface (context, image_allocation, image_surface);

            // Render the overlay
            if (IsGridLayout && prelight_opacity > 0) {
                var a = prelight_opacity;
                var cr = context.Context;
                var grad = new RadialGradient (5, 5, (image_allocation.Width + image_allocation.Height) / 2.0, 5, 5, 0);
                grad.AddColorStop (0, new Color (0, 0, 0, 0.65 * a));
                grad.AddColorStop (1, new Color (0, 0, 0, 0.15 * a));
                cr.Pattern = grad;
                CairoExtensions.RoundedRectangle (cr, image_allocation.X, image_allocation.Y,
                    image_allocation.Width, image_allocation.Height, context.Theme.Context.Radius);
                cr.Fill ();
                grad.Destroy ();

                /*cr.Save ();
                cr.LineWidth = 2;
                cr.Antialias = Cairo.Antialias.Default;

                // math prep for rendering multiple controls...
                double max_controls = 3;
                double spacing = 4;
                double radius = (width - ((max_controls + 1) * spacing)) / max_controls / 2;

                // render first control
                cr.Arc (width / 2, height - radius - 2 * spacing, radius, 0, 2 * Math.PI);

                cr.Color = new Color (0, 0, 0, 0.4);
                cr.FillPreserve ();
                cr.Color = new Color (1, 1, 1, 0.8);
                cr.Stroke ();

                cr.Restore ();*/
            }

            if (lines == null || lines.Length < 2) {
                return;
            }

            var text_color = context.Theme.Colors.GetWidgetColor (GtkColorClass.Text, context.State);

            var layout = context.Layout;
            layout.Ellipsize = Pango.EllipsizeMode.End;
            layout.Width = (int)(first_line_allocation.Width * Pango.Scale.PangoScale);

            int normal_size = layout.FontDescription.Size;
            int small_size = (int)(normal_size * Pango.Scale.Small);

            if (!String.IsNullOrEmpty (lines[0])) {
                layout.FontDescription.Size = small_size;
                layout.SetText (lines[0]);

                context.Context.Color = text_color;
                context.Context.MoveTo (first_line_allocation.X, first_line_allocation.Y);
                PangoCairoHelper.ShowLayout (context.Context, layout);
            }

            if (!String.IsNullOrEmpty (lines[1])) {
                layout.FontDescription.Weight = Pango.Weight.Normal;
                layout.FontDescription.Size = small_size;
                layout.SetText (lines[1]);

                text_color.A = 0.60;
                context.Context.Color = text_color;
                context.Context.MoveTo (second_line_allocation.X, second_line_allocation.Y);
                PangoCairoHelper.ShowLayout (context.Context, layout);
            }

            layout.FontDescription.Size = normal_size;
        }

        public override Size Measure (Size available)
        {
            var widget = ParentLayout.View;

            var fd = widget.PangoContext.FontDescription;
            int normal_size = fd.Size;

            fd.Size = (int)(fd.Size * Pango.Scale.Small);
            first_line_allocation.Height = fd.MeasureTextHeight (widget.PangoContext);

            fd.Weight = Pango.Weight.Normal;
            fd.Size = (int)(fd.Size * Pango.Scale.Small);
            second_line_allocation.Height = fd.MeasureTextHeight (widget.PangoContext);

            fd.Size = normal_size;

            double width, height;
            double text_height = first_line_allocation.Height + second_line_allocation.Height;

            if (IsGridLayout) {
                width = ImageSize + Padding.X;
                height = ImageSize + ImageSpacing + TextSpacing + text_height + Padding.Y;
            } else {
                double list_text_height = text_height + TextSpacing;
                width = ImageSize + ImageSpacing + Padding.Y;
                height = (list_text_height < ImageSize ? ImageSize : list_text_height) + Padding.X;
            }

            return new Size (Math.Round (width), Math.Round (height));
        }

        protected void InvalidateImage ()
        {
            var damage = image_allocation;
            damage.Offset (inner_allocation);
            Invalidate (damage);
        }

        protected virtual void RenderImageSurface (CellContext context, Rect allocation, ImageSurface imageSurface)
        {
            ArtworkRenderer.RenderThumbnail (context.Context,
                imageSurface, false,
                allocation.X, allocation.Y, allocation.Width, allocation.Height,
                true, context.Theme.Context.Radius,
                imageSurface == null, new Color (0.8, 0.8, 0.8));
        }

        protected virtual bool HandleBoundObject (out ImageSurface image, out string [] lines)
        {
            image = null;
            lines = null;

            var album = BoundObject as AlbumInfo;
            if (album == null) {
                if (BoundObject == null) {
                    return false;
                };

                throw new InvalidCastException ("ColumnCellAlbum can only bind to AlbumInfo objects");
            }

            lines = new [] { album.DisplayTitle, ModelRowIndex == 0 ? "" : album.DisplayArtistName };
            image = artwork_manager != null
                ? artwork_manager.LookupScaleSurface (album.ArtworkId, (int)ImageSize, true)
                : null;

            return true;
        }

        public override void CursorEnterEvent ()
        {
            prelight_in = true;
            stage.AddOrReset (this);
        }

        public override void CursorLeaveEvent ()
        {
            prelight_in = false;
            stage.AddOrReset (this);
        }

#if false
#region Accessibility

        private class ColumnCellAlbumAccessible : ColumnCellAccessible
        {
            public ColumnCellAlbumAccessible (object bound_object, ColumnCellAlbum cell, ICellAccessibleParent parent)
                : base (bound_object, cell as ColumnCell, parent)
            {
                var bound_album_info = (AlbumInfo)bound_object;
                Name = String.Format ("{0} - {1}", bound_album_info.DisplayTitle, bound_album_info.DisplayArtistName);
            }
        }
        
        public override Atk.Object GetAccessible (ICellAccessibleParent parent)
        {
            return new ColumnCellAlbumAccessible (BoundObject, this, parent);
        }

#endregion
#endif

    }
}
