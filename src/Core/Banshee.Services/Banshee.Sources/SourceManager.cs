//
// SourceManager.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2005-2007 Novell, Inc.
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
using System.Collections.Generic;

using Mono.Unix;
using Mono.Addins;

using Hyena;

using Banshee.ServiceStack;
using Banshee.Library;

namespace Banshee.Sources
{
    public delegate void SourceEventHandler(SourceEventArgs args);
    public delegate void SourceAddedHandler(SourceAddedArgs args);

    public class SourceEventArgs : EventArgs
    {
        public Source Source;
    }

    public class SourceAddedArgs : SourceEventArgs
    {
        public int Position;
    }

    public class SourceManager : /*ISourceManager,*/ IInitializeService, IRequiredService, IDBusExportable, IDisposable
    {
        private List<Source> sources = new List<Source>();
        private List<GroupSource> group_sources = new List<GroupSource> ();
        private Dictionary<string, Source> extension_sources = new Dictionary<string, Source> ();

        private Source active_source;
        private Source default_source;
        private MusicLibrarySource music_library;
        private VideoLibrarySource video_library;

        public event SourceEventHandler SourceUpdated;
        public event SourceAddedHandler SourceAdded;
        public event SourceEventHandler SourceRemoved;
        public event SourceEventHandler ActiveSourceChanged;

        public class GroupSource : Source
        {
            public GroupSource (string name, int order) : base (name, name, order)
            {
            }
        }

        public void Initialize ()
        {
            // TODO should add library sources here, but requires changing quite a few
            // things that depend on being loaded before the music library is added.
            //AddSource (music_library = new MusicLibrarySource (), true);
            //AddSource (video_library = new VideoLibrarySource (), false);

            group_sources.Add (new GroupSource (Catalog.GetString ("Online Media"), 60));
            group_sources.Add (new GroupSource (Catalog.GetString ("Libraries"), 39));
        }

        internal void LoadExtensionSources ()
        {
            lock (this) {
                AddinManager.AddExtensionNodeHandler ("/Banshee/SourceManager/Source", OnExtensionChanged);
            }
        }

        public void Dispose ()
        {
            lock (this) {
                try {
                    AddinManager.RemoveExtensionNodeHandler ("/Banshee/SourceManager/Source", OnExtensionChanged);
                } catch {}

                // Do dispose extension sources
                foreach (Source source in extension_sources.Values) {
                    RemoveSource (source, true);
                }

                // But do not dispose non-extension sources
                while (sources.Count > 0) {
                    RemoveSource (sources[0], false);
                }

                sources.Clear ();
                extension_sources.Clear ();

                active_source = null;
                default_source = null;
                music_library = null;
                video_library = null;
            }
        }

        private void OnExtensionChanged (object o, ExtensionNodeEventArgs args)
        {
            lock (this) {
                TypeExtensionNode node = (TypeExtensionNode)args.ExtensionNode;

                if (args.Change == ExtensionChange.Add && !extension_sources.ContainsKey (node.Id)) {
                    try {
                        Source source = (Source)node.CreateInstance ();
                        extension_sources.Add (node.Id, source);
                        if (source.Properties.Get<bool> ("AutoAddSource", true)) {
                            AddSource (source);
                        }
                        Log.DebugFormat ("Extension source loaded: {0}", source.Name);
                    } catch {}
                } else if (args.Change == ExtensionChange.Remove && extension_sources.ContainsKey (node.Id)) {
                    Source source = extension_sources[node.Id];
                    extension_sources.Remove (node.Id);
                    RemoveSource (source, true);
                    Log.DebugFormat ("Extension source unloaded: {0}", source.Name);
                }
            }
        }

        public void AddSource(Source source)
        {
            AddSource(source, false);
        }

        public void AddSource(Source source, bool isDefault)
        {
            ThreadAssist.AssertInMainThread ();
            if(source == null || ContainsSource (source)) {
                return;
            }

            GroupSource group_source = source as GroupSource;
            if (group_source != null && !group_sources.Contains (group_source)) {
                group_sources.Add (group_source);
                return;
            }

            AddSource (FindAssociatedGroupSource (source.Order));

            int position = FindSourceInsertPosition(source);
            sources.Insert(position, source);

            if(isDefault) {
                default_source = source;
            }

            source.Updated += OnSourceUpdated;
            source.ChildSourceAdded += OnChildSourceAdded;
            source.ChildSourceRemoved += OnChildSourceRemoved;

            if (source is MusicLibrarySource) {
                music_library = source as MusicLibrarySource;
            } else if (source is VideoLibrarySource) {
                video_library = source as VideoLibrarySource;
            }

            SourceAdded.SafeInvoke (new SourceAddedArgs () {
                Position = position,
                Source = source
            });

            IDBusExportable exportable = source as IDBusExportable;
            if (exportable != null) {
                ServiceManager.DBusServiceManager.RegisterObject (exportable);
            }

            List<Source> children = new List<Source> (source.Children);
            foreach(Source child_source in children) {
                AddSource (child_source, false);
            }

            if(isDefault && ActiveSource == null) {
                SetActiveSource(source);
            }
        }

        public void RemoveSource (Source source)
        {
            RemoveSource (source, false);
        }

