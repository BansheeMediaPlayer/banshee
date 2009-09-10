// 
// SearchSource.cs
//  
// Author:
//   Mike Urbanski <michael.c.urbanski@gmail.com>
// 
// Copyright (c) 2009 Michael C. Urbanski
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Linq;
using System.Collections.Generic;

using Mono.Unix;

using Gtk;

using Hyena.Data;
using Hyena.Collections;

using Banshee.Base;
using Banshee.Widgets;

using Banshee.Sources;
using Banshee.Sources.Gui;

using Banshee.Paas.Aether;
using Banshee.Paas.Aether.MiroGuide;

using Banshee.Paas.MiroGuide.Gui;

namespace Banshee.Paas.MiroGuide
{
    public class SearchSource : ChannelSource, IImplementsCustomSearch
    {
        private MiroGuideSearchEntry search_entry;

        public SearchEntry SearchEntry
        {
            get { return search_entry; }
        }

        public SearchSource (MiroGuideClient client) : base (client, "MiroGuideSearch", Catalog.GetString ("Search"), (int)MiroGuideSourcePosition.Search)
        {
            BuildSearchEntry ();
            Properties.SetStringList ("Icon.Name", "find");            
            Client.GetChannelsCompleted += OnGetChannelsCompletedHandler;
        }

        private void BuildSearchEntry ()
        {
            search_entry = new MiroGuideSearchEntry ();
            search_entry.SetSizeRequest (200, -1);
            
            search_entry.Activated += OnSearchEntryActivated;
            search_entry.Changed += OnSearchEntryChanged;
            
            search_entry.Cleared += (sender, e) => {
                ThreadAssist.ProxyToMain (delegate {
                    Context = null;
                    Contents.ScrolledWindow.VscrollbarPolicy = PolicyType.Automatic;
                    
                    ChannelModel.Selection.Clear ();
                    ChannelModel.Clear ();
                });                
            };

            search_entry.Show ();
        }

        protected override void FetchAdditionalChannels (SearchContext context)
        {
            Client.GetChannelsAsync (context);
        }

        protected override void OnMiroGuideClientStateChanged (object sender, AetherClientStateChangedEventArgs e)
        {
            ThreadAssist.ProxyToMain (delegate {
                search_entry.Ready = (e.NewState == AetherClientState.Idle);
                search_entry.InnerEntry.Sensitive = (e.NewState == AetherClientState.Idle);
            });

            base.OnMiroGuideClientStateChanged (sender, e);
        }

        private void OnSearchEntryActivated (object sender, EventArgs e)
        {
            Client.GetChannelsAsync (
                (MiroGuideFilterType)search_entry.ActiveFilterID, 
                search_entry.InnerEntry.Text, MiroGuideSortType.Name, false, 20, 0
            );
        }

        private void OnSearchEntryChanged (object sender, EventArgs e)
        {
            FilterQuery = search_entry.InnerEntry.Text;
        }

        private void OnGetChannelsCompletedHandler (object sender, GetChannelsCompletedEventArgs e)
        {
            ThreadAssist.ProxyToMain (delegate {
                Context = e.Context;                    
                Console.WriteLine ("Count:  {0} - Page:  {1}", Context.Count, Context.Page);
                
                if (e.Channels != null) {
                    foreach (MiroGuideChannelInfo channel in e.Channels.Reverse ()) {
                        RefreshArtworkFor (channel);
                    }
                                                
                    if (Context.Page == 0) {
                        ChannelModel.Selection.Clear ();
                        ChannelModel.Clear ();
                    }
                    
                    ChannelModel.Add (e.Channels);
    
                    if (Context.Count > 0 && Context.ChannelsAvailable) {
                        Contents.ScrolledWindow.VscrollbarPolicy = PolicyType.Always;
                        CheckVScrollbarValue (Contents.ScrolledWindow.VScrollbar as VScrollbar);
                    } else {
                        Contents.ScrolledWindow.VscrollbarPolicy = PolicyType.Automatic;
                        ChannelModel.Reload ();
                    }
                }
            });            
        }
    }
}
