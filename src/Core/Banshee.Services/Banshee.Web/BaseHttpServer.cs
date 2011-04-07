//
// BaseWebServer.cs
//
// Author:
//   Aaron Bockover <aaron@aaronbock.net>
//   James Wilcox   <snorp@snorp.net>
//   Neil Loknath   <neil.loknath@gmail.com
//
// Copyright (C) 2005-2006 Novell, Inc.
// Copyright (C) 2009 Neil Loknath
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
using System.Text;
using System.Web;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

using Hyena;

namespace Banshee.Web
{
    public abstract class BaseHttpServer
    {
        protected Socket server;
        private bool running;
        private int backlog;
        private ushort port;

        protected readonly ArrayList clients = new ArrayList();

        public BaseHttpServer (EndPoint endpoint, string name)
        {
            this.end_point = endpoint;
            this.name = name;
        }

        public BaseHttpServer (EndPoint endpoint, string name, int chunk_length) : this (endpoint, name)
        {
            this.chunk_length = chunk_length;
        }

        private string name = "Banshee Web Server";
        public string Name {
            get { return name; }
        }

        public bool IsBound {
            get { return server != null && server.IsBound; }
        }

        public bool IsRunning {
            get { return running; }
        }

        private EndPoint end_point = new IPEndPoint (IPAddress.Loopback, 80);
        protected EndPoint EndPoint {
            get { return end_point; }
            set {
                if (value == null) {
                    throw new ArgumentNullException ("end_point");
                }
                if (IsBound) {
                    throw new InvalidOperationException ("Cannot set EndPoint while running.");
                }
                end_point = value;
            }
        }

        private int chunk_length = 8192;
        public int ChunkLength {
            get { return chunk_length; }
        }

        public ushort Port {
            get { return port; }
        }

        public void Start ()
        {
            Start (10);
        }

        public void Start (int backlog)
        {
            if (backlog < 0) {
                throw new ArgumentOutOfRangeException ("backlog");
            }

            if (running) {
                return;
            }

            this.backlog = backlog;
            running = true;
            Thread thread = new Thread (ServerLoop);
            thread.Name = this.Name;
            thread.IsBackground = true;
            thread.Start ();
        }

        public virtual void Stop ()
        {
            running = false;

            if (server != null) {
                server.Close ();
                server = null;
            }

            foreach (Socket client in (ArrayList)clients.Clone ()) {
                client.Close ();
            }
        }

        private void ServerLoop ()
        {
            if (!BindServerSocket ()) {
                running = false;
                return;
            }

            server.Listen (backlog);

            IPEndPoint ip_endpoint;
            if ((ip_endpoint = server.LocalEndPoint as IPEndPoint) != null) {
                port = (ushort) ip_endpoint.Port;
            }

            Log.DebugFormat ("{0} listening for connections on port {1}", name, port);

            while (true) {
                try {
                    if (!running) {
                        break;
                    }

                    Socket client = server.Accept ();
                    clients.Add (client);
                    ThreadPool.QueueUserWorkItem (HandleConnection, client);
                } catch (SocketException) {
                    break;
                }
            }
        }

        private void HandleConnection (object o)
        {
            Socket client = (Socket) o;

            try {
                while (HandleRequest(client));
            } catch (IOException) {
            } catch (Exception e) {
                Log.Exception (e);
            } finally {
                clients.Remove (client);
                client.Close ();
            }
        }

        protected virtual bool BindServerSocket ()
        {
            server = new Socket (this.EndPoint.AddressFamily, SocketType.Stream, ProtocolType.IP);
            try {
                server.Bind (this.EndPoint);
            } catch (System.Net.Sockets.SocketException e) {
                if (e.SocketErrorCode == SocketError.AddressAlreadyInUse && this.EndPoint is IPEndPoint) {
                    Log.InformationFormat ("Unable to bind {0} to port {1}", name, (EndPoint as IPEndPoint).Port);
                } else {
                    Log.Exception (e);
                }
                return false;
            }

            return true;
        }

        protected virtual long ParseRangeRequest (string line)
        {
            long offset = 0;
            if (String.IsNullOrEmpty (line)) {
                return offset;
            }

            string [] split_line = line.Split (' ', '=', '-');
            foreach (string word in split_line) {
                if (long.TryParse (word, out offset)) {
                    return offset;
                }
            }

            return offset;
        }

