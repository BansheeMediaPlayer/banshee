
/***************************************************************************
 *  Source.cs
 *
 *  Copyright (C) 2005 Novell
 *  Written by Aaron Bockover (aaron@aaronbock.net)
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
        public IEnumerable Tracks;
    }
    
    public abstract class Source
    {
        private int order;
        private string name;

        public event EventHandler Updated;
        public event TrackEventHandler TrackAdded;
        public event TrackEventHandler TrackRemoved;
        public event EventHandler ViewChanged;
        
        protected Source(string name, int order)
        {
            this.name = name;
            this.order = order;
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
        
        public virtual bool Eject()
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
            TrackEventHandler handler = TrackAdded;
            if(handler != null) {
                TrackEventArgs args = new TrackEventArgs();
                args.Track = track;
                handler(this, args);
            }
        }
        
        public virtual void OnTrackRemoved(TrackInfo track)
        {
            TrackEventHandler handler = TrackRemoved;
            if(handler != null) {
                TrackEventArgs args = new TrackEventArgs();
                args.Track = track;
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
        
        public virtual int Count {
            get {
                return -1;
            }
        }
                
        public string Name {
            get {
                return name;
            }
            
            protected set {
                name = value;
            }
        }
        
        private static readonly ArrayList empty_track_list = new ArrayList();
        
        public virtual IEnumerable Tracks {
            get {
                return empty_track_list;
            }
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
        
        public virtual Gdk.Pixbuf Icon {
            get {
                return null;
            }
        }
        
        public virtual Gtk.Widget ViewWidget {
            get {
                return null;
            }
        }
        
        public virtual bool ShowPlaylistHeader {
            get {
                return true;
            }
        }
        
        public virtual bool HandlesSearch {
            get {
                return false;
            }
        }
        
        public virtual bool SearchEnabled {
            get {
                return true;
            }
        }
        
        public int Order {
            get {
                return order;
            }
        }
        
        public bool CanEject {
            get {
                return ReflectionUtil.IsVirtualMethodImplemented(GetType(), "Eject");
            }
        }
        
        private bool can_rename = true;
        public bool CanRename {
            get {
                return ReflectionUtil.IsVirtualMethodImplemented(GetType(), "UpdateName") && can_rename;
            }
            
            protected set {
                can_rename = value;
            }
        }
        
        public bool HasProperties {
            get {
                return ReflectionUtil.IsVirtualMethodImplemented(GetType(), "ShowPropertiesDialog");
            }
        }
        
        public bool CanRemoveTracks {
            get {
                return ReflectionUtil.IsVirtualMethodImplemented(GetType(), "RemoveTrack");
            }
        }
    }
}
