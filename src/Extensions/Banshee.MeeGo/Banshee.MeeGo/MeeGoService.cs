//
// MeeGoService.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright 2009-2010 Novell, Inc.
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
using MeeGo.Panel;

using Hyena;

using Banshee.Base;
using Banshee.Collection;
using Banshee.Sources;
using Banshee.ServiceStack;
using Banshee.MediaEngine;
using Banshee.Gui;

namespace Banshee.MeeGo
{
    public class MeeGoService : IExtensionService
    {
        private GtkElementsService elements_service;
        private InterfaceActionService interface_action_service;
        private SourceManager source_manager;
        private PlayerEngineService player;
        private Source now_playing;

        private MeeGoPanel panel;

        void IExtensionService.Initialize ()
        {
            // We need to create the MeeGo panel connection as soon as possible
            // to keep mutter-moblin's toolbar from thinking we crashed (timing out).
            // The contents of the panel will be constructed later on.
            if (MeeGoPanel.Instance.Enabled) {
                panel = MeeGoPanel.Instance;
            }

            elements_service = ServiceManager.Get<GtkElementsService> ();
            interface_action_service = ServiceManager.Get<InterfaceActionService> ();
            source_manager = ServiceManager.SourceManager;
            player = ServiceManager.PlayerEngine;

            if (!ServiceStartup ()) {
                ServiceManager.ServiceStarted += OnServiceStarted;
            }
        }

        private void OnServiceStarted (ServiceStartedArgs args)
        {
            if (args.Service is Banshee.Gui.InterfaceActionService) {
                interface_action_service = (InterfaceActionService)args.Service;
            } else if (args.Service is GtkElementsService) {
                elements_service = (GtkElementsService)args.Service;
            } else if (args.Service is SourceManager) {
                source_manager = ServiceManager.SourceManager;
            } else if (args.Service is PlayerEngineService) {
                player = ServiceManager.PlayerEngine;
            }

            ServiceStartup ();
        }

        private bool ServiceStartup ()
        {
            if (elements_service == null || interface_action_service == null ||
                source_manager == null || player == null) {
                return false;
            }

            Initialize ();

            ServiceManager.ServiceStarted -= OnServiceStarted;

            return true;
        }

        private void Initialize ()
        {
            // regular metacity does not seem to like this at all, crashing
            // and complaining "Window manager warning: Buggy client sent a
            // _NET_ACTIVE_WINDOW message with a timestamp of 0 for 0x2e00020"
            if (panel != null) {
                elements_service.PrimaryWindow.Decorated = false;
                elements_service.PrimaryWindow.Maximize ();
            }

            if (panel == null) {
                Log.Warning ("MeeGo extension initialized without a panel");
                return;
            }

            panel.BuildContents ();

            elements_service.PrimaryWindowClose = () => {
                elements_service.PrimaryWindow.Hide ();
                return true;
            };

            // Since the Panel is running, we don't actually ever want to quit!
            Banshee.ServiceStack.Application.ShutdownRequested += () => {
                elements_service.PrimaryWindow.Hide ();
                return false;
            };

            FindNowPlaying ();
            ServiceManager.PlayerEngine.ConnectEvent (OnPlayerStateChanged,
                PlayerEvent.StateChange | PlayerEvent.StartOfStream);
        }

        private void OnPlayerStateChanged (PlayerEventArgs args)
        {
            var player = ServiceManager.PlayerEngine;
            if (player.CurrentState == PlayerState.Playing &&
                player.CurrentTrack.HasAttribute (TrackMediaAttributes.VideoStream)) {
                if (now_playing != null) {
                    ServiceManager.SourceManager.SetActiveSource (now_playing);
                }

                PresentPrimaryInterface ();
            }
        }

        private void FindNowPlaying ()
        {
            foreach (var src in ServiceManager.SourceManager.Sources) {
                if (src.UniqueId.Contains ("now-playing")) {
                    now_playing = src;
                    break;
                }
            }

            if (now_playing != null) {
                return;
            }

            Banshee.ServiceStack.ServiceManager.SourceManager.SourceAdded += (args) => {
                if (now_playing == null && args.Source.UniqueId.Contains ("now-playing")) {
                    now_playing = args.Source;
                }
            };
        }

        public void PresentPrimaryInterface ()
        {
            elements_service.PrimaryWindow.Maximize ();
            elements_service.PrimaryWindow.Present ();
            if (panel != null) {
                panel.Hide ();
            }
        }

        public void Dispose ()
        {
            if (panel != null) {
                panel.Dispose ();
            }

            interface_action_service = null;
            elements_service = null;
        }

        string IService.ServiceName {
            get { return "MeeGoService"; }
        }
    }
}
