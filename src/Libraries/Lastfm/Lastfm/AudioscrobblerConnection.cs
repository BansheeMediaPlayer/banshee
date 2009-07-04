//
// AudioscrobblerConnection.cs
//
// Author:
//   Chris Toshok <toshok@ximian.com>
//   Alexander Hixon <hixon.alexander@mediati.org>
//
// Copyright (C) 2005-2008 Novell, Inc.
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
using System.Net;
using System.Text;
using System.Timers;
using System.Security.Cryptography;
using Mono.Security.Cryptography;
using System.Web;

using Hyena;
using Mono.Unix;

namespace Lastfm
{
    public class AudioscrobblerConnection
    {
        private enum State {
            Idle,
            NeedHandshake,
            NeedTransmit,
            WaitingForRequestStream,
            WaitingForHandshakeResp,
            WaitingForResponse
        };

        private const int TICK_INTERVAL = 2000; /* 2 seconds */
        private const int FAILURE_LOG_MINUTES = 5; /* 5 minute delay on logging failure to upload information */
        private const int RETRY_SECONDS = 60; /* 60 second delay for transmission retries */
        private const int MAX_RETRY_SECONDS = 7200; /* 2 hours, as defined in the last.fm protocol */
        private const int TIME_OUT = 5000; /* 5 seconds timeout for webrequests */
        private const string CLIENT_ID = "bsh";
        private const string CLIENT_VERSION = "0.1";
        private const string SCROBBLER_URL = "http://post.audioscrobbler.com/";
        private const string SCROBBLER_VERSION = "1.2";

        private string post_url;
        private string session_id = null;
        private string now_playing_url;
        
        private bool connected = false; /* if we're connected to network or not */
        public bool Connected {
            get { return connected; }
        }
        
        private bool started = false; /* engine has started and was/is connected to AS */
        public bool Started {
            get { return started; }
        }

        private System.Timers.Timer timer;
        private DateTime next_interval;
        private DateTime last_upload_failed_logged;

        private IQueue queue;
        
        private int hard_failures = 0;
        private int hard_failure_retry_sec = 60;
        
        private HttpWebRequest now_playing_post;
        private bool now_playing_started;
        private string current_now_playing_data;
        private HttpWebRequest current_web_req;
        private IAsyncResult current_async_result;
        private State state;
        
        internal AudioscrobblerConnection (IQueue queue)
        {
            LastfmCore.Account.Updated += AccountUpdated;
            
            state = State.Idle;
            this.queue = queue;
        }
        
        private void AccountUpdated (object o, EventArgs args)
        {
            Stop ();
            session_id = null;
            Start ();
        }
        
        public void UpdateNetworkState (bool connected)
        {
            Log.DebugFormat ("Audioscrobbler state: {0}", connected ? "connected" : "disconnected");
            this.connected = connected;
        }

        public void Start ()
        {            
            if (started) {
                return;
            }
            
            started = true;
            hard_failures = 0;
            queue.TrackAdded += delegate(object o, EventArgs args) {
                StartTransitionHandler ();
            };
            
            queue.Load ();
            StartTransitionHandler ();
        }

        private void StartTransitionHandler ()
        {
            if (!started) {
                // Don't run if we're not actually started.
                return;
            }
            
            if (timer == null) {
                timer = new System.Timers.Timer ();
                timer.Interval = TICK_INTERVAL;
                timer.AutoReset = true;
                timer.Elapsed += new ElapsedEventHandler (StateTransitionHandler);
                
                timer.Start ();
            } else if (!timer.Enabled) {
                timer.Start ();
            }
        }

        public void Stop ()
        {
            StopTransitionHandler ();

            if (current_web_req != null) {
                current_web_req.Abort ();
            }

            queue.Save ();
            
            started = false;
        }

        private void StopTransitionHandler ()
        {
            if (timer != null) {
                timer.Stop ();
            }
        }

