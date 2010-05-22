//
// BaseClientWindow.cs
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
using Gtk;
using Mono.Unix;

using Hyena;

using Banshee.Base;
using Banshee.ServiceStack;
using Banshee.MediaEngine;
using Banshee.Collection;
using Banshee.Configuration;

namespace Banshee.Gui
{
    public abstract class BaseClientWindow : Window
    {
        private PersistentWindowController window_controller;
        private string accel_map_file = Paths.Combine (Paths.ApplicationData, "gtk_accel_map");

        private GtkElementsService elements_service;
        protected GtkElementsService ElementsService {
            get { return elements_service; }
        }

        private InterfaceActionService action_service;
        protected InterfaceActionService ActionService {
            get { return action_service; }
        }

        public event EventHandler TitleChanged;

        protected BaseClientWindow (IntPtr ptr) : base (ptr)
        {
        }

        public BaseClientWindow (string title, string configNameSpace, int defaultWidth, int defaultHeight) : base (title)
        {
            elements_service = ServiceManager.Get<GtkElementsService> ();
            action_service = ServiceManager.Get<InterfaceActionService> ();

            ConfigureWindow ();

            window_controller = new PersistentWindowController (this, configNameSpace, defaultWidth, defaultHeight, WindowPersistOptions.All);
            window_controller.Restore ();

            elements_service.PrimaryWindow = this;

            AddAccelGroup (action_service.UIManager.AccelGroup);

            InitializeWindow ();

            try {
                if (System.IO.File.Exists (accel_map_file)) {
                    Gtk.AccelMap.Load (accel_map_file);
                }
            } catch (Exception e) {
                Hyena.Log.Exception ("Failed to load custom AccelMap", e);
            }
        }

        public override void Dispose ()
        {
            base.Dispose ();

            try {
                Gtk.AccelMap.Save (accel_map_file);
            } catch (Exception e) {
                Hyena.Log.Exception ("Failed to save custom AccelMap", e);
            }
        }

        protected void InitialShowPresent ()
        {
            bool hide = ApplicationContext.CommandLine.Contains ("hide");
            bool present = !hide && !ApplicationContext.CommandLine.Contains ("no-present");
            bool fullscreen = !hide && ApplicationContext.CommandLine.Contains ("fullscreen");

            if (!hide) {
                Show ();
            }

            if (present) {
                Present ();
            }

            if (fullscreen) {
                Fullscreen ();
            }
        }

        public virtual Box ViewContainer { get { return null; } }

        public void ToggleVisibility ()
        {
            SetVisible (!(Visible && IsActive));
        }

        public void SetVisible (bool visible)
        {
            if (!visible) {
                window_controller.Save ();
                Visible = false;
            } else {
                if (!Visible) {
                    window_controller.Restore ();
                }
                Present ();
            }
        }

        private void InitializeWindow ()
        {
            Initialize ();
        }

        protected abstract void Initialize ();

        protected virtual void ConnectEvents ()
        {
            ServiceManager.PlayerEngine.ConnectEvent (OnPlayerEvent,
                PlayerEvent.StartOfStream |
                PlayerEvent.TrackInfoUpdated |
                PlayerEvent.EndOfStream);
        }

        private void OnPlayerEvent (PlayerEventArgs args)
        {
            UpdateTitle ();
        }

        protected virtual void ConfigureWindow ()
        {
        }

        protected override bool OnDeleteEvent (Gdk.Event evnt)
        {
             if (ElementsService.PrimaryWindowClose != null) {
                if (ElementsService.PrimaryWindowClose ()) {
                    return true;
                }
            }

            Banshee.ServiceStack.Application.Shutdown ();
            return true;
        }

        protected override bool OnWindowStateEvent (Gdk.EventWindowState evnt)
        {
            var action_service = ServiceManager.Get<InterfaceActionService> ();
            if (action_service != null) {
                ((ToggleAction)action_service.ViewActions["FullScreenAction"]).Active = (evnt.NewWindowState & Gdk.WindowState.Fullscreen) != 0;
            }

            if ((evnt.NewWindowState & Gdk.WindowState.Withdrawn) == 0) {
                window_controller.Save ();
            }

            return base.OnWindowStateEvent (evnt);
        }

        protected virtual void OnTitleChanged ()
        {
            EventHandler handler = TitleChanged;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }

        protected virtual void UpdateTitle ()
        {
            TrackInfo track = ServiceManager.PlayerEngine.CurrentTrack;
            if (track != null) {
                // Translators: this is the window title when a track is playing
                //              {0} is the track title, {1} is the artist name
                Title = String.Format (Catalog.GetString ("{0} by {1}"),
                    track.DisplayTrackTitle, track.DisplayArtistName);
            } else {
                Title = Catalog.GetString ("Banshee Media Player");
            }

            OnTitleChanged ();
        }

        protected void OnToolbarExposeEvent (object o, ExposeEventArgs args)
        {
            Toolbar toolbar = (Toolbar)o;

            // This forces the toolbar to look like it's just a regular part
            // of the window since the stock toolbar look makes Banshee look ugly.
            Style.ApplyDefaultBackground (toolbar.GdkWindow, true, State,
                args.Event.Area, toolbar.Allocation.X, toolbar.Allocation.Y,
                toolbar.Allocation.Width, toolbar.Allocation.Height);

            // Manually expose all the toolbar's children
            foreach (Widget child in toolbar.Children) {
                toolbar.PropagateExpose (child, args.Event);
            }
        }
    }
}
