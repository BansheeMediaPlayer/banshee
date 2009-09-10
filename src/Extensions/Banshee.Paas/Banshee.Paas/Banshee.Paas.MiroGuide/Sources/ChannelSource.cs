// 
// ChannelSource.cs
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

using Gtk;

using Banshee.Base;
using Banshee.Sources;
using Banshee.Sources.Gui;

using Banshee.Paas.Aether;
using Banshee.Paas.Aether.MiroGuide;

using Banshee.Paas.MiroGuide.Gui;

namespace Banshee.Paas.MiroGuide
{
    public abstract class ChannelSource : Source
    {
        private MiroGuideClient client;
        //private ManualResetEvent client_wait;
        
        private ChannelSourceContents contents;
        private MiroGuideChannelListModel channel_model;
        
        protected MiroGuideClient Client { 
            get { return client; }
        }

        protected ChannelSourceContents Contents {
            get { return contents; }
        }

        protected SearchContext Context { get; set; }
        
        public MiroGuideChannelListModel ChannelModel { 
            get { return channel_model; }
        }

        public ChannelSource (MiroGuideClient client, string genericName, string name, int order) : base (genericName, name, order)
        {
            if (client == null) {
                throw new ArgumentNullException ("client");
            }

            this.client = client;
           
            channel_model = new MiroGuideChannelListModel ();
            
            channel_model.Cleared  += (sender, e) => { OnUpdated (); };
            channel_model.Reloaded += (sender, e) => { QueueDraw (); OnUpdated (); };
            
            this.client.StateChanged += OnMiroGuideClientStateChanged;

            TypeUniqueId = String.Concat (genericName, "ChannelSource");

            ServiceStack.ServiceManager.SourceManager.ActiveSourceChanged += (e) => {
                ActiveSourceChangedEventArgs args = e as ActiveSourceChangedEventArgs;
                
                if (args.OldSource == this && args.Source is ChannelSource) {
                    Client.CancelAsync ();
                }
            };

            contents = new ChannelSourceContents ();
            
            (contents.ScrolledWindow.VScrollbar as VScrollbar).ValueChanged += OnVScrollbarValueChangedHandler;

            Properties.Set<ISourceContents> ("Nereid.SourceContents", contents);
            Properties.Set<bool> ("Nereid.SourceContentsPropagate", false);
        }

        public virtual void QueueDraw ()
        {
            ThreadAssist.ProxyToMain (delegate {
                contents.Widget.QueueDraw ();
            });
        }

        protected virtual void SetRequestStatus (string message)
        {
            SetRequestStatus (message, null);        
        }

        protected virtual void SetRequestStatus (string message, string iconName)
        {
            ThreadAssist.ProxyToMain (delegate {
                SourceMessage m = new SourceMessage (this) {
                    Text = message,
                    CanClose = true,
                    IsSpinning = true
                };

                m.Updated += (sender, e) => {
                    if (m.IsHidden) {
                        client.CancelAsync ();
                    }
                };

                PushMessage (m);
            });        
        }

        protected virtual void FetchAdditionalChannels (SearchContext context)
        {
        }
        
        protected virtual void CheckVScrollbarValue (VScrollbar vsb)
        {
            if (vsb.Value == vsb.Adjustment.Upper-vsb.Adjustment.PageSize ||
                vsb.Adjustment.Upper-vsb.Adjustment.PageSize < 0) {
                if (Context != null && Context.ChannelsAvailable) {
                    FetchAdditionalChannels (Context);
                }   
            }            
        }

        protected virtual void RefreshArtworkFor (MiroGuideChannelInfo channel)
        {
            if (!CoverArtSpec.CoverExists (PaasService.ArtworkIdFor (channel.Name))) {
                Banshee.Kernel.Scheduler.Schedule (
                    new MiroGuideImageFetchJob (channel), Banshee.Kernel.JobPriority.AboveNormal
                );
            }
        }

        protected virtual void OnMiroGuideClientStateChanged (object sender, AetherClientStateChangedEventArgs e)
        {
            if (e.NewState == AetherClientState.Busy) {
                ThreadAssist.ProxyToMain (delegate {
                    SetRequestStatus ("Updating..."); 
                });
            } else {
                ThreadAssist.ProxyToMain (delegate { PopMessage (); });
            }
        }

        protected virtual void OnVScrollbarValueChangedHandler (object sender, EventArgs e)
        {
            CheckVScrollbarValue (sender as VScrollbar);            
        }
    }
}
