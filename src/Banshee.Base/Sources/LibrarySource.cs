/***************************************************************************
 *  LibrarySource.cs
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
using Mono.Unix;

using Banshee.Base;

namespace Banshee.Sources
{
    public class LibrarySource : Source
    {
        private static LibrarySource instance;
        public static LibrarySource Instance {
            get {
                if(instance == null) {
                    instance = new LibrarySource();
                }
                
                return instance;
            }
        }
        
        private LibrarySource() : base(Catalog.GetString("Music Library"), 0)
        {
            Globals.Library.TrackRemoved += delegate(object o, LibraryTrackRemovedArgs args) {
                OnTrackRemoved(args.Track);
                OnUpdated();
            };
              
            Globals.Library.TrackAdded += delegate(object o, LibraryTrackAddedArgs args) {
                OnTrackAdded(args.Track);
                OnUpdated();
            };  
        }
        
        public override void RemoveTrack(TrackInfo track)
        {
            Globals.Library.QueueRemove(track);
        }
        
        public override void Commit()
        {
            Globals.Library.CommitRemoveQueue();
        }
        
        public override IEnumerable Tracks {
            get { return Globals.Library.Tracks.Values; }
        }
        
        public override object TracksMutex {
            get { return Globals.Library.Tracks.SyncRoot; }
        }
        
        public override int Count {
            get { return Globals.Library.Tracks.Count; }
        }  
        
        public override Gdk.Pixbuf Icon {
            get { return IconThemeUtils.LoadIcon(22, Gtk.Stock.Home, "user-home", "source-library"); } 
        }
    }
}
