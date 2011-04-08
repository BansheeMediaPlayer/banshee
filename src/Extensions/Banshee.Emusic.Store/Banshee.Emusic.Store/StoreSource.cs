//
// StoreSource.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//   Will Thompson <will@willthompson.co.uk>
//
// Copyright 2010 Novell, Inc.
// Copyright 2011 Will Thompson
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
using Mono.Unix;

using Hyena;

using Banshee.WebSource;

namespace Banshee.Emusic.Store
{
    public class StoreSource : Banshee.WebSource.WebSource, IDisposable
    {
        public StoreWebBrowserShell Shell { get; private set; }

        public StoreSource () : base (Catalog.GetString ("eMusic"), 150, "emusic")
        {
            Properties.SetString ("Icon.Name", "emusic-store");
        }

        public void Dispose ()
        {
        }

        protected override WebBrowserShell GetWidget ()
        {
            return (Shell = new StoreWebBrowserShell (new StoreView ()));
        }
    }
}
