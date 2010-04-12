//
// LastfmSource.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2007-2008 Novell, Inc.
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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Mono.Unix;

using Lastfm;
using Lastfm.Gui;
using Lastfm.Data;
using Hyena.Data;

using Banshee.Base;
using Banshee.Collection;
using Banshee.Configuration;
using Banshee.Sources;
using Banshee.MediaEngine;
using Banshee.ServiceStack;
using Banshee.Networking;
using Banshee.Preferences;

using Banshee.Sources.Gui;

using Browser = Lastfm.Browser;

namespace Banshee.Lastfm.Radio
{
    public class LastfmSource : Source, IDisposable
    {
        private const string lastfm = "Last.fm";

        private RadioConnection connection;
        public RadioConnection Connection {
            get { return connection; }
        }

        private Account account;
        public Account Account {
            get { return account; }
        }

        private LastfmActions actions;
        public LastfmActions Actions {
            get { return actions; }
        }

        public LastfmSource () : base (lastfm, lastfm, 210, lastfm)
        {
            account = LastfmCore.Account;

            // We don't automatically connect to Last.fm, but load the last Last.fm
            // username we used so we can load the user's stations.
            if (account.UserName != null) {
                account.UserName = LastUserSchema.Get ();
                account.SessionKey = LastSessionKeySchema.Get ();
                account.Subscriber = LastIsSubscriberSchema.Get ();
            }

            if (LastfmCore.UserAgent == null) {
                LastfmCore.UserAgent = Banshee.Web.Browser.UserAgent;
            }

            Browser.Open = Banshee.Web.Browser.Open;

            connection = LastfmCore.Radio;
            Network network = ServiceManager.Get<Network> ();
            connection.UpdateNetworkState (network.Connected);
            network.StateChanged += HandleNetworkStateChanged;
            Connection.StateChanged += HandleConnectionStateChanged;
            UpdateUI ();

            Properties.SetString ("ActiveSourceUIResource", "ActiveSourceUI.xml");
            Properties.Set<bool> ("ActiveSourceUIResourcePropagate", true);
            Properties.SetString ("GtkActionPath", "/LastfmSourcePopup");
            Properties.SetString ("Icon.Name", "lastfm-audioscrobbler");
            Properties.SetString ("SortChildrenActionLabel", Catalog.GetString ("Sort Stations by"));
            Properties.Set<LastfmColumnController> ("TrackView.ColumnController", new LastfmColumnController ());

            // Initialize DataCore's UserAgent and CachePath
            DataCore.UserAgent = Banshee.Web.Browser.UserAgent;
            DataCore.CachePath = System.IO.Path.Combine (Hyena.Paths.ExtensionCacheRoot, "lastfm");

            // FIXME this is temporary until we split the GUI part from the non-GUI part
            Properties.Set<ISourceContents> ("Nereid.SourceContents", new LazyLoadSourceContents<LastfmSourceContents> ());
            Properties.Set<bool> ("Nereid.SourceContents.HeaderVisible", false);

            actions = new LastfmActions (this);
            InstallPreferences ();

            ServiceManager.SourceManager.AddSource (this);

            ServiceManager.Get<DBusCommandService> ().ArgumentPushed += OnCommandLineArgument;
        }

        public void Dispose ()
        {
            ServiceManager.Get<DBusCommandService> ().ArgumentPushed -= OnCommandLineArgument;
            Connection.StateChanged -= HandleConnectionStateChanged;
            ServiceManager.Get<Network> ().StateChanged -= HandleNetworkStateChanged;
            Connection.Dispose ();
            UninstallPreferences ();
            actions.Dispose ();

            actions = null;
            connection = null;
            account = null;
        }

        private void OnCommandLineArgument (string uri, object value, bool isFile)
        {
            if (!isFile || String.IsNullOrEmpty (uri)) {
                return;
            }

            // Handle lastfm:// URIs
            if (uri.StartsWith ("lastfm://")) {
                StationSource.CreateFromUrl (this, uri);
            }
        }

        // Order by the playCount of a station, then by inverted name
        public class PlayCountComparer : IComparer<Source>
        {
            public int Compare (Source sa, Source sb)
            {
                StationSource a = sa as StationSource;
                StationSource b = sb as StationSource;
                return a.PlayCount.CompareTo (b.PlayCount);
            }
        }

