// 
// ChannelSourceContents.cs
//  
// Author:
//       Mike Urbanski <michael.c.urbanski@gmail.com>
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

using Banshee.Gui;
using Banshee.Base;
using Banshee.Sources;
using Banshee.Sources.Gui;
using Banshee.ServiceStack;
using Banshee.Configuration;

namespace Banshee.Paas.MiroGuide.Gui
{
    public class ChannelSourceContents : ISourceContents
    {
        private HPaned hp;
        private ScrolledWindow sw;
        private ChannelSource source;
        
        private MiroGuideChannelListView channel_view;

        public ISource Source { 
            get { return source; } 
        }
        
        public Widget Widget { 
            get { return hp as Widget; }
        }

        public HPaned HPane {
            get { return hp; }
        }

        public ScrolledWindow ScrolledWindow {
            get { return sw; }
        }

        private static SortPreferenceActionButton sb;

        static ChannelSourceContents () 
        {
            sb = new SortPreferenceActionButton ();
            sb.Hide ();
            
            ServiceManager.Get<InterfaceActionService> ().PopulateToolbarPlaceholder (
                (Toolbar)ServiceManager.Get<InterfaceActionService> ().UIManager.GetWidget ("/FooterToolbar"),
                "/FooterToolbar/Extensions/MiroGuideChannelSortButton",
                sb
            );
        }
        
        public ChannelSourceContents ()
        {
            channel_view = new MiroGuideChannelListView ();
            
            sw = new ScrolledWindow () {
                HscrollbarPolicy = PolicyType.Automatic,
                VscrollbarPolicy = PolicyType.Automatic
            };
            
            sw.Add (channel_view);
            
            hp = new HPaned ();

            hp.SizeAllocated += (sender, e) => {
                HPanedPosition.Set (hp.Position);
            };
            
            hp.Add1 (sw);
            hp.Add2 (new Label ("Coming Soon."));
            hp.ShowAll ();
        }

        public bool SetSource (ISource source)
        {
            this.source = source as ChannelSource;
            
            if (this.source == null) {
                return false;
            }
            
            channel_view.HeaderVisible = true;
            hp.Position = HPanedPosition.Get ();                                    
            channel_view.SetModel (this.source.ChannelModel);

            if (this.source.Properties.Get<bool> ("MiroGuide.Gui.Source.ShowSortPreference")) {
                sb.Show ();
            }
            
            return true;
        }

        public void ResetSource ()
        {
            source = null;
            
            channel_view.SetModel (null);
            channel_view.HeaderVisible = false;

            sb.Hide ();
        }

        public static readonly SchemaEntry<int> HPanedPosition = new SchemaEntry<int> (
            "plugins.paas.miroguide.ui", "channel_source_hpaned_pos", 255, "", ""
        ); 
    }
}
