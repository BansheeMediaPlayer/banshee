// 
// AetherRequest.cs
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
    public enum HttpMethod
    {
        GET  = 0,
        POST = 1
    }
    
    public sealed class AetherRequest
    {
        private int timeout;
        private AsyncStateManager asm;        
        
        private HttpWebRequest request;
        private CookieContainer cookie_container;        

        public EventHandler<AetherRequestCompletedEventArgs> Completed;

        public CookieContainer CookieContainer {
            get { return cookie_container; }
            set { cookie_container = value; }
        }

        public string ContentType { get; set; }
        public ICredentials Credentials { get; set; }

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

        public AetherRequest ()
        {
            asm = new AsyncStateManager ();
        }

        public void BeginGetRequest (Uri uri)
        {
            CryOutThroughTheAetherAsync (uri, HttpMethod.GET, null, null);
        }

        public void BeginGetRequest (Uri uri, object userState)
        {
            CryOutThroughTheAetherAsync (uri, HttpMethod.GET, null, userState);
        }

        public void BeginPostRequest (Uri uri, byte[] postData)
        {
            CryOutThroughTheAetherAsync (uri, HttpMethod.POST, postData, null);
        }

        public void BeginPostRequest (Uri uri, byte[] postData, object userState)
        {
            CryOutThroughTheAetherAsync (uri, HttpMethod.POST, postData, userState);        
        }
        
        public void CancelAsync ()
        {
            if (asm.SetCancelled ()) {
                Abort ();   
            }
        }

        private void Abort ()
        {
            HttpWebRequest req = request;
                
            if (req != null) {
                req.Abort ();
            }
        }                

        private void RequestCompleted (RequestState state)
        {
            try {
                asm.SetCompleted ();
                OnCompleted (CreateAetherArgs (state));
            } finally {
                request = null;
                state.Dispose ();
                asm.Reset ();
            }
        }
        
        private void HandleException (RequestState state, Exception e)
        {
            state.Error = e;
            RequestCompleted (state);            
        }

        private void CryOutThroughTheAetherAsync (Uri uri, HttpMethod method, byte[] postData, object userState)
        {
            asm.SetBusy ();
            
            RequestState state = new RequestState () {
                UserState = new object[2] { null, userState }
            };

            try {
                request = WebRequest.Create (uri) as HttpWebRequest;

                if (cookie_container != null) {
                    request.CookieContainer = cookie_container;
                }
                
                request.Timeout = Timeout;
                request.UserAgent = UserAgent;
                request.Credentials = Credentials;
                
                request.AllowAutoRedirect = true;

                state.Request = request;

                switch (method) {
                case HttpMethod.GET:
                    request.Method = "GET";
                    GetResponse (state);
                    break;
                case HttpMethod.POST:
                    request.Method = "POST";
                    request.ContentType = ContentType;
                    SendRequest (postData, state);
                    break;
                }
            } catch (Exception e) {
                state.Error = e;
                RequestCompleted (state);
            }
        }

        private void SendRequest (byte[] data, RequestState state)
        {
            state.WriteBuffer = data;
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
                    ((object[])(state.UserState))[0] = state.ReadData.ToArray ();
                    RequestCompleted (state);
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

        private AetherRequestCompletedEventArgs CreateAetherArgs (RequestState state)
        {
            byte[] data = null;
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
                    data = userState[0] as byte[];
                }
            }
            
            return new AetherRequestCompletedEventArgs (data, err, cancelled, timedout, userState[1]);    
        }

        private void OnTimeout (object userState, bool timedOut) 
        {
            if (timedOut) {
                if (asm.SetTimedout ()) {
                    Abort ();
                }
            }
        }

        private void OnCompleted (AetherRequestCompletedEventArgs e) {
            var handler = Completed;
            
            if (handler != null) {
                handler (this, e);
            }
        }
    }
}