        private void StateTransitionHandler (object o, ElapsedEventArgs e)
        {
            // if we're not connected, don't bother doing anything involving the network.
            if (!connected) {
                return;
            }
                        
            if ((state == State.Idle || state == State.NeedTransmit) && hard_failures > 2) {
                state = State.NeedHandshake;
                hard_failures = 0;
            }
            
            // and address changes in our engine state
            switch (state) {
            case State.Idle:
                if (LastfmCore.Account.UserName != null &&
                    LastfmCore.Account.CryptedPassword != null && session_id == null) {
                    
                    state = State.NeedHandshake;
                } else {
                    if (queue.Count > 0 && session_id != null) {
                        state = State.NeedTransmit;
                    } else if (current_now_playing_data != null && session_id != null) {
                        // Now playing info needs to be sent
                        NowPlaying (current_now_playing_data);
                    } else {
                        StopTransitionHandler ();
                    }
                }
                
                break;
            case State.NeedHandshake:
                if (DateTime.Now > next_interval) {
                    Handshake ();
                }
                
                break;
            case State.NeedTransmit:
                if (DateTime.Now > next_interval) {
                    TransmitQueue ();
                }
                break;
            case State.WaitingForResponse:
            case State.WaitingForRequestStream:
            case State.WaitingForHandshakeResp:
                // nothing here
                break;
            }
        }

        //
        // Async code for transmitting the current queue of tracks
        //
        internal class TransmitState
        {
            public StringBuilder StringBuilder;
            public int Count;
        }

        private void TransmitQueue ()
        {
            int num_tracks_transmitted;

            // save here in case we're interrupted before we complete
            // the request.  we save it again when we get an OK back
            // from the server
            queue.Save ();

            next_interval = DateTime.MinValue;

            if (post_url == null || !connected) {
                return;
            }

            StringBuilder sb = new StringBuilder ();

            sb.AppendFormat ("s={0}", session_id);

            sb.Append (queue.GetTransmitInfo (out num_tracks_transmitted));

            current_web_req = (HttpWebRequest) WebRequest.Create (post_url);
            current_web_req.UserAgent = LastfmCore.UserAgent;
            current_web_req.Method = "POST";
            current_web_req.ContentType = "application/x-www-form-urlencoded";
            current_web_req.ContentLength = sb.Length;
            
            //Console.WriteLine ("Sending {0} ({1} bytes) to {2}", sb.ToString (), sb.Length, post_url);

            TransmitState ts = new TransmitState ();
            ts.Count = num_tracks_transmitted;
            ts.StringBuilder = sb;

            state = State.WaitingForRequestStream;
            current_async_result = current_web_req.BeginGetRequestStream (TransmitGetRequestStream, ts);
            if (!(current_async_result.AsyncWaitHandle.WaitOne (TIME_OUT, false))) {
                Log.Warning ("Audioscrobbler upload failed", "The request timed out and was aborted", false);
                next_interval = DateTime.Now + new TimeSpan (0, 0, RETRY_SECONDS);
                hard_failures++;
                state = State.Idle;
                
                current_web_req.Abort();
            }
        }

        private void TransmitGetRequestStream (IAsyncResult ar)
        {
            Stream stream;

            try {
                stream = current_web_req.EndGetRequestStream (ar);
            } catch (Exception e) {
                Log.Exception ("Failed to get the request stream", e);
                state = State.Idle;
                next_interval = DateTime.Now + new TimeSpan (0, 0, RETRY_SECONDS);
                return;
            }

            TransmitState ts = (TransmitState) ar.AsyncState;
            StringBuilder sb = ts.StringBuilder;

            StreamWriter writer = new StreamWriter (stream);
            writer.Write (sb.ToString ());
            writer.Close ();

            state = State.WaitingForResponse;
            current_async_result = current_web_req.BeginGetResponse (TransmitGetResponse, ts);
            if (current_async_result == null) {
                next_interval = DateTime.Now + new TimeSpan (0, 0, RETRY_SECONDS);
                hard_failures++;
                state = State.Idle;
            }
        }

