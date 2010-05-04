// 
// LastfmPreferences.cs
// 
// Author:
//   Aaron Bockover <abockover@novell.com>
// 
// Copyright (c) 2010 Novell, Inc.
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
using Mono.Unix;

using Lastfm.Gui;

using Banshee.ServiceStack;
using Banshee.Preferences;

using Banshee.Lastfm.Audioscrobbler;

using StationError = Lastfm.StationError;
using RadioConnection = Lastfm.RadioConnection;

namespace Banshee.Lastfm.Radio
{
    public class LastfmPreferences : IDisposable
    {
        private SourcePage source_page;
        private Section account_section;
        private Section prefs_section;
        private SchemaPreference<string> username_preference;
        private Preference<bool> reporting_preference;
        private LastfmSource source;
        private AudioscrobblerService scrobbler;

        private bool Authorized {
            get { return !String.IsNullOrEmpty (source.Account.SessionKey); }
        }

        private bool NeedAuthorization {
            get { return !String.IsNullOrEmpty (username_preference.Value) && !Authorized; }
        }

        public string PageId {
            get { return source_page.Id; }
        }

        public LastfmPreferences (LastfmSource source)
        {
            var service = ServiceManager.Get<PreferenceService> ();
            if (service == null) {
                return;
            }

            this.source = source;

            service.InstallWidgetAdapters += OnPreferencesServiceInstallWidgetAdapters;
            source_page = new Banshee.Preferences.SourcePage (source) {
                (account_section = new Section ("lastfm-account", Catalog.GetString ("Account"), 20) {
                    (username_preference = new SchemaPreference<string> (LastfmSource.LastUserSchema,
                        Catalog.GetString ("_Username")) {
                        ShowLabel = false
                    }),
                    new VoidPreference ("lastfm-signup")
                }),
                (prefs_section = new Section ("lastfm-settings", Catalog.GetString ("Preferences"), 30))
            };

            scrobbler = ServiceManager.Get<Banshee.Lastfm.Audioscrobbler.AudioscrobblerService> ();
            if (scrobbler != null) {
                reporting_preference = new Preference<bool> ("enable-song-reporting",
                    Catalog.GetString ("_Enable Song Reporting"), null, scrobbler.Enabled);
                reporting_preference.ValueChanged += root => scrobbler.Enabled = reporting_preference.Value;
                prefs_section.Add (reporting_preference);
            }
        }

        public void Dispose ()
        {
            PreferenceService service = ServiceManager.Get<PreferenceService> ();
            if (service == null || source_page == null) {
                return;
            }

            service.InstallWidgetAdapters -= OnPreferencesServiceInstallWidgetAdapters;
            source_page.Dispose ();
            source_page = null;
        }

        private enum SignInState {
            SignedOut,
            NeedAuthorization,
            Failed,
            SignedIn
        }

        private SignInState sign_in_state;
        private StationError last_sign_in_error;
        private Table sign_in_box;
        private LinkButton signup_button;
        private LinkButton profile_page_button;
        private bool need_authorization_checked = true; // state for at which button the auth arrows should point

        private void OnPreferencesServiceInstallWidgetAdapters (object sender, EventArgs args)
        {
            if (reporting_preference != null && scrobbler != null) {
                reporting_preference.Value = scrobbler.Enabled;
            }

            if (account_section == null) {
                return;
            }

            var align = new Alignment (0.0f, 0.0f, 1.0f, 1.0f) {
                LeftPadding = 20,
                RightPadding = 20
            };

            sign_in_box = new Table (0, 0, false) {
                ColumnSpacing = 6,
                RowSpacing = 2
            };

            align.Add (sign_in_box);
            align.Show ();

            username_preference.DisplayWidget = align;

            align = new Alignment (0.5f, 0.5f, 1.0f, 1.0f) {
                LeftPadding = 20,
                RightPadding = 20,
                TopPadding = 5
            };

            var button_box = new HBox () {
                Spacing = 6
            };

            button_box.PackStart (new Badge (source.Account), false, false, 0);

            signup_button = new Gtk.LinkButton (source.Account.SignUpUrl, Catalog.GetString ("Sign up for Last.fm"));
            signup_button.Xalign = 0f;
            button_box.PackStart (signup_button, false, false, 0);

            profile_page_button = new Gtk.LinkButton (String.Empty, Catalog.GetString ("Visit Your Last.fm Profile Page"));
            profile_page_button.Clicked += (o, e) => source.Account.VisitUserProfile (source.Account.UserName);
            profile_page_button.Xalign = 0f;
            button_box.PackStart (profile_page_button, false, false, 0);

            align.Add (button_box);
            align.ShowAll ();

            account_section["lastfm-signup"].DisplayWidget = align;

            GetSignInState ();
            BuildSignIn ();
        }

        private void SignOut ()
        {
            LastfmSource.LastSessionKeySchema.Set (String.Empty);
            username_preference.Value = String.Empty;
            source.Account.UserName = String.Empty;
            source.Account.SessionKey = null;
            source.Account.Save ();
        }

        private void OnSignInClicked (object o, EventArgs args)
        {
            if (sign_in_state != SignInState.NeedAuthorization) {
                sign_in_state = SignInState.NeedAuthorization;
            }

            need_authorization_checked = !need_authorization_checked;
            BuildSignIn ();

            source.Account.SessionKey = null;
            source.Account.RequestAuthorization ();
        }

