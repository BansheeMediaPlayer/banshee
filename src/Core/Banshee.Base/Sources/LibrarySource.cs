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
using System.Collections.Generic;
using Mono.Unix;

using Banshee.Base;
using Banshee.Configuration.Schema;

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
            };  

            SortCriteria criteria;
            SortOrder order;
            
            try {
                criteria = (SortCriteria)LibrarySchema.PlaylistSortCriteria.Get();
                order = (SortOrder)LibrarySchema.PlaylistSortOrder.Get();
            } catch {
                criteria = SortCriteria.Name;
                order = SortOrder.Ascending;
            }

            LoadPlaylists();
            SortChildren(criteria, order);
        }
        
        private void LoadPlaylists()
        {
            foreach(ChildSource playlist in PlaylistUtil.LoadSources()) {
                AddChildSource(playlist);
            }
        }

        public override void SortChildren(SortCriteria criteria, SortOrder order)
        {
            base.SortChildren(criteria, order);
            
            LibrarySchema.PlaylistSortCriteria.Set((int)criteria);
            LibrarySchema.PlaylistSortOrder.Set((int)order);
        }
        
        public override void RemoveTrack(TrackInfo track)
        {
            Globals.Library.QueueRemove(track);
        }
        
        public override void Commit()
        {
            Globals.Library.CommitRemoveQueue();
        }
        
        private Queue<TrackInfo> add_pending_queue = new Queue<TrackInfo>();
        private uint add_pending_queue_flush_timeout = 0;
        
        public override void OnTrackAdded(TrackInfo track)
        {
            lock(this) {
                add_pending_queue.Enqueue(track);
            
                if(add_pending_queue_flush_timeout == 0) {
                    add_pending_queue_flush_timeout = GLib.Timeout.Add(1500, FlushAddPendingQueue);
                }
            }
        }
        
        private bool FlushAddPendingQueue()
        {
            lock(this) {
                while(add_pending_queue.Count > 0) {
                    base.OnTrackAdded(add_pending_queue.Dequeue());
                    base.OnUpdated();
                }
                
                add_pending_queue_flush_timeout = 0;
                return false;
            }
        }

        private Gtk.ActionGroup action_group = null;
        public override string ActionPath {
            get {
                if(action_group != null) {
                    return "/LibraryMenu";
                }
                
                action_group = new Gtk.ActionGroup("Library");
                action_group.Add(new Gtk.ActionEntry [] {
                    new Gtk.ActionEntry("SortPlaylistAction", null, 
                        Catalog.GetString("Sort Playlists"), null, null, null),
                        
                    new Gtk.ActionEntry("SortPlaylistNameAscAction", null, 
                        Catalog.GetString("Name Ascending"), null, null, 
                        delegate { SortChildren(SortCriteria.Name, SortOrder.Ascending); }),
                        
                    new Gtk.ActionEntry("SortPlaylistNameDescAction", null, 
                        Catalog.GetString("Name Descending"), null, null, 
                        delegate { SortChildren(SortCriteria.Name, SortOrder.Descending); }),
                        
                    new Gtk.ActionEntry("SortPlaylistSizeAscAction", null, 
                        Catalog.GetString("Size Ascending"), null, null, 
                        delegate { SortChildren(SortCriteria.Size, SortOrder.Ascending); }),
                        
                    new Gtk.ActionEntry("SortPlaylistSizeDescAction", null, 
                        Catalog.GetString("Size Descending"), null, null, 
                        delegate { SortChildren(SortCriteria.Size, SortOrder.Descending); })
                });
                
                Globals.ActionManager.UI.AddUiFromString(@"
                    <ui>
                        <popup name='LibraryMenu' action='LibraryMenuActions'>
                            <menuitem name='NewPlaylist' action='NewPlaylistAction' />
                            <menuitem name='ImportMusic' action='ImportMusicAction' />
                            <menu name='SortPlaylist' action='SortPlaylistAction'>
                                <menuitem name='SortPlaylistNameAsc' action='SortPlaylistNameAscAction' />
                                <menuitem name='SortPlaylistNameDesc' action='SortPlaylistNameDescAction' />
                                <menuitem name='SortPlaylistSizeAsc' action='SortPlaylistSizeAscAction' />
                                <menuitem name='SortPlaylistSizeDesc' action='SortPlaylistSizeDescAction' />
                            </menu>
                        </popup>
                    </ui>
                ");
                
                Globals.ActionManager.UI.InsertActionGroup(action_group, 0);
                
                return "/LibraryMenu";
            }
        }
        
        public override IEnumerable<TrackInfo> Tracks {
            get { return Globals.Library.Tracks.Values; }
        }
        
        public override object TracksMutex {
            get { return ((IDictionary)Globals.Library.Tracks).SyncRoot; }
        }
        
        public override int Count {
            get { return Globals.Library.Tracks.Count; }
        }  
        
        public override int SortColumn {
            get { return LibrarySchema.SortColumn.Get(base.SortColumn); }
            set { LibrarySchema.SortColumn.Set(value); }
        }        
        
        public override Gtk.SortType SortType {
            get { return (Gtk.SortType)LibrarySchema.SortType.Get((int)base.SortType); }
            set { LibrarySchema.SortType.Set((int)value); }
        }
        
        public override bool? AutoExpand {
            get { return null; }
        }
        
        public override bool Expanded {
            get { return LibrarySchema.SourceExpanded.Get(); }
            set { LibrarySchema.SourceExpanded.Set(value); }
        }
        
        private static Gdk.Pixbuf icon = IconThemeUtils.LoadIcon(22, Gtk.Stock.Home, "user-home", "source-library");
        public override Gdk.Pixbuf Icon {
            get { return icon; } 
        }
    }
}
