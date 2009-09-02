// 
// MiroGuideRequestState.cs
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
using System.Text;

using System.Collections.Generic;
using System.Collections.Specialized;

namespace Banshee.Paas.Aether.MiroGuide
{
    class MiroGuideRequestState
    {
        private Dictionary<string, string> parameters;

        public bool Timedout { get; set; }
        public bool Cancelled { get; set; }        
        
        public string BaseUri { get; set; }
        public Exception Error { get; set; }
        public HttpMethod HttpMethod { get; set; }        
        public MiroGuideClientMethod Method { get; set; }

        public string RequestData { get; set; }
        public string ResponseData { get; set; }        
        
        public ServiceFlags ServiceFlags { get; set; }
        public object UserState { get; set; }
        
        public MiroGuideRequestState CallingState { get; set; }

        public MiroGuideRequestState ()
        {
            parameters = new Dictionary<string, string> ();
        }

        public Uri GetFullUri ()
        {
            return new Uri (String.Format ("{0}{1}", BaseUri, GetParameterString ()));
        }

        public void AddParameter (string key, string val)
        {
            if (parameters.ContainsKey (key)) {
                parameters[key] = val;
            } else {
                parameters.Add (key, val);
            }
        }

        public void AddParameters (NameValueCollection nvc)
        {
            if (nvc == null) {
                throw new ArgumentNullException ("nvc");
            }

            foreach (string key in nvc) {
                AddParameter (key, nvc[key]);
            }
        }

        public void ClearParameters ()
        {
            parameters.Clear ();
        }

        public void RemoveParameter (string key)
        {
            if (parameters.ContainsKey (key)) {
                parameters.Remove (key);
            }
        }

        private string GetParameterString ()
        {
            if (parameters.Count == 0) {
                return String.Empty;
            }

            bool first = true;
            StringBuilder sb = new StringBuilder ();
            
            sb.Append ('?');

            foreach (KeyValuePair<string, string> kvp in parameters) {
                if (!first) {
                    sb.Append ('&');    
                }
                
                sb.AppendFormat ("{0}={1}",
                    System.Web.HttpUtility.UrlEncode (kvp.Key),
                    System.Web.HttpUtility.UrlEncode (kvp.Value)
                );

                first = false;
            }

            return sb.ToString ();
        }
    }
}