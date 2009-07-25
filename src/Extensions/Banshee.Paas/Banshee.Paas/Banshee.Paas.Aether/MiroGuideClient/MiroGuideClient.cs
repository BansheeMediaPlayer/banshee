// 
// MiroGuideClient.cs
//  
// Author:
//       Mike Urbanski <michael.c.urbanski@gmail.com>
// 
// Copyright (c) 2009 Michael C. Urbanski
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
using System.IO;
using System.Net;
using System.Text;

using System.Collections;
using System.Collections.Generic;

using Hyena;
using Migo2.Async;

namespace Banshee.Paas.Aether
{
    public sealed class MiroGuideClient : AetherClient
    {
        enum Method {
            Authenticate,
            RegisterClient,
            RequestDeltas
        }

        private int timeout;
        private string client_id;
        private string session_id;
        private Uri aether_service_uri;

        private string username;
        private string password_hash;

        AsyncStateManager asm;
        
        private HttpWebRequest request;
        private Dictionary<Method, EventHandler<AetherAsyncCompletedEventArgs>> event_table;

        public event EventHandler<AetherAsyncCompletedEventArgs> AuthenticationCompleted {
            add    { AddHandler    (Method.Authenticate, value); }
            remove { RemoveHandler (Method.Authenticate, value); }
        }
        
        public event EventHandler<AetherAsyncCompletedEventArgs> RegisterClientCompleted {
            add    { AddHandler    (Method.RegisterClient, value); }
            remove { RemoveHandler (Method.RegisterClient, value); }
        }
        
        public event EventHandler<AetherAsyncCompletedEventArgs> RequestDeltasCompleted {
            add    { AddHandler    (Method.RequestDeltas, value); }
            remove { RemoveHandler (Method.RequestDeltas, value); }
        }

        public string ClientID { 
            get {
                if (String.IsNullOrEmpty (client_id)) {
                    throw new InvalidOperationException ("ClientID is not set.");
                }
                
                return client_id;
            }
            
            set { client_id = value; }
        }

        public ICredentials Credentials { get; set; }

        public string PasswordHash {
            get { return password_hash; }
            set { password_hash = value; }
        }

        public string ServiceUri {
            get { return aether_service_uri.AbsoluteUri; }
            
            set { 
                Uri tmp = new Uri (value.Trim ().TrimEnd ('/'));
                
                if (tmp.Scheme != "http" && tmp.Scheme != "https") {
                    throw new UriFormatException ("Scheme must be either http or https.");
                }

                aether_service_uri = tmp;
            }
        }
        
        public string SessionID { 
            get {
                if (String.IsNullOrEmpty (session_id)) {
                    throw new InvalidOperationException ("SessionID is not set.");
                }
                    
                return session_id;
            }
            
            set { session_id = value; }
        }
    
        public int Timeout {
            get { return timeout; }
            set {
                if (value < 0) {
                    throw new ArgumentOutOfRangeException ("Timeout", "Must be greater than 0.");
                }

                timeout = value;
            }
        }
        
        public string UserAgent { get; set; }

        public string Username {
            get { return username; }
            set { username = value; }
        }

        public MiroGuideClient ()
        {
            asm = new AsyncStateManager ();
            event_table = new Dictionary<Method, EventHandler<AetherAsyncCompletedEventArgs>> ();
        }

        public void CancelAsync ()
        {
            if (asm.SetCancelled ()) {
                Abort ();   
            }
        }

        public void AuthenticateAsync ()
        {
            AuthenticateAsync (null);
        }        
        
        private void AuthenticateAsync (Method callingMethod, Action continuation)
        {
            CryOutThroughTheAetherAsync (
                "/aether/auth/", HttpMethod.POST, 
                System.Web.HttpUtility.HtmlEncode (
                    String.Format ("username={0}&password_hash={1}", username, password_hash)
                ), ServiceFlags.None, callingMethod, userState
            );
        }
        
        public void RegisterClientAsync ()
        {
            RegisterClientAsync (null);
        }
        
        public void RegisterClientAsync (object userState)
        {
            CryOutThroughTheAetherAsync (
                "/aether/register", HttpMethod.GET, null,
                ServiceFlags.RequireAuth, Method.RegisterClient, userState
            );            
        }
        
