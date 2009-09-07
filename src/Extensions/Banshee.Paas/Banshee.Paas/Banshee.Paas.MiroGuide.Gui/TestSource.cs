// 
// DownloadSource.cs
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

namespace Banshee.Paas.MiroGuide.Gui
{
    public class TestSource : Source, IImplementsCustomSearch, IDisposable
    {
        private MiroGuideActions actions;
        private ISourceContents contents;

        private MiroGuideClient client;
        private MiroGuideSearchEntry search_entry;
        private MiroGuideChannelListModel channel_model;

        public SearchEntry SearchEntry
        {
            get { return search_entry; }
        }
        
        public MiroGuideChannelListModel ChannelModel
        {
            get { return channel_model; }
        }

        public TestSource (MiroGuideClient client) : base ("Miro Guide", "Miro Guide", 201)
        {
            if (client == null) {
                throw new ArgumentNullException ("client");
            }

            actions = new MiroGuideActions (client);

            BuildSearchEntry ();

            this.client = client;
            channel_model = new MiroGuideChannelListModel ();
            
            channel_model.Cleared  += (sender, e) => { OnUpdated (); };
            channel_model.Reloaded += (sender, e) => { QueueDraw (); OnUpdated (); };
            
            client.StateChanged += OnMiroGuideClientStateChanged;

            client.GetChannelsCompleted += (sender, e) => {
                ThreadAssist.ProxyToMain (delegate {
                    foreach (MiroGuideChannelInfo channel in e.Channels) {
                        RefreshArtworkFor (channel);
                    }
                    
                    channel_model.Selection.Clear ();
                    channel_model.Clear ();
                    channel_model.Add (e.Channels);
                });
            };

            TypeUniqueId = "TestSource";
            Properties.SetStringList ("Icon.Name", "miroguide");

            Properties.SetString ("GtkActionPath", "/MiroGuideSourcePopup");

            contents = new TestSourceContents ();
            Properties.Set<ISourceContents> ("Nereid.SourceContents", contents);
            Properties.Set<bool> ("Nereid.SourceContentsPropagate", false);
        }

        public void Dispose ()
        {
            if (actions != null) {
                actions.Dispose ();
                actions = null;
            }            
        }

        public void QueueDraw ()
        {
            ThreadAssist.ProxyToMain (delegate {
                contents.Widget.QueueDraw ();
            });
        }

        private void BuildSearchEntry ()
        {
            search_entry = new MiroGuideSearchEntry ();
            search_entry.SetSizeRequest (200, -1);
            
            search_entry.Activated += OnSearchEntryActivated;
            search_entry.Changed += OnSearchEntryChanged;
            search_entry.Cleared += (sender, e) => {
                ThreadAssist.ProxyToMain (delegate {
                    channel_model.Selection.Clear ();
                    channel_model.Clear ();
                });                
            };

            search_entry.Show ();
        }

        protected void RefreshArtworkFor (MiroGuideChannelInfo channel)
        {
            if (!CoverArtSpec.CoverExists (PaasService.ArtworkIdFor (channel.Name))) {
                Banshee.Kernel.Scheduler.Schedule (
                    new MiroGuideImageFetchJob (channel), Banshee.Kernel.JobPriority.BelowNormal
                );
            }
        }

        protected virtual void OnMiroGuideClientStateChanged (object sender, AetherClientStateChangedEventArgs e)
        {
            ThreadAssist.ProxyToMain (delegate {
                search_entry.Ready = (e.NewState == AetherClientState.Idle);
                search_entry.InnerEntry.Sensitive = (e.NewState == AetherClientState.Idle);
            });
        }

        protected virtual void OnSearchEntryActivated (object sender, EventArgs e)
        {
            client.GetChannels (
                (MiroGuideFilterType)search_entry.ActiveFilterID, 
                search_entry.InnerEntry.Text, MiroGuideSortType.Name, false, 100, 0
            );
        }

        protected virtual void OnSearchEntryChanged (object sender, EventArgs e)
        {
            FilterQuery = search_entry.InnerEntry.Text;
        }
    }
}
