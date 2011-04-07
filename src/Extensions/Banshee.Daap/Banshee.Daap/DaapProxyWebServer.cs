
/***************************************************************************
 *  DaapProxyWebServer.cs
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
 *  Copyright (C) 2009 Neil Loknath
 *  Written by Aaron Bockover <aaron@aaronbock.net>
 *             James Wilcox <snorp@snorp.net>
 *             Neil Loknath <neil.loknath@gmail.com>
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
using System.Text;
using System.Web;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;

using Banshee.Web;

using DAAP = Daap;

namespace Banshee.Daap
{
    internal class DaapProxyWebServer : BaseHttpServer
    {
        private ArrayList databases = new ArrayList();

        public DaapProxyWebServer() : base (new IPEndPoint(IPAddress.Loopback, 8089), "DAAP Proxy")
        {
        }

        protected override bool BindServerSocket ()
        {
            if (!base.BindServerSocket ()) {
                EndPoint = new IPEndPoint(IPAddress.Loopback, 0);
                try {
                    server.Bind (EndPoint);
                } catch (System.Net.Sockets.SocketException e) {
                    Hyena.Log.Exception (e);
                    return false;
                }
            }

            return true;
        }

        public void RegisterDatabase(DAAP.Database database)
        {
            databases.Add(database);
        }

        public void UnregisterDatabase(DAAP.Database database)
        {
            databases.Remove(database);
        }

        protected override void HandleValidRequest(Socket client, string [] split_request, string [] body_request)
        {
            if(split_request[1].StartsWith("/")) {
               split_request[1] = split_request[1].Substring(1);
            }

            string [] nodes = split_request[1].Split('/');
            string body = String.Empty;
            HttpStatusCode code = HttpStatusCode.OK;

            if(nodes.Length == 1 && nodes[0] == String.Empty) {
               body = GetHtmlHeader("Available Databases");

               if(databases.Count == 0) {
                   body += "<blockquote><p><em>No databases found. Connect to a " +
                       "share in Banshee.</em></p></blockquote>";
               } else {
                   body += "<ul>";
                   foreach(DAAP.Database database in (ArrayList)databases.Clone()) {
                       body += String.Format("<li><a href=\"/{0}\">{1} ({2} Tracks)</a></li>",
                           database.GetHashCode(), Escape (database.Name), database.TrackCount);
                   }
                   body += "</ul>";
               }
            } else if(nodes.Length == 1 && nodes[0] != String.Empty) {
                bool db_found = false;
                int id = 0;
                try {
                    id = Convert.ToInt32(nodes[0]);
                } catch {
                }

                foreach(DAAP.Database database in (ArrayList)databases.Clone()) {
                    if(database.GetHashCode() != id) {
                        continue;
                    }

                    body = GetHtmlHeader("Tracks in " + Escape (database.Name));

                    if(database.TrackCount == 0) {
                        body += "<blockquote><p><em>No songs in this database.</em></p></blockquote>";
                    } else {
                        body += "<p>Showing all " + database.TrackCount + " songs:</p><ul>";
                        foreach(DAAP.Track song in database.Tracks) {
                            body += String.Format("<li><a href=\"/{0}/{1}\">{2} - {3}</a> ({4}:{5})</li>",
                                database.GetHashCode(), song.Id, Escape (song.Artist), Escape (song.Title),
                                song.Duration.Minutes, song.Duration.Seconds.ToString("00"));
                        }
                        body += "</ul>";
                    }

                    db_found = true;
                    break;
                }

                if(!db_found) {
                    code = HttpStatusCode.BadRequest;
                    body = GetHtmlHeader("Invalid Request");
                    body += String.Format("<p>No database with id `{0}'</p>", id);
                }
            } else if(nodes.Length == 2) {
                bool db_found = false;
                int db_id = 0;
                int song_id = 0;

                try {
                    db_id = Convert.ToInt32(nodes[0]);
                    song_id = Convert.ToInt32(nodes[1]);
                } catch {
                }

                foreach(DAAP.Database database in (ArrayList)databases.Clone()) {
                    if(database.GetHashCode() != db_id) {
                        continue;
                    }

                    try {
                        DAAP.Track song = database.LookupTrackById(song_id);
                        if(song != null) {
                            long offset = -1;
                            foreach (string line in body_request) {
                                if (line.ToLower ().Contains ("range:")) {
                                    offset = ParseRangeRequest (line);
                                }
                            }

                            StreamTrack(client, database, song, offset);
                            return;
                        }
                    } catch (Exception e) {
                        Hyena.Log.Exception (e);
                    }

                    code = HttpStatusCode.BadRequest;
                    body = GetHtmlHeader("Invalid Request");
                    body += String.Format("<p>No song with id `{0}'</p>", song_id);

                    db_found = true;
                    break;
                }

                if(!db_found) {
                    code = HttpStatusCode.BadRequest;
                    body = GetHtmlHeader("Invalid Request");
                    body += String.Format("<p>No database with id `{0}'</p>", db_id);
                }
            } else {
               code = HttpStatusCode.BadRequest;
               body = GetHtmlHeader("Invalid Request");
               body += String.Format("<p>The request '{0}' could not be processed by server.</p>",
                   Escape (split_request[1]));
            }

            WriteResponse(client, code, body + GetHtmlFooter());
        }

        protected void StreamTrack(Socket client, DAAP.Database database, DAAP.Track song)
        {
            StreamTrack (client, database, song, -1);
        }

        protected virtual void StreamTrack(Socket client, DAAP.Database database, DAAP.Track song, long offset)
        {
            long length;
            Stream stream = database.StreamTrack(song, offset, out length);
            WriteResponseStream(client, stream, length, song.FileName, offset < 0 ? 0 : offset);
            stream.Close();
            client.Close();
        }

        private static string GetHtmlHeader(string title)
        {
            return String.Format("<html><head><title>{0} - Banshee DAAP Browser</title></head><body><h1>{0}</h1>",
                title);
        }

        private static string GetHtmlFooter()
        {
            return String.Format("<hr /><address>Generated on {0} by " +
                "Banshee DAAP Extension (<a href=\"http://banshee.fm\">http://banshee.fm</a>)",
                DateTime.Now.ToString());
        }

        private static IPAddress local_address = IPAddress.Parse("127.0.0.1");
        public IPAddress IPAddress {
            get {
                return local_address;
            }
        }

        public string HttpBaseAddress {
            get {
                return String.Format("http://{0}:{1}/", IPAddress, Port);
            }
        }
    }
}