        private void OnSignOutClicked (object o, EventArgs args)
        {
            SignOut ();
            GetSignInState ();
            BuildSignIn ();
        }

        private void OnFinishSignInClicked (object o, EventArgs args)
        {
            last_sign_in_error = source.Account.FetchSessionKey ();
            Hyena.Log.InformationFormat ("Last.fm authorization result = {0}", last_sign_in_error);
            if (last_sign_in_error == StationError.TokenNotAuthorized) {
                need_authorization_checked = true;
                GetSignInState ();
            } else if (last_sign_in_error == StationError.None) {
                LastfmSource.LastSessionKeySchema.Set (source.Account.SessionKey);
                source.Account.UserName = LastfmSource.LastUserSchema.Get ();
                source.Account.Save ();
                GetSignInState ();
            } else {
                SignOut ();
                sign_in_state = SignInState.Failed;
            }
            BuildSignIn ();
        }

        private void GetSignInState ()
        {
            if (Authorized) {
                sign_in_state = SignInState.SignedIn;
            } else if (NeedAuthorization) {
                sign_in_state = SignInState.NeedAuthorization;
            } else {
                sign_in_state = SignInState.SignedOut;
            }
        }

        private void BuildSignIn ()
        {
            signup_button.Visible = sign_in_state != SignInState.SignedIn;
            profile_page_button.Visible = sign_in_state == SignInState.SignedIn;

            var children = sign_in_box.Children;
            foreach (var child in children) {
                sign_in_box.Remove (child);
            }

            var oauth_explain = Catalog.GetString ("Open Last.fm in a browser, giving you the option to authorize Banshee to work with your account");

            switch (sign_in_state) {
                case SignInState.SignedOut:
                case SignInState.Failed:
                    need_authorization_checked = true;

                    var username_entry = new Entry () { Text = username_preference.Value };
                    username_entry.Changed += (o, e) => username_preference.Value = username_entry.Text;
                    username_entry.GrabFocus ();

                    var sign_in_button = new Button (Catalog.GetString ("Log in to Last.fm")) {
                        TooltipText = oauth_explain
                    };
                    sign_in_button.Clicked += OnSignInClicked;

                    sign_in_box.Attach (new Label (Catalog.GetString ("_Username")),
                        0, 1, 0, 1, AttachOptions.Shrink, AttachOptions.Shrink, 0, 0);
                    sign_in_box.Attach (username_entry,
                        1, 2, 0, 1, AttachOptions.Fill | AttachOptions.Expand, AttachOptions.Shrink, 0, 0);
                    sign_in_box.Attach (sign_in_button,
                        2, 3, 0, 1, AttachOptions.Shrink, AttachOptions.Shrink, 0, 0);
                    if (sign_in_state == SignInState.Failed) {
                        sign_in_box.Attach (new Hyena.Widgets.WrapLabel () {
                            Markup = String.Format ("<i>{0}</i>", GLib.Markup.EscapeText (
                                RadioConnection.ErrorMessageFor (last_sign_in_error)))
                        }, 1, 3, 1, 2, AttachOptions.Fill | AttachOptions.Expand, AttachOptions.Shrink, 0, 0);
                    }
                    break;
                case SignInState.NeedAuthorization:
                    sign_in_box.Attach (new Hyena.Widgets.WrapLabel () {
                        Markup = String.Format ("<i>{0}</i>", GLib.Markup.EscapeText (
                            Catalog.GetString ("You need to allow Banshee to access your Last.fm account."))),
                    }, 0, 1, 0, 2, AttachOptions.Fill | AttachOptions.Expand, AttachOptions.Shrink, 0, 0);

                    var r = need_authorization_checked ? 1u : 0u;
                    sign_in_box.Attach (new Image (Stock.GoForward, IconSize.Button),
                        1, 2, r, r + 1, AttachOptions.Shrink, AttachOptions.Shrink, 0, 0);

                    sign_in_box.Attach (new Image (Stock.GoBack, IconSize.Button),
                        3, 4, r, r + 1, AttachOptions.Shrink, AttachOptions.Shrink, 0, 0);

                    var check_auth_button = new Button (Catalog.GetString ("Finish Logging In"));
                    check_auth_button.Clicked += OnFinishSignInClicked;
                    sign_in_box.Attach (check_auth_button,
                        2, 3, 0, 1, AttachOptions.Fill, AttachOptions.Shrink, 0, 0);

                    var try_again_button = new Button (Catalog.GetString ("Try Again")) {
                        TooltipText = oauth_explain
                    };
                    try_again_button.Clicked += OnSignInClicked;
                    sign_in_box.Attach (try_again_button,
                        2, 3, 1, 2, AttachOptions.Fill, AttachOptions.Shrink, 0, 0);
                    break;
                case SignInState.SignedIn:
                    sign_in_box.Attach (new Hyena.Widgets.WrapLabel () {
                        Markup = String.Format (Catalog.GetString ("You are logged in to Last.fm as the user <i>{0}</i>."),
                            source.Account.UserName)
                    }, 0, 1, 0, 1, AttachOptions.Fill | AttachOptions.Expand, AttachOptions.Shrink, 0, 0);
                    var log_out_button = new Button (Catalog.GetString ("Log out of Last.fm"));
                    log_out_button.Clicked += OnSignOutClicked;
                    sign_in_box.Attach (log_out_button, 1, 2, 0, 1,
                        AttachOptions.Shrink, AttachOptions.Shrink, 0, 0);
                    break;
            }

            sign_in_box.ShowAll ();
        }
    }
}
