//
// ConnectedSeekSlider.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2007 Novell, Inc.
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

using Banshee.Widgets;
using Banshee.MediaEngine;
using Banshee.ServiceStack;

namespace Banshee.Gui.Widgets
{
    public enum SeekSliderLayout {
        Horizontal,
        Vertical
    }

    public class ConnectedSeekSlider : Alignment
    {
        private SeekSlider seek_slider;
        private StreamPositionLabel stream_position_label;
        private Box box;
        private Hyena.Widgets.GrabHandle grabber;

        public ConnectedSeekSlider () : this (SeekSliderLayout.Vertical)
        {
        }

        public ConnectedSeekSlider (SeekSliderLayout layout) : base (0.5f, 0.5f, 1.0f, 0.0f)
        {
            RightPadding = 10;
            LeftPadding = 10;

            BuildSeekSlider (layout);

            ServiceManager.PlayerEngine.ConnectEvent (OnPlayerEvent,
                PlayerEvent.Iterate |
                PlayerEvent.Buffering |
                PlayerEvent.StartOfStream |
                PlayerEvent.StateChange);

            ServiceManager.PlayerEngine.TrackIntercept += OnTrackIntercept;
            SizeAllocated += delegate { QueueDraw (); };

            seek_slider.SeekRequested += OnSeekRequested;

            // Initialize the display if we're paused since we won't get any
            // events or state change until something actually happens (BGO #536564)
            if (ServiceManager.PlayerEngine.CurrentState == PlayerState.Paused) {
                OnPlayerEngineTick ();
            }
        }

        public void Disconnect ()
        {
            ServiceManager.PlayerEngine.DisconnectEvent (OnPlayerEvent);
            ServiceManager.PlayerEngine.TrackIntercept -= OnTrackIntercept;
            seek_slider.SeekRequested -= OnSeekRequested;
            base.Dispose ();
        }

        public StreamPositionLabel StreamPositionLabel {
            get { return stream_position_label; }
        }

        public SeekSlider SeekSlider {
            get { return seek_slider; }
        }

        public int Spacing {
            get { return box.Spacing; }
            set { box.Spacing = value; }
        }

        private void BuildSeekSlider (SeekSliderLayout layout)
        {
            var hbox = new HBox () { Spacing = 2 };
            seek_slider = new SeekSlider ();
            stream_position_label = new StreamPositionLabel (seek_slider);

            if (layout == SeekSliderLayout.Horizontal) {
                box = new HBox ();
                box.Spacing = 5;
                stream_position_label.FormatString = "<b>{0}</b>";
            } else {
                box = new VBox ();
            }

            seek_slider.SetSizeRequest (175, -1);

            box.PackStart (seek_slider, false, false, 0);
            box.PackStart (stream_position_label, true, true, 0);

            hbox.PackStart (box, true, true, 0);

            grabber = new Hyena.Widgets.GrabHandle () { NoShowAll = true };
            grabber.ControlWidthOf (seek_slider, 125, 1024, true);

            hbox.PackStart (grabber, true, true, 0);
            hbox.ShowAll ();
            Resizable = false;

            Add (hbox);
        }

        public bool Resizable {
            get { return grabber.Visible; }
            set {
                grabber.Visible = value;
                // grabber is 5 + 2 spacing, do reduce right padding to 3
                RightPadding = value ? (uint)3 : (uint)10;
            }
        }

        private bool transitioning = false;
        private bool OnTrackIntercept (Banshee.Collection.TrackInfo track)
        {
            transitioning = true;
            return false;
        }

        private void OnPlayerEvent (PlayerEventArgs args)
        {
            switch (args.Event) {
                case PlayerEvent.Iterate:
                    OnPlayerEngineTick ();
                    break;
                case PlayerEvent.StartOfStream:
                    stream_position_label.StreamState = StreamLabelState.Playing;
                    seek_slider.CanSeek = ServiceManager.PlayerEngine.CanSeek;
                    break;
                case PlayerEvent.Buffering:
                    PlayerEventBufferingArgs buffering = (PlayerEventBufferingArgs)args;
                    if (buffering.Progress >= 1.0) {
                        stream_position_label.StreamState = StreamLabelState.Playing;
                        break;
                    }

                    stream_position_label.StreamState = StreamLabelState.Buffering;
                    stream_position_label.BufferingProgress = buffering.Progress;
                    seek_slider.Sensitive = false;
                    break;
                case PlayerEvent.StateChange:
                    switch (((PlayerEventStateChangeArgs)args).Current) {
                        case PlayerState.Contacting:
                            transitioning = false;
                            stream_position_label.StreamState = StreamLabelState.Contacting;
                            seek_slider.SetIdle ();
                            break;
                        case PlayerState.Loading:
                            transitioning = false;
                            if (((PlayerEventStateChangeArgs)args).Previous == PlayerState.Contacting) {
                                stream_position_label.StreamState = StreamLabelState.Loading;
                                seek_slider.SetIdle ();
                            }
                            break;
                        case PlayerState.Idle:
                            seek_slider.CanSeek = false;
                            if (!transitioning) {
                                stream_position_label.StreamState = StreamLabelState.Idle;
                                seek_slider.Duration = 0;
                                seek_slider.SeekValue = 0;
                                seek_slider.SetIdle ();
                            }
                            break;
                        default:
                            transitioning = false;
                            break;
                    }
                    break;
            }
        }

        private void OnPlayerEngineTick ()
        {
            if (ServiceManager.PlayerEngine == null) {
                return;
            }

            Banshee.Collection.TrackInfo track = ServiceManager.PlayerEngine.CurrentTrack;
            stream_position_label.IsLive = track == null ? false : track.IsLive;
            seek_slider.Duration = ServiceManager.PlayerEngine.Length;

            if (stream_position_label.StreamState != StreamLabelState.Buffering) {
                stream_position_label.StreamState = StreamLabelState.Playing;
                seek_slider.SeekValue = ServiceManager.PlayerEngine.Position;
            }

            seek_slider.CanSeek = ServiceManager.PlayerEngine.CanSeek;
        }

        private void OnSeekRequested (object o, EventArgs args)
        {
            ServiceManager.PlayerEngine.Position = (uint)seek_slider.Value;
        }
    }
}