        public void RequestDeltasAsync ()
        {
            RequestDeltasAsync (null);
        }
        
        public void RequestDeltasAsync (object userState)
        {
            CryOutThroughTheAetherAsync (
                String.Format ("/aether/deltas/{0}", ClientID), HttpMethod.GET,
                null, ServiceFlags.RequireAuth, Method.RequestDeltas, userState
            );                      
        }

        private void Abort ()
        {
            HttpWebRequest req = request;
                
            if (req != null) {
                req.Abort ();
            }
        }                

        private void Completed (RequestState state)
        {
            try {
                asm.SetCompleted ();
                
                OnClientMethodCompleted (
                    (Method)((object[])state.UserState)[0], CreateAetherArgs (state)
                );
            } finally {
                request = null;
                state.Dispose ();
                asm.Reset ();
                OnStateChanged (AetherClientState.Busy, AetherClientState.Idle);
            }
        }

        private AetherAsyncCompletedEventArgs CreateAetherArgs (RequestState state)
        {
            string data = null;
            Exception err = null;         
            bool timedout = false;
            bool cancelled = false; 
            
            object[] userState = state.UserState as object[];                            

            if (asm.Cancelled) {
                cancelled = true;
            } else if (asm.Timedout) {
                timedout = true;
            } else {
                if (state.Error != null) {
                    if (state.Error is WebException) {
                        WebException e = state.Error as WebException;
                        if (e.Status == WebExceptionStatus.Timeout) {
                            timedout = true;
                        }
                    }
                }
    
                if (!timedout) {
                    err  = state.Error;
                    data = (string) userState[1];
                }
            }
            
            return new AetherAsyncCompletedEventArgs (data, err, cancelled, timedout, userState[2]);    
        }
        
        private Cookie CreateCookie (string name, string val)
        {
            return new Cookie (name, val, "/", aether_service_uri.Host);
        }

        private void HandleException (RequestState state, Exception e)
        {
            state.Error = e;
            Completed (state);            
        }

        private void CryOutThroughTheAetherAsync (string ppf,       // Path, parameters, fragment.
                                                  HttpMethod method,
                                                  string requestData,
                                                  ServiceFlags flags,
                                                  Method acm,
                                                  object userState)
        {
            asm.SetBusy ();
            OnStateChanged (AetherClientState.Idle, AetherClientState.Busy);        
            CryOutThroughTheAetherAsync (ppf, method, requestData, flags, acm, userState, null);           
        }

        private void CryOutThroughTheAetherAsync (string ppf,       // Path, parameters, fragment.
                                                  HttpMethod method,
                                                  string requestData,
                                                  ServiceFlags flags,
                                                  Method acm,
                                                  object userState,
                                                  Action<RequestState> c)
        {
            RequestState state = new RequestState () {
                Continuation = c,
                UserState = new object[3] { acm, null, userState }
            };

            try {
                lock () {
                    request = WebRequest.Create (aether_service_uri.AbsoluteUri+ppf) as HttpWebRequest;
                }
                
                request.Timeout = Timeout;
                request.UserAgent = UserAgent;
                request.Credentials = Credentials;
                
                request.AllowAutoRedirect = true;
                
                state.Request = request;

                if (flags != ServiceFlags.None) {
                    request.CookieContainer = new CookieContainer ();
                    
                    if ((flags & ServiceFlags.RequireAuth) != 0) {
                        request.CookieContainer.Add (CreateCookie ("sessionid", SessionID));
                    }
                    
                    if ((flags & ServiceFlags.RequireClientID) != 0) {
                        request.CookieContainer.Add (CreateCookie ("clientid", ClientID));
                    }
                }
    
                switch (method) {
                case HttpMethod.GET:
                    request.Method = "GET";
                    GetResponse (state);
                    break;
                case HttpMethod.POST:
                    request.Method = "POST";
                    request.ContentType = "application/x-www-form-urlencoded";
                    SendRequest (requestData, state);
                    break;
                }
            } catch (Exception e) {
                state.Error = e;
                Completed (state);
            }
        }

