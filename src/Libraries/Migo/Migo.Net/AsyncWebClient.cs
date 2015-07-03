/***************************************************************************
 *  AsyncWebClient.cs
 *
 *  Copyright (C) 2007 Michael C. Urbanski
 *  Written by Mike Urbanski <michael.c.urbanski@gmail.com>
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
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.ComponentModel;

namespace Migo.Net
{
    enum DownloadType
    {
        None = 0,
        Data = 1,
        File = 2,
        String = 3
    };

    public sealed class AsyncWebClient
    {
        private int range = 0;
        private int timeout = (120 * 1000); // 2 minutes
        private DateTime ifModifiedSince = DateTime.MinValue;
        private static Regex encoding_regexp = new Regex (@"encoding=[""']([^""']+)[""']",
                                                    RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private string fileName;

        private Exception error;

        private Uri uri;
        private object userState;
        private DownloadType type;

        private IWebProxy proxy;
        private string user_agent;
        private Encoding encoding;
        private ICredentials credentials;

        private HttpWebRequest request;
        private HttpWebResponse response;

        private WebHeaderCollection headers;
        private WebHeaderCollection responseHeaders;

        private byte[] result;
        private Stream localFile;
        private MemoryStream memoryStream;

        private TransferStatusManager tsm;

        private AutoResetEvent readTimeoutHandle;
        private RegisteredWaitHandle registeredTimeoutHandle;

        private bool busy;
        private bool completed;
        private bool cancelled;

        private readonly object cancelBusySync = new object ();

        public event EventHandler<EventArgs> ResponseReceived;
        public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;

        public event EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted;
        public event EventHandler<DownloadDataCompletedEventArgs> DownloadDataCompleted;
        public event EventHandler<DownloadStringCompletedEventArgs> DownloadStringCompleted;

        public ICredentials Credentials {
            get { return credentials; }
            set { credentials = value; }
        }

        public Encoding Encoding {
            get { return encoding; }

            set {
                if (value == null) {
                    throw new ArgumentNullException ("encoding");
                } else {
                    encoding = Encoding.Default;
                }
            }
        }

        public WebHeaderCollection Headers {
            get { return headers; }

            set {
                if (value == null) {
                    headers = new WebHeaderCollection ();
                } else {
                    headers = value;
                }
            }
        }

        public DateTime IfModifiedSince {
            get { return ifModifiedSince; }
            set { ifModifiedSince = value; }
        }

        public bool IsBusy {
            get {
                lock (cancelBusySync) {
                    return busy;
                }
            }
        }

        public IWebProxy Proxy {
            get { return proxy; }
            set { proxy = value; }
        }

        public int Range {
            get { return range; }
            set {
                if (range > -1) {
                    range = value;
                } else {
                    throw new ArgumentOutOfRangeException ("Range");
                }
            }
        }

        public HttpWebResponse Response {
            get { return response; }
        }

        public WebHeaderCollection ResponseHeaders {
            get { return responseHeaders; }
        }

        public AsyncWebClientStatus Status {
            get {
                if (type == DownloadType.String) {
                    throw new InvalidOperationException (
                        "Status cannot be reported for string downloads"
                    );
                }

                lock (tsm.SyncRoot) {
                    return new AsyncWebClientStatus (
                        tsm.Progress, tsm.BytesReceived,
                        tsm.TotalBytes, tsm.TotalBytesReceived
                    );
                }
            }
        }

        public int Timeout {
            get { return timeout; }
            set {
                if (value < -1) {
                    throw new ArgumentOutOfRangeException (
                        "Value must be greater than or equal to -1"
                    );
                }

                timeout = value;
            }
        }

        private static string default_user_agent;
        public static string DefaultUserAgent {
            get { return default_user_agent; }
            set { default_user_agent = value; }
        }

        public string UserAgent {
            get { return user_agent ?? DefaultUserAgent; }
            set { user_agent = value; }
        }

        private bool Cancelled {
            get {
                lock (cancelBusySync) {
                    return cancelled;
                }
            }
        }

        public AsyncWebClient ()
        {
            encoding = Encoding.Default;
            tsm = new TransferStatusManager ();
            tsm.ProgressChanged += OnDownloadProgressChangedHandler;
        }

        public void DownloadDataAsync (Uri address)
        {
            DownloadDataAsync (address, null);
        }

        public void DownloadDataAsync (Uri address, object userState)
        {
            if (address == null) {
                throw new ArgumentNullException ("address");
            }

            SetBusy ();
            DownloadAsync (address, DownloadType.Data, userState);
        }

        public void DownloadFileAsync (Uri address, string fileName)
        {
            DownloadFileAsync (address, fileName, null, null);
        }

        public void DownloadFileAsync (Uri address, Stream file)
        {
            DownloadFileAsync (address, null, file, null);
        }

        public void DownloadFileAsync (Uri address, string file, object userState)
        {
            DownloadFileAsync (address, file, null, userState);
        }

        public void DownloadFileAsync (Uri address, Stream file, object userState)
        {
            DownloadFileAsync (address, null, file, userState);
        }

        private void DownloadFileAsync (Uri address,
                                        string filePath,
                                        Stream fileStream,
                                        object userState)
        {
            if (String.IsNullOrEmpty (filePath) &&
                fileStream == null || fileStream == Stream.Null) {
                throw new ArgumentNullException ("file");
            } else if (address == null) {
                throw new ArgumentNullException ("address");
            } else if (fileStream != null) {
                if (!fileStream.CanWrite) {
                    throw new ArgumentException ("Cannot write to stream");
                } else {
                    localFile = fileStream;
                }
            } else {
                this.fileName = filePath;
            }

            SetBusy ();
            DownloadAsync (address, DownloadType.File, userState);
        }

        public void DownloadStringAsync (Uri address)
        {
            DownloadStringAsync (address, null);
        }

        public void DownloadStringAsync (Uri address, object userState)
        {
            if (address == null) {
                throw new ArgumentNullException ("address");
            }

            SetBusy ();
            DownloadAsync (address, DownloadType.String, userState);
        }

        public void CancelAsync ()
        {
            CancelAsync (true);
        }

        public void CancelAsync (bool deleteFile)
        {
            if (SetCancelled ()) {
                AbortDownload ();
            }
        }

        private void AbortDownload ()
        {
            AbortDownload (null);
        }

        private void AbortDownload (Exception e)
        {
            error = e;

            try {
                HttpWebRequest req = request;

                if (req != null) {
                    req.Abort();
                    Completed ();
                }
            } catch (Exception ae) {
                Console.WriteLine ("Abort Download Error:  {0}", ae.Message);
            }
        }

        private void Completed ()
        {
            Completed (null);
        }

        private void Completed (Exception e)
        {
            Exception err = (SetCompleted ()) ? e : error;

            object statePtr = userState;
            byte[] resultPtr = result;
            bool cancelledCpy = Cancelled;

            CleanUp ();

            DownloadCompleted (resultPtr, err, cancelledCpy, statePtr);

            Reset ();
        }

        private void CleanUp ()
        {
            if (localFile != null) {
                localFile.Close ();
                localFile = null;
            }

            if (memoryStream != null) {
                memoryStream.Close ();
                memoryStream = null;
            }

            if (response != null) {
                response.Close ();
                response = null;
            }

            result = null;
            request = null;

            CleanUpHandles ();
        }

        private void CleanUpHandles ()
        {
            if (registeredTimeoutHandle != null) {
                registeredTimeoutHandle.Unregister (readTimeoutHandle);
                readTimeoutHandle = null;
            }

            if (readTimeoutHandle != null) {
                readTimeoutHandle.Close ();
                readTimeoutHandle = null;
            }
        }

        private bool SetBusy ()
        {
            bool ret = false;

            lock (cancelBusySync) {
                if (busy) {
                    throw new InvalidOperationException (
                        "Concurrent transfer operations are not supported."
                    );
                } else {
                    ret = busy = true;
                }
            }

            return ret;
        }

        private bool SetCancelled ()
        {
            bool ret = false;

            lock (cancelBusySync) {
                if (busy && !completed && !cancelled) {
                    ret = cancelled = true;
                }
            }

            return ret;
        }

        private bool SetCompleted ()
        {
            bool ret = false;

            lock (cancelBusySync) {
                if (busy && !completed && !cancelled) {
                    ret = completed = true;
                }
            }

            return ret;
        }

        private void DownloadAsync (Uri uri, DownloadType type, object state)
        {
            this.uri = uri;
            this.type = type;
            this.userState = state;

            ImplDownloadAsync ();
        }

        private void ImplDownloadAsync ()
        {
            try {
                tsm.Reset ();
                request = PrepRequest (uri);

                IAsyncResult ar = request.BeginGetResponse (
                    OnResponseCallback, null
                );

                ThreadPool.RegisterWaitForSingleObject (
                    ar.AsyncWaitHandle,
                    new WaitOrTimerCallback (OnTimeout),
                    request, timeout, true
                );
            } catch (Exception e) {
                Completed (e);
            }
        }

        private HttpWebRequest PrepRequest (Uri address)
        {
            responseHeaders = null;
            HttpWebRequest req = HttpWebRequest.Create (address) as HttpWebRequest;

            req.AllowAutoRedirect = true;
            req.Credentials = credentials;

            if (proxy != null) {
                req.Proxy = proxy;
            }

            if (headers != null && headers.Count != 0) {
                int rangeHdr = -1;
                string expect = headers ["Expect"];
                string contentType = headers ["Content-Type"];
                string accept = headers ["Accept"];
                string connection = headers ["Connection"];
                string userAgent = headers ["User-Agent"];
                string referer = headers ["Referer"];
                string rangeStr = headers ["Range"];
                string ifModifiedSince = headers ["If-Modified-Since"];

                if (!String.IsNullOrEmpty (rangeStr)) {
                    Int32.TryParse (rangeStr, out rangeHdr);
                }

                headers.Remove ("Expect");
                headers.Remove ("Content-Type");
                headers.Remove ("Accept");
                headers.Remove ("Connection");
                headers.Remove ("Referer");
                headers.Remove ("User-Agent");
                headers.Remove ("Range");
                headers.Remove ("If-Modified-Since");

                req.Headers = headers;

                if (!String.IsNullOrEmpty (expect)) {
                    req.Expect = expect;
                }

                if (!String.IsNullOrEmpty (accept)) {
                    req.Accept = accept;
                }

                if (!String.IsNullOrEmpty (contentType)) {
                    req.ContentType = contentType;
                }

                if (!String.IsNullOrEmpty (connection)) {
                    req.Connection = connection;
                }

                if (!String.IsNullOrEmpty (userAgent)) {
                    req.UserAgent = userAgent;
                }

                if (!String.IsNullOrEmpty (referer)) {
                    req.Referer = referer;
                }

                if (rangeHdr > 0) {
                    req.AddRange (range);
                }

                if (!String.IsNullOrEmpty (ifModifiedSince)) {
                    DateTime modDate;

                    if (DateTime.TryParse (ifModifiedSince, out modDate)) {
                        req.IfModifiedSince = modDate;
                    }
                }
            } else {
                if (!String.IsNullOrEmpty (UserAgent)) {
                    req.UserAgent = UserAgent;
                }

                if (this.range > 0) {
                    req.AddRange (this.range);
                }

                if (this.ifModifiedSince > DateTime.MinValue) {
                    req.IfModifiedSince = this.ifModifiedSince;
                }
            }

            responseHeaders = null;

            return req;
        }

        private void OnResponseCallback (IAsyncResult ar)
        {
            Exception err = null;
            bool redirect_workaround = false;

            try {
                response = request.EndGetResponse (ar) as HttpWebResponse;

                responseHeaders = response.Headers;
                OnResponseReceived ();
                Download (response.GetResponseStream ());
            } catch (ObjectDisposedException) {
            } catch (WebException we) {
                if (we.Status != WebExceptionStatus.RequestCanceled) {
                    err = we;

                    HttpWebResponse response = we.Response as HttpWebResponse;
                    if (response != null
                        && (response.StatusCode == HttpStatusCode.BadRequest
                            || response.StatusCode == HttpStatusCode.NotAcceptable)
                        && response.ResponseUri != request.RequestUri) {
                        Hyena.Log.DebugFormat ("Identified Content-Length: 0 redirection bug for {0}; trying to get {1} directly", request.RequestUri, response.ResponseUri);
                        redirect_workaround = true;
                        uri = response.ResponseUri;
                        ImplDownloadAsync ();
                    }
                }
            } catch (Exception e) {
                err = e;
            } finally {
                if (!redirect_workaround) {
                    Completed (err);
                }
            }
        }

        // All of this download code could be abstracted
        // and put in a helper class.
        private void Download (Stream st)
        {
            long cLength = (response.ContentLength + range);

            if (cLength == 0) {
                return;
            }

            int nread = -1;
            int offset = 0;

            int length = (cLength == -1 || cLength > 8192) ? 8192 : (int) cLength;

            Stream dest = null;
            readTimeoutHandle = new AutoResetEvent (false);

            byte[] buffer = null;

            bool dataDownload = false;
            bool writeToStream = false;

            if (type != DownloadType.String) {
                tsm.TotalBytes = cLength;
                tsm.BytesReceivedPreviously = range;
            }

            switch (type) {
                case DownloadType.String:
                case DownloadType.Data:
                    dataDownload = true;

                    if (cLength != -1) {
                        length = (int) cLength;
                        buffer = new byte[cLength];
                    } else {
                        writeToStream = true;
                        buffer = new byte[length];
                        dest = OpenMemoryStream ();
                    }
                    break;
                case DownloadType.File:
                    writeToStream = true;
                    buffer = new byte [length];
                    if (localFile == null) {
                        dest = OpenLocalFile (fileName);
                    } else {
                        dest = localFile;
                    }

                    break;
            }

            registeredTimeoutHandle = ThreadPool.RegisterWaitForSingleObject (
                readTimeoutHandle, new WaitOrTimerCallback (OnTimeout), null, timeout, false
            );

            IAsyncResult ar;

            while (nread != 0) {
                // <hack>
                // Yeah, Yeah, Yeah, I'll change this later,
                // it's here to get around abort issues.

                ar = st.BeginRead (buffer, offset, length, null, null);
                nread = st.EndRead (ar);

                // need an auxiliary downloader class to replace this.
                // </hack>

                readTimeoutHandle.Set ();

                if (writeToStream) {
                    dest.Write (buffer, 0, nread);
                } else {
                    offset += nread;
                    length -= nread;
                }

                if (type != DownloadType.String) {
                    tsm.AddBytes (nread);
                }
            }

            CleanUpHandles ();

            if (type != DownloadType.String) {
                if (tsm.TotalBytes == -1) {
                    tsm.TotalBytes = tsm.BytesReceived;
                }
            }

            if (dataDownload) {
                if (writeToStream) {
                    result = memoryStream.ToArray ();
                } else {
                    result = buffer;
                }
            }
        }

        private Stream OpenLocalFile (string filePath)
        {
            return File.Open (
                filePath, FileMode.OpenOrCreate,
                FileAccess.Write, FileShare.None
            );
        }

        private MemoryStream OpenMemoryStream ()
        {
            return memoryStream = new MemoryStream ();
        }

        private void Reset ()
        {
            lock (cancelBusySync) {
                busy = false;
                cancelled = false;
                completed = false;
                error = null;
                fileName = String.Empty;
                ifModifiedSince = DateTime.MinValue;
                range = 0;
                type = DownloadType.None;
                uri = null;
                userState = null;
            }
        }

        private void DownloadCompleted (byte[] resultPtr,
                                        Exception errPtr,
                                        bool cancelledCpy,
                                        object userStatePtr)
        {
            switch (type) {
                case DownloadType.Data:
                    OnDownloadDataCompleted (
                        resultPtr, errPtr, cancelledCpy, userStatePtr
                    );
                    break;
                case DownloadType.File:
                    OnDownloadFileCompleted (errPtr, cancelledCpy, userStatePtr);
                    break;
                case DownloadType.String:
                    string s = null;

                    if (resultPtr != null) {
                        try {
                            s = Encoding.GetString (resultPtr).TrimStart ();

                            // Workaround if the string is a XML to set the encoding from it
                            if (s.StartsWith("<?xml")) {
                                Match match = encoding_regexp.Match (s);
                                if (match.Success && match.Groups.Count > 0) {
                                    string encodingStr = match.Groups[1].Value;
                                    try {
                                        Encoding enc = Encoding.GetEncoding (encodingStr);
                                        if (!enc.Equals (Encoding)) {
                                            s = enc.GetString (resultPtr);
                                        }
                                    } catch (ArgumentException) {}
                                }
                            }
                        } catch (Exception ex) {
                            Hyena.Log.DebugException (ex);
                            s = String.Empty;
                        }
                    }

                    OnDownloadStringCompleted (
                        s ?? "", errPtr, cancelledCpy, userStatePtr
                    );
                    break;
            }
        }

        private void OnTimeout (object state, bool timedOut)
        {
            if (timedOut) {
                if (SetCompleted ()) {
                    try {
                        AbortDownload (new WebException (
                            "The operation timed out", null,
                            WebExceptionStatus.Timeout, response
                        ));
                    } finally {
                        Completed ();
                    }
                }
            }
        }

        private void OnResponseReceived ()
        {
            EventHandler<EventArgs> handler = ResponseReceived;

            if (handler != null) {
                handler (this, new EventArgs ());
            }
        }

        private void OnDownloadProgressChanged (long bytesReceived,
                                                long BytesToReceive,
                                                int progressPercentage,
                                                object userState)
        {
            OnDownloadProgressChanged (
                new DownloadProgressChangedEventArgs (
                    progressPercentage, userState,
                    bytesReceived, BytesToReceive
                )
            );
        }

        private void OnDownloadProgressChanged (DownloadProgressChangedEventArgs args)
        {
            EventHandler <DownloadProgressChangedEventArgs>
                handler = DownloadProgressChanged;

            if (handler != null) {
                handler (this, args);
            }
        }

        private void OnDownloadProgressChangedHandler (object sender,
                                                       DownloadProgressChangedEventArgs e)
        {
            OnDownloadProgressChanged (
                e.BytesReceived,
                e.TotalBytesToReceive,
                e.ProgressPercentage,
                userState
            );
        }

        private void OnDownloadDataCompleted (byte[] bytes,
                                              Exception error,
                                              bool cancelled,
                                              object userState)
        {
            OnDownloadDataCompleted (
                new DownloadDataCompletedEventArgs (
                    bytes, error, cancelled, userState
                )
            );
        }

        private void OnDownloadDataCompleted (DownloadDataCompletedEventArgs args)
        {
            EventHandler <DownloadDataCompletedEventArgs>
                handler = DownloadDataCompleted;

            if (handler != null) {
                handler (this, args);
            }
        }

        private void OnDownloadFileCompleted (Exception error,
                                              bool cancelled,
                                              object userState)
        {
            OnDownloadFileCompleted (
                new AsyncCompletedEventArgs (error, cancelled, userState)
            );
        }

        private void OnDownloadFileCompleted (AsyncCompletedEventArgs args)
        {
            EventHandler <AsyncCompletedEventArgs>
                handler = DownloadFileCompleted;

            if (handler != null) {
                handler (this, args);
            }
        }

        private void OnDownloadStringCompleted (string resultStr,
                                                Exception error,
                                                bool cancelled,
                                                object userState)
        {
            OnDownloadStringCompleted (
                new DownloadStringCompletedEventArgs (
                    resultStr, error, cancelled, userState
                )
            );
        }

        private void OnDownloadStringCompleted (DownloadStringCompletedEventArgs args)
        {
            EventHandler <DownloadStringCompletedEventArgs>
                handler = DownloadStringCompleted;

            if (handler != null) {
                handler (this, args);
            }
        }
    }
}
