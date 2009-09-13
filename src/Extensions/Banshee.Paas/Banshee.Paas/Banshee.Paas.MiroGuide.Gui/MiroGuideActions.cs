// 
// MiroGuideActions.cs
//  
// Author:
//    Mike Urbanski <michael.c.urbanski@gmail.com>
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

using Banshee.Gui;
using Banshee.Base;
using Banshee.Sources;
using Banshee.ServiceStack;
using Banshee.Configuration;

using Banshee.Paas.Aether.MiroGuide;

namespace Banshee.Paas.MiroGuide.Gui
{
    public class MiroGuideActions : BansheeActionGroup
    {
        private uint actions_id;
        private MiroGuideClient client;

        private ISource source;
        
        private RadioAction active_action;
        public RadioAction ActiveSortAction {
            get { return active_action; }
        }
        
        public EventHandler<SortPreferenceChangedEventArgs> SortPreferenceChanged;

        private ChannelSource ChannelSource {
            get { return source as ChannelSource; }
        }

        public MiroGuideChannelListModel ActiveModel {
            get { return (ChannelSource != null) ? ChannelSource.ChannelModel : null; }
        }
        
        public MiroGuideActions (MiroGuideClient client) : base (ServiceManager.Get<InterfaceActionService> (), "MiroGuide")
        {
            this.client = client;
            
            Add (new ActionEntry [] {
                new ActionEntry (
                   "MiroGuideChannelPopupAction", null, null, null, null, OnChannelPopup
                ), new ActionEntry (
                    "MiroGuideChannelSubscribeAction", Stock.Add,
                    Catalog.GetString ("Subscribe"), null,
                    null, OnMiroGuideChannelSubscribeHandler
                ), new ActionEntry (
                    "PaasEditMiroGuidePropertiesAction", Stock.Properties,
                    Catalog.GetString ("Edit Miro Guide Settings"), "<control>M",
                    null, (sender, e) => { 
                        MiroGuideAccountDialog mgad = new MiroGuideAccountDialog (PaasService.MiroGuideAccount);
                        mgad.Run ();
                        mgad.Destroy ();
                    }
                )
            });

            Add (new RadioActionEntry [] {
                new RadioActionEntry ("MiroGuideSortByNameAction", null, 
                    Catalog.GetString ("Sort Channels by Name"), null,
                    Catalog.GetString ("Order results by name."),
                    (int)MiroGuideSortType.Name),
                    
                new RadioActionEntry ("MiroGuideSortByRatingAction", null, 
                    Catalog.GetString ("Sort Channels by Rating"), null,
                    Catalog.GetString ("Order results by rating."),
                    (int)MiroGuideSortType.Rating),
                    
                new RadioActionEntry ("MiroGuideSortByPopularityAction", null, 
                    Catalog.GetString ("Sort Channels by Popularity"), null,
                    Catalog.GetString ("Order results by popularity."),
                    (int)MiroGuideSortType.Popular)
            }, 0, OnActionChangedHandler);

            SetActiveSortPreference (MiroGuideSortType.Name);
            
            actions_id = Actions.UIManager.AddUiFromResource ("MiroGuideUI.xml");
            Actions.AddActionGroup (this);

            ServiceManager.SourceManager.ActiveSourceChanged += (e) => {
                source = e.Source;
            };
        }
        
        public override void Dispose ()
        {
            Actions.UIManager.RemoveUi (actions_id);
            Actions.RemoveActionGroup (this);
            base.Dispose ();
        }
        
        public void SetActiveSortPreference (MiroGuideSortType sort)
        {
            if (active_action == null || (int)sort != active_action.Value) {
                active_action = GetSortPreferenceAction (sort);
                active_action.Active = true;
                OnSortPreferenceChanged (sort);
            }
        }

        private IEnumerable<MiroGuideChannelInfo> GetSelectedChannels ()
        {
            return new List<MiroGuideChannelInfo> (ChannelSource.ChannelModel.GetSelected ());
        }

        private void OnMiroGuideChannelSubscribeHandler (object sender, EventArgs e)
        {
            client.RequestSubsubscription (GetSelectedChannels ().Select (c => new Uri (c.Url)));
        }
        
        private void OnChannelPopup (object sender, EventArgs e)
        {
            ShowContextMenu ("/MiroGuideChannelPopup");
        }

        private RadioAction GetSortPreferenceAction (MiroGuideSortType sort)
        {
            switch (sort) {
            case MiroGuideSortType.Name: return GetAction ("MiroGuideSortByNameAction") as RadioAction;
            case MiroGuideSortType.Rating: return GetAction ("MiroGuideSortByRatingAction") as RadioAction;
            case MiroGuideSortType.Popular: return GetAction ("MiroGuideSortByPopularityAction") as RadioAction;            
            default:
                goto case MiroGuideSortType.Name;
            }
        }

        private void OnSortPreferenceChanged (MiroGuideSortType sort)
        {
            var handler = SortPreferenceChanged;
            
            if (handler != null) {
                handler (null, new SortPreferenceChangedEventArgs (ChannelSource, sort));
            }
        }
        
        private void OnActionChangedHandler (object o, ChangedArgs args)
        {
            if (active_action != args.Current) { 
                active_action = args.Current;
                OnSortPreferenceChanged ((MiroGuideSortType)active_action.Value);
            }
        }
    }
}
