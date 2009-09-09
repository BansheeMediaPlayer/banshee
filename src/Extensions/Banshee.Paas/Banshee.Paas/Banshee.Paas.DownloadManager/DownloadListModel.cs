// 
// DownloadListModel.cs
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
using System.Collections;
using System.Collections.Generic;

using Mono.Unix;

using Hyena.Data;
using Hyena.Collections;

using Migo2.Async;
using Migo2.Collections;
using Migo2.DownloadService;

using Banshee.Paas.Utils;

using Banshee.Paas.Data;

namespace Banshee.Paas.DownloadManager
{
    public class DownloadListModel : ListModel<HttpFileDownloadTask>
    {
        public override bool CanReorder { 
            get { return true; } 
        }

        public void AddTaskPair (Pair<int,HttpFileDownloadTask> pair)
        {
            if (pair != null && pair.Second != null) {
                Items.Insert (pair.First, pair.Second);           
            }
            
            OnReloaded ();
        }

        public void AddTaskPairs (IEnumerable<Pair<int,HttpFileDownloadTask>> pairs)
        {
            if (pairs != null) {
                foreach (Pair<int,HttpFileDownloadTask> pair in pairs) {
                    if (pair != null && pair.Second != null) {
                        Items.Insert (pair.First, pair.Second);
                    }
                }
            }
            
            OnReloaded ();
        }    
    }
}
