// 
// RequestState.cs
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
using System.Threading;

// This needs to be added to Migo and used in the AsyncWebClient.

namespace Banshee.Paas.Aether
{
    class RequestState : IDisposable
    {
        public const int BufferSize = 8192;

        private AutoResetEvent timeout_handle;
        private RegisteredWaitHandle registered_timeout_handle;        

        public byte[] ReadBuffer  { get; set; }
        public byte[] WriteBuffer { get; set; }        

        public Exception Error { get; set; }
            
        public HttpWebRequest  Request  { get; set; }
        public HttpWebResponse Response { get; set; }

        public MemoryStream ReadData { get; set; }        
        
        public Stream RequestStream   {get; set; }
        public Stream ResponseStream  {get; set; }

        public object UserState { get; set; }
                
        public RequestState ()
        {
            ReadBuffer = new byte[BufferSize];
            ReadData = new MemoryStream ();
        }        

        public void Dispose () 
        {
            RemoveTimeout ();
        
            if (ReadData != null) {
                ReadData.Close ();
                ReadData = null;
            }
            
            if (Response != null) {
                Response.Close ();
                Response = null;
            }

            if (RequestStream != null) {
                RequestStream.Close ();
                RequestStream = null;
            }
            
            if (ResponseStream != null) {
                ResponseStream.Close ();
                ResponseStream = null;
            }

            Request = null;
            UserState = null;
            
            ReadBuffer = null;
            WriteBuffer = null;
        }

        public void AddTimeout (WaitOrTimerCallback callback, int timeout, bool executeOnlyOnce, object state) 
        {
            if (timeout_handle != null) {
                throw new InvalidOperationException ("Cannot nest timeouts.");
            }
            
            timeout_handle = new AutoResetEvent (false);

            if (executeOnlyOnce) {
                ThreadPool.RegisterWaitForSingleObject (
                    timeout_handle, callback, state, timeout, true
                );                
            } else {
                registered_timeout_handle = ThreadPool.RegisterWaitForSingleObject (
                    timeout_handle, callback, state, timeout, false
                );
            }
        }

        public void RemoveTimeout ()
        {
            RemoveTimeout (false);
        }
        
        public void RemoveTimeout (bool flag)
        {
            if (flag) {
                SetTimeoutHandle ();
            }
            
            if (registered_timeout_handle != null) {
                registered_timeout_handle.Unregister (null);
                registered_timeout_handle = null;
            }

            timeout_handle = null;            
        }

        public void SetTimeoutHandle ()
        {
            if (timeout_handle != null) {
                timeout_handle.Set ();                    
            }
        }
    }
}
