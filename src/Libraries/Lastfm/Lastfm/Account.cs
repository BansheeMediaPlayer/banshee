//
// Account.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2006-2008 Novell, Inc.
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
using System.Collections;
using System.Text;

using Hyena;

namespace Lastfm
{
    public class Account
    {
        public event EventHandler Updated;

        // Only used during the authentication process
        private string authentication_token;

        private string username;
        public string UserName {
            get { return username; }
            set { username = value; }
        }

        private string session_key;
        public string SessionKey {
            get { return session_key; }
            set { session_key = value; }
        }

        private bool subscriber;
        public bool Subscriber {
            get { return subscriber; }
            set { subscriber = value; }
        }

        private string scrobble_url;
        public string ScrobbleUrl {
            get { return scrobble_url; }
            set { scrobble_url = value; }
        }

        public string SignUpUrl {
            get { return "http://www.last.fm/join"; }
        }

        public void SignUp ()
        {
            Browser.Open (SignUpUrl);
        }

        public void VisitUserProfile (string username)
        {
            Browser.Open (String.Format ("http://last.fm/user/{0}", username));
        }

        public string HomePageUrl {
            get { return "http://www.last.fm/"; }
        }

        public void VisitHomePage ()
        {
            Browser.Open (HomePageUrl);
        }

        public virtual void Save ()
        {
            OnUpdated ();
        }

        public StationError RequestAuthorization ()
        {
            try {
                LastfmRequest get_token = new LastfmRequest ("auth.getToken", RequestType.Read, ResponseFormat.Json);
                get_token.Send ();

                var response = get_token.GetResponseObject ();
                object error_code;
                if (response.TryGetValue ("error", out error_code)) {
                    Log.WarningFormat ("Lastfm error {0} : {1}", (int)error_code, (string)response["message"]);
                    return (StationError) error_code;
                }

                authentication_token = (string)response["token"];
                Browser.Open (String.Format ("http://www.last.fm/api/auth?api_key={0}&token={1}", LastfmCore.ApiKey, authentication_token));

                return StationError.None;
            } catch (Exception e) {
                Log.Error ("Error in Lastfm.Account.RequestAuthorization", e);
                return StationError.Unknown;
            }
        }

        public StationError FetchSessionKey ()
        {
            if (authentication_token == null) {
                return StationError.TokenNotAuthorized;
            }

            try {
                LastfmRequest get_session = new LastfmRequest ("auth.getSession", RequestType.SessionRequest, ResponseFormat.Json);
                get_session.AddParameter ("token", authentication_token);
                get_session.Send ();
                var response = get_session.GetResponseObject ();
                object error_code;
                if (response.TryGetValue ("error", out error_code)) {
                    Log.WarningFormat ("Lastfm error {0} : {1}", (int)error_code, (string)response["message"]);
                    return (StationError) error_code;
                }

                var session = (Hyena.Json.JsonObject)response["session"];
                UserName = (string)session["name"];
                SessionKey = (string)session["key"];
                Subscriber = session["subscriber"].ToString ().Equals ("1");

                // The authentication token is only valid once, and for a limited time
                authentication_token = null;

                return StationError.None;
            } catch (Exception e) {
                Log.Error ("Error in Lastfm.Account.FetchSessionKey", e);
                return StationError.Unknown;
            }
        }

        protected void OnUpdated ()
        {
            EventHandler handler = Updated;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }
    }
}

