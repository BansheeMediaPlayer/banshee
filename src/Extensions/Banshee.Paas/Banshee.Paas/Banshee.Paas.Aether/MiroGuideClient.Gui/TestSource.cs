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

namespace Banshee.Paas.Aether.MiroGuide.Gui
{
    public class TestSource : Source, IImplementsCustomSearch
    {
        private ISourceContents contents;
        private MiroGuideSearchEntry search_entry;

        public SearchEntry SearchEntry
        {
            get { return search_entry; }
        }

//        private DownloadListModel download_model;
//
//        public DownloadListModel DownloadListModel
//        {
//            get { return download_model; }
//        }

        public override bool CanSearch {
            get { return true; }
        }

        public TestSource (/*DownloadListModel model, DownloadListView view*/) : base ("Miro Guide", "Miro Guide", 0)
        {
//            if (model == null) {
//                throw new ArgumentNullException ("model");
//            }

            TypeUniqueId = "TestSource";
//
//            download_model = model;
//            download_model.Cleared  += (sender, e) => { OnUpdated (); };
//            download_model.Reloaded += (sender, e) => { QueueDraw (); OnUpdated (); };
        
            Properties.SetStringList ("Icon.Name", "miroguide");

            contents = new TestSourceContents ();
            Properties.Set<ISourceContents> ("Nereid.SourceContents", contents);
            Properties.Set<bool> ("Nereid.SourceContentsPropagate", false);

            BuildSearchEntry ();
        }

        private void BuildSearchEntry ()
        {
            search_entry = new MiroGuideSearchEntry ();
            search_entry.SetSizeRequest (200, -1);
            search_entry.Show ();
        }

//        public override void Activate ()
//        {
//            download_model.Reload ();
//        }
//
//        public void QueueDraw ()
//        {
//            ThreadAssist.ProxyToMain (delegate {
//                contents.Widget.QueueDraw ();
//            });
//        }
//
//        public override int Count {
//            get { return download_model.Count; }
//        }
    }
}
