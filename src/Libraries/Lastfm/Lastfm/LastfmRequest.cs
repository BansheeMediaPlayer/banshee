//
// LastfmRequest.cs
//
// Authors:
//   Bertrand Lorentz <bertrand.lorentz@gmail.com>
//   Phil Trimble <philtrimble@gmail.com>
//   Andres G. Aragoneses <knocte@gmail.com>
//
// Copyright (C) 2009 Bertrand Lorentz
// Copyright (C) 2013 Phil Trimble
// Copyright (C) 2013 Andres G. Aragoneses
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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

using Hyena;
using Hyena.Json;

namespace Lastfm
{
    public enum RequestType {
        Read,
        SessionRequest, // Needs the signature, but we don't have the session key yet
        AuthenticatedRead,
        Write
    }

    public enum ResponseFormat {
        Json,
        Raw
    }

    public delegate void SendRequestHandler ();

    public class MaxSizeExceededException : ApplicationException
    {
    }

    internal class WebRequestCreator : IWebRequestCreate
    {
        public WebRequest Create (Uri uri)
        {
            return (HttpWebRequest) HttpWebRequest.Create (uri);
        }
    }

    public class LastfmRequest
    {
        private const string API_ROOT = "http://ws.audioscrobbler.com/2.0/";

        private Dictionary<string, string> parameters = new Dictionary<string, string> ();
        private Stream response_stream;
        private string response_string;
        IWebRequestCreate web_request_creator;

        public LastfmRequest ()
        {}

        internal LastfmRequest (string method, RequestType request_type, ResponseFormat response_format, IWebRequestCreate web_request_creator)
            : this (method, request_type, response_format)
        {
            this.web_request_creator = web_request_creator;
        }

        public LastfmRequest (string method) : this (method, RequestType.Read, ResponseFormat.Json)
        {}

        public LastfmRequest (string method, RequestType request_type, ResponseFormat response_format)
        {
            this.method = method;
            this.request_type = request_type;
            this.response_format = response_format;
            if (this.web_request_creator == null) {
                this.web_request_creator = new WebRequestCreator ();
            }

            Init ();
        }

        private void Init ()
        {
            this.incremental_data = new StringBuilder ();

            if (request_type != RequestType.Write) {
                incremental_data.Append (API_ROOT);
            }
            incremental_data.AppendFormat ("?method={0}", method);
            incremental_data.AppendFormat ("&api_key={0}", LastfmCore.ApiKey);

            if (request_type == RequestType.AuthenticatedRead || request_type == RequestType.Write) {
                AddParameter ("sk", LastfmCore.Account.SessionKey);
            }

            if (response_format == ResponseFormat.Json) {
                AddParameter ("format", "json");
            } else if (response_format == ResponseFormat.Raw) {
                AddParameter ("raw", "true");
            }
        }

        private string method;

        private RequestType request_type;

        private ResponseFormat response_format;

        private StringBuilder incremental_data;

        // This is close to the max based on testing.
        private const int MAX_POST_LENGTH = 6900;

        public void AddParameter (string param_name, string param_value)
        {
            var chunk = String.Format ("&{0}={1}",
                                       param_name, param_value != null ? Uri.EscapeDataString (param_value) : null);

            CheckSize (chunk.Length);

            incremental_data.Append (chunk);
            parameters.Add (param_name, param_value);
        }

        public void AddParameters (NameValueCollection parms)
        {
            var chunks = new StringBuilder ();
            foreach (string key in parms) {
                var value = parms [key];
                chunks.AppendFormat ("&{0}={1}",
                                     key, value != null ? Uri.EscapeDataString (value) : null);
            }

            CheckSize (chunks.Length);

            incremental_data.Append (chunks.ToString ());
            foreach (string key in parms) {
                var value = parms [key];
                parameters.Add (key, value);
            }
        }

        private void CheckSize (int length)
        {
            var target_length = incremental_data.Length + length;
            if (request_type != RequestType.Read) {
                target_length += 9 + 32; // length of &api_sig={GetSignature()}
            }

            if (target_length > MAX_POST_LENGTH) {
                throw new MaxSizeExceededException ();
            }
        }

        public Stream GetResponseStream ()
        {
            return response_stream;
        }

        public void Send ()
        {
            if (method == null) {
                throw new InvalidOperationException ("The method name should be set");
            }

            if (request_type == RequestType.Write) {
                response_stream = Post (API_ROOT, BuildPostData ());
            } else {
                response_stream = Get (BuildGetUrl ());
            }
        }

        public JsonObject GetResponseObject ()
        {
            if (response_stream == null) {
                return null;
            }

            SetResponseString ();

            Deserializer deserializer = new Deserializer (response_string);
            object obj = deserializer.Deserialize ();
            JsonObject json_obj = obj as Hyena.Json.JsonObject;

            if (json_obj == null) {
                throw new ApplicationException ("Lastfm invalid response : not a JSON object");
            }

            return json_obj;
        }