        private static SourceSortType[] sort_types = new SourceSortType[] {
            SortNameAscending,
            new SourceSortType (
                "LastfmTotalPlayCount",
                Catalog.GetString ("Total Play Count"),
                SortType.Descending, new PlayCountComparer ())
        };

        public override SourceSortType[] ChildSortTypes {
            get { return sort_types; }
        }

        public override SourceSortType DefaultChildSort {
            get { return SortNameAscending; }
        }

        private string last_username;
        private bool last_was_subscriber = false;
        public void SetUserName (string username)
        {
            if (username != last_username || last_was_subscriber != Account.Subscriber) {
                last_username = username;
                last_was_subscriber = Account.Subscriber;
                LastfmSource.LastUserSchema.Set (last_username);
                ClearChildSources ();
                PauseSorting ();
                foreach (StationSource child in StationSource.LoadAll (this, Account.UserName)) {
                    AddChildSource (child);
                }
                ResumeSorting ();
                SortChildSources ();
            }
        }

        public override void Activate ()
        {
            //InterfaceElements.ActionButtonBox.PackStart (add_button, false, false, 0);
            if (Connection.State == ConnectionState.Disconnected) {
                Connection.Connect ();
            }
        }

        public override bool? AutoExpand {
            get { return ExpandedSchema.Get (); }
        }

        public override bool Expanded {
            get { return ExpandedSchema.Get (); }
            set { ExpandedSchema.Set (value); }
        }

        public override bool CanActivate {
            get { return true; }
        }

        public override bool HasProperties {
            get { return false; }
        }

        private void HandleNetworkStateChanged (object o, NetworkStateChangedArgs args)
        {
            connection.UpdateNetworkState (args.Connected);
        }

        private void HandleConnectionStateChanged (object sender, ConnectionStateChangedArgs args)
        {
            UpdateUI ();
        }

        private void UpdateUI ()
        {
            bool have_user = Account.UserName != null;
            bool have_session_key = Account.SessionKey != null;

            if (have_session_key) {
                LastSessionKeySchema.Set (Account.SessionKey);
                LastIsSubscriberSchema.Set (Account.Subscriber);
            }

            if (have_user) {
                SetUserName (Account.UserName);
            } else {
                ClearChildSources ();
            }

            if (Connection.Connected) {
                HideStatus ();
            } else {
                SetStatus (RadioConnection.MessageFor (Connection.State), Connection.State != ConnectionState.Connecting, Connection.State);
            }

            OnUpdated ();
        }

        public override void SetStatus (string message, bool error)
        {
            base.SetStatus (message, error);
            SetStatus (status_message, this, error, ConnectionState.Connected);
        }

        public void SetStatus (string message, bool error, ConnectionState state)
        {
            base.SetStatus (message, error);
            SetStatus (status_message, this, error, state);
        }

        internal static void SetStatus (SourceMessage status_message, LastfmSource lastfm, bool error, ConnectionState state)
        {
            status_message.FreezeNotify ();
            if (error) {
                if (state == ConnectionState.NoAccount || state == ConnectionState.InvalidAccount || state == ConnectionState.NotAuthorized) {
                    status_message.AddAction (new MessageAction (Catalog.GetString ("Account Settings"),
                        delegate { lastfm.Actions.ShowLoginDialog (); }));
                }
                if (state == ConnectionState.NoAccount || state == ConnectionState.InvalidAccount) {
                    status_message.AddAction (new MessageAction (Catalog.GetString ("Join Last.fm"),
                        delegate { lastfm.Account.SignUp (); }));
                }
            }
            status_message.ThawNotify ();
        }

        private SourcePage pref_page;
        private Section pref_section;
        private SchemaPreference<string> user_pref;

        private void InstallPreferences ()
        {
            PreferenceService service = ServiceManager.Get<PreferenceService> ();
            if (service == null) {
                return;
            }

            service.InstallWidgetAdapters += OnPreferencesServiceInstallWidgetAdapters;
            pref_page = new Banshee.Preferences.SourcePage (this);
            pref_section = pref_page.Add (new Section ("lastfm-account", Catalog.GetString ("Account"), 20));
            pref_section.ShowLabel = false;

            user_pref = new SchemaPreference<string> (LastUserSchema, Catalog.GetString ("_Username"));
            pref_section.Add (user_pref);
            pref_section.Add (new VoidPreference ("lastfm-signup"));
        }

