// 
// DownloadSourceContents.cs
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
using Banshee.Sources.Gui;

namespace Banshee.Paas.DownloadManager.Gui
{
    public class DownloadSourceContents : ISourceContents
    {
        DownloadSource download_source;
        DownloadListView download_view;

        public ISource Source { 
            get { return download_source; } 
        }
        
        public Widget Widget { 
            get { return download_view as Widget; }
        }

        public DownloadSourceContents (DownloadListView view)
        {
            download_view = view;
        }

        public bool SetSource (ISource source)
        {
            download_source = source as DownloadSource;
            
            if (download_source == null) {
                return false;
            }

            download_view.HeaderVisible = true;
            download_view.SetModel (download_source.DownloadListModel);
            
            return true;
        }

        public void ResetSource ()
        {            
            download_source = null;
            
            download_view.SetModel (null);
            download_view.HeaderVisible = false;
        }
    }
}
