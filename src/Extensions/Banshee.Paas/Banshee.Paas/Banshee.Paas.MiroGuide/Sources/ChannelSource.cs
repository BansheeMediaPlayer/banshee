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
using System.Threading;

using System.Linq;
using System.Collections.Generic;

using Gtk;

using Mono.Unix;

using Banshee.Gui;
using Banshee.Base;
using Banshee.Sources;
using Banshee.Sources.Gui;
using Banshee.ServiceStack;

using Banshee.Paas.Aether;
using Banshee.Paas.Aether.MiroGuide;

using Banshee.Paas.MiroGuide.Gui;

namespace Banshee.Paas.MiroGuide
{
    public abstract class ChannelSource : Source, IDisposable
    {
        private MiroGuideClient client;
        
        private ChannelSourceContents contents;
        private MiroGuideChannelListModel channel_model;

        private readonly MiroGuideFilterType filter_type;

        protected MiroGuideClient Client { 
            get { return client; }
        }

        protected ChannelSourceContents Contents {
            get { return contents; }
        }

        protected string BusyStatusMessage { get; set; }
        protected SearchContext Context { get; set; }
        
        public MiroGuideChannelListModel ChannelModel { 
            get { return channel_model; }
        }

        // Remove when MG client can queue tasks.
        private static ManualResetEvent client_handle = new ManualResetEvent (true);
        protected static ManualResetEvent ClientHandle {
            get { return client_handle; }
        }

        public ChannelSource (MiroGuideClient client, 
                              MiroGuideFilterType filterType, 
                              string genericName, string name, int order) : base (genericName, name, order)
        {
            if (client == null) {
                throw new ArgumentNullException ("client");
            }

            this.client = client;
            this.filter_type = filterType;
            
            MiroGuideActions actions = ServiceManager.Get <InterfaceActionService> ().FindActionGroup ("MiroGuide") as MiroGuideActions;
            actions.SortPreferenceChanged += OnSortPreferenceChangedHandler;
            
            BusyStatusMessage = Catalog.GetString ("Updating");

            channel_model = new MiroGuideChannelListModel ();
            
            channel_model.Cleared  += (sender, e) => { OnUpdated (); };
            channel_model.Reloaded += (sender, e) => { QueueDraw (); OnUpdated (); };
            
            this.client.StateChanged += OnMiroGuideClientStateChanged;

            TypeUniqueId = String.Concat (genericName, "ChannelSource");

            Client.GetChannelsCompleted += OnGetChannelsCompletedHandler;

//            ServiceStack.ServiceManager.SourceManager.ActiveSourceChanged += (e) => {
//                ActiveSourceChangedEventArgs args = e as ActiveSourceChangedEventArgs;
//                
//                if (args.OldSource == this && args.Source is ChannelSource) {
//                }
//
//                if (args.Source == this) {
//                    ClientHandle.WaitOne ();
//                    Activate
//                }
//            };

            contents = new ChannelSourceContents ();
            
            (contents.ScrolledWindow.VScrollbar as VScrollbar).ValueChanged += OnVScrollbarValueChangedHandler;

            Properties.Set<ISourceContents> ("Nereid.SourceContents", contents);
            Properties.Set<bool> ("Nereid.SourceContentsPropagate", false);
        }

        public override void Activate ()
        {
            Console.WriteLine ("Activate - {0}", TypeUniqueId);        
            ClientHandle.WaitOne ();
        }

        public override void Deactivate ()
        {
            Console.WriteLine ("Deactivate - {0}", TypeUniqueId);
            Client.CancelAsync ();
        }

        public void Dispose ()
        {
            Client.GetChannelsCompleted -= OnGetChannelsCompletedHandler;            
        }

        public virtual void QueueDraw ()
        {
            ThreadAssist.ProxyToMain (delegate {
                contents.Widget.QueueDraw ();
            });
        }

        protected virtual void GetChannelsAsync (string filterValue) 
        {
            Client.GetChannelsAsync (
                filter_type, filterValue, (MiroGuideSortType)MiroGuideActions.ActiveSort.Get (), false, 20, 0, this
            );
        }

        protected virtual void GetChannelsAsync () 
        {
            if (Context != null) {
                Client.GetChannelsAsync (Context, this);
            }
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
        
        protected virtual void FetchAdditionalChannels (SearchContext context)
        {
            GetChannelsAsync ();
        }
        
        protected virtual void RefreshArtworkFor (MiroGuideChannelInfo channel)
        {
            if (!CoverArtSpec.CoverExists (PaasService.ArtworkIdFor (channel.Name))) {
                Banshee.Kernel.Scheduler.Schedule (
                    new MiroGuideImageFetchJob (channel), Banshee.Kernel.JobPriority.AboveNormal
                );
            }
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
                        Client.CancelAsync ();
                    }
                };

                PushMessage (m);
            });        
        }
        
        protected virtual void OnMiroGuideClientStateChanged (object sender, AetherClientStateChangedEventArgs e)
        {
            if (e.NewState == AetherClientState.Busy) {
                ClientHandle.Reset ();
                ThreadAssist.ProxyToMain (delegate {
                    SetRequestStatus (String.Format ("{0}...", BusyStatusMessage)); 
                });
            } else {
                ClientHandle.Set ();
                ThreadAssist.ProxyToMain (delegate { PopMessage (); });
            }
        }

        protected virtual void OnVScrollbarValueChangedHandler (object sender, EventArgs e)
        {
            CheckVScrollbarValue (sender as VScrollbar);            
        }

        protected virtual void OnGetChannelsCompletedHandler (object sender, GetChannelsCompletedEventArgs e)
        {
            if (e.UserState != this) {
                return;
            }
            
            ThreadAssist.ProxyToMain (delegate {
                if (e.Cancelled || e.Error != null) {
                    return;
                }
                
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

        protected virtual void OnSortPreferenceChangedHandler (object sender, SortPreferenceChangedEventArgs e)
        {
            ThreadAssist.ProxyToMain (delegate {
                if (Context != null) {
                    Context.SortType = e.Sort;
                    Context.ResetCount ();

                    GetChannelsAsync ();
                }
            });                    
        }
    }
}