        public void RemoveSource (Source source, bool recursivelyDispose)
        {
            if(source == null || !ContainsSource (source)) {
                return;
            }

            if(source == default_source) {
                default_source = null;
            }

            source.Updated -= OnSourceUpdated;
            source.ChildSourceAdded -= OnChildSourceAdded;
            source.ChildSourceRemoved -= OnChildSourceRemoved;

            sources.Remove(source);

            GroupSource associated_groupsource = FindAssociatedGroupSource (source.Order);
            if (!GroupSourceHasMembers (associated_groupsource)) {
                RemoveSource (associated_groupsource, recursivelyDispose);
            }

            foreach(Source child_source in source.Children) {
                RemoveSource (child_source, recursivelyDispose);
            }

            IDBusExportable exportable = source as IDBusExportable;
            if (exportable != null) {
                ServiceManager.DBusServiceManager.UnregisterObject (exportable);
            }

            if (recursivelyDispose) {
                IDisposable disposable = source as IDisposable;
                if (disposable != null) {
                    disposable.Dispose ();
                }
            }

            ThreadAssist.ProxyToMain (delegate {
                if(source == active_source) {
                    if (source.Parent != null && source.Parent.CanActivate) {
                        SetActiveSource(source.Parent);
                    } else {
                        SetActiveSource(default_source);
                    }
                }

                SourceEventHandler handler = SourceRemoved;
                if(handler != null) {
                    SourceEventArgs args = new SourceEventArgs();
                    args.Source = source;
                    handler(args);
                }
            });
        }

        public void RemoveSource(Type type)
        {
            Queue<Source> remove_queue = new Queue<Source>();

            foreach(Source source in Sources) {
                if(source.GetType() == type) {
                    remove_queue.Enqueue(source);
                }
            }

            while(remove_queue.Count > 0) {
                RemoveSource(remove_queue.Dequeue());
            }
        }

        public bool ContainsSource(Source source)
        {
            return sources.Contains(source);
        }

        private void OnSourceUpdated(object o, EventArgs args)
        {
            ThreadAssist.ProxyToMain (delegate {
                SourceEventHandler handler = SourceUpdated;
                if(handler != null) {
                    SourceEventArgs evargs = new SourceEventArgs();
                    evargs.Source = o as Source;
                    handler(evargs);
                }
            });
        }

        private void OnChildSourceAdded(SourceEventArgs args)
        {
            AddSource (args.Source);
        }

        private void OnChildSourceRemoved(SourceEventArgs args)
        {
            RemoveSource (args.Source);
        }


        private GroupSource FindAssociatedGroupSource (int order)
        {
            int current_order = -1;
            GroupSource associated_groupsource = null;
            foreach (GroupSource source in group_sources){
                if (order == source.Order) {
                    return null;
                }

                if (order > source.Order && current_order < source.Order) {
                    associated_groupsource = source;
                    current_order = source.Order;
                }
            }
            return associated_groupsource;
        }

        private bool GroupSourceHasMembers (GroupSource group_source) {
            Source source = group_source as Source;
            if (group_source == null || !sources.Contains (source)) {
                return false;
            }

            int source_index = FindSourceInsertPosition (source);

            if (source_index < sources.Count - 1) {
                Source next_source = sources[source_index + 1];
                GroupSource associated_groupsource = FindAssociatedGroupSource (next_source.Order);
                return group_source.Equals (associated_groupsource);
            } else {
                return false;
            }
        }

        private int FindSourceInsertPosition(Source source)
        {
            for(int i = sources.Count - 1; i >= 0; i--) {
                if((sources[i] as Source).Order == source.Order) {
                    return i;
                }
            }

            for(int i = 0; i < sources.Count; i++) {
                if((sources[i] as Source).Order >= source.Order) {
                    return i;
                }
            }

            return sources.Count;
        }

        public Source DefaultSource {
            get { return default_source; }
            set { default_source = value; }
        }

        public MusicLibrarySource MusicLibrary {
            get { return music_library; }
        }

        public VideoLibrarySource VideoLibrary {
            get { return video_library; }
        }

        public Source ActiveSource {
            get { return active_source; }
        }

        /*ISource ISourceManager.DefaultSource {
            get { return DefaultSource; }
        }

        ISource ISourceManager.ActiveSource {
            get { return ActiveSource; }
            set { value.Activate (); }
        }*/

        public void SetActiveSource(Source source)
        {
            SetActiveSource(source, true);
        }

        public void SetActiveSource(Source source, bool notify)
        {
            ThreadAssist.AssertInMainThread ();
            if(source == null || !source.CanActivate || active_source == source) {
                return;
            }

            if(active_source != null) {
                active_source.Deactivate();
            }

            active_source = source;
            if (source.Parent != null) {
                source.Parent.Expanded = true;
            }

            if(!notify) {
                source.Activate();
                return;
            }

            SourceEventHandler handler = ActiveSourceChanged;
            if(handler != null) {
                SourceEventArgs args = new SourceEventArgs();
                args.Source = active_source;
                handler(args);
            }

            source.Activate();
        }

        public IEnumerable<T> FindSources<T> () where T : Source
        {
            foreach (Source source in Sources) {
                T t_source = source as T;
                if (t_source != null) {
                    yield return t_source;
                }
            }
        }

        public ICollection<Source> Sources {
            get { return sources; }
        }

        /*string [] ISourceManager.Sources {
            get { return DBusServiceManager.MakeObjectPathArray<Source>(sources); }
        }*/

        IDBusExportable IDBusExportable.Parent {
            get { return null; }
        }

        string Banshee.ServiceStack.IService.ServiceName {
            get { return "SourceManager"; }
        }
    }
}