        public IAsyncResult BeginSend (AsyncCallback callback)
        {
            return BeginSend (callback, null);
        }

        private SendRequestHandler send_handler;
        public IAsyncResult BeginSend (AsyncCallback callback, object context)
        {
            send_handler = new SendRequestHandler (Send);

            return send_handler.BeginInvoke (callback, context);
        }

        public void EndSend (IAsyncResult result)
        {
            send_handler.EndInvoke (result);
        }

        public StationError GetError ()
        {
            StationError error = StationError.None;

            SetResponseString ();

            if (response_string == null) {
                return StationError.Unknown;
            }

            if (response_string.Contains ("<lfm status=\"failed\">")) {
                // XML reply indicates an error
                Match match = Regex.Match (response_string, "<error code=\"(\\d+)\">");
                if (match.Success) {
                    error = (StationError) Int32.Parse (match.Value);
                    Log.WarningFormat ("Lastfm error {0}", error);
                } else {
                    error = StationError.Unknown;
                }
            }
            if (response_format == ResponseFormat.Json && response_string.Contains ("\"error\":")) {
                // JSON reply indicates an error
                Deserializer deserializer = new Deserializer (response_string);
                JsonObject json = deserializer.Deserialize () as JsonObject;
                if (json != null && json.ContainsKey ("error")) {
                    error = (StationError) json["error"];
                    Log.WarningFormat ("Lastfm error {0} : {1}", error, (string)json["message"]);
                }
            }

            return error;
        }

        private string BuildGetUrl ()
        {
            if (request_type == RequestType.AuthenticatedRead || request_type == RequestType.SessionRequest) {
                incremental_data.AppendFormat ("&api_sig={0}", GetSignature ());
            }

            return incremental_data.ToString ();
        }

        private string BuildPostData ()
        {
            incremental_data.AppendFormat ("&api_sig={0}", GetSignature ());
            return incremental_data.ToString ();
        }

        private string GetSignature ()
        {
            // We need to have trackNumber[0] before track[0], so we use StringComparer.Ordinal
            var sorted_params = new SortedDictionary<string, string> (parameters, StringComparer.Ordinal);

            if (!sorted_params.ContainsKey ("api_key")) {
                sorted_params.Add ("api_key", LastfmCore.ApiKey);
            }
            if (!sorted_params.ContainsKey ("method")) {
                sorted_params.Add ("method", method);
            }
            StringBuilder signature = new StringBuilder ();
            foreach (var parm in sorted_params) {
                if (parm.Key.Equals ("format")) {
                    continue;
                }
                signature.Append (parm.Key);
                signature.Append (parm.Value);
            }
            signature.Append (LastfmCore.ApiSecret);

            return Hyena.CryptoUtil.Md5Encode (signature.ToString (), Encoding.UTF8);
        }

        public override string ToString ()
        {
            StringBuilder sb = new StringBuilder ();

            sb.Append (method);
            foreach (KeyValuePair<string, string> param in parameters) {
                sb.AppendFormat ("\n\t{0}={1}", param.Key, param.Value);
            }
            return sb.ToString ();
        }

        private void SetResponseString ()
        {
            if (response_string == null && response_stream != null) {
                using (StreamReader sr = new StreamReader (response_stream)) {
                    response_string = sr.ReadToEnd ();
                }
            }
        }

#region HTTP helpers

        private Stream Get (string uri)
        {
            return Get (uri, null);
        }

        private Stream Get (string uri, string accept)
        {
            var request = (HttpWebRequest)web_request_creator.Create (new Uri (uri));
            if (accept != null) {
                request.Accept = accept;
            }
            request.UserAgent = LastfmCore.UserAgent;
            request.Timeout = 10000;
            request.KeepAlive = false;
            request.AllowAutoRedirect = true;

            HttpWebResponse response = null;
            try {
                response = (HttpWebResponse) request.GetResponse ();
            } catch (WebException e) {
                Log.DebugException (e);
                response = (HttpWebResponse)e.Response;
            }
            return response != null ? response.GetResponseStream () : null;
        }

        private Stream Post (string uri, string data)
        {
            // Do not trust docs : it doesn't work if parameters are in the request body
            var request = (HttpWebRequest)web_request_creator.Create (new Uri (String.Concat (uri, data)));
            request.UserAgent = LastfmCore.UserAgent;
            request.Timeout = 10000;
            request.Method = "POST";
            request.KeepAlive = false;
            request.ContentType = "application/x-www-form-urlencoded";

            HttpWebResponse response = null;
            try {
                response = (HttpWebResponse) request.GetResponse ();
            } catch (WebException e) {
                Log.DebugException (e);
                response = (HttpWebResponse)e.Response;
            }
            return response != null ? response.GetResponseStream () : null;
        }

#endregion
    }
}
