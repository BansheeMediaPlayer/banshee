//
// ArtistListActions.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//   Alexander Hixon <hixon.alexander@mediati.org>
//   Frank Ziegler <funtastix@googlemail.com>
//
// Copyright (C) 2007-2008 Novell, Inc.
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
using System.Collections;
using System.Collections.Generic;

using Mono.Addins;
using Mono.Unix;

using Gtk;

using Hyena;
using Hyena.Widgets;

using Banshee.Collection.Gui;
using Banshee.Widgets;
using Banshee.Configuration;
using Banshee.ServiceStack;

namespace Banshee.Gui
{
    public class ArtistListModeChangedEventArgs : EventArgs
    {
        public IArtistListRenderer Renderer { get; set; }
    }

    public class ArtistListActions : BansheeActionGroup, IEnumerable<RadioAction>
    {
        private class ArtistListActionProxy : CustomActionProxy
        {
            private ArtistListActions actions;
            private ComplexMenuItem last_item;

            public ArtistListActions ListActions {
                set {
                    value.ArtistListModeChanged += (o, args) => {
                        if (last_item != null) {
                            actions.AttachSubmenu (last_item);
                        }
                    };
                    actions = value;
                }
            }

            public ArtistListActionProxy (UIManager ui, Gtk.Action action) : base (ui, action)
            {
            }

            protected override void InsertProxy (Gtk.Action menuAction, Widget parent, Widget afterItem)
            {
                int position = 0;
                Widget item = null;
                if (parent is MenuItem || parent is Menu) {
                    Menu parent_menu = ((parent is MenuItem) ? (parent as MenuItem).Submenu : parent) as Menu;
                    position = (afterItem != null) ? Array.IndexOf (parent_menu.Children, afterItem as MenuItem) + 1 : 0;
                    item = GetNewMenuItem ();
                    if (item != null) {
                        var separator1 = new SeparatorMenuItem ();
                        var separator2 = new SeparatorMenuItem ();
                        //fix for separators that potentially already exist below the insert position
                        bool alreadyHasSeparator2 = ((parent_menu.Children [position]) as SeparatorMenuItem != null);
                        parent_menu.Insert (separator2, position);
                        parent_menu.Insert (item, position);
                        parent_menu.Insert (separator1, position);
                        item.Shown += (o, e) => {
                            separator1.Show ();
                            if (!alreadyHasSeparator2) {
                                separator2.Show ();
                            }
                        };
                        item.Hidden += (o, e) => {
                            separator1.Hide ();
                            separator2.Hide ();
                        };
                    }
                }
                var activatable = item as IActivatable;
                if (activatable != null) {
                    activatable.RelatedAction = action;
                }
            }

            protected override ComplexMenuItem GetNewMenuItem ()
            {
                var item = new ComplexMenuItem ();
                var box = new HBox ();
                box.Spacing = 5;

                var label = new Label (action.Label);
                box.PackStart (label, false, false, 0);
                label.Show ();

                box.ShowAll ();
                item.Add (box);

                last_item = item;

                actions.AttachSubmenu (item);

                return item;
            }
        }

        private bool service_manager_startup_finished = false;
        private ArtistListActionProxy artist_list_proxy;
        private RadioAction active_action;
        private List<IArtistListRenderer> renderers = new List<IArtistListRenderer> ();
        private Dictionary<int, String> rendererActions = new Dictionary<int, String> ();
        private Dictionary<TypeExtensionNode, IArtistListRenderer> node_map = new Dictionary<TypeExtensionNode, IArtistListRenderer> ();

        public event EventHandler<ArtistListModeChangedEventArgs> ArtistListModeChanged;

        public Action<IArtistListRenderer> ArtistListModeAdded;
        public Action<IArtistListRenderer> ArtistListModeRemoved;

        public RadioAction Active {
            get { return active_action; }
            set {
                active_action = value;
                SetRenderer (renderers [active_action.Value]);
            }
        }

        public ArtistListActions () : base ("ArtistList")
        {
            ServiceManager.StartupFinished += delegate {
                service_manager_startup_finished = true;
            };

            Add (new [] {
                new ActionEntry ("ArtistListMenuAction", null, Catalog.GetString ("Artist List View"),
                                 null, null, null)
            });

            this ["ArtistListMenuAction"].Visible = false;

            AddinManager.AddExtensionNodeHandler ("/Banshee/Gui/ArtistListView", OnExtensionChanged);

            Actions.UIManager.ActionsChanged += HandleActionsChanged;
        }

