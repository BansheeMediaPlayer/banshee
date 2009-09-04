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
using Banshee.Sources;
using Banshee.ServiceStack;

using Banshee.Paas.Aether.MiroGuide;

namespace Banshee.Paas.Aether.MiroGuide.Gui
{
    public class MiroGuideActions : BansheeActionGroup
    {
        private uint actions_id;
        private MiroGuideClient client;

        private ISource source;

        private TestSource TestSource {
            get { return source as TestSource; }
        }

        public MiroGuideChannelListModel ActiveModel {
            get {
                return (TestSource != null) ? TestSource.ChannelModel : null;
            }
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
                )
            });
            
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

        private IEnumerable<MiroGuideChannelInfo> GetSelectedChannels ()
        {
            return new List<MiroGuideChannelInfo> (TestSource.ChannelModel.GetSelected ());
        }

        private void OnMiroGuideChannelSubscribeHandler (object sender, EventArgs e)
        {
            
            client.RequestSubsubscription (GetSelectedChannels ().Select (c => new Uri (c.Url)));
        }
        
        private void OnChannelPopup (object sender, EventArgs e)
        {
            Console.WriteLine ("/MiroGuideChannelPopup");
            ShowContextMenu ("/MiroGuideChannelPopup");
        }
    }
}
