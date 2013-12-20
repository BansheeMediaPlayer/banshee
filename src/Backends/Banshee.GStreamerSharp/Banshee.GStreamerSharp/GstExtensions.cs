//
// GstExtensions.cs
//
// Author:
//   Andrés G. Aragoneses <knocte@gmail.com>
//
// Copyright 2013 Andrés G. Aragoneses
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

using Gst;

namespace Banshee.GStreamerSharp
{
    internal static class GstExtensions
    {
        internal static uint AddWatch (this Bus bus, BusFunc func)
        {
            //https://developer.gnome.org/glib/unstable/glib-The-Main-Event-Loop.html#G-PRIORITY-DEFAULT:CAPS
            int G_PRIORITY_DEFAULT = 0;
            //FIXME: use AddWatch() when it is bindable
            return bus.AddWatchFull (G_PRIORITY_DEFAULT, func);
            //gst_bus_add_watch (gst_pipeline_get_bus (GST_PIPELINE (detector->pipeline)), bbd_pipeline_bus_callback, detector);
        }

        //TODO: use Add(params..) from the binding when it's available
        internal static void Add (this Bin sink, params Element[] elements)
        {
            foreach (var elt in elements) {
                sink.Add (elt);
            }
        }
    }
}

