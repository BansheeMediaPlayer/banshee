/***************************************************************************
 *  FeedUpdateTask.cs
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
using System.Threading;
using System.ComponentModel;
using System.Net;

using Hyena;

using Migo.Net;
using Migo.TaskCore;

namespace Migo.Syndication
{
    public class FeedUpdateTask : Task, IDisposable
    {
        private Feed feed;
        private bool disposed, cancelled, completed;
        private ManualResetEvent mre;
        private AsyncWebClient wc = null;

        public Feed Feed {
            get { return feed; }
        }

        public override WaitHandle WaitHandle {
            get {
                lock (SyncRoot) {
                    if (mre == null) {
                        mre = new ManualResetEvent (true);
                    }

                    return mre;
                }
            }
        }

#region Constructor

        public FeedUpdateTask (Feed feed)
        {
            if (feed == null) {
                throw new ArgumentNullException ("feed");
            }

            this.feed = feed;
            this.Name = feed.Link;

            feed.DownloadStatus = FeedDownloadStatus.Pending;
        }

#endregion

#region Public Methods

        public override void CancelAsync ()
        {
            lock (SyncRoot) {
                if (!completed) {
                    cancelled = true;

                    if (wc != null) {
                        wc.CancelAsync ();
                    }

                    EmitCompletionEvents (FeedDownloadError.Canceled);
                }
            }
        }

        public void Dispose ()
        {
            lock (SyncRoot) {
                if (!disposed) {
                    if (mre != null) {
                        mre.Close ();
                        mre = null;
                    }

                    disposed = true;
                }
            }
        }

        public override void ExecuteAsync ()
        {
            lock (SyncRoot) {
                SetStatus (TaskStatus.Running);

                if (mre != null) {
                    mre.Reset ();
                }
            }

            try {
                wc = new AsyncWebClient ();
                wc.Timeout = (30 * 1000); // 30 Seconds
                if (feed.LastDownloadError == FeedDownloadError.None && feed.LastDownloadTime != DateTime.MinValue) {
                    wc.IfModifiedSince = feed.LastDownloadTime.ToUniversalTime ();
                }
                wc.DownloadStringCompleted += OnDownloadDataReceived;

                feed.DownloadStatus = FeedDownloadStatus.Downloading;
                wc.DownloadStringAsync (new Uri (feed.Url));
            } catch (Exception e) {
                if (wc != null) {
                    wc.DownloadStringCompleted -= OnDownloadDataReceived;
                }

                EmitCompletionEvents (FeedDownloadError.DownloadFailed);
                Log.Error (e);
            }
        }

#endregion

        private void OnDownloadDataReceived (object sender, Migo.Net.DownloadStringCompletedEventArgs args)
        {
            bool notify_on_save = true;
            lock (SyncRoot) {
                if (cancelled)
                    return;

                wc.DownloadStringCompleted -= OnDownloadDataReceived;
                FeedDownloadError error;

                WebException we = args.Error as WebException;
                if (we == null) {
                     try {
                        DateTime last_built_at = feed.LastBuildDate;
                        RssParser parser = new RssParser (feed.Url, args.Result);
                        parser.UpdateFeed (feed);
                        feed.SetItems (parser.GetFeedItems (feed));
                        error = FeedDownloadError.None;
                        notify_on_save = feed.LastBuildDate > last_built_at;
                    } catch (FormatException e) {
                        Log.Warning (e);
                        error = FeedDownloadError.InvalidFeedFormat;
                    }
                } else {
                    error = FeedDownloadError.DownloadFailed;
                    HttpWebResponse resp = we.Response as HttpWebResponse;
                    if (resp != null) {
                        switch (resp.StatusCode) {
                        case HttpStatusCode.NotFound:
                        case HttpStatusCode.Gone:
                            error = FeedDownloadError.DoesNotExist;
                            break;
                        case HttpStatusCode.NotModified:
                            notify_on_save = false;
                            error = FeedDownloadError.None;
                            break;
                        case HttpStatusCode.Unauthorized:
                            error = FeedDownloadError.UnsupportedAuth;
                            break;
                        default:
                            error = FeedDownloadError.DownloadFailed;
                            break;
                        }
                    }
                }

                feed.LastDownloadError = error;
                feed.LastDownloadTime = DateTime.Now;

                feed.Save (notify_on_save);

                EmitCompletionEvents (error);
                completed = true;
            }
        }

        private void EmitCompletionEvents (FeedDownloadError err)
        {
            switch (err) {
                case FeedDownloadError.None:
                    SetStatus (TaskStatus.Succeeded);
                    feed.DownloadStatus = FeedDownloadStatus.Downloaded;
                    break;
                case FeedDownloadError.Canceled:
                    SetStatus (TaskStatus.Cancelled);
                    feed.DownloadStatus = FeedDownloadStatus.None;
                    break;
                default:
                    SetStatus (TaskStatus.Failed);
                    feed.DownloadStatus = FeedDownloadStatus.DownloadFailed;
                    break;
            }

            OnTaskCompleted (null, (Status == TaskStatus.Cancelled));

            if (mre != null) {
                mre.Set ();
            }
        }
	}
}
