//
// LastfmRequest.cs
//
// Authors:
//   Bertrand Lorentz <bertrand.lorentz@gmail.com>
//
// Copyright (C) 2009 Bertrand Lorentz
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

    public class LastfmRequest
    {
        private const string API_ROOT = "http://ws.audioscrobbler.com/2.0/";

        private Dictionary<string, string> parameters = new Dictionary<string, string> ();
        private Stream response_stream;

        public LastfmRequest ()
        {}

        public LastfmRequest (string method) : this (method, RequestType.Read, ResponseFormat.Json)
        {}

        public LastfmRequest (string method, RequestType request_type, ResponseFormat response_format)
        {
            this.method = method;
            this.request_type = request_type;
            this.response_format = response_format;
        }

        private string method;
        public string Method { get; set; }


        private RequestType request_type;
        public RequestType RequestType { get; set; }


        private ResponseFormat response_format;
        public ResponseFormat ResponseFormat { get; set; }


        public void AddParameter (string param_name, string param_value)
        {
            parameters.Add (param_name, param_value);
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

            if (response_format == ResponseFormat.Json) {
                AddParameter ("format", "json");
            } else if (response_format == ResponseFormat.Raw) {
                AddParameter ("raw", "true");
            }

            if (request_type == RequestType.Write) {
                response_stream = Post (API_ROOT, BuildPostData ());
            } else {
                response_stream = Get (BuildGetUrl ());
            }
        }

        public JsonObject GetResponseObject ()
        {
            Deserializer deserializer = new Deserializer (response_stream);
            object obj = deserializer.Deserialize ();
            JsonObject json_obj = obj as Hyena.Json.JsonObject;

            if (json_obj == null) {
                throw new ApplicationException ("Lastfm invalid response : not a JSON object");
            }

            return json_obj;
        }

        public StationError GetError ()
        {
            StationError error = StationError.None;

            string response;
            using (StreamReader sr = new StreamReader (response_stream)) {
                response = sr.ReadToEnd ();
            }

            if (response.Contains ("<lfm status=\"failed\">")) {
                // XML reply indicates an error
                Match match = Regex.Match (response, "<error code=\"(\\d+)\">");
                if (match.Success) {
                    error = (StationError) Int32.Parse (match.Value);
                    Log.WarningFormat ("Lastfm error {0}", error);
                } else {
                    error = StationError.Unknown;
                }
            }
            if (response_format == ResponseFormat.Json && response.Contains ("\"error\":")) {
                // JSON reply indicates an error
                Deserializer deserializer = new Deserializer (response);
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
            if (request_type == RequestType.AuthenticatedRead) {
                parameters.Add ("sk", LastfmCore.Account.SessionKey);
            }

            StringBuilder url = new StringBuilder (API_ROOT);
            url.AppendFormat ("?method={0}", method);
            url.AppendFormat ("&api_key={0}", LastfmCore.ApiKey);
            foreach (KeyValuePair<string, string> param in parameters) {
                url.AppendFormat ("&{0}={1}", param.Key, Uri.EscapeDataString (param.Value));
            }
            if (request_type == RequestType.AuthenticatedRead || request_type == RequestType.SessionRequest) {
                url.AppendFormat ("&api_sig={0}", GetSignature ());
            }

            return url.ToString ();
        }

        private string BuildPostData ()
        {
            parameters.Add ("sk", LastfmCore.Account.SessionKey);

            StringBuilder data = new StringBuilder ();
            data.AppendFormat ("method={0}", method);
            data.AppendFormat ("&api_key={0}", LastfmCore.ApiKey);
            foreach (KeyValuePair<string, string> param in parameters) {
                data.AppendFormat ("&{0}={1}", param.Key, Uri.EscapeDataString (param.Value));
            }
            data.AppendFormat ("&api_sig={0}", GetSignature ());

            return data.ToString ();
        }

        private string GetSignature ()
        {
            SortedDictionary<string, string> sorted_params = new SortedDictionary<string, string> (parameters);

            if (!sorted_params.ContainsKey ("api_key")) {
                sorted_params.Add ("api_key", LastfmCore.ApiKey);
            }
            if (!sorted_params.ContainsKey ("method")) {
                sorted_params.Add ("method", method);
            }
            StringBuilder signature = new StringBuilder ();
            foreach (KeyValuePair<string, string> parm in sorted_params) {
                if (parm.Key.Equals ("format")) {
                    continue;
                }
                signature.Append (parm.Key);
                signature.Append (parm.Value);
            }
            signature.Append (LastfmCore.ApiSecret);

            return Hyena.CryptoUtil.Md5Encode (signature.ToString (), Encoding.UTF8);
        }

#region HTTP helpers

        private Stream Get (string uri)
        {
            return Get (uri, null);
        }

        private Stream Get (string uri, string accept)
        {
            HttpWebRequest request = (HttpWebRequest) WebRequest.Create (uri);
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
            HttpWebRequest request = (HttpWebRequest) WebRequest.Create (String.Concat (uri, "?", data));
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
