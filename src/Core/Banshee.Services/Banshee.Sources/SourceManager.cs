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
using System.Collections;
using System.Collections.Generic;

using Banshee.ServiceStack;

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
    
    public class SourceManager : ISourceManager
    {
        private List<Source> sources = new List<Source>();
        private Source active_source;
        private Source default_source;
        
        public event SourceEventHandler SourceUpdated;
        public event SourceEventHandler SourceViewChanged;
        public event SourceAddedHandler SourceAdded;
        public event SourceEventHandler SourceRemoved;
        public event SourceEventHandler ActiveSourceChanged;
        
        public void AddSource(Source source, bool isDefault)
        {
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
            
            ServiceManager.DBusServiceManager.RegisterObject(source);
            
            foreach(Source child_source in source.Children) {
                AddSource(child_source, false);
            }
        }
        
        public void AddSource(Source source)
        {
            AddSource(source, false);
        }
        
        public void RemoveSource(Source source)
        {
            if(source == default_source) {
                default_source = null;
            }
            
            sources.Remove(source);

            source.Updated -= OnSourceUpdated;

            if(source == active_source) {
                SetActiveSource(default_source);
            }
                
            SourceEventHandler handler = SourceRemoved;
            if(handler != null) {
                SourceEventArgs args = new SourceEventArgs();
                args.Source = source;
                handler(args);
            }
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
            SourceEventHandler handler = SourceUpdated;
            if(handler != null) {
                SourceEventArgs evargs = new SourceEventArgs();
                evargs.Source = o as Source;
                handler(evargs);
            }
        }
        
        private void OnSourceViewChanged(object o, EventArgs args)
        {
            SourceEventHandler handler = SourceViewChanged;
            if(handler != null) {
                SourceEventArgs evargs = new SourceEventArgs();
                evargs.Source = o as Source;
                handler(evargs);
            }
        }

        private void OnChildSourceAdded(SourceEventArgs args)
        {
            args.Source.Updated += OnSourceUpdated;
        }
        
        private void OnChildSourceRemoved(SourceEventArgs args)
        {
            args.Source.Updated -= OnSourceUpdated;
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
        
        public Source ActiveSource {
            get { return active_source; }
        }
        
        ISource ISourceManager.DefaultSource {
            get { return DefaultSource; }
        }
        
        ISource ISourceManager.ActiveSource {
            get { return ActiveSource; }
        }
        
        public void SetActiveSource(Source source)
        {
            SetActiveSource(source, true);
        }
        
        public void SetActiveSource(Source source, bool notify)
        {
            if(active_source != null) {
                active_source.Deactivate();
            }
            
            active_source = source;
            source.Activate();
            
            if(!notify) {
                return;
            }
            
            SourceEventHandler handler = ActiveSourceChanged;
            if(handler != null) {
                SourceEventArgs args = new SourceEventArgs();
                args.Source = active_source;
                handler(args);
            }
        }
     
        public ICollection<Source> Sources {
            get { return sources; }
        }
        
        string [] ISourceManager.Sources {
            get { return DBusServiceManager.MakeObjectPathArray<Source>(sources); }
        }
        
        IDBusExportable IDBusExportable.Parent {
            get { return null; }
        }
        
        string Banshee.ServiceStack.IService.ServiceName {
            get { return "SourceManager"; }
        }
    }
}
