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
using System.Text.RegularExpressions;
using System.Xml;
using System.Web;

using Mono.Gettext;

using Banshee.Base;
using Banshee.Playlists.Formats.Xspf;
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

    // Error codes returned when trying to adjust.php to a new station
    public enum StationError
    {
        None = 0,
        NotEnoughContent = 1,
        FewGroupMembers,
        FewFans,
        Unavailable,
        Subscribe,
        FewNeighbors,
        Offline,
        Unknown // not an official code, just the fall back
    }

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
        private bool connect_requested = false;

        private static Connection instance;
        private static Regex station_error_regex = new Regex ("error=(\\d+)", RegexOptions.Compiled);

        // Properties

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

        public static Connection Instance {
            get {
                if (instance == null)
                    instance = new Connection ();
                return instance;
            }
        }

        // Public Methods

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

        public void Dispose ()
        {
            Banshee.Base.NetworkDetect.Instance.StateChanged -= HandleNetworkStateChanged;
            Last.FM.Account.LoginRequestFinished -= HandleKeyringEvent;
            Last.FM.Account.LoginCommitFinished -= HandleKeyringEvent;
            instance = null;
        }

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

        public bool Love    (TrackInfo track) { return PostTrackRequest ("loveTrack", track); }
        public bool UnLove  (TrackInfo track) { return PostTrackRequest ("unLoveTrack", track); }
        public bool Ban     (TrackInfo track) { return PostTrackRequest ("banTrack", track); }
        public bool UnBan   (TrackInfo track) { return PostTrackRequest ("unBanTrack", track); }

        public StationError ChangeStationTo (string station)
        {
            lock (instance) {
                if (Station == station)
                    return StationError.None;

                try {
                    Stream stream = Get (StationUrlFor (station));
                    using (StreamReader strm = new StreamReader (stream)) {
                        string body = strm.ReadToEnd ();
                        if (body.IndexOf ("response=OK") == -1) {
                            Match match = station_error_regex.Match (body);
                            if (match.Success) {
                                int i = Int32.Parse (match.Groups[1].Value);
                                return (StationError) i;
                            } else {
                                return StationError.Unknown;
                            }
                        }
                    }

                    this.station = station;
                    return StationError.None;
                } catch (Exception e) {
                    Console.WriteLine (e.ToString ());
                    return StationError.Unknown;
                }
            }
        }

		public Playlist LoadPlaylistFor (string station) 
		{
            lock (instance) {
                if (station != Station)
                    return null;

                string url = StationRefreshUrl;
                Playlist pl = new Playlist ();
                Stream stream = null;
                Console.WriteLine ("StationSource Loading: {0}", url);
                try {
                    stream = Connection.Instance.GetXspfStream (new SafeUri (url));
                    pl.Load (stream);
                    LogCore.Instance.PushDebug (String.Format ("Adding {0} Tracks to Last.fm Station {1}", pl.TrackCount, station), null);
                } catch (System.Net.WebException e) {
                    LogCore.Instance.PushWarning ("Error Loading Last.fm Station", e.Message, false);
                    return null;
                } catch (Exception e) {
                    string body = "Unable to get body";
                    try {
                        using (StreamReader strm = new StreamReader (stream)) {
                            body = strm.ReadToEnd ();
                        }
                    } catch {}
                    LogCore.Instance.PushWarning (
                        "Error loading station",
                        String.Format ("Exception:\n{0}\n\nResponse Body:\n{1}", e.ToString (), body), false
                    );
                    return null;
                }

                return pl;
            }
		}

        // Private methods

        private void Initialize ()
        {
            subscriber = false;
            base_url = base_path = session = station = info_message = null;
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

				switch (opts[0].Trim ().ToLower ()) {
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

        // Basic HTTP helpers

        private HttpStatusCode Post (string uri, string body)
        {
            if (!Globals.Network.Connected) {
                throw new NetworkUnavailableException ();
            }
        
            HttpWebRequest request = (HttpWebRequest) WebRequest.Create (uri);
            request.UserAgent = Banshee.Web.Browser.UserAgent;
            request.Timeout = 10000;
            request.Method = "POST";
            request.KeepAlive = false;
            request.ContentLength = body.Length;

            using (StreamWriter writer = new StreamWriter (request.GetRequestStream ())) {
                writer.Write (body);
            }

            HttpWebResponse response = (HttpWebResponse) request.GetResponse ();
            using (Stream stream = response.GetResponseStream ()) {
                using (StreamReader reader = new StreamReader (stream)) {
                    Console.WriteLine ("Posted {0} got response {1}", body, reader.ReadToEnd ());
                }
            }
            return response.StatusCode;
        }

        private Stream GetXspfStream (SafeUri uri)
        {
            return Get (uri, "application/xspf+xml");
        }

        private Stream Get (string uri)
        {
            return Get (new SafeUri (uri), null);
        }

        private Stream Get (SafeUri uri, string accept)
        {
            if (!Globals.Network.Connected) {
                throw new NetworkUnavailableException ();
            }
        
            HttpWebRequest request = (HttpWebRequest) WebRequest.Create (uri.AbsoluteUri);
            if (accept != null) {
                request.Accept = accept;
            }
            request.UserAgent = Banshee.Web.Browser.UserAgent;
            request.Timeout = 10000;
            request.KeepAlive = false;
            request.AllowAutoRedirect = true;

            HttpWebResponse response = (HttpWebResponse) request.GetResponse ();
            return response.GetResponseStream ();
        }


        // URL generators for internal use
 
        private string XmlRpcUrl {
            get { return String.Format ("http://{0}/1.0/rw/xmlrpc.php", base_url); }
        }

		private string StationUrlFor (string station) 
		{
            return String.Format (
                "http://{0}{1}/adjust.php?session={2}&url={3}&lang=en",
                base_url, base_path, session, HttpUtility.UrlEncode (station)
            );
		}

        private string StationRefreshUrl {
            get {
                return String.Format (
                    "http://{0}{1}/xspf.php?sk={2}&discovery=0&desktop=1.3.1.1",
                    base_url, base_path, session
                );
            }
        }

        // Translated error message strings

        public static string ErrorMessageFor (StationError error)
        {
            switch (error) {
            case StationError.NotEnoughContent:  return Catalog.GetString ("There is not enough content to play this station.");
            case StationError.FewGroupMembers:   return Catalog.GetString ("This group does not have enough members for radio.");
            case StationError.FewFans:           return Catalog.GetString ("This artist does not have enough fans for radio.");
            case StationError.Unavailable:       return Catalog.GetString ("This station is not available.");
            case StationError.Subscribe:         return Catalog.GetString ("This station is only available to subscribers.");
            case StationError.FewNeighbors:      return Catalog.GetString ("There are not enough neighbours for this station.");
            case StationError.Offline:           return Catalog.GetString ("The streaming system is offline for maintenance, please try again later.");
            case StationError.Unknown:           return Catalog.GetString ("There was an unknown error.");
            }
            return String.Empty;
        }

        public static string MessageFor (ConnectionState state)
        {
            switch (state) {
            case ConnectionState.Disconnected:      return Catalog.GetString ("Not connected to Last.fm.");
            case ConnectionState.NoAccount:         return Catalog.GetString ("Need account details before can connect to Last.fm");
            case ConnectionState.NoNetwork:         return Catalog.GetString ("No network connection detected.");
            case ConnectionState.InvalidAccount:    return Catalog.GetString ("Last.fm username or password is invalid.");
            case ConnectionState.Connecting:        return Catalog.GetString ("Connecting to Last.fm.");
            case ConnectionState.Connected:         return Catalog.GetString ("Connected to Last.fm.");
            }
            return String.Empty;
        }

        // XML-RPC foo

        private bool PostTrackRequest (string method, TrackInfo track)
        {
            return PostXmlRpc (LastFMXmlRpcRequest (method).AddStringParams (track.Artist, track.Title));
        }

        private bool PostXmlRpc (LameXmlRpcRequest request)
        {
            if (State != ConnectionState.Connected)
                return false;

            return Post (XmlRpcUrl, request.ToString ()) == HttpStatusCode.OK;
        }

        private string UnixTime ()
        {
            return ((int) (DateTime.Now - new DateTime (1970, 1, 1)).TotalSeconds).ToString ();
        }

        private LameXmlRpcRequest LastFMXmlRpcRequest (string method)
        {
            string time = UnixTime ();
            string auth_hash = Last.FM.Account.Md5Encode (md5_password + time);
            return new LameXmlRpcRequest (method).AddStringParams (username, time, auth_hash);
        }

        protected class LameXmlRpcRequest
        {
            private StringBuilder sb = new StringBuilder ();
            public LameXmlRpcRequest (string method_name)
            {
                sb.Append ("<?xml version=\"1.0\" encoding=\"us-ascii\"?>\n");
                sb.Append ("<methodCall><methodName>");
                sb.Append (Encode (method_name));
                sb.Append ("</methodName>\n");
                sb.Append ("<params>\n");
            }

            public LameXmlRpcRequest AddStringParams (params string [] values)
            {
                foreach (string value in values)
                    AddStringParam (value);
                return this;
            }

            public LameXmlRpcRequest AddStringParam (string value)
            {
                sb.Append ("<param><value><string>");
                sb.Append (Encode (value));
                sb.Append ("</string></value></param>\n");
                return this;
            }

            public static string Encode (string val)
            {
                return HttpUtility.HtmlEncode (val);
            }

            private bool closed = false;
            public override string ToString ()
            {
                if (!closed) {
                    sb.Append ("</params>\n</methodCall>");
                    closed = true;
                }

                return sb.ToString ();
            }
        }
    }

	public sealed class StringUtils {
		public static string StringToUTF8 (string s)
		{
			byte [] ba = (new UnicodeEncoding ()).GetBytes (s);
			return System.Text.Encoding.UTF8.GetString (ba);
		}
    }
}
