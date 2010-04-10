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
    public class MeeGoTheme : GtkTheme
    {
        public MeeGoTheme (Widget widget) : base (widget)
        {
        }

        private bool IsPanelWidget {
            get { return Widget != null && Widget.Name.StartsWith ("meego-panel"); }
        }

        public override void PushContext ()
        {
            PushContext (new ThemeContext () { Radius = 3 });
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
            if (!IsPanelWidget) {
                base.DrawFrameBorder (cr, alloc);
            }
        }

        public override void DrawFrameBorderFocused (Cairo.Context cr, Gdk.Rectangle alloc)
        {
            if (!IsPanelWidget) {
                base.DrawFrameBorderFocused (cr, alloc);
            }
        }
    }
}

