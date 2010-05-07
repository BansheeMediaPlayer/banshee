// 
// MeeGoTheme.cs
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

using System;

using Gtk;
using Cairo;

using Hyena.Gui;
using Hyena.Gui.Theming;

namespace Banshee.MeeGo
{
    public class MeeGoThemeLoader
    {
        public MeeGoThemeLoader ()
        {
            Hyena.Gui.Theming.ThemeEngine.SetCurrentTheme<MeeGoTheme> ();
        }
    }

    public class MeeGoTheme : GtkTheme
    {
        public MeeGoTheme (Widget widget) : base (widget)
        {
        }

        private bool IsSourceViewWidget;
        private bool IsPanelWidget;
        private bool IsRoundedFrameWidget;

        public override void PushContext ()
        {
            IsPanelWidget = Widget != null && Widget.Name.StartsWith ("meego-panel");
            IsSourceViewWidget = Widget is Banshee.Sources.Gui.SourceView;
            IsRoundedFrameWidget = Widget is Hyena.Widgets.RoundedFrame;

            PushContext (new ThemeContext () {
                Radius = IsRoundedFrameWidget || IsSourceViewWidget ? 0 : 3,
                ToplevelBorderCollapse = true
            });
        }

        protected override void OnColorsRefreshed ()
        {
            base.OnColorsRefreshed ();
        }

        public override void DrawFrameBackground (Cairo.Context cr, Gdk.Rectangle alloc,
            Cairo.Color color, Cairo.Pattern pattern)
        {
            if (!IsPanelWidget) {
                base.DrawFrameBackground (cr, alloc, color, pattern);
            }
        }

        public override void DrawFrameBorder (Cairo.Context cr, Gdk.Rectangle alloc)
        {
            if (IsPanelWidget) {
                return;
            } else if (!IsSourceViewWidget) {
                base.DrawFrameBorder (cr, alloc);
                return;
            }

            cr.Color = TextMidColor;
            cr.LineWidth = 1.0;
            cr.Antialias = Cairo.Antialias.None;

            cr.MoveTo (alloc.Right - 1, alloc.Top);
            cr.LineTo (alloc.Right - 1, alloc.Bottom);
            cr.Stroke ();

            if (Widget.Allocation.Bottom < Widget.Toplevel.Allocation.Height) {
                cr.MoveTo (alloc.Left, alloc.Bottom - 1);
                cr.LineTo (alloc.Right, alloc.Bottom - 1);
                cr.Stroke ();
            }

            cr.Antialias = Cairo.Antialias.Default;
        }

        public override void DrawHeaderBackground (Cairo.Context cr, Gdk.Rectangle alloc)
        {
            CairoCorners corners = CairoCorners.TopLeft | CairoCorners.TopRight;

            LinearGradient grad = new LinearGradient (alloc.X, alloc.Y, alloc.X, alloc.Bottom);
            grad.AddColorStop (0, CairoExtensions.RgbToColor (0xf6f3f3));
            grad.AddColorStop (0.33, CairoExtensions.RgbToColor (0xeeecec));
            grad.AddColorStop (0.66, CairoExtensions.RgbToColor (0xeeecec));
            grad.AddColorStop (1, CairoExtensions.RgbToColor (0xe1dfdf));

            cr.Pattern = grad;
            CairoExtensions.RoundedRectangle (cr, alloc.X, alloc.Y, alloc.Width, alloc.Height, Context.Radius, corners);
            cr.Fill ();

            cr.Color = CairoExtensions.RgbToColor (0x919191);
            cr.Rectangle (alloc.X, alloc.Bottom, alloc.Width, BorderWidth);
            cr.Fill ();
            grad.Destroy ();
        }

        public override void DrawRowSelection (Cairo.Context cr, int x, int y, int width, int height,
            bool filled, bool stroked, Cairo.Color color, CairoCorners corners)
        {
            if (!IsSourceViewWidget) {
                base.DrawRowSelection (cr, x, y, width, height, filled,
                    stroked, color, corners, true);
                return;
            }

            y -= 1;
            x -= 1;
            width += 1;
            height += 1;

            color = TextMidColor;

            base.DrawRowSelection (cr, x, y, width, height,
                filled, false, color, corners, true);

            if (stroked) {
                cr.Color = color;
                cr.LineWidth = 1.0;
                cr.Antialias = Cairo.Antialias.None;

                cr.MoveTo (x, y);
                cr.LineTo (x + width, y);
                cr.Stroke ();

                cr.MoveTo (x, y + height);
                cr.LineTo (x + width, y + height);
                cr.Stroke ();

                cr.Antialias = Cairo.Antialias.Default;
            }
        }
    }
}

