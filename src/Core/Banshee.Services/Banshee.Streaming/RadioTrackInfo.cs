//
// RadioTrackInfo.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2006-2007 Novell, Inc.
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
using System.Threading;
using System.Collections.Generic;
using Mono.Unix;

using Hyena;
using Media.Playlists.Xspf;

using Banshee.Base;
using Banshee.Collection;
using Banshee.ServiceStack;
using Banshee.MediaEngine;
using Banshee.PlaybackController;
using Banshee.Playlists.Formats;

namespace Banshee.Streaming
{
    public class RadioTrackInfo : TrackInfo
    {
        public static readonly string UnknownStream = Catalog.GetString ("Unknown Stream");

#region Static Helper Methods

        public static RadioTrackInfo OpenPlay (string uri)
        {
            try {
                return OpenPlay (new SafeUri (uri));
            } catch (Exception e) {
                Log.Error (e);
                return null;
            }
        }

        public static RadioTrackInfo OpenPlay (SafeUri uri)
        {
            RadioTrackInfo track = Open (uri);
            if (track != null) {
                track.Play ();
            }
            return track;
        }

        public static RadioTrackInfo Open (string uri)
        {
            return Open (new SafeUri (uri));
        }

        public static RadioTrackInfo Open (SafeUri uri)
        {
            try {
                RadioTrackInfo radio_track = new RadioTrackInfo (uri);
                radio_track.ParsingPlaylistEvent += delegate {
                    ThreadAssist.ProxyToMain (delegate {
                        if (radio_track.PlaybackError != StreamPlaybackError.None) {
                            Log.Error (Catalog.GetString ("Error opening stream"),
                                Catalog.GetString ("Could not open stream or playlist"), true);
                            radio_track = null;
                        }
                    });
                };

                return radio_track;
            } catch {
                Log.Error (Catalog.GetString ("Error opening stream"),
                    Catalog.GetString("Problem parsing playlist"), true);
                return null;
            }
        }

#endregion

        private Track track;
        private SafeUri single_location;
        private List<SafeUri> stream_uris = new List<SafeUri>();
        private int stream_index = 0;
        private bool loaded = false;
        private bool parsing_playlist = false;
        private bool trying_to_play;

        private TrackInfo parent_track;
        public TrackInfo ParentTrack {
            get { return parent_track; }
            set { parent_track = value; }
        }

        public event EventHandler ParsingPlaylistEvent;

        protected RadioTrackInfo()
        {
            IsLive = true;
        }

        public RadioTrackInfo(Track track) : this()
        {
            TrackTitle = track.Title;
            ArtistName = track.Creator;

            this.track = track;
        }

        public RadioTrackInfo(SafeUri uri) : this()
        {
            this.single_location = uri;
        }

        public RadioTrackInfo (TrackInfo parentTrack) : this (parentTrack.Uri)
        {
            ArtistName = parentTrack.ArtistName;
            TrackTitle = parentTrack.TrackTitle;
            AlbumTitle = parentTrack.AlbumTitle;
            ParentTrack = parentTrack;
        }

        public void Play()
        {
            if (trying_to_play) {
                return;
            }

            trying_to_play = true;
            SavePlaybackError (StreamPlaybackError.None);

            if (loaded) {
                PlayCore ();
            } else {
                // Stop playing until we load this radio station and play it
                ServiceManager.PlayerEngine.Close (true);

                ServiceManager.PlayerEngine.TrackIntercept += OnTrackIntercept;

                // Tell the seek slider that we're connecting
                // TODO move all this playlist-downloading/parsing logic into PlayerEngine?
                ServiceManager.PlayerEngine.StartSynthesizeContacting (this);

                OnParsingPlaylistStarted ();
                ThreadPool.QueueUserWorkItem (delegate {
                    try {
                        LoadStreamUris ();
                    } catch (Exception e) {
                        trying_to_play = false;
                        Log.Error (this.ToString (), e);
                        SavePlaybackError (StreamPlaybackError.Unknown);
                        OnParsingPlaylistFinished ();
                        ServiceManager.PlayerEngine.Close ();
                    }
                });
            }
        }

        public override StreamPlaybackError PlaybackError {
            get { return ParentTrack == null ? base.PlaybackError : ParentTrack.PlaybackError; }
            set {
                if (value != StreamPlaybackError.None) {
                    ServiceManager.PlayerEngine.EndSynthesizeContacting (this, true);
                }

                if (ParentTrack == null) {
                    base.PlaybackError = value;
                } else {
                    ParentTrack.PlaybackError = value;
                }
            }
        }

