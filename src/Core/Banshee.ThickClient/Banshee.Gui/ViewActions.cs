//
// ViewActions.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//   Alexander Hixon <hixon.alexander@mediati.org>
//
// Copyright (C) 2007-2008 Novell, Inc.
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

using Banshee.MediaEngine;
using Banshee.ServiceStack;
using Banshee.Equalizer.Gui;

namespace Banshee.Gui
{
    public class ViewActions : BansheeActionGroup
    {
        public delegate void FullscreenHandler (bool fullscreen);
        private FullscreenHandler fullscreen_handler;
        
        public FullscreenHandler Fullscreen {
            get { return fullscreen_handler; }
            set { 
                fullscreen_handler = value; 
                
                GtkElementsService service = ServiceManager.Get<GtkElementsService> ();
                Gtk.ToggleAction action = this["FullScreenAction"] as Gtk.ToggleAction;
                if (service != null && action != null && value == null) {
                    action.Active = (service.PrimaryWindow.GdkWindow.State & Gdk.WindowState.Fullscreen) != 0;
                }
            }
        }
    
        public ViewActions () : base ("View")
        {
            Add (new ActionEntry [] {
                new ActionEntry ("ViewMenuAction", null,
                    Catalog.GetString ("_View"), null, null, null),
                    
                new ActionEntry ("ShowEqualizerAction", null,
                   Catalog.GetString ("_Equalizer"), "<control>E",
                   Catalog.GetString ("View the graphical equalizer"), OnShowEqualizer)
            });

            Add (new ToggleActionEntry [] {
                new ToggleActionEntry (
                   "ShowContextPaneAction", null,
                   Catalog.GetString ("_Context Pane"), "",
                   Catalog.GetString ("Show the context pane beneath the track list"), null,
                   Banshee.ContextPane.ContextPane.ShowSchema.Get ()
                )
            });

            AddImportant (new ToggleActionEntry [] {
                new ToggleActionEntry ("FullScreenAction", "gtk-fullscreen",
                    Catalog.GetString ("_Fullscreen"), "F",
                    Catalog.GetString ("Enter or leave fullscreen mode"), OnFullScreen, false),
            });

            /*Add (new ToggleActionEntry [] {
                new ToggleActionEntry ("ShowCoverArtAction", null,
                    Catalog.GetString ("Show Cover _Art"), null,
                    Catalog.GetString ("Toggle display of album cover art"), null, false),
            });*/
            
            ServiceManager.PlayerEngine.ConnectEvent (OnPlayerEvent, PlayerEvent.StateChange);
            OnFullScreen (null, EventArgs.Empty);
        }
        
        private void OnPlayerEvent (PlayerEventArgs args)
        {
            if (((PlayerEventStateChangeArgs)args).Current == PlayerState.Ready) {
                Actions["View.ShowEqualizerAction"].Sensitive = ServiceManager.PlayerEngine.SupportsEqualizer;
            }
        }
                
        private void OnShowEqualizer (object o, EventArgs args)
        {
            if (EqualizerWindow.Instance == null) {
                EqualizerWindow eqwin = new EqualizerWindow (ServiceManager.Get<GtkElementsService> ().PrimaryWindow);
                eqwin.Show ();
            } else {
                EqualizerWindow.Instance.Present ();
            }
        }

        private void OnFullScreen (object o, EventArgs args)
        {
            Gtk.ToggleAction action = this["FullScreenAction"] as Gtk.ToggleAction;
            if (action == null) {
                return;
            }
            
            if (Fullscreen != null) {
                Fullscreen (action.Active);
                return;
            }
            
            GtkElementsService service = ServiceManager.Get<GtkElementsService> ();
            if (service == null || action == null) {
                return;
            }
            
            Gtk.Window window = service.PrimaryWindow;
            
            if (window == null) {
                return;
            } else if (action.Active) {
                window.Fullscreen ();
            } else {
                window.Unfullscreen ();
            }
        }
    }
}