        protected virtual bool HandleRequest (Socket client)
        {
            if (client == null || !client.Connected) {
                return false;
            }

            bool keep_connection = true;

            using (StreamReader reader = new StreamReader (new NetworkStream (client, false))) {
                string request_line = reader.ReadLine ();

                if (request_line == null) {
                    return false;
                }

                List <string> request_headers = new List <string> ();
                string line = null;

                do {
                    line = reader.ReadLine ();
                    if (line.ToLower () == "connection: close") {
                        keep_connection = false;
                    }
                    request_headers.Add (line);
                } while (line != String.Empty && line != null);

                string [] split_request_line = request_line.Split ();

                if (split_request_line.Length < 3) {
                    WriteResponse (client, HttpStatusCode.BadRequest, "Bad Request");
                    return keep_connection;
                } else {
                    try {
                        HandleValidRequest (client, split_request_line, request_headers.ToArray () );
                    } catch (IOException) {
                        keep_connection = false;
                    } catch (Exception e) {
                        keep_connection = false;
                        Console.Error.WriteLine("Trouble handling request {0}: {1}", split_request_line[1], e);
                    }
                }
            }

            return keep_connection;
        }

        protected abstract void HandleValidRequest(Socket client, string [] split_request, string [] request_headers);

        protected void WriteResponse (Socket client, HttpStatusCode code, string body)
        {
            WriteResponse (client, code, Encoding.UTF8.GetBytes (body));
        }

        protected virtual void WriteResponse (Socket client, HttpStatusCode code, byte [] body)
        {
            if (client == null || !client.Connected) {
                return;
            }
            else if (body == null) {
                throw new ArgumentNullException ("body");
            }

            StringBuilder headers = new StringBuilder ();
            headers.AppendFormat ("HTTP/1.1 {0} {1}\r\n", (int) code, code.ToString ());
            headers.AppendFormat ("Content-Length: {0}\r\n", body.Length);
            headers.Append ("Content-Type: text/html\r\n");
            headers.Append ("Connection: close\r\n");
            headers.Append ("\r\n");

            using (BinaryWriter writer = new BinaryWriter (new NetworkStream (client, false))) {
                writer.Write (Encoding.UTF8.GetBytes (headers.ToString ()));
                writer.Write (body);
            }

            client.Close ();
        }

        protected void WriteResponseStream (Socket client, Stream response, long length, string filename)
        {
            WriteResponseStream (client, response, length, filename, 0);
        }

        protected virtual void WriteResponseStream (Socket client, Stream response, long length, string filename, long offset)
        {
            if (client == null || !client.Connected) {
                return;
            }
            if (response == null) {
                throw new ArgumentNullException ("response");
            }
            if (length < 1) {
                throw new ArgumentOutOfRangeException ("length", "Must be > 0");
            }
            if (offset < 0) {
                throw new ArgumentOutOfRangeException ("offset", "Must be positive.");
            }

            using (BinaryWriter writer = new BinaryWriter (new NetworkStream (client, false))) {
                StringBuilder headers = new StringBuilder ();

                if (offset > 0) {
                    headers.Append ("HTTP/1.1 206 Partial Content\r\n");
                    headers.AppendFormat ("Content-Range: {0}-{1}\r\n", offset, offset + length);
                } else {
                    headers.Append ("HTTP/1.1 200 OK\r\n");
                }

                if (length > 0) {
                    headers.AppendFormat ("Content-Length: {0}\r\n", length);
                }

                if (filename != null) {
                    headers.AppendFormat ("Content-Disposition: attachment; filename=\"{0}\"\r\n",
                        filename.Replace ("\"", "\\\""));
                }

                headers.Append ("Connection: close\r\n");
                headers.Append ("\r\n");

                writer.Write (Encoding.UTF8.GetBytes (headers.ToString ()));

                using (BinaryReader reader = new BinaryReader (response)) {
                    while (true) {
                        byte [] buffer = reader.ReadBytes (ChunkLength);
                        if (buffer == null) {
                            break;
                        }

                        writer.Write(buffer);

                        if (buffer.Length < ChunkLength) {
                            break;
                        }
                    }
                }
            }
        }

        protected static string Escape (string input)
        {
            return String.IsNullOrEmpty (input) ? "" : System.Web.HttpUtility.HtmlEncode (input);
        }
    }
}
