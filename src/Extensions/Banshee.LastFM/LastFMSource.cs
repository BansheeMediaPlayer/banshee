/***************************************************************************
 *  LastFMSource.cs
 *
 *  Copyright (C) 2007-2008 Novell, Inc.
 *  Written by Gabriel Burt <gabriel.burt@gmail.com>
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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Mono.Gettext;
using Gtk;

using Banshee.Base;
using Banshee.Configuration;
using Banshee.Widgets;
using Banshee.Sources;
using Banshee.MediaEngine;
using Last.FM.Gui;
 
namespace Banshee.Plugins.LastFM
{   
    public class LastFMSource : Source
    {
        static string lastfm = "Last.fm";
        public LastFMSource () : base (lastfm, 150)
        {
        }

        public void Initialize ()
        {
            Connection.Instance.StateChanged += HandleConnectionStateChanged;
            UpdateUI ();
        }

        protected override void OnDispose ()
        {
            Connection.Instance.StateChanged -= HandleConnectionStateChanged;
            ClearChildSources ();
        }

        public override void ClearChildSources ()
        {
            lock (Children) {
                foreach (StationSource child in Children) {
                    if (SourceManager.ContainsSource (child))
                        SourceManager.RemoveSource (child);
                    child.Dispose ();
                }
            }

            base.ClearChildSources ();
        }

        public override void AddChildSource (ChildSource source)
        {
            base.AddChildSource (source);
            SortChildSources ();
            source.Updated += HandleChildUpdated;
        }

        public override void RemoveChildSource (ChildSource source)
        {
            base.RemoveChildSource (source);
            source.Updated -= HandleChildUpdated;
        }

        public override void ShowPropertiesDialog ()
        {
            AccountLoginDialog dialog = new AccountLoginDialog (true);
            dialog.SaveOnEdit = true;
            if (Connection.Instance.Username == null) {
                dialog.AddSignUpButton ();
            }
            dialog.Run ();
            dialog.Destroy ();
        }

        // Order by the playCount of a station, then by inverted name
        public class PlayCountComparer : IComparer<Source>
        {
            public int Compare (Source sa, Source sb)
            {
                StationSource a = sa as StationSource;
                StationSource b = sb as StationSource;
                int c = a.PlayCount.CompareTo (b.PlayCount);
                return c == 0 ? -(a.Name.CompareTo (b.Name)) : c; 
            }
        }

        // Order by the type of station, then by the station name
        public class TypeComparer : IComparer<Source>
        {
            public int Compare (Source sa, Source sb)
            {
                StationSource a = sa as StationSource;
                StationSource b = sb as StationSource;
                int c = a.Type.Name.CompareTo (b.Type.Name);
                return c == 0 ? (a.Name.CompareTo (b.Name)) : c; 
            }
        }

        public static IComparer<Source> [] ChildComparers = new IComparer<Source> [] {
            new NameComparer (), new PlayCountComparer (), new TypeComparer ()
        };
        public static SortOrder [] child_orders = new SortOrder [] {
            SortOrder.Ascending, SortOrder.Descending, SortOrder.Ascending
        };

        public IComparer<Source> ChildComparer {
            get {
                if (child_comparer == null) {
                    int i = (int) LastFMPlugin.StationSortSchema.Get ();
                    ChildComparer = ChildComparers [i];
                }
                return child_comparer;
            }
            set {
                child_comparer = value;
                int i = Array.IndexOf (ChildComparers, child_comparer);
                child_order = child_orders[i];
                LastFMPlugin.StationSortSchema.Set (i);
            }
        }

        private bool sorting = false;
        private IComparer<Source> child_comparer;
        private SortOrder child_order;
        public override void SortChildSources (IComparer<Source> comparer, SortOrder order)
        {
            lock (TracksMutex) {
                if (sorting)
                    return;
                sorting = true;
            }

            base.SortChildSources (comparer, order);
            ChildComparer = comparer;
            sorting = false;
        }

        public void SortChildSources  ()
        {
            SortChildSources (ChildComparer, child_order);
        }

        /*public override void Activate ()
        {
            InterfaceElements.ActionButtonBox.PackStart (add_button, false, false, 0);
        }*/

        public override string ActionPath {
            get { return "/LastFMSourcePopup"; }
        }

        private static string properties_label = Catalog.GetString ("Edit Last.fm Settings");
        public override string SourcePropertiesLabel {
            get { return properties_label; }
        }

        public override bool SearchEnabled {
            get { return false; }
        }
        
        public override bool CanWriteToCD {
            get { return false; }
        }
                
        public override bool ShowPlaylistHeader {
            get { return false; }
        }

        public override bool? AutoExpand {
            get { return LastFMPlugin.ExpandedSchema.Get (); }
        }

        public override bool Expanded {
            get { return LastFMPlugin.ExpandedSchema.Get (); }
            set { LastFMPlugin.ExpandedSchema.Set (value); }
        }

        public override bool CanActivate {
            get { return false; }
        }

        private Gdk.Pixbuf icon = Gdk.Pixbuf.LoadFromResource ("audioscrobbler.png");
        public override Gdk.Pixbuf Icon {
            get { return icon; }
        }

        private void HandleConnectionStateChanged (object sender, ConnectionStateChangedArgs args)
        {
            UpdateUI ();
        }

        private void HandleChildUpdated (object sender, EventArgs args)
        {
            SortChildSources ();
        }

        private string last_username;
        private bool updating = false;
        private void UpdateUI ()
        {
            lock (TracksMutex) {
                if (updating)
                    return;
                updating = true;
            }

            bool have_user = (Connection.Instance.Username != null);
            Globals.ActionManager["LastFMAddAction"].Sensitive = have_user;
            Globals.ActionManager["LastFMSortAction"].Sensitive = have_user;
            Globals.ActionManager["LastFMConnectAction"].Visible = Connection.Instance.State == ConnectionState.Disconnected;

            if (have_user) {
                if (Connection.Instance.Username != last_username) {
                    last_username = Connection.Instance.Username;
                    LastFMPlugin.LastUserSchema.Set (last_username);
                    ClearChildSources ();
                    sorting = true;
                    foreach (StationSource child in StationSource.LoadAll (Connection.Instance.Username)) {
                        if (!child.Type.SubscribersOnly || Connection.Instance.Subscriber) {
                            AddChildSource (child);
                            SourceManager.AddSource (child);
                        }
                    }
                    sorting = false;
                    SortChildSources ();
                }
            } else {
                ClearChildSources ();
            }

            Name = (Connection.Instance.State == ConnectionState.Connected ) ? lastfm : Catalog.GetString ("Last.fm (Disconnected)");
            OnUpdated ();
            updating = false;
        }
    }
}
