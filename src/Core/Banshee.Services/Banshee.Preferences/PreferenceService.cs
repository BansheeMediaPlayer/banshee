//
// PreferenceService.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
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

using Banshee.ServiceStack;
using Banshee.Library;
using Banshee.Configuration.Schema;
using Banshee.Configuration;

namespace Banshee.Preferences
{
    public class PreferenceService : Collection<Page>, IRequiredService
    {
        private event EventHandler install_widget_adapters;
        public event EventHandler InstallWidgetAdapters {
            add { install_widget_adapters += value; }
            remove { install_widget_adapters -= value; }
        }

        public PreferenceService ()
        {
            // Pages (tabs)
            Page general = Add (new Page ("general", Catalog.GetString ("General"), 0));
            Add (new Page ("source-specific", Catalog.GetString ("Source Specific"), 1));
            Add (new Page ("extensions", Catalog.GetString ("Extensions"), 10));

            // General policies
            Section policies = general.Add (new Section ("policies", Catalog.GetString ("File Policies"), 0));

            policies.Add (new SchemaPreference<bool> (LibrarySchema.CopyOnImport,
                Catalog.GetString ("Co_py files to media folders when importing")));

            policies.Add (Banshee.Metadata.SaveTrackMetadataService.WriteMetadataEnabled);
            policies.Add (Banshee.Metadata.SaveTrackMetadataService.WriteRatingsAndPlayCountsEnabled);
            policies.Add (Banshee.Metadata.SaveTrackMetadataService.RenameEnabled);

            // Misc section
            var misc = new Section ("misc", Catalog.GetString ("Miscellaneous"), 20);
            general.Add (misc);

            var anon_data = misc.Add (new SchemaPreference<bool> (Banshee.Metrics.BansheeMetrics.EnableCollection,
                Catalog.GetString ("Improve Banshee by sending anonymous usage data"))
            );
            anon_data.ValueChanged += (o) => {
                if (Banshee.Metrics.BansheeMetrics.EnableCollection.Get ()) {
                    Banshee.Metrics.BansheeMetrics.Start ();
                } else {
                    Banshee.Metrics.BansheeMetrics.Stop ();
                }
            };
        }

        public void RequestWidgetAdapters ()
        {
            EventHandler handler = install_widget_adapters;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }

        string IService.ServiceName {
            get { return "PreferenceService"; }
        }
    }
}
