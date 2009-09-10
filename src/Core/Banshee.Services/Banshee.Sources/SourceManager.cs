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

using Mono.Addins;

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

    public class ActiveSourceChangedEventArgs : SourceEventArgs
    {
        public Source OldSource;
    }

    public class SourceAddedArgs : SourceEventArgs
    {
        public int Position;
    }
    
    public class SourceManager : /*ISourceManager,*/ IInitializeService, IRequiredService, IDBusExportable, IDisposable
    {
        private List<Source> sources = new List<Source>();
        private Dictionary<string, Source> extension_sources = new Dictionary<string, Source> ();
        
        private Source active_source;
        private Source default_source;
        private MusicLibrarySource music_library;
        private VideoLibrarySource video_library;
        
        public event SourceEventHandler SourceUpdated;
        public event SourceAddedHandler SourceAdded;
        public event SourceEventHandler SourceRemoved;
        public event SourceEventHandler ActiveSourceChanged;

        public void Initialize ()
        {
            // TODO should add library sources here, but requires changing quite a few
            // things that depend on being loaded before the music library is added.
            //AddSource (music_library = new MusicLibrarySource (), true);
            //AddSource (video_library = new VideoLibrarySource (), false);
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

                active_source = null;
                default_source = null;
                music_library = null;
                video_library = null;

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
            }
        }
        
        private void OnExtensionChanged (object o, ExtensionNodeEventArgs args) 
        {
            lock (this) {
                TypeExtensionNode node = (TypeExtensionNode)args.ExtensionNode;
                
                if (args.Change == ExtensionChange.Add && !extension_sources.ContainsKey (node.Id)) {
                    Source source = (Source)node.CreateInstance ();
                    extension_sources.Add (node.Id, source);
                    bool add_source = true;
                    if (source.Properties.Contains ("AutoAddSource")) {
                        add_source = source.Properties.GetBoolean ("AutoAddSource");
                    }
                    if (add_source) {
                        AddSource (source);
                    }
                } else if (args.Change == ExtensionChange.Remove && extension_sources.ContainsKey (node.Id)) {
                    Source source = extension_sources[node.Id];
                    extension_sources.Remove (node.Id);
                    RemoveSource (source, true);
                }
            }
        }

        public void AddSource(Source source)
        {
            AddSource(source, false);
        }
        
        public void AddSource(Source source, bool isDefault)
        {
            Banshee.Base.ThreadAssist.AssertInMainThread ();
            if(source == null || ContainsSource (source)) {
                return;
            }
            
            int position = FindSourceInsertPosition(source);
            sources.Insert(position, source);
            
            if(isDefault) {
                default_source = source;
            }

            source.Updated += OnSourceUpdated;
            source.ChildSourceAdded += OnChildSourceAdded;
            source.ChildSourceRemoved += OnChildSourceRemoved;
            
            SourceAddedHandler handler = SourceAdded;
            if(handler != null) {
                SourceAddedArgs args = new SourceAddedArgs();
                args.Position = position;
                args.Source = source;
                handler(args);
            }

            if (source is MusicLibrarySource) {
                music_library = source as MusicLibrarySource;
            } else if (source is VideoLibrarySource) {
                video_library = source as VideoLibrarySource;
            }
            
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

            Banshee.Base.ThreadAssist.ProxyToMain (delegate {
                if(source == active_source) {
                    SetActiveSource(default_source);
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
            Banshee.Base.ThreadAssist.ProxyToMain (delegate {
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
            Banshee.Base.ThreadAssist.AssertInMainThread ();
            if(source == null || !source.CanActivate || active_source == source) {
                return;
            }
            
            if(active_source != null) {
                active_source.Deactivate();
            }
            
            Source old_source = active_source;
            active_source = source;
            
            if(!notify) {
                source.Activate();
                return;
            }
            
            SourceEventHandler handler = ActiveSourceChanged;
            if(handler != null) {
                SourceEventArgs args = new ActiveSourceChangedEventArgs () {
                    Source = active_source,
                    OldSource = old_source                    
                };
                
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