        private void TransmitGetResponse (IAsyncResult ar)
        {
            WebResponse resp;

            try {
                resp = current_web_req.EndGetResponse (ar);
            }
            catch (Exception e) {
                Log.Warning (String.Format("Failed to get the response: {0}", e), false);

                state = State.Idle;
                next_interval = DateTime.Now + new TimeSpan (0, 0, RETRY_SECONDS);
                return;
            }

            TransmitState ts = (TransmitState) ar.AsyncState;

            Stream s = resp.GetResponseStream ();

            StreamReader sr = new StreamReader (s, Encoding.UTF8);

            string line;
            line = sr.ReadLine ();
            
            DateTime now = DateTime.Now;
            if (line.StartsWith ("FAILED")) {
                if (now - last_upload_failed_logged > TimeSpan.FromMinutes(FAILURE_LOG_MINUTES)) {
                    Log.Warning ("Audioscrobbler upload failed", line.Substring ("FAILED".Length).Trim(), false);
                    last_upload_failed_logged = now;
                }
                
                // retransmit the queue on the next interval
                hard_failures++;
                state = State.NeedTransmit;
            }
            else if (line.StartsWith ("BADSESSION")) {
                if (now - last_upload_failed_logged > TimeSpan.FromMinutes(FAILURE_LOG_MINUTES)) {
                    Log.Warning ("Audioscrobbler upload failed", "session ID sent was invalid", false);
                    last_upload_failed_logged = now;
                }
                
                // attempt to re-handshake (and retransmit) on the next interval
                session_id = null;
                next_interval = DateTime.Now + new TimeSpan (0, 0, RETRY_SECONDS);
                state = State.NeedHandshake;
                return;
            } else if (line.StartsWith ("OK")) {
                /* if we've previously logged failures, be nice and log the successful upload. */
                if (last_upload_failed_logged != DateTime.MinValue) {
                    Log.Debug ("Audioscrobbler upload succeeded");
                    last_upload_failed_logged = DateTime.MinValue;
                }
                
                hard_failures = 0;
                
                // we succeeded, pop the elements off our queue
                queue.RemoveRange (0, ts.Count);
                queue.Save ();
                
                state = State.Idle;
            } else {
                if (now - last_upload_failed_logged > TimeSpan.FromMinutes(FAILURE_LOG_MINUTES)) {
                    Log.Warning ("Audioscrobbler upload failed", String.Format ("Unrecognized response: {0}", line), false);
                    last_upload_failed_logged = now;
                }
                
                state = State.Idle;
            }
        }

        //
        // Async code for handshaking
        //
        
        private string UnixTime ()
        {
            return ((int) (DateTime.UtcNow - new DateTime (1970, 1, 1)).TotalSeconds).ToString ();
        }
        
        private void Handshake ()
        {
            string timestamp = UnixTime();
            string security_token = Hyena.CryptoUtil.Md5Encode
                (LastfmCore.Account.CryptedPassword + timestamp);
            
            string api_url = LastfmCore.Account.ScrobbleUrl;
            if (String.IsNullOrEmpty (api_url)) {
                api_url = SCROBBLER_URL;
            } else {
                Log.DebugFormat ("Scrobbling to non-standard API URL: {0}", api_url);
            }

            string uri = String.Format ("{0}?hs=true&p={1}&c={2}&v={3}&u={4}&t={5}&a={6}",
                                        api_url,
                                        SCROBBLER_VERSION,
                                        CLIENT_ID, CLIENT_VERSION,
                                        HttpUtility.UrlEncode (LastfmCore.Account.UserName),
                                        timestamp,
                                        security_token);

            current_web_req = (HttpWebRequest) WebRequest.Create (uri);

            state = State.WaitingForHandshakeResp;
            current_async_result = current_web_req.BeginGetResponse (HandshakeGetResponse, null);
            if (current_async_result == null) {
                next_interval = DateTime.Now + new TimeSpan (0, 0, hard_failure_retry_sec);
                hard_failures++;
                if (hard_failure_retry_sec < MAX_RETRY_SECONDS)
                    hard_failure_retry_sec *= 2;
                state = State.NeedHandshake;
            }
        }

