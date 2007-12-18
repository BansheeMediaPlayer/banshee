/***************************************************************************
 *  Source.cs
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@abock.org>
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */
 
using System;
using System.Collections;
using System.Collections.Generic;
using Mono.Unix;

using Banshee.Base;

namespace Banshee.Sources
{
    public class InvalidSourceException : ApplicationException
    {
        public InvalidSourceException(string message) : base(message)
        {
        }
    }

    public delegate void TrackEventHandler(object o, TrackEventArgs args);

    public class TrackEventArgs : EventArgs
    {
        public TrackInfo Track;
        public ICollection Tracks;
    }

    public enum SortCriteria
    {
        Name,
        Size
    }
    
    public enum SortOrder
    {
        Ascending,
        Descending
    }
    
    public abstract class Source
    {
        private int order;
        private string name;
        private string filter_query;
        private TrackFilterType filter_type;
        private int sort_column = -1;
        private Gtk.SortType sort_type = Gtk.SortType.Ascending;
        private bool expanded;

        private List<Source> child_sources;

        public event EventHandler Updated;
        public event TrackEventHandler TrackAdded;
        public event TrackEventHandler TrackRemoved;
        public event EventHandler ViewChanged;
        public event SourceEventHandler ChildSourceAdded;
        public event SourceEventHandler ChildSourceRemoved;
        
        protected Source(string name, int order)
        {
            this.name = name;
            this.order = order;
            this.child_sources = new List<Source>();
        }
        
        public void Dispose()
        {
            OnDispose();
        }
        
        public virtual void Activate()
        {
        }

        public virtual void Deactivate()
        {
        }
        
        protected virtual void OnDispose()
        {
        }

        public bool Rename(string newName)
        {
            if(!UpdateName(name, newName)) {
                return false;
            }
                    
            OnUpdated();
            
            return true;
        }
        
        protected virtual bool UpdateName(string oldName, string newName)
        {
            return false;
        }

        public virtual bool Unmap()
        {
            return false;
        }
        
        public virtual void ShowPropertiesDialog()
        {
        }
        
        public virtual void AddTrack(TrackInfo track)
        {
        }
        
        public virtual void RemoveTrack(TrackInfo track)
        {
        }
        
        public void AddTrack(IEnumerable tracks)
        {
            foreach(TrackInfo track in tracks) {
                AddTrack(track);
            }
        }
        
        public void RemoveTrack(IEnumerable tracks)
        {
            foreach(TrackInfo track in tracks) {
                RemoveTrack(track);
            }
        }
        
        public virtual void OnTrackAdded(TrackInfo track)
        {
            OnTrackAdded(track, null);
        }

        public virtual void OnTrackAdded(TrackInfo track, ICollection tracks)
        {
            TrackEventHandler handler = TrackAdded;
            if(handler != null) {
                TrackEventArgs args = new TrackEventArgs();
                args.Track = track;
                args.Tracks = tracks;
                handler(this, args);
            }
        }
        
        public virtual void OnTrackRemoved(TrackInfo track)
        {
            OnTrackRemoved(track, null);
        }

        public virtual void OnTrackRemoved(TrackInfo track, ICollection tracks)
        {
            TrackEventHandler handler = TrackRemoved;
            if(handler != null) {
                TrackEventArgs args = new TrackEventArgs();
                args.Track = track;
                args.Tracks = tracks;
                handler(this, args);
            }
        }
        
        public virtual void Commit()
        {
        }
        
        public virtual void Reorder(TrackInfo track, int position)
        {
        }
        
        protected virtual void OnUpdated()
        {
            EventHandler handler = Updated;
            if(handler != null) {
                handler(this, new EventArgs());
            }
        }
        
        protected virtual void OnViewChanged()
        {
            EventHandler handler = ViewChanged;
            if(handler != null) {
                handler(this, new EventArgs());
            }
        }

        public class NameComparer : IComparer<Source>
        {
            public int Compare (Source a, Source b)
            {
                return a.Name.CompareTo (b.Name);
            }
        }

        public class SizeComparer : IComparer<Source>
        {
            public int Compare (Source a, Source b)
            {
                return a.Count.CompareTo (b.Count);
            }
        }

        public virtual void SortChildSources (IComparer<Source> comparer, SortOrder order)
        {
            lock(Children) {
                child_sources.Sort (comparer);
                if (order == SortOrder.Descending) {
                    child_sources.Reverse ();
                }

                int i = 0;
                foreach (Source child in child_sources) {
                    child.Order = i++;
                    child.OnUpdated ();
                }
            }
        }

        public virtual void SortChildren (SortCriteria criteria, SortOrder order)
        {
            if (criteria == SortCriteria.Name) {
                SortChildSources (new NameComparer(), order);
            } else if (criteria == SortCriteria.Size) {
                SortChildSources (new SizeComparer(), order);
            }
        }
        
        public virtual void AddChildSource(ChildSource source)
        {
            lock(Children) {
                source.SetParentSource(this);
                child_sources.Add(source);
            
                SourceEventHandler handler = ChildSourceAdded;
                if(handler != null) {
                    SourceEventArgs evargs = new SourceEventArgs();
                    evargs.Source = source;
                    handler(evargs);
                }
            }
        }