        private void HandleActionsChanged (object sender, EventArgs e)
        {
            if (Actions.UIManager.GetAction ("/MainMenu/ViewMenu") != null) {
                artist_list_proxy = new ArtistListActionProxy (Actions.UIManager, this ["ArtistListMenuAction"]);
                artist_list_proxy.ListActions = this;
                artist_list_proxy.AddPath ("/MainMenu/ViewMenu", "FullScreen");
                artist_list_proxy.AddPath ("/TrackContextMenu", "AddToPlaylist");
                Actions.UIManager.ActionsChanged -= HandleActionsChanged;
            }
        }

        private void OnExtensionChanged (object o, ExtensionNodeEventArgs args)
        {
            var tnode = (TypeExtensionNode)args.ExtensionNode;
            IArtistListRenderer changed_renderer = null;

            if (args.Change == ExtensionChange.Add) {
                lock (renderers) {
                    try {
                        changed_renderer = (IArtistListRenderer)tnode.CreateInstance ();
                        renderers.Add (changed_renderer);
                        node_map [tnode] = changed_renderer;
                    } catch (Exception e) {
                        Log.Error (String.Format ("Failed to load ArtistListRenderer extension: {0}", args.Path), e);
                    }
                }

                if (changed_renderer != null) {
                    var handler = ArtistListModeAdded;
                    if (handler != null) {
                        handler (changed_renderer);
                    }
                    if (service_manager_startup_finished) {
                        SetRenderer (changed_renderer);
                    }
                }
            } else {
                lock (renderers) {
                    if (node_map.ContainsKey (tnode)) {
                        changed_renderer = node_map [tnode];
                        node_map.Remove (tnode);
                        renderers.Remove (changed_renderer);
                        if (this.renderer == changed_renderer) {
                            SetRenderer (renderers [0]);
                        }
                    }
                }

                if (changed_renderer != null) {
                    var handler = ArtistListModeRemoved;
                    if (handler != null) {
                        handler (changed_renderer);
                    }
                }
            }
            UpdateActions ();
        }

        private void UpdateActions ()
        {
            // Clear out the old options
            foreach (string id in rendererActions.Values) {
                Remove (id);
            }
            rendererActions.Clear ();

            var radio_group = new RadioActionEntry [renderers.Count];
            int i = 0;

            // Add all the renderer options
            foreach (var rendererIterator in renderers) {
                string action_name = rendererIterator.GetType ().FullName;
                int id = rendererActions.Count;
                rendererActions [id] = action_name;
                radio_group [i++] = new RadioActionEntry (
                    action_name, null,
                    rendererIterator.Name, null,
                    rendererIterator.Name,
                    id
                );
            }

            Add (radio_group, 0, OnActionChanged);

            var radio_action = this [ArtistListMode.Get ()] as RadioAction;
            if (renderers.Count > 0 && radio_action != null) {
                this.renderer = renderers [radio_action.Value];

                if (this.renderer == null) {
                    SetRenderer (renderers [0]);
                }

                var action = this [this.renderer.GetType ().FullName];
                if (action is RadioAction) {
                    Active = (RadioAction)action;
                }

                Active.Activate ();
            }
        }

        private IArtistListRenderer renderer;

        private void SetRenderer (IArtistListRenderer renderer)
        {
            this.renderer = renderer;
            ArtistListMode.Set (renderer.GetType ().FullName);

            ThreadAssist.ProxyToMain (() => {
                var handler = ArtistListModeChanged;
                if (handler != null) {
                    handler (this, new ArtistListModeChangedEventArgs { Renderer = renderer });
                }
            });
        }

        public IArtistListRenderer ArtistListRenderer {
            get { return this.renderer; }
        }

        private void OnActionChanged (object o, ChangedArgs args)
        {
            Active = args.Current;
        }

        private void AttachSubmenu (ComplexMenuItem item)
        {
            MenuItem parent = item;
            parent.Submenu = CreateMenu ();
        }

        private Menu CreateMenu ()
        {
            var menu = new Gtk.Menu ();
            bool separator = false;
            foreach (RadioAction action in this) {
                menu.Append (action.CreateMenuItem ());
                if (!separator) {
                    separator = true;
                    menu.Append (new SeparatorMenuItem ());
                }
            }

            menu.ShowAll ();
            return menu;
        }

        public IEnumerator<RadioAction> GetEnumerator ()
        {
            foreach (string id in rendererActions.Values) {
                yield return (RadioAction)this [id];
            }
        }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return GetEnumerator ();
        }

        public static readonly SchemaEntry<string> ArtistListMode = new SchemaEntry<string> (
            "player_window", "artist_list_view_type",
            typeof (ColumnCellArtistText).FullName,
            "Artist List View Type",
            "The view type chosen for the artist list"
        );
    }
}
