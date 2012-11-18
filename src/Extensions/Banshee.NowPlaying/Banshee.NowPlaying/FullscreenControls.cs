//
// FullscreenControls.cs
//
// Authors:
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
using Gtk;

using Banshee.ServiceStack;
using Banshee.Gui;
using Banshee.Gui.Widgets;

namespace Banshee.NowPlaying
{
    public class FullscreenControls : OverlayWindow
    {
        private InterfaceActionService action_service;
        private ConnectedVolumeButton volume_button;
        private ConnectedSeekSlider slider;

        public FullscreenControls (Window toplevel, InterfaceActionService actionService) : base (toplevel, 1)
        {
            action_service = actionService;
            AddAccelGroup (action_service.UIManager.AccelGroup);
            BuildInterface ();
        }

        private void BuildInterface ()
        {
            HBox box = new HBox ();

            volume_button = new ConnectedVolumeButton ();

            box.PackStart (action_service.PlaybackActions["PreviousAction"].CreateToolItem (), false, false, 0);
            box.PackStart (action_service.PlaybackActions["PlayPauseAction"].CreateToolItem (), false, false, 0);
            box.PackStart (new NextButton (action_service), false, false, 0);
            box.PackStart (new RepeatActionButton (true), false, false, 0);
            box.PackStart (slider = new ConnectedSeekSlider (SeekSliderLayout.Horizontal), true, true, 0);
            box.PackStart (volume_button, false, false, 0);

            Button exit = new Button (Stock.LeaveFullscreen);
            exit.Relief = ReliefStyle.None;
            exit.Clicked += delegate { TransientFor.Hide (); };
            box.PackStart (exit, false, false, 0);

            Add (box);
            box.ShowAll ();
        }

        public override void Destroy ()
        {
            slider.Disconnect ();
            volume_button.Disconnect ();
            base.Destroy ();
        }

        public bool Active {
            get { return volume_button.Active || IsActive; }
        }
    }
}
