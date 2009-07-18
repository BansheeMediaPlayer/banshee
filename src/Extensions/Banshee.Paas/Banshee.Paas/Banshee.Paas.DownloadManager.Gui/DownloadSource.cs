// 
// DownloadSource.cs
//  
// Authors:
//   Mike Urbanski <michael.c.urbanski@gmail.com>
//   Aaron Bockover <abockover@novell.com>
// 
// Copyright (c) 2009 Michael C. Urbanski
// Copyright (C) 2007 Novell, Inc. (ErrorSource.cs)
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

using Hyena.Data;
using Hyena.Collections;

using Banshee.Base;
using Banshee.Sources;
using Banshee.Sources.Gui;

using Migo2.Async;
using Migo2.Collections;
using Migo2.DownloadService;

namespace Banshee.Paas.DownloadManager.Gui
{
    public class DownloadSource : Source, IUnmapableSource
    {
        private ISourceContents contents;
        private DownloadListModel download_model;
        
        public DownloadListModel DownloadListModel
        {
            get { return download_model; }
        }

        public DownloadSource (DownloadListModel model, DownloadListView view) : base (Catalog.GetString ("Downloads"), Catalog.GetString ("Downloads"), 0)
        {
            if (model == null) {
                throw new ArgumentNullException ("model");
            }

            TypeUniqueId = "DownloadSource";

            download_model = model;
            download_model.Cleared  += (sender, e) => { OnUpdated (); };
            download_model.Reloaded += (sender, e) => { QueueDraw (); OnUpdated (); };
        
            Properties.SetStringList ("Icon.Name", "gtk-network", "network");

            contents = new DownloadSourceContents (view);
            Properties.Set<ISourceContents> ("Nereid.SourceContents", contents);
            Properties.Set<bool> ("Nereid.SourceContentsPropagate", false);
            //Properties.SetString ("GtkActionPath", "");
        }

        public bool Unmap ()
        {
            download_model.Clear ();
            Parent.RemoveChildSource (this);
            return true;
        }
        
        public override void Activate ()
        {
            download_model.Reload ();
        }

        public void QueueDraw ()
        {
            ThreadAssist.ProxyToMain (delegate {
                contents.Widget.QueueDraw ();
            });
        }

        public virtual bool CanUnmap {
            get { return false; }
        }

        public bool ConfirmBeforeUnmap {
            get { return false; }
        }

        public override int Count {
            get { return download_model.Count; }
        }
    }
}