        public virtual void RemoveChildSource(ChildSource source)
        {
            lock(Children) {
                if (source.Children.Count > 0) {
                    source.ClearChildSources();
                }
                child_sources.Remove(source);
                
                if(SourceManager.ActiveSource == source) {
                    SourceManager.SetActiveSource(SourceManager.DefaultSource);
                }

                SourceEventHandler handler = ChildSourceRemoved;
                if(handler != null) {
                    SourceEventArgs evargs = new SourceEventArgs();
                    evargs.Source = source;
                    handler(evargs);
                }
            }
        }
        
        public virtual void ClearChildSources()
        {
            lock(Children) {
                while(child_sources.Count > 0) {
                    RemoveChildSource(child_sources[child_sources.Count - 1] as ChildSource);
                }
            }
        }
        
        public virtual void SourceDrop(Source source)
        {
        }
        
        public virtual TrackInfo GetTrackAt(int index)
        {
            // this is an awful hack to make older sources
            // compatible with new playback model (did not 
            // want to change existing API)
            
            int current_index = 0;
            
            foreach(TrackInfo track in Tracks) {
                if(current_index++ == index) {
                    return track;
                }
            }
            
            return null;
        }
        
        public virtual void StartPlayback()
        {
        }
        
        // Translators: Source being the generic word for playlist, device, library, etc
        private static string generic_name = Catalog.GetString("Source");
        public virtual string GenericName {
            get { return generic_name; }
        }

        public virtual string ActionPath {
            get { return null; }
        }
    
        public ICollection<Source> Children {
            get { return child_sources; }
        }

        public virtual int Count {
            get { return -1; }
        }
                
        public string Name {
            get { return name; }
            protected set { name = value; }
        }
        
        public string FilterQuery {
            get { return filter_query; }
            set { filter_query = value; }
        }
        
        public TrackFilterType FilterType {
            get { return filter_type; }
            set { filter_type = value; }
        }
        
        private static readonly List<TrackInfo> empty_track_list = new List<TrackInfo>();
        
        public virtual IEnumerable<TrackInfo> Tracks {
            get { return empty_track_list; }
        }
        
        private object tracks_mutex = null;
        public virtual object TracksMutex {
            get { 
                if(tracks_mutex == null) {
                    tracks_mutex = new object();
                }
                
                return tracks_mutex; 
            }
        }
        
        public virtual IEnumerable<string> Artists {
            get { return null; }
        }
        
        public virtual IEnumerable<string> GetAlbumsForArtist(string artist)
        {
            return null;
        }
        
        public virtual Gdk.Pixbuf Icon {
            get { return null; }
        }

        public virtual string SourcePropertiesIcon {
            get { return Gtk.Stock.Properties; }
        }

        public virtual string SourcePropertiesLabel {
            get { return Catalog.GetString("Source Properties..."); }
        }

        public virtual string UnmapIcon {
            get { return Gtk.Stock.Delete; }
        }

        public virtual string UnmapLabel {
            get { return String.Format(Catalog.GetString("Delete {0}"), GenericName); }
        }

        public bool CanUnmap {
            get { return ReflectionUtil.IsVirtualMethodImplemented(GetType(), "Unmap"); }
        }
        
        public virtual Gtk.Widget ViewWidget {
            get { return null; }
        }
        
        public virtual bool ShowPlaylistHeader {
            get { return true; }
        }
        
        public virtual bool HandlesSearch {
            get { return false; }
        }
        
        public virtual bool SearchEnabled {
            get { return true; }
        }
        
        public virtual bool AcceptsInput {
            get { return false; }
        }
        
        public virtual bool IsDragSource {
            get { return false; }
        }
        
        public int Order {
            get { return order; }
            private set { order = value; }
        }
 
        public virtual bool HasEmphasis {
            get { return false; }
        }
        
        public virtual bool? AutoExpand {
            get { return true; }
        }

        public virtual bool CanActivate {
            get { return true; }
        }
        
        public virtual bool CanWriteToCD {
            get { return true; }
        }

        private bool can_rename = true;
        public bool CanRename {
            get { return ReflectionUtil.IsVirtualMethodImplemented(GetType(), "UpdateName") && can_rename; }
            protected set { can_rename = value; }
        }
        
        public bool HasProperties {
            get { return ReflectionUtil.IsVirtualMethodImplemented(GetType(), "ShowPropertiesDialog"); }
        }
        
        public bool CanRemoveTracks {
            get { return ReflectionUtil.IsVirtualMethodImplemented(GetType(), "RemoveTrack", new Type [] { typeof (TrackInfo) }); }
        }
        
        public bool AcceptsSourceDrop {
            get { return ReflectionUtil.IsVirtualMethodImplemented(GetType(), "SourceDrop"); }
        }
        
        public bool HandlesStartPlayback {
            get { return ReflectionUtil.IsVirtualMethodImplemented(GetType(), "StartPlayback"); }
        }
        
        public virtual int SortColumn {
            get { return sort_column; }
            set { sort_column = value; }
        }
        
        public virtual Gtk.SortType SortType {
            get { return sort_type; }
            set { sort_type = value; }
        }
        
        public virtual bool Expanded {
            get { return expanded; }
            set {
                if(expanded == value) {
                    return;
                }
                
                expanded = value;
                OnUpdated();
            }
        }
    }
}
