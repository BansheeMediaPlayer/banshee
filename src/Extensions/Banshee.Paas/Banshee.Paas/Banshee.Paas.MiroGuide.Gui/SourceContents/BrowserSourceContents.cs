// 
// BrowserSourceContents.cs
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

using Banshee.Sources;
using Banshee.Configuration;

namespace Banshee.Paas.MiroGuide.Gui
{
    public class BrowserSourceContents : ChannelSourceContents
    {
        private VPaned browser_pane;
        private MiroGuideCategoryListView category_view;

        public MiroGuideCategoryListView CategoryListView {
            get { return category_view; }
        }

        public BrowserSourceContents ()
        {        
            browser_pane = new VPaned ();
            category_view = new MiroGuideCategoryListView ();
            
            ScrolledWindow category_sw = new ScrolledWindow () {
                HscrollbarPolicy = PolicyType.Automatic,
                VscrollbarPolicy = PolicyType.Automatic
            };

            category_sw.Add (category_view);
            
            browser_pane.Add1 (ScrolledWindow);
            browser_pane.Add2 (category_sw);

            browser_pane.Position = BrowserSourceVPanedPosition.Get ();
            
            browser_pane.SizeAllocated += (sender, e) => {
                BrowserSourceVPanedPosition.Set (browser_pane.Position);
            };            
        }

        public override void Initialize ()
        {
            FilterBox.Add (browser_pane);
            Widget.ShowAll ();                        
        }

        public override bool SetSource (ISource source)
        {
            BrowseChannelsSource s = source as BrowseChannelsSource;
            
            if (base.SetSource (source) && s != null) {
                category_view.SetModel (s.CategoryModel);
                browser_pane.Position = BrowserSourceVPanedPosition.Get ();
                return true;
            }

            return false;
        }

        public override void ResetSource ()
        {
            category_view.SetModel (null);
            base.ResetSource ();
        }

        public static readonly SchemaEntry<int> BrowserSourceVPanedPosition = new SchemaEntry<int> (
            "plugins.paas.miroguide.ui", "browser_source_vpaned_pos", 150, "", ""
        ); 
    }
}
