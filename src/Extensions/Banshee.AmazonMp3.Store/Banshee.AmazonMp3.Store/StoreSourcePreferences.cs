// 
// StoreSourcePreferences.cs
// 
// Author:
//   Aaron Bockover <abockover@novell.com>
// 
// Copyright 2010 Novell, Inc.
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

using Banshee.ServiceStack;
using Banshee.Preferences;
using Banshee.Configuration;

namespace Banshee.AmazonMp3.Store
{
    public class StoreSourcePreferences : IDisposable
    {
        private StoreSource source;
        private SourcePage source_page;
        private PreferenceBase country_pref;
        // private PreferenceBase logout_pref;

        public StoreSourcePreferences (StoreSource source)
        {
            var service = ServiceManager.Get<PreferenceService> ();
            if (service == null) {
                return;
            }

            this.source = source;

            service.InstallWidgetAdapters += OnPreferencesServiceInstallWidgetAdapters;

            source_page = new SourcePage (source);

            var country_section = source_page.Add (new Section ("country", Catalog.GetString ("Country"), 20));
            country_section.Add (country_pref = new SchemaPreference<string> (StoreCountry,
                null,
                Catalog.GetString ("Which Amazon MP3 storefront to use by default.")));

            /*var session_section = source_page.Add (new Section ("session", Catalog.GetString ("Session"), 30));
            session_section.Add (new SchemaPreference<bool> (PersistLogin,
                Catalog.GetString ("_Keep me logged in"),
                Catalog.GetString ("Keep any session cookies that Amazon MP3 may set across instances.")));
            session_section.Add (logout_pref = new VoidPreference ("log-out-button"));*/

            Hyena.Log.InformationFormat ("AmazonMP3 store redirect URL: {0}", RedirectUrl.Get ());
        }

        public void Dispose ()
        {
            var service = ServiceManager.Get<PreferenceService> ();
            if (service == null || source_page == null) {
                return;
            }

            service.InstallWidgetAdapters -= OnPreferencesServiceInstallWidgetAdapters;
            source_page.Dispose ();
            source_page = null;
        }

        private void OnPreferencesServiceInstallWidgetAdapters (object sender, EventArgs args)
        {
            /*if (source != null && source.Shell != null && source.Shell.StoreView != null) {
                logout_pref.DisplayWidget = new SignOutButton (source.Shell.StoreView);
            }*/

            var combo = new Banshee.Widgets.DictionaryComboBox<string> ();
            combo.Add (Catalog.GetString ("Automatic (Geo IP detection)"), "geo");
            combo.Add (null, null);
            // TODO uncomment this after string-freeze
            //combo.Add (Catalog.GetString ("Canada (amazon.ca)"), "CA");
            combo.Add (Catalog.GetString ("France (amazon.fr)"), "FR");
            combo.Add (Catalog.GetString ("Germany, Switzerland, Austria (amazon.de)"), "DE");
            combo.Add (Catalog.GetString ("Japan (amazon.co.jp)"), "JP");
            combo.Add (Catalog.GetString ("United Kingdom (amazon.co.uk)"), "UK");
            combo.Add (Catalog.GetString ("United States (amazon.com)"), "US");
            combo.RowSeparatorFunc = (model, iter) => model.GetValue (iter, 0) == null;
            combo.ActiveValue = StoreCountry.Get ();
            combo.Changed += (o, e) => {
                StoreCountry.Set (combo.ActiveValue);
                if (source != null && source.Shell != null && source.Shell.StoreView != null) {
                    source.Shell.StoreView.Country = combo.ActiveValue;
                    source.Shell.StoreView.GoHome ();
                }
            };
            country_pref.DisplayWidget = combo;
        }

        public string PreferencesPageId {
            get { return source_page == null ? null : source_page.Id; }
        }

        public static readonly SchemaEntry<string> StoreCountry = new SchemaEntry<string> (
            "plugins.amazonmp3store", "country",
            "geo",
            "Which store front to use (\"geo\" for auto-detect, US, UK, FR, DE, or JP", null);

        public static readonly SchemaEntry<bool> PersistLogin = new SchemaEntry<bool> (
            "plugins.amazonmp3store", "persist-login",
            true,
            "Persist the Amazon MP3 store account login across sessions (via cookies)", null);

        public static readonly SchemaEntry<string> RedirectUrl = new SchemaEntry<string> (
            "plugins.amazonmp3store", "redirect_url",
            StoreView.REDIRECT_URL,
            "The URL of the redirect server to use for the AmazonMP3 store.", null);
    }
}

