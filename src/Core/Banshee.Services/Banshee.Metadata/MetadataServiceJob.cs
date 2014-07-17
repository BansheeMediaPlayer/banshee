//
// MetadataServiceJob.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2006-2008 Novell, Inc.
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
using System.Collections.Generic;

using Hyena;

using Banshee.Base;
using Banshee.Collection;
using Banshee.Streaming;
using Banshee.Networking;
using Banshee.ServiceStack;

namespace Banshee.Metadata
{
    public class MetadataServiceJob : IMetadataLookupJob
    {
        private MetadataService service;
        private bool cancelled;
        private IBasicTrackInfo track;
        private List<StreamTag> tags = new List<StreamTag>();
        private IMetadataLookupJob current_job;

        protected bool InternetConnected {
            get { return ServiceManager.Get<Network> ().Connected; }
        }

        protected MetadataServiceJob()
        {
        }

        public MetadataServiceJob(MetadataService service, IBasicTrackInfo track)
        {
            this.service = service;
            this.track = track;
        }

        public virtual void Cancel ()
        {
            cancelled = true;
            lock (this) {
                if (current_job != null) {
                    current_job.Cancel ();
                }
            }
        }

        public virtual void Run()
        {
            foreach(IMetadataProvider provider in service.Providers) {
                if (cancelled)
                    break;;

                try {
                    lock (this) {
                        current_job = provider.CreateJob(track);
                    }

                    current_job.Run();

                    if (cancelled)
                        break;;

                    foreach(StreamTag tag in current_job.ResultTags) {
                        AddTag(tag);
                    }

                    lock (this) {
                        current_job = null;
                    }
                } catch (System.Threading.ThreadAbortException) {
                    throw;
                } catch(Exception e) {
                    Log.Error (e);
                }
            }

            service.OnHaveResult (this);
        }

        public virtual IBasicTrackInfo Track {
            get { return track; }
            protected set { track = value; }
        }

        public virtual IList<StreamTag> ResultTags {
            get { return tags; }
        }

        protected void AddTag(StreamTag tag)
        {
            tags.Add(tag);
        }

        protected HttpWebResponse GetHttpStream(Uri uri)
        {
            return GetHttpStream(uri, null);
        }

        protected HttpWebResponse GetHttpStream(Uri uri, string [] ignoreMimeTypes)
        {
            if(!InternetConnected) {
                throw new NetworkUnavailableException();
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri.AbsoluteUri);
            request.UserAgent = Banshee.Web.Browser.UserAgent;
            request.Timeout = 10 * 1000;
            request.KeepAlive = false;
            request.AllowAutoRedirect = true;

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            if(ignoreMimeTypes != null) {
                string [] content_types = response.Headers.GetValues("Content-Type");
                if(content_types != null) {
                    foreach(string content_type in content_types) {
                        for(int i = 0; i < ignoreMimeTypes.Length; i++) {
                            if(content_type == ignoreMimeTypes[i]) {
                                return null;
                            }
                        }
                    }
                }
            }

            return response;
        }

        protected bool SaveHttpStream(Uri uri, string path)
        {
            return SaveHttpStream(uri, path, null);
        }

        protected bool SaveHttpStream(Uri uri, string path, string [] ignoreMimeTypes)
        {
            using (HttpWebResponse response = GetHttpStream (uri, ignoreMimeTypes)) {
                if (response == null) {
                    return false;
                }

                using (Stream from_stream = response.GetResponseStream ()) {
                    if (from_stream == null) {
                        return false;
                    }

                    SaveAtomically (path, from_stream);
                    return true;
                }
            }
        }

        protected bool SaveHttpStreamCover (Uri uri, string albumArtistId, string [] ignoreMimeTypes)
        {
            return SaveHttpStream (uri, CoverArtSpec.GetPath (albumArtistId), ignoreMimeTypes);
        }

        protected void SaveAtomically (string path, Stream from_stream)
        {
            if (String.IsNullOrEmpty (path) || from_stream == null || !from_stream.CanRead) {
                return;
            }

            SafeUri path_uri = new SafeUri (path);
            if (Banshee.IO.File.Exists (path_uri)) {
                return;
            }

            // Save the file to a temporary path while downloading/copying,
            // so that nobody sees it and thinks it's ready for use before it is
            SafeUri tmp_uri = new SafeUri (String.Format ("{0}.part", path));
            try {
                Banshee.IO.StreamAssist.Save (from_stream, Banshee.IO.File.OpenWrite (tmp_uri, true));
                Banshee.IO.File.Move (tmp_uri, path_uri);
            } catch (Exception e) {
                Log.Error (e);
            }
        }
    }
}