        private void HandshakeGetResponse (IAsyncResult ar)
        {
            bool success = false;
            bool hard_failure = false;
            WebResponse resp;

            try {
                resp = current_web_req.EndGetResponse (ar);
            }
            catch (Exception e) {
                Log.Warning ("Failed to handshake: {0}", e.ToString (), false);

                // back off for a time before trying again
                state = State.Idle;
                next_interval = DateTime.Now + new TimeSpan (0, 0, RETRY_SECONDS);
                return;
            }

            Stream s = resp.GetResponseStream ();

            StreamReader sr = new StreamReader (s, Encoding.UTF8);

            string line;

            line = sr.ReadLine ();
            if (line.StartsWith ("BANNED")) {
                Log.Warning ("Audioscrobbler sign-on failed", "Player is banned", false);
                                   
            } else if (line.StartsWith ("BADAUTH")) {
                Log.Warning ("Audioscrobbler sign-on failed", Catalog.GetString ("Last.fm username or password is invalid."));
                LastfmCore.Account.CryptedPassword = null;
            } else if (line.StartsWith ("BADTIME")) {
                Log.Warning ("Audioscrobbler sign-on failed", 
                                                  "timestamp provided was not close enough to the current time", false);
            } else if (line.StartsWith ("FAILED")) {
                Log.Warning ("Audioscrobbler sign-on failed",
                    String.Format ("Temporary server failure: {0}", line.Substring ("FAILED".Length).Trim ()), false);
                hard_failure = true;
            } else if (line.StartsWith ("OK")) {
                success = true;
            } else {
                Log.Error ("Audioscrobbler sign-on failed", String.Format ("Unknown error: {0}", line.Trim()));
                hard_failure = true;
            }
            
            if (success == true) {
                Log.Debug ("Audioscrobbler sign-on succeeded", "Session ID received"); 
                session_id = sr.ReadLine ().Trim ();
                now_playing_url = sr.ReadLine ().Trim ();
                post_url = sr.ReadLine ().Trim ();
                
                hard_failures = 0;
                hard_failure_retry_sec = 60;
            } else {
                if (hard_failure == true) {
                    next_interval = DateTime.Now + new TimeSpan (0, 0, hard_failure_retry_sec);
                    hard_failures++;
                    if (hard_failure_retry_sec < MAX_RETRY_SECONDS)
                        hard_failure_retry_sec *= 2;
                }
            }

            state = State.Idle;
        }
        
        //
        // Async code for now playing
        
        public void NowPlaying (string artist, string title, string album, double duration,
                                int tracknum)
        {
            NowPlaying (artist, title, album, duration, tracknum, "");
        }
        
        public void NowPlaying (string artist, string title, string album, double duration,
                                int tracknum, string mbrainzid)
        {
            if (String.IsNullOrEmpty(artist) || String.IsNullOrEmpty(title) || !connected) {
                return;
            }
        
            string str_track_number = String.Empty;
            if (tracknum != 0) {
                str_track_number = tracknum.ToString();
            }
            
            // Fall back to prefixing the URL with a # in case we haven't actually
            // authenticated yet. We replace it with the now_playing_url and session_id
            // later on in NowPlaying(uri).
            string dataprefix = "#";
            
            if (session_id != null) {
                dataprefix = String.Format ("s={0}", session_id);
            }
            
            string data = String.Format ("{0}&a={1}&t={2}&b={3}&l={4}&n={5}&m={6}",
                                        dataprefix,
                                        HttpUtility.UrlEncode(artist),
                                        HttpUtility.UrlEncode(title),
                                        HttpUtility.UrlEncode(album),
                                        duration.ToString("F0"),
                                        str_track_number,
                                        mbrainzid);

            Console.WriteLine ("Submitting via non-uri handler.");            
            NowPlaying (data);
        }
        
