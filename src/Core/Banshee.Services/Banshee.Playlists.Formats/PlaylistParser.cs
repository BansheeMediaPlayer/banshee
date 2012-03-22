//
// PlaylistParser.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//   Bertrand Lorentz <bertrand.lorentz@gmail.com>
//
// Copyright (C) 2007 Novell, Inc.
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
using System.Web;
using System.Collections.Generic;

using Hyena;

using Banshee.Base;

namespace Banshee.Playlists.Formats
{
    public class PlaylistParser
    {
        private static PlaylistFormatDescription [] playlist_formats = new PlaylistFormatDescription [] {
            M3uPlaylistFormat.FormatDescription,
            PlsPlaylistFormat.FormatDescription,
            AsxPlaylistFormat.FormatDescription,
            AsfReferencePlaylistFormat.FormatDescription,
            XspfPlaylistFormat.FormatDescription
        };

        private List<Dictionary<string, object>> elements;
        private Uri base_uri = null;
        private string title = null;
        private readonly int HTTP_REQUEST_RETRIES = 3;

        public PlaylistParser ()
        {
            if (Environment.CurrentDirectory.Equals ("/")) {
                // System.Uri doesn't like / as a value
                base_uri = new Uri ("file:///");
            } else {
                base_uri = new Uri (Environment.CurrentDirectory);
            }
        }

        public bool Parse (SafeUri uri)
        {
            ThreadAssist.AssertNotInMainThread ();
            lock (this) {
                elements = null;
                HttpWebResponse response = null;
                Stream stream = null;
                Stream web_stream = null;
                bool partial_read = false;
                long saved_position = 0;

                if (uri.Scheme == "file") {
                    stream = Banshee.IO.File.OpenRead (uri);
                } else if (uri.Scheme == "http") {
                    response = Download (uri, HTTP_REQUEST_RETRIES);
                    web_stream = response.GetResponseStream ();
                    try {
                        stream = new MemoryStream ();

                        byte [] buffer = new byte[4096];
                        int read;

                        // If we haven't read the whole stream and if
                        // it turns out to be a playlist, we'll read the rest
                        read = web_stream.Read (buffer, 0, buffer.Length);
                        stream.Write (buffer, 0, read);
                        if ((read = web_stream.Read (buffer, 0, buffer.Length)) > 0) {
                            partial_read = true;
                            stream.Write (buffer, 0, read);
                            saved_position = stream.Position;
                        }

                        stream.Position = 0;
                    } finally {
                        if (!partial_read) {
                            web_stream.Close ();
                            response.Close ();
                        }
                    }
                } else {
                    Hyena.Log.DebugFormat ("Not able to parse playlist at {0}", uri);
                    return false;
                }

                PlaylistFormatDescription matching_format = null;

                foreach (PlaylistFormatDescription format in playlist_formats) {
                    stream.Position = 0;

                    if (format.MagicHandler (new StreamReader (stream))) {
                        matching_format = format;
                        break;
                    }
                }

                if (matching_format == null) {
                    if (partial_read) {
                        web_stream.Close ();
                        response.Close ();
                    }
                    return false;
                }

                if (partial_read) {
                    try {
                        stream.Position = saved_position;
                        Banshee.IO.StreamAssist.Save (web_stream, stream, false);
                    } finally {
                        web_stream.Close ();
                        response.Close ();
                    }
                }
                stream.Position = 0;
                IPlaylistFormat playlist = (IPlaylistFormat)Activator.CreateInstance (matching_format.Type);
                playlist.BaseUri = BaseUri;
                playlist.Load (stream, false);
                stream.Dispose ();

                elements = playlist.Elements;
                Title = playlist.Title ?? Path.GetFileNameWithoutExtension (uri.LocalPath);
                return true;
            }
        }

        private HttpWebResponse Download (SafeUri uri, int nb_retries)
        {
            Hyena.ThreadAssist.AssertNotInMainThread ();

            while (true) {
                try {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create (uri.AbsoluteUri);
                    request.UserAgent = Banshee.Web.Browser.UserAgent;
                    request.KeepAlive = false;
                    request.Timeout = 5 * 1000;
                    request.AllowAutoRedirect = true;

                    // Parse out and set credentials, if any
                    string user_info = new Uri (uri.AbsoluteUri).UserInfo;
                    if (!String.IsNullOrEmpty (user_info)) {
                        string username = String.Empty;
                        string password = String.Empty;
                        int cIndex = user_info.IndexOf (":");
                        if (cIndex != -1) {
                            username = user_info.Substring (0, cIndex);
                            password = user_info.Substring (cIndex + 1);
                        } else {
                            username = user_info;
                        }
                        request.Credentials = new NetworkCredential (username, password);
                    }

                    var response = (HttpWebResponse)request.GetResponse ();
                    if (response.StatusCode == HttpStatusCode.GatewayTimeout) {
                        throw new WebException ("", WebExceptionStatus.Timeout);
                    }
                    return response;
                } catch (WebException e) {
                    if (e.Status == WebExceptionStatus.Timeout && nb_retries > 0) {
                        nb_retries--;
                        Log.InformationFormat ("Playlist download from {0} timed out, retrying in 1 second...", uri.AbsoluteUri);
                        System.Threading.Thread.Sleep (1000);
                    } else {
                        throw;
                    }
                }
            }
        }

        public List<Dictionary<string, object>> Elements {
            get { return elements; }
        }

        public Uri BaseUri {
            get { return base_uri; }
            set { base_uri = value; }
        }

        public string Title {
            get { return title; }
            set { title = value; }
        }
    }
}
