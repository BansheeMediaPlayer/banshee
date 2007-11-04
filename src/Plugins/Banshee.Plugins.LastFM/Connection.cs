/***************************************************************************
 *  Connection.cs
 *
 *  Copyright (C) 2007 Novell, Inc.
 *  Written by Gabriel Burt <gabriel.burt@gmail.com>
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Web;

using Mono.Gettext;

using Banshee.Base;
using Last.FM;

namespace Banshee.Plugins.LastFM
{
    public class ConnectionStateChangedArgs : EventArgs
    {
        public ConnectionState State;

        public ConnectionStateChangedArgs (ConnectionState state)
        {
            State = state;
        }
    }

    public enum ConnectionState {
        Disconnected,
        NoAccount,
        NoNetwork,
        InvalidAccount,
        Connecting,
        Connected
    };

	public class Connection 
	{
		public delegate void StateChangedHandler (Connection connection, ConnectionStateChangedArgs args);
		public event StateChangedHandler StateChanged;

		private ConnectionState state;
		private string session;
        private string username;
        private string md5_password;
		private string base_url;
		private string base_path;
		private string station;
		private string info_message;
		private bool subscriber;

		public bool Subscriber {
			get { return subscriber; }
		}

        public string Username {
            get { return username; }
        }

		public ConnectionState State {
			get { return state; }

            private set {
                if (value == state)
                    return;

                state = value;
                LogCore.Instance.PushDebug (String.Format ("Last.fm State Changed to {0}", state), null, false);
                StateChangedHandler handler = StateChanged;
                if (handler != null) {
                    handler (this, new ConnectionStateChangedArgs (state));
                }
            }
		}

        public bool Connected {
            get { return state == ConnectionState.Connected; }
        }

		public string Station {
			get { return station; }
		}

        private static Connection instance;
        public static Connection Instance {
            get {
                if (instance == null)
                    instance = new Connection ();
                return instance;
            }
        }

		private Connection () 
		{
            Initialize ();
            username = Last.FM.Account.Username;
            md5_password = Last.FM.Account.Md5Password;
            State = ConnectionState.Disconnected;
            Banshee.Base.NetworkDetect.Instance.StateChanged += HandleNetworkStateChanged;
            Last.FM.Account.LoginRequestFinished += HandleKeyringEvent;
            Last.FM.Account.LoginCommitFinished += HandleKeyringEvent;
        }

        private void Initialize ()
        {
            subscriber = false;
            base_url = base_path = session = station = info_message = null;
        }

        public void Dispose ()
        {
            Banshee.Base.NetworkDetect.Instance.StateChanged -= HandleNetworkStateChanged;
            Last.FM.Account.LoginRequestFinished -= HandleKeyringEvent;
            Last.FM.Account.LoginCommitFinished -= HandleKeyringEvent;
            instance = null;
        }

        private bool connect_requested = false;
        public void Connect ()
        {
            connect_requested = true;
            if (State == ConnectionState.Connecting || State == ConnectionState.Connected)
                return;

            if (username == null || md5_password == null) {
                State = ConnectionState.NoAccount;
                Last.FM.Account.RequestLogin ();
                return;
            }

            if (!Globals.Network.Connected) {
                State = ConnectionState.NoNetwork;
                return;
            }

            // Otherwise, we're good to try to connect
            State = ConnectionState.Connecting;
            Handshake ();
		}

        private void HandleKeyringEvent (AccountEventArgs args)
        {
            if (args.Success) {
                if (Account.Username != username || Account.Md5Password != md5_password) {
                    username = Last.FM.Account.Username;
                    md5_password = Last.FM.Account.Md5Password;

                    State = ConnectionState.Disconnected;
                    Connect ();
                }
            } else {
                LogCore.Instance.PushWarning ("Failed to Get Last.fm Account From Keyring", "", false);
            }
        }

        private void HandleNetworkStateChanged (object sender, NetworkStateChangedArgs args)
        {
            if (args.Connected) {
                if (State == ConnectionState.NoNetwork) {
                    Connect ();
                }
            } else {
                if (State == ConnectionState.Connected) {
                    Initialize ();
                    State = ConnectionState.NoNetwork;
                }
            }
        }

        private void Handshake ()
        {
            ThreadAssist.Spawn (delegate {
                try {
                    Stream stream = Get (String.Format (
                        "http://ws.audioscrobbler.com/radio/handshake.php?version={0}&platform={1}&username={2}&passwordmd5={3}&language={4}&session=324234",
                        "1.1.1",
                        "linux", // FIXME
                        username, md5_password,
                        "en" // FIXME
                    ));

                    // Set us as connecting, assuming the connection attempt wasn't changed out from under us
                    if (ParseHandshake (new StreamReader (stream).ReadToEnd ()) && session != null) {
                        State = ConnectionState.Connected;
                        LogCore.Instance.PushDebug (String.Format ("Logged into Last.fm as {0}", Username), null, false);
                        return;
                    }
                } catch (Exception e) {
                    LogCore.Instance.PushDebug ("Error in Last.fm Handshake", e.ToString (), false);
                }
                
                // Must not have parsed correctly
                Initialize ();
                if (State == ConnectionState.Connecting)
                    State = ConnectionState.Disconnected;
            });
        }


		private bool ParseHandshake (string content) 
		{
            LogCore.Instance.PushDebug ("Got Last.fm Handshake Response", content, false);
			string [] lines = content.Split (new Char[] {'\n'});
			foreach (string line in lines) {
				string [] opts = line.Split (new Char[] {'='});

				switch (opts[0].Trim().ToLower()) {
				case "session":
					if (opts[1].ToLower () == "failed") {
						session = null;
						State = ConnectionState.InvalidAccount;
                        LogCore.Instance.PushWarning (
                            Catalog.GetString ("Failed to Login to Last.fm"),
                            Catalog.GetString ("Either your username or password is invalid."),
                            false
                        );
						return false;
					}

					session = opts[1];
					break;

				case "stream_url":
					//stream_url = opts[1];
					break;

				case "subscriber":
					subscriber = (opts[1] != "0");
					break;

				case "base_url":
					base_url = opts[1];
					break;

				case "base_path":
					base_path = opts[1];
					break;
					
				case "info_message":
					info_message = opts[1];
					break;

				default:
					break;
				}
			}

			return true;
		}

		public string StationUrlFor (string station) 
		{
            return String.Format (
                "http://{0}{1}/adjust.php?session={2}&url={3}&lang=en",
                base_url, base_path, session, HttpUtility.UrlEncode (station)
            );
		}

        public string StationRefreshUrl ()
        {
            return String.Format (
                "http://{0}{1}/xspf.php?sk={2}&discovery=0&desktop=1.3.1.1",
                base_url, base_path, session
            );
        }

        public void SendCommand (string command)
        {
            Get (String.Format (
                "http://{0}{1}/control.php?session={2}&command={3}&debug=0",
                base_url, base_path, session, command
            ));
        }

        public Stream GetXspfStream (SafeUri uri)
        {
            return Get (uri, "application/xspf+xml");
        }

        public Stream Get (string uri)
        {
            return Get (new SafeUri (uri), null);
        }

        public Stream Get (SafeUri uri, string accept)
        {
            if(!Globals.Network.Connected) {
                throw new NetworkUnavailableException();
            }
        
            HttpWebRequest request = (HttpWebRequest) WebRequest.Create (uri.AbsoluteUri);
            if (accept != null) {
                request.Accept = accept;
            }
            request.UserAgent = Banshee.Web.Browser.UserAgent;
            request.Timeout = 10000;
            request.KeepAlive = false;
            request.AllowAutoRedirect = true;
            
            return ((HttpWebResponse) request.GetResponse ()).GetResponseStream ();
        }
	}

	public sealed class StringUtils {
		public static string StringToUTF8 (string s)
		{
			byte [] ba = (new UnicodeEncoding()).GetBytes(s);
			return System.Text.Encoding.UTF8.GetString (ba);
		}
    }
}
