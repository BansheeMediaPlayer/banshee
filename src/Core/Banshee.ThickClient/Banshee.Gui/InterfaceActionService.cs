//
// InterfaceActionService.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2006-2007 Novell, Inc.
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

        private BansheeActionGroup active_source_actions;
        private uint active_source_uiid = 0;

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
            UIManager.AddUiFromResource ("core-ui-actions-layout.xml");

            AddinManager.AddExtensionNodeHandler ("/Banshee/ThickClient/ActionGroup", OnExtensionChanged);

            ServiceManager.DBusServiceManager.RegisterObject (global_actions);
        }

        private void OnActiveSourceChanged (SourceEventArgs args)
        {
            // FIXME: Can't use an anonymous delegate here because of compiler
            // bug in Mono 1.2.6
            ThreadAssist.ProxyToMain (OnActiveSourceChangedGui);
        }

        private void OnActiveSourceChangedGui ()
        {
            if (active_source_uiid > 0) {
                UIManager.RemoveUi (active_source_uiid);
                active_source_uiid = 0;
            }

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

            Assembly assembly =
                active_source.GetProperty<Assembly> ("ActiveSourceUIResource.Assembly", propagate) ??
                Assembly.GetAssembly (active_source.GetType ());

            active_source_uiid = AddUiFromFile (active_source.GetProperty<string> ("ActiveSourceUIResource", propagate), assembly);
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
                        Log.DebugFormat ("Extension actions loaded: {0}", node.Id);
                    }
                } else if (args.Change == ExtensionChange.Remove) {
                    if (extension_actions.ContainsKey (node.Id)) {
                        extension_actions[node.Id].Dispose ();
                        extension_actions.Remove (node.Id);
                        Log.DebugFormat ("Extension actions unloaded: {0}", node.Id);
                    }
                }
            } catch (Exception e) {
                Log.Exception (e);
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

        string IService.ServiceName {
            get { return "InterfaceActionService"; }
        }
    }
}
