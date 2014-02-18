//
// DapInfoBar.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
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
using Hyena.Gui;
using Hyena.Widgets;

namespace Banshee.Dap.Gui
{
    public class DapInfoBar : RoundedFrame
    {
        private DapSource source;
        private SegmentedBar disk_bar;
        private Alignment disk_bar_align;

        public DapInfoBar (DapSource source)
        {
            this.source = source;
            source.Updated += OnSourceUpdated;

            BuildWidget ();
        }

        protected DapInfoBar (IntPtr handle) : base (handle)
        {
        }

        protected override void OnDestroyed ()
        {
            base.OnDestroyed ();
            if (source != null) {
                source.Updated -= OnSourceUpdated;
                source = null;
            }
        }

        private void BuildWidget ()
        {
            HBox box = new HBox ();

            disk_bar_align = new Alignment (0.5f, 0.5f, 1.0f, 1.0f);
            disk_bar = new SegmentedBar ();
            disk_bar.ValueFormatter = DapValueFormatter;

            disk_bar.AddSegmentRgb (Catalog.GetString ("Audio"), 0, 0x3465a4);
            disk_bar.AddSegmentRgb (Catalog.GetString ("Video"), 0, 0x73d216);
            disk_bar.AddSegmentRgb (Catalog.GetString ("Other"), 0, 0xf57900);
            disk_bar.AddSegment (Catalog.GetString ("Free Space"), 0, disk_bar.RemainderColor, false);

            UpdateUsage ();

            disk_bar_align.Add (disk_bar);

            box.PackStart (disk_bar_align, true, true, 0);
            disk_bar_align.TopPadding = 6;

            Add (box);
            box.ShowAll ();

            SizeAllocated += delegate (object o, Gtk.SizeAllocatedArgs args) {
                SetBackground ();
                disk_bar.HorizontalPadding = (int)(args.Allocation.Width * 0.25);
            };
        }

        private string DapValueFormatter (SegmentedBar.Segment segment)
        {
            if (source == null) {
                return null;
            }

            long size = (long)(source.BytesCapacity * segment.Percent);
            return size <= 0
                ? Catalog.GetString ("None")
                : new Hyena.Query.FileSizeQueryValue (size).ToUserQuery ();
        }

        private void OnSourceUpdated (object o, EventArgs args)
        {
            ThreadAssist.ProxyToMain (delegate {
                try {
                    if (source == null || source.Sync.Syncing) {
                        return;
                    }
                    UpdateUsage ();
                } catch (Exception e) {
                    Hyena.Log.Exception (e);
                }
            });
        }

        protected override void OnStyleSet (Style previous_style)
        {
            base.OnStyleSet (previous_style);
            SetBackground ();
        }

        private void SetBackground ()
        {
            Cairo.Color light = CairoExtensions.GdkColorToCairoColor (Style.Background (StateType.Normal));
            Cairo.Color dark = CairoExtensions.ColorShade (light, 0.85);

            Cairo.LinearGradient grad = new Cairo.LinearGradient (0, Allocation.Y, 0, Allocation.Y + Allocation.Height);
            grad.AddColorStop (0, dark);
            grad.AddColorStop (1, light);
            FillPattern = grad;
        }

        private void UpdateUsage ()
        {
            long data = source.BytesUsed - source.BytesMusic - source.BytesVideo;
            double cap = (double)source.BytesCapacity;

            disk_bar.UpdateSegment (0, source.BytesMusic / cap);
            disk_bar.UpdateSegment (1, source.BytesVideo / cap);
            disk_bar.UpdateSegment (2, data / cap);
            disk_bar.UpdateSegment (3, (cap - source.BytesUsed) / cap);
        }
    }
}