        public new void SavePlaybackError (StreamPlaybackError value)
        {
            PlaybackError = value;
            Save ();
            if (ParentTrack != null) {
                ParentTrack.Save ();
            }
        }

        private void PlayCore ()
        {
            ServiceManager.PlayerEngine.EndSynthesizeContacting (this, false);

            if(track != null) {
                TrackTitle = track.Title;
                ArtistName = track.Creator;
            }

            AlbumTitle = null;
            Duration = TimeSpan.Zero;

            lock(stream_uris) {
                if(stream_uris.Count > 0) {
                    Uri = stream_uris[stream_index];
                    Log.Debug("Playing Radio Stream", Uri.AbsoluteUri);
                    if (Uri.IsFile) {
                        try {
                            using (var file = StreamTagger.ProcessUri (Uri)) {
                                StreamTagger.TrackInfoMerge (this, file, true);
                            }
                        } catch (Exception e) {
                            Log.Warning (String.Format ("Failed to update metadata for {0}", this),
                                e.GetType ().ToString (), false);
                        }
                    }
                    ServiceManager.PlayerEngine.OpenPlay(this);
                }
            }

            trying_to_play = false;
        }

        public bool PlayNextStream()
        {
            if(stream_index < stream_uris.Count - 1) {
                stream_index++;
                Play();
                return true;
            }
            ServiceManager.PlaybackController.StopWhenFinished = true;
            return false;
        }

        public bool PlayPreviousStream()
        {
            if (stream_index > 0) {
                stream_index--;
                Play();
                return true;
            }
            ServiceManager.PlaybackController.StopWhenFinished = true;
            return false;
        }

        private void LoadStreamUris()
        {
            lock(stream_uris) {
                if(track != null) {
                    foreach(Uri uri in track.Locations) {
                        LoadStreamUri(uri.AbsoluteUri);
                    }
                } else {
                    LoadStreamUri(single_location.AbsoluteUri);
                }

                loaded = true;
            }

            ServiceManager.PlayerEngine.TrackIntercept -= OnTrackIntercept;
            OnParsingPlaylistFinished();

            if (ServiceManager.PlayerEngine.CurrentTrack == this) {
                PlayCore();
            } else {
                trying_to_play = false;
            }
        }

        private bool OnTrackIntercept (TrackInfo track)
        {
            if (track != this && track != ParentTrack) {
                ServiceManager.PlayerEngine.EndSynthesizeContacting (this, false);
                ServiceManager.PlayerEngine.TrackIntercept -= OnTrackIntercept;
            }
            return false;
        }

        private void LoadStreamUri(string uri)
        {
            try {
                var parsed_playlist = PlaylistParser.Parse (new SafeUri (uri));
                if (parsed_playlist != null) {
                    foreach (PlaylistElement element in parsed_playlist.Elements) {
                        if (element.Uri != null) {
                            // mms can be a nested link
                            string element_uri = element.Uri.ToString ();
                            if(element_uri.StartsWith("mms:", StringComparison.CurrentCultureIgnoreCase)){
                                LoadStreamUri("http" + element_uri.Substring(element_uri.IndexOf(":")));
                            }
                            stream_uris.Add (new SafeUri (element.Uri.AbsoluteUri));
                        }
                    }
                } else {
                    stream_uris.Add(new SafeUri(uri));
                }
                Log.DebugFormat ("Parsed {0} URIs out of {1}", stream_uris.Count, this);
            } catch (System.Net.WebException e) {
                Log.Warning (this.ToString (), e);
                SavePlaybackError (StreamPlaybackError.ResourceNotFound);
            } catch (Exception e) {
                Log.Error (this.ToString (), e);
                SavePlaybackError (StreamPlaybackError.Unknown);
            }
        }

        private void OnParsingPlaylistStarted()
        {
            parsing_playlist = true;
            OnParsingPlaylistEvent();
        }

        private void OnParsingPlaylistFinished()
        {
            parsing_playlist = false;
            OnParsingPlaylistEvent();
        }

        private void OnParsingPlaylistEvent()
        {
            EventHandler handler = ParsingPlaylistEvent;
            if(handler != null) {
                handler(this, EventArgs.Empty);
            }
        }

        public Track XspfTrack {
            get { return track; }
        }

        public bool ParsingPlaylist {
            get { return parsing_playlist; }
        }
    }
}
