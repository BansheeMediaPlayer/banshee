//
// AmazonMp3GroupSource.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//   Jeff Wheeler <jeff@jeffwheeler.name>
//
// Copyright (C) 2008 Novell, Inc.
// Copyright (C) 2009 Jeff Wheeler
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using Mono.Unix;

using Banshee.Library;

namespace Banshee.Dap.MassStorage
{
    public class AmazonMp3GroupSource : MediaGroupSource, IPurchasedMusicSource
    {
        private String amazon_base_dir;

        public AmazonMp3GroupSource (DapSource parent, String amazon_dir, String amazon_base_dir)
            : base (parent, Catalog.GetString ("Purchased Music"))
        {
            this.amazon_base_dir = amazon_base_dir;

            Properties.SetString ("Icon.Name", "amazon-mp3-source");
            ConditionSql = String.Format ("(CoreTracks.Uri LIKE \"%{0}%\")", amazon_dir);
        }

        public override void Activate ()
        {
            base.Activate ();
            active = true;
        }

        public override void Deactivate ()
        {
            base.Deactivate ();
            active = false;
        }

        public void Import ()
        {
            LibraryImportManager importer = new LibraryImportManager (true);
            importer.Enqueue (amazon_base_dir);
        }

        private bool active;
        public bool Active {
            get { return active; }
        }
    }
}
