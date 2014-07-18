//
// InterfaceActionService.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//   Frank Ziegler <funtastix@googlemail.com>
//
// Copyright (C) 2006-2007 Novell, Inc.
// Copyright (C) 2013 Frank Ziegler
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
using System.IO;
using System.Reflection;
using System.Collections.Generic;

using Mono.Addins;

using Gtk;
using Action = Gtk.Action;

using Hyena;

using Banshee.Sources;
using Banshee.ServiceStack;

namespace Banshee.Gui
{
    public class InterfaceActionService : Hyena.Gui.ActionManager, IInitializeService
    {
        private Dictionary<string, ActionGroup> extension_actions = new Dictionary<string, ActionGroup> ();

        private GlobalActions   global_actions;
        private ViewActions     view_actions;
        private PlaybackActions playback_actions;
        private TrackActions    track_actions;
        private SourceActions   source_actions;

        private ArtistListActions artist_list_actions;
        private BansheeActionGroup active_source_actions;
        private List<uint> active_source_uis = new List<uint> ();

        public InterfaceActionService ()
        {
            ServiceManager.SourceManager.ActiveSourceChanged += OnActiveSourceChanged;
        }

        public override void Initialize ()
        {
            AddActionGroup (global_actions      = new GlobalActions ());
            AddActionGroup (view_actions        = new ViewActions ());
            AddActionGroup (playback_actions    = new PlaybackActions ());
            AddActionGroup (track_actions       = new TrackActions ());
            AddActionGroup (source_actions      = new SourceActions ());
            AddActionGroup (artist_list_actions = new ArtistListActions ());
            UIManager.AddUiFromResource ("core-ui-actions-layout.xml");

            AddinManager.AddExtensionNodeHandler ("/Banshee/ThickClient/ActionGroup", OnExtensionChanged);

            ServiceManager.DBusServiceManager.RegisterObject (global_actions);
        }

        private void OnActiveSourceChanged (SourceEventArgs args)
        {
            ThreadAssist.ProxyToMain (OnActiveSourceChangedGui);
        }

        private void OnActiveSourceChangedGui ()
        {
            foreach (uint ui_id in active_source_uis) {
                if (ui_id != 0) {
                    UIManager.RemoveUi (ui_id);
                }
            }
            active_source_uis.Clear ();

            if (active_source_actions != null) {
                RemoveActionGroup (active_source_actions.Name);
                active_source_actions = null;
            }

            Source active_source = ServiceManager.SourceManager.ActiveSource;
            if (active_source == null) {
                return;
            }

            bool propagate = active_source.GetInheritedProperty<bool> ("ActiveSourceUIResourcePropagate");

            active_source_actions = active_source.GetProperty<BansheeActionGroup> ("ActiveSourceActions", propagate);
            if (active_source_actions != null) {
                AddActionGroup (active_source_actions);
            }

            Assembly assembly = active_source.GetProperty<Assembly> ("ActiveSourceUIResource.Assembly", propagate) ??
                Assembly.GetAssembly (active_source.GetType ());
            active_source_uis.Add (AddUiFromFile (active_source.GetProperty<string> ("ActiveSourceUIResource", propagate), assembly));

            var ui_str = active_source.GetProperty<string> ("ActiveSourceUIString", propagate);
            if (ui_str != null) {
                active_source_uis.Add (UIManager.AddUiFromString (ui_str));
            }

            UIManager.EnsureUpdate ();
        }

        private void OnExtensionChanged (object o, ExtensionNodeEventArgs args)
        {
            try {
                TypeExtensionNode node = (TypeExtensionNode)args.ExtensionNode;

                if (args.Change == ExtensionChange.Add) {
                    if (!extension_actions.ContainsKey (node.Id)) {
                        ActionGroup group = (ActionGroup)node.CreateInstance (typeof (ActionGroup));
                        extension_actions[node.Id] = group;
                        AddActionGroup (group);
                        Log.DebugFormat ("Extension actions loaded: {0}", group.Name);
                    }
                } else if (args.Change == ExtensionChange.Remove) {
                    if (extension_actions.ContainsKey (node.Id)) {
                        string name = extension_actions[node.Id].Name;
                        extension_actions[node.Id].Dispose ();
                        extension_actions.Remove (node.Id);
                        Log.DebugFormat ("Extension actions unloaded: {0}", name);
                    }
                }
            } catch (Exception e) {
                Log.Error (e);
            }
        }

        public GlobalActions GlobalActions {
            get { return global_actions; }
        }

        public PlaybackActions PlaybackActions {
            get { return playback_actions; }
        }

        public TrackActions TrackActions {
            get { return track_actions; }
        }

        public SourceActions SourceActions {
            get { return source_actions; }
        }

        public ViewActions ViewActions {
            get { return view_actions; }
        }

        public ArtistListActions ArtistListActions {
            get { return artist_list_actions; }
        }

        string IService.ServiceName {
            get { return "InterfaceActionService"; }
        }
    }
}
