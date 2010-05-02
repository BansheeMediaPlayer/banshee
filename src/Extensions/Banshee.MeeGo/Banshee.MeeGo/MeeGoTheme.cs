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
            TextMidColor = CairoExtensions.ColorShade (Colors.GetWidgetColor (
                GtkColorClass.Background, StateType.Selected), 0.85);
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

            cr.Antialias = Cairo.Antialias.Default;
        }

        public override void DrawFrameBorderFocused (Cairo.Context cr, Gdk.Rectangle alloc)
        {
            if (!IsPanelWidget) {
                base.DrawFrameBorderFocused (cr, alloc);
            }
        }

        public override void DrawRowSelection (Cairo.Context cr, int x, int y, int width, int height,
            bool filled, bool stroked, Cairo.Color color, CairoCorners corners)
        {
            if (!IsSourceViewWidget) {
                base.DrawRowSelection (cr, x, y, width, height, filled,
                    stroked, color, corners);
                return;
            }

            y -= 1;
            x -= 1;
            width += 1;
            height += 1;

            base.DrawRowSelection (cr, x, y, width, height,
                filled, false, color, corners);

            if (stroked) {
                cr.Color = CairoExtensions.ColorShade (color, 0.85);
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