        private void SendRequest (string data, RequestState state)
        {
            state.WriteBuffer = Encoding.UTF8.GetBytes (data);

            state.AddTimeout (OnTimeout, timeout, true, state);
            request.BeginGetRequestStream (FuckitIJustNeedAName, state);
        }
        
        private void GetResponse (RequestState state)
        {
            state.AddTimeout (OnTimeout, timeout, true, state);       
            request.BeginGetResponse (ThisIsCthulhuGoAheadCaller, state);
        }

        private void FuckitIJustNeedAName (IAsyncResult ar) {
            RequestState state = ar.AsyncState as RequestState;

            try {
                state.RemoveTimeout (true);
                state.RequestStream = state.Request.EndGetRequestStream (ar);

                state.AddTimeout (OnTimeout, timeout, false, state);
                state.RequestStream.BeginWrite (state.WriteBuffer, 0, state.WriteBuffer.Length, EndWrite, state);
            } catch (Exception e) {
                HandleException (state, e);
            }
        }
        
        private void ThisIsCthulhuGoAheadCaller (IAsyncResult ar)
        {
            // Wow, ok, hi.  Uhh, first time caller, longtime dreamer of mad dreams.

            RequestState state = ar.AsyncState as RequestState;

            try {
                state.RemoveTimeout (true);
                state.Response = state.Request.EndGetResponse (ar) as HttpWebResponse;
                state.ResponseStream = state.Response.GetResponseStream ();

                state.AddTimeout (OnTimeout, timeout, false, state);
                state.ResponseStream.BeginRead (state.ReadBuffer, 0, RequestState.BufferSize, EndRead, state);
            } catch (Exception e) {
                HandleException (state, e);
            }
        }

        private void EndRead (IAsyncResult ar)
        {
            int nread = -1;
            RequestState state = ar.AsyncState as RequestState;

            try {
                state.SetTimeoutHandle ();
                nread = state.ResponseStream.EndRead (ar);
            
                if (nread != 0) {
                    state.ReadData.Write (state.ReadBuffer, 0, nread);
                    state.ResponseStream.BeginRead (state.ReadBuffer, 0, RequestState.BufferSize, EndRead, state);
                } else {
                    // Assuming utf-8 encoding.  Don't give me that look.
                    ((object[])(state.UserState))[1] = Encoding.UTF8.GetString (state.ReadData.ToArray ());
                    Completed (state);
                }            
            } catch (Exception e) {
                HandleException (state, e);
            }
        }

        private void EndWrite (IAsyncResult ar)
        {            
            RequestState state = ar.AsyncState as RequestState;

            try {
                state.RemoveTimeout (true);
                state.RequestStream.EndWrite (ar);
                
                if (state.RequestStream != null) {
                    state.RequestStream.Close ();
                    state.RequestStream = null;
                }
                
                GetResponse (state);
            } catch (Exception e) {                
                HandleException (state, e);
            }
        }

        private void OnTimeout (object userState, bool timedOut) 
        {
            if (timedOut) {
                if (asm.SetTimedout ()) {
                    Abort ();
                }
            }
        }   

        private void AddHandler (Method method, EventHandler<AetherAsyncCompletedEventArgs> handler) {
            lock (((ICollection)event_table).SyncRoot) {
                if (!event_table.ContainsKey (method)) {
                    event_table.Add (method, handler);
                } else {
                    event_table[method] += handler;
                }
            }
        }

        private void RemoveHandler (Method method, EventHandler<AetherAsyncCompletedEventArgs> handler) {
            lock (((ICollection)event_table).SyncRoot) {
                EventHandler<AetherAsyncCompletedEventArgs> h;

                if (event_table.TryGetValue (method, out h)) {
                    h -= handler;                    
                }
            }
        }        

        private EventHandler<AetherAsyncCompletedEventArgs> GetHandler (Method method)
        {
            lock (((ICollection)event_table).SyncRoot) {
                EventHandler<AetherAsyncCompletedEventArgs> handler;
                event_table.TryGetValue (method, out handler);
                return handler;
            }
        }

        private void OnClientMethodCompleted (Method method, AetherAsyncCompletedEventArgs e) {
            var handler = GetHandler (method);
            
            if (handler != null) {
                handler (this, e);
            }
        }       
    }
}