        private void UninstallPreferences ()
        {
            PreferenceService service = ServiceManager.Get<PreferenceService> ();
            if (service == null || pref_page == null) {
                return;
            }

            service.InstallWidgetAdapters -= OnPreferencesServiceInstallWidgetAdapters;
            pref_page.Dispose ();
            pref_page = null;
        }

        private bool Authorized { get { return !String.IsNullOrEmpty (account.SessionKey); } }
        private bool NeedAuth { get { return !String.IsNullOrEmpty (user_pref.Value) && !Authorized; } }

        private void OnPreferencesServiceInstallWidgetAdapters (object sender, EventArgs args)
        {
            if (pref_section == null) {
                return;
            }

            var user_entry = new Gtk.Entry (user_pref.Value ?? "");
            user_entry.Changed += (o, a) => { user_pref.Value = user_entry.Text; };

            var auth_button = new Gtk.Button (Authorized ? Catalog.GetString ("Authorized!") : Catalog.GetString ("Authorize..."));
            user_pref.ValueChanged += (s) => { auth_button.Sensitive = NeedAuth; };
            auth_button.Sensitive = NeedAuth;
            auth_button.TooltipText = Catalog.GetString ("Open Last.fm in a browser, giving you the option to authorize Banshee to work with your account");
            auth_button.Clicked += delegate {
                account.SessionKey = null;
                account.RequestAuthorization ();
            };

            var signup_button = new Gtk.LinkButton (account.SignUpUrl, Catalog.GetString ("Sign up for Last.fm"));
            signup_button.Xalign = 0f;

            var refresh_button = new Gtk.Button (new Gtk.Image (Gtk.Stock.Refresh, Gtk.IconSize.Button));
            user_pref.ValueChanged += (s) => { refresh_button.Sensitive = NeedAuth; };
            refresh_button.Sensitive = NeedAuth;
            refresh_button.TooltipText = Catalog.GetString ("Check if Banshee has been authorized");
            refresh_button.Clicked += delegate {
                if (String.IsNullOrEmpty (account.UserName) || account.SessionKey == null) {
                    account.UserName = LastUserSchema.Get ();
                    account.FetchSessionKey ();
                    account.Save ();
                    if (account.SessionKey != null) {
                        LastSessionKeySchema.Set (account.SessionKey);
                    }
                }

                auth_button.Sensitive = refresh_button.Sensitive = NeedAuth;
                auth_button.Label = Authorized
                    ? Catalog.GetString ("Authorized!")
                    : Catalog.GetString ("Authorize...");
            };

            var auth_box = new Gtk.HBox () { Spacing = 6 };
            auth_box.PackStart (user_entry, true, true, 0);
            auth_box.PackStart (auth_button, false, false, 0);
            auth_box.PackStart (refresh_button, false, false, 0);
            auth_box.ShowAll ();

            signup_button.Visible = String.IsNullOrEmpty (user_pref.Value);

            var button_box = new Gtk.HBox () { Spacing = 6 };
            button_box.PackStart (new Badge (account) { Visible = true}, false, false, 0);
            button_box.PackStart (signup_button, true, true, 0);

            user_pref.DisplayWidget = auth_box;
            pref_section["lastfm-signup"].DisplayWidget = button_box;
        }

        public override string PreferencesPageId {
            get { return pref_page.Id; }
        }

        public static readonly SchemaEntry<bool> EnabledSchema = new SchemaEntry<bool> (
            "plugins.lastfm", "enabled", false, "Extension enabled", "Last.fm extension enabled"
        );

        public static readonly SchemaEntry<string> LastUserSchema = new SchemaEntry<string> (
            "plugins.lastfm", "username", "", "Last.fm user", "Last.fm username"
        );

        public static readonly SchemaEntry<string> LastSessionKeySchema = new SchemaEntry<string> (
            "plugins.lastfm", "session_key", "", "Last.fm session key", "Last.fm session key used in authenticated calls"
        );

        public static readonly SchemaEntry<bool> LastIsSubscriberSchema = new SchemaEntry<bool> (
            "plugins.lastfm", "subscriber", false, "User is Last.fm subscriber", "User is Last.fm subscriber"
        );

        public static readonly SchemaEntry<bool> ExpandedSchema = new SchemaEntry<bool> (
            "plugins.lastfm", "expanded", false, "Last.fm expanded", "Last.fm expanded"
        );
    }
}
