//
// RadioConnection.cs
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
using System.Collections;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Web;
using System.Threading;

using Hyena;
using Hyena.Json;
using Mono.Unix;

using Media.Playlists.Xspf;

namespace Lastfm
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
        NotAuthorized,
        Connecting,
        Connected
    };

    // Error codes returned by the API methods
    public enum StationError
    {
        None = 0,
        NotUsed = 1,
        InvalidService,
        InvalidMethod,
        AuthenticationFailed,
        InvalidFormat,
        InvalidParameters,
        InvalidResource,
        TokenFailure,
        InvalidSessionKey,
        InvalidApiKey,
        ServiceOffline,
        SubscriptionError,
        InvalidSignature,
        TokenNotAuthorized,
        ExpiredToken,
        TemporarilyUnavailable,
        Login,
        SubscriptionRequired,
        NotEnoughContent = 20,
        NotEnoughMembers,
        NotEnoughFans,
        NotEnoughNeighbours,
        NoPeakRadio,
        RadioNotFound,
        ApiKeySuspended,
        Deprecated,
        RateLimitExceded = 29,
        Unknown = -1 // not an official code, just the fall back
    }

    public class RadioConnection
    {
        public delegate void StateChangedHandler (RadioConnection connection, ConnectionStateChangedArgs args);
        public event StateChangedHandler StateChanged;

        private ConnectionState state;
        private string info_message;
        private bool network_connected = false;

        public string InfoMessage {
            get { return info_message; }
        }

        public ConnectionState State {
            get { return state; }

            private set {
                if (value == state)
                    return;

                state = value;
                Log.Debug (String.Format ("Last.fm State Changed to {0}", state), null);
                StateChangedHandler handler = StateChanged;
                if (handler != null) {
                    handler (this, new ConnectionStateChangedArgs (state));
                }
            }
        }

        public bool Connected {
            get { return state == ConnectionState.Connected; }
        }

        private string station;
        public string Station {
            get { return station; }
        }

        internal RadioConnection ()
        {
            Initialize ();
            State = ConnectionState.Disconnected;

            LastfmCore.Account.Updated += HandleAccountUpdated;
        }

        public void Dispose ()
        {
            LastfmCore.Account.Updated -= HandleAccountUpdated;
        }

        public void Connect ()
        {
            if (State == ConnectionState.Connecting || State == ConnectionState.Connected)
                return;

            if (String.IsNullOrEmpty (LastfmCore.Account.UserName)) {
                State = ConnectionState.NoAccount;
                return;
            }

            if (String.IsNullOrEmpty (LastfmCore.Account.SessionKey)) {
                State = ConnectionState.NotAuthorized;
                return;
            }

            if (!network_connected) {
                State = ConnectionState.NoNetwork;
                return;
            }

            // Otherwise, we're good and consider ourselves connected
            State = ConnectionState.Connected;
        }

        public bool Love    (string artist, string title) { return PostTrackRequest ("love", artist, title); }
        public bool Ban     (string artist, string title) { return PostTrackRequest ("ban", artist, title); }

        public StationError ChangeStationTo (string station)
        {
            lock (this) {
                if (Station == station)
                    return StationError.None;

                try {

                    LastfmRequest radio_tune = new LastfmRequest ("radio.tune", RequestType.Write, ResponseFormat.Json);
                    radio_tune.AddParameter ("station", station);
                    radio_tune.Send ();
                    StationError error = radio_tune.GetError ();
                    if (error != StationError.None) {
                        return error;
                    }

                    this.station = station;
                    return StationError.None;
                } catch (Exception e) {
                    Log.Error (e);
                    return StationError.Unknown;
                }
            }
        }

        public Playlist LoadPlaylistFor (string station)
        {
            lock (this) {
                if (station != Station)
                    return null;

                Playlist pl = new Playlist ();
                Stream stream = null;
                LastfmRequest radio_playlist = new LastfmRequest ("radio.getPlaylist", RequestType.AuthenticatedRead, ResponseFormat.Raw);
                try {
                    radio_playlist.Send ();
                    stream = radio_playlist.GetResponseStream ();
                    pl.Load (stream);
                    Log.Debug (String.Format ("Adding {0} Tracks to Last.fm Station {1}", pl.TrackCount, station), null);
                } catch (System.Net.WebException e) {
                    Log.Warning ("Error Loading Last.fm Station", e.Message, false);
                    return null;
                } catch (Exception e) {
                    string body = null;
                    try {
                        using (StreamReader strm = new StreamReader (stream)) {
                            body = strm.ReadToEnd ();
                        }
                    } catch {}
                    Log.Warning (
                        "Error loading station",
                        String.Format ("Exception:\n{0}\n\nResponse:\n{1}", e.ToString (), body ?? "Unable to get response"), false
                    );
                    return null;
                }

                return pl;
            }
        }

        // Private methods

        private void Initialize ()
        {
            station = info_message = null;
        }

        private void HandleAccountUpdated (object o, EventArgs args)
        {
            State = ConnectionState.Disconnected;
            Connect ();
        }

        public void UpdateNetworkState (bool connected)
        {
            network_connected = connected;
            if (connected) {
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

        // Translated error message strings

        public static string ErrorMessageFor (StationError error)
        {
            switch (error) {
                case StationError.InvalidService:
                case StationError.InvalidMethod:
                    return Catalog.GetString ("This service does not exist.");
                case StationError.AuthenticationFailed:
                case StationError.SubscriptionError:
                case StationError.SubscriptionRequired:
                    return Catalog.GetString ("Stations are only available to paying Last.fm subscribers.");
                case StationError.InvalidFormat:
                    return Catalog.GetString ("This station is not available.");
                case StationError.InvalidParameters:
                    return Catalog.GetString ("The request is missing a required parameter.");
                case StationError.InvalidResource:
                    return Catalog.GetString ("The specified resource is invalid.");
                case StationError.TokenFailure:
                    return Catalog.GetString ("Server error, please try again later.");
                case StationError.InvalidSessionKey:
                    return Catalog.GetString ("Invalid authentication information, please re-authenticate.");
                case StationError.InvalidApiKey:
                    return Catalog.GetString ("The API key used by this application is invalid.");
                case StationError.ServiceOffline:
                    return Catalog.GetString ("The streaming system is offline for maintenance, please try again later.");
                case StationError.InvalidSignature:
                    return Catalog.GetString ("The method signature is invalid.");
                case StationError.TokenNotAuthorized:
                case StationError.ExpiredToken:
                    return Catalog.GetString ("You need to allow Banshee to access your Last.fm account.");
                case StationError.NotEnoughContent:
                    return Catalog.GetString ("There is not enough content to play this station.");
                case StationError.NotEnoughMembers:
                    return Catalog.GetString ("This group does not have enough members for radio.");
                case StationError.NotEnoughFans:
                    return Catalog.GetString ("This artist does not have enough fans for radio.");
                case StationError.NotEnoughNeighbours:
                    return Catalog.GetString ("There are not enough neighbours for this station.");
                case StationError.NoPeakRadio:
                    return Catalog.GetString ("This user is not allowed to listen to radio during peak usage");
                case StationError.RadioNotFound:
                    return Catalog.GetString ("Radio station not found");
                case StationError.ApiKeySuspended:
                    return Catalog.GetString ("This application is not allowed to make requests to the web services");
                case StationError.Deprecated:
                    return Catalog.GetString ("This type of request is no longer supported");
                case StationError.RateLimitExceded:
                    return Catalog.GetString ("Your IP has made too many requests in a short period, exceeding our API guidelines");
                case StationError.Unknown:
                    return Catalog.GetString ("There was an unknown error.");
            }
            return String.Empty;
        }

        public static string MessageFor (ConnectionState state)
        {
            switch (state) {
                case ConnectionState.Disconnected:
                    return Catalog.GetString ("Not connected to Last.fm.");
                case ConnectionState.NoAccount:
                    return Catalog.GetString ("Account details are needed before you can connect to Last.fm");
                case ConnectionState.NoNetwork:
                    return Catalog.GetString ("No network connection detected.");
                case ConnectionState.InvalidAccount:
                    return Catalog.GetString ("Last.fm username is invalid.");
                case ConnectionState.NotAuthorized:
                    return Catalog.GetString ("You need to allow Banshee to access your Last.fm account.");
                case ConnectionState.Connecting:
                    return Catalog.GetString ("Connecting to Last.fm.");
                case ConnectionState.Connected:
                    return Catalog.GetString ("Connected to Last.fm.");
            }
            return String.Empty;
        }

        private bool PostTrackRequest (string method, string artist, string title)
        {
            if (State != ConnectionState.Connected)
                return false;

            // track.love and track.ban do not return JSON
            LastfmRequest track_request = new LastfmRequest (String.Concat ("track.", method), RequestType.Write, ResponseFormat.Raw);
            track_request.AddParameter ("track", title);
            track_request.AddParameter ("artist", artist);
            track_request.Send ();

            return (track_request.GetError () == StationError.None);
        }
    }
}
