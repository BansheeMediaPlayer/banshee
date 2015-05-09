//
// DvdManager.cs
//
// Authors:
//   Olivier Dufour <olivier.duff@gmail.com>
//   Andrés G. Aragoneses <knocte@gmail.com>
//   Stephan Sundermann <stephansundermann@gmail.com>
//
// Copyright (C) 2011 Olivier Dufour
// Copyright (C) 2013 Andrés G. Aragoneses
// Copyright (C) 2013 Stephan Sundermann
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

using Gst;
using Gst.Video;

using Hyena;

namespace Banshee.GStreamerSharp
{
    public class DvdManager
    {
        public DvdManager (Element playbin)
        {
            if (playbin != null) {
                playbin.AddNotification ("source", OnSourceChanged);
            }
        }

        INavigation Navigation {
            get; set;
        }
        Element NavigationElement {
            get; set;
        }

        public string Device {
            get; set;
        }

        public bool InDvdMenu {
            get; set;
        }

        private Element GetDvdSource (Element playbin)
        {
            Element source = null;

            if (playbin == null) {
                return null;
            }

            source = playbin ["source"] as Element;

            return source;
        }

        private void OnSourceChanged (object o, GLib.NotifyArgs args)
        {
            if (Device == null) {
                return;
            }

            var playbin = o as Element;
            var dvd_src = GetDvdSource (playbin);
            if (dvd_src == null) {
                return;
            }

            // dvd source elements should always have this property
            dvd_src ["device"] = Device;
            Log.DebugFormat ("dvd: setting device property on source ({0})", Device);
        }

        public bool HandleURI (Element playbin, string uri)
        {
            // Processes URIs like dvd://<device-node> and overrides
            // track transitioning through playbin if playback was already happening
            // from the device node by seeking directly to the track since the disc
            // is already spinning; playbin doesn't handle DVD URIs with device nodes
            // so we have to handle setting the device property on GstCddaBaseSrc
            // through the notify::source signal on playbin

            string new_dvd_device;

            if (playbin == null || String.IsNullOrEmpty (uri) || !uri.StartsWith ("dvd://")) {
                // Something is hosed or the URI isn't actually DVD
                if (Device != null) {
                    Log.WarningFormat ("dvd: finished using device ({0})", Device);
                    Device = null;
                }
                return false;
            }

            // 6 is the size of "dvd://"
            // so we skip this part to only get the device
            new_dvd_device = uri.Substring (6);

            if (Device == null) {
                // If we weren't already playing from a DVD, cache the
                // device and allow playbin to begin playing it
                Device = new_dvd_device;
                Log.DebugFormat ("dvd: storing device node for fast seeks ({0})", Device);
                return false;
            }

            if (new_dvd_device == Device) {
                Log.DebugFormat ("dvd: Already playing device ({0})", Device);

                return true;
            }

            // We were already playing some CD, but switched to a different device node,
            // so unset and re-cache the new device node and allow playbin to do its thing
            Log.DebugFormat ("dvd: switching devices for DVD playback (from {0}, to {1})", Device, new_dvd_device);
            Device = new_dvd_device;

            return false;
        }

        public void HandleCommandsChanged (Element playbin)
        {
            InDvdMenu = false;
            // Get available command to know if player is in menu
            // FIXME: GlobalVideo should be Gst.Video.Global
            Gst.Query query = Gst.Video.Global.NavigationQueryNewCommands ();

            NavigationCommand[] cmds;
            if (Navigation == null) {
                FindNavigation (playbin);
            }
            if (!(NavigationElement.Query (query) && NavigationAdapter.ParseCommands (query, out cmds))) {
                return;
            }
            foreach (NavigationCommand cmd in cmds) {
                switch (cmd) {
                        case NavigationCommand.Activate:
                        case NavigationCommand.Left:
                        case NavigationCommand.Right:
                        case NavigationCommand.Up:
                        case NavigationCommand.Down:
                            InDvdMenu = true;
                            break;
                        default:
                            break;
                }
            }
        }

        public void FindNavigation (Element playbin)
        {
            Element video_sink = null;
            INavigation previous_navigation;

            previous_navigation = Navigation;
            video_sink = playbin ["video-sink"] as Element;

            if (video_sink == null) {
                Navigation = null;
                if (previous_navigation != null) {
                    previous_navigation = null;
                }
            }

            NavigationElement = (video_sink is Bin)
                ? ((Bin)video_sink).GetByInterface (NavigationAdapter.GType)
                : video_sink;
            Navigation = NavigationAdapter.GetObject (NavigationElement);
        }

        public void NotifyMouseMove (Element playbin, double x, double y)
        {
            if (Navigation == null) {
                FindNavigation (playbin);
            }
            if (Navigation != null) {
                Navigation.SendMouseEvent ("mouse-move", 0, x, y);
            }
        }

        public void NotifyMouseButtonPressed (Element playbin, int button, double x, double y)
        {
            if (Navigation == null) {
                FindNavigation (playbin);
            }
            if (Navigation != null) {
                Navigation.SendMouseEvent ("mouse-button-press", button, x, y);
            }
        }

        public void NotifyMouseButtonReleased (Element playbin, int button, double x, double y)
        {
            if (Navigation == null) {
                FindNavigation (playbin);
            }
            if (Navigation != null) {
                Navigation.SendMouseEvent ("mouse-button-release", button, x, y);
            }
        }

        public void NavigateToLeftMenu (Element playbin)
        {
            if (Navigation == null) {
                FindNavigation (playbin);
            }
            if (Navigation != null) {
                Navigation.SendCommand (NavigationCommand.Left);
            }
        }

        public void NavigateToRightMenu (Element playbin)
        {
            if (Navigation == null) {
                FindNavigation (playbin);
            }
            if (Navigation != null) {
                Navigation.SendCommand (NavigationCommand.Right);
            }
        }

        public void NavigateToUpMenu (Element playbin)
        {
            if (Navigation == null) {
                FindNavigation (playbin);
            }
            if (Navigation != null) {
                Navigation.SendCommand (NavigationCommand.Up);
            }
        }

        public void NavigateToDownMenu (Element playbin)
        {
            if (Navigation == null) {
                FindNavigation (playbin);
            }
            if (Navigation != null) {
                Navigation.SendCommand (NavigationCommand.Down);
            }
        }

        public void NavigateToMenu (Element playbin)
        {
            if (Navigation == null) {
                FindNavigation (playbin);
            }
            if (Navigation != null) {
                // Menu1 == DvdMenu http://cgit.freedesktop.org/gstreamer/gst-plugins-base/tree/gst-libs/gst/video/navigation.h?h=1.0#n96 
                Navigation.SendCommand (NavigationCommand.Menu1);
            }
        }

        public void ActivateCurrentMenu (Element playbin)
        {
            if (Navigation == null) {
                FindNavigation (playbin);
            }
            if (Navigation != null) {
                Navigation.SendCommand (NavigationCommand.Activate);
            }
        }

        public void GoToNextChapter (Element playbin)
        {
            long index;
            Format format = Util.FormatGetByNick ("chapter");
            playbin.QueryPosition (format, out index);
            playbin.Seek (1.0, format, SeekFlags.Flush, SeekType.Set, index + 1, SeekType.None, 0L);
        }

        public void GoToPreviousChapter (Element playbin)
        {
            long index;
            Format format = Util.FormatGetByNick ("chapter");
            playbin.QueryPosition (format, out index);
            playbin.Seek (1.0, format, SeekFlags.Flush, SeekType.Set, index - 1, SeekType.None, 0L);
        }
    }
}
