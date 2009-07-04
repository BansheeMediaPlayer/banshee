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

namespace Lastfm
{
    public class Account
    {    
        public event EventHandler Updated;

        private string username;
        public string UserName {
            get { return username; }
            set { username = value; }
        }

        private string password; 
        public string Password {
            get { return password; }
            set { password = value; }
        }

        public string CryptedPassword {
            get {
                // Okay, so this will explode if someone has a raw text password 
                // that matches ^[a-f0-9]{32}$ ... likely? I hope not.
            
                if (password == null) {
                    return null;
                } else if (Hyena.CryptoUtil.IsMd5Encoded (password)) {
                    return password;
                }
                
                password = Hyena.CryptoUtil.Md5Encode (password);
                return password;
            }
            set { password = String.IsNullOrEmpty (value) ? null : value; }
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

        protected void OnUpdated ()
        {
            EventHandler handler = Updated;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }
    }
}