        private void NowPlaying (string data)
        {            
            if (now_playing_started) {
                return;
            }
            
            // If the URI begins with #, then we know the URI was created before we
            // had actually authenticated. So, because we didn't know the session_id and
            // now_playing_url previously, we should now, so we put that in and create our
            // new URI.
            if (data.StartsWith ("#") && session_id != null) {
                data = String.Format ("s={0}{1}",
                                      session_id,
                                      data.Substring (1));
            }
            
            current_now_playing_data = data;
            
            if (session_id == null) {
                // Go connect - we'll come back later in main timer loop.
                Start ();
                return;
            }

            try {
                now_playing_post = (HttpWebRequest) WebRequest.Create (now_playing_url);
                now_playing_post.UserAgent = LastfmCore.UserAgent;
                now_playing_post.Method = "POST";
                now_playing_post.ContentType = "application/x-www-form-urlencoded";

                if (state == State.Idle) {
                    // Don't actually POST it until we're idle (that is, we
                    // probably have stuff queued which will reset the Now
                    // Playing if we send them first).
                    now_playing_started = true;
                    now_playing_post.BeginGetRequestStream (NowPlayingGetRequestStream, data);
                }
            } catch (Exception ex) {
                Log.Warning ("Audioscrobbler NowPlaying failed",
                                  String.Format ("Exception while creating request: {0}", ex), false);
                
                // Reset current_now_playing_data if it was the problem.
                current_now_playing_data = null;
            }
        }

        private void NowPlayingGetRequestStream (IAsyncResult ar)
        {
            try {
                string data = ar.AsyncState as string;
                ASCIIEncoding encoding = new ASCIIEncoding ();
                byte[] data_bytes = encoding.GetBytes (data);
                Stream request_stream = now_playing_post.EndGetRequestStream (ar);
                request_stream.Write (data_bytes, 0, data_bytes.Length);
                request_stream.Close ();

                now_playing_post.BeginGetResponse (NowPlayingGetResponse, null);
            } catch (Exception e) {
                Log.Exception (e);
            }
        }

        private void NowPlayingGetResponse (IAsyncResult ar)
        {
            try {
                WebResponse my_resp = now_playing_post.EndGetResponse (ar);

                Stream s = my_resp.GetResponseStream ();
                StreamReader sr = new StreamReader (s, Encoding.UTF8);

                string line = sr.ReadLine ();
                if (line == null) {
                    Log.Warning ("Audioscrobbler NowPlaying failed", "No response", false);
                }
                
                if (line.StartsWith ("BADSESSION")) {
                    Log.Warning ("Audioscrobbler NowPlaying failed", "Session ID sent was invalid", false);
                    // attempt to re-handshake on the next interval
                    session_id = null;
                    next_interval = DateTime.Now + new TimeSpan (0, 0, RETRY_SECONDS);
                    state = State.NeedHandshake;
                    StartTransitionHandler ();
                    return;
                } else if (line.StartsWith ("OK")) {
                    // NowPlaying submitted  
                    Log.DebugFormat ("Submitted NowPlaying track to Audioscrobbler");
                    now_playing_started = false;
                    now_playing_post = null;
                    current_now_playing_data = null;
                    return;
                } else {
                    Log.Warning ("Audioscrobbler NowPlaying failed", "Unexpected or no response", false);       
                }
            }
            catch (Exception e) {
                Log.Warning ("Audioscrobbler NowPlaying failed", 
                    String.Format("Failed to post NowPlaying: {0}", e), false);
            }
            
            // NowPlaying error/success is non-crutial.
            hard_failures++;
            if (hard_failures < 3) {
                NowPlaying (current_now_playing_data);
            } else {
                // Give up - NowPlaying status information is non-critical.
                current_now_playing_data = null;
                now_playing_started = false;
                now_playing_post = null;
            }
        }
    }
}
