//
// UPnPContainerSource.cs
//
// Authors:
//   Tobias 'topfs2' Arrskog <tobias.arrskog@gmail.com>
//
// Copyright (C) 2011 Tobias 'topfs2' Arrskog
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

using Mono.Unix;

using Mono.Upnp;
using Mono.Upnp.Dcp.MediaServer1.ContentDirectory1;
using Mono.Upnp.Dcp.MediaServer1.ContentDirectory1.AV;

using Banshee.Configuration;
using Banshee.Sources;

using Hyena;

namespace Banshee.UPnPClient
{
    public class UPnPServerSource : Source
    {
        private string udn;
        private UPnPMusicSource music_source;
        private UPnPVideoSource video_source;
        private SchemaEntry<bool> expanded_schema;

        public UPnPServerSource (Device device) :  base (Catalog.GetString ("UPnP Share"), device.FriendlyName, 300)
        {
            Properties.SetStringList ("Icon.Name", "computer", "network-server");
            TypeUniqueId = "upnp-container";
            expanded_schema = new SchemaEntry<bool> ("plugins.upnp." + device.Udn, "expanded", true,
                                                     "UPnP Share expanded", "UPnP Share expanded" );
            udn = device.Udn;

            ContentDirectoryController content_directory = null;

            foreach (Service service in device.Services) {
                Log.Debug ("UPnPService \"" + device.FriendlyName + "\" Implements " + service.Type);
                if (service.Type.Type == "ContentDirectory") {
                    content_directory = new ContentDirectoryController (service.GetController());
                }
            }

            if (content_directory == null) {
                throw new ArgumentNullException("content_directory");
            }

            ThreadAssist.Spawn (delegate {
                Parse (device, content_directory);
            });
            
        }

        ~UPnPServerSource ()
        {
            if (music_source != null) {
                RemoveChildSource (music_source);
                music_source = null;
            }

            if (video_source != null) {
                RemoveChildSource (video_source);
                video_source = null;
            }
        }

        delegate void ChunkHandler<T> (Results<T> results);

        void HandleResults<T> (Results<T> results, RemoteContentDirectory remote_dir, ChunkHandler<T> chunkHandler)
        {
            bool has_results = results.Count > 0;

            while (has_results) {
                chunkHandler (results);

                has_results = results.HasMoreResults;
                if (has_results) {
                    results = results.GetMoreResults (remote_dir);
                }
            }
        }

        List<string[]> FindBrowseQuirks (Device device)
        {
            List<string[]> core = new List<string[]>();
            if (device.ModelName == "MediaTomb" && device.ModelNumber == "0.12.1") {
                core.Add (new string[2] { "Audio", "Albums" });
                core.Add (new string[2] { "Video", "All Video" });
            } else if (device.ModelName == "Coherence UPnP A/V MediaServer" && device.ModelNumber == "0.6.6.2") {
                core.Add (new string[1] { "Albums" });
            } else {
              core.Add (new string[0]);
            }

            return core;
        }

        void Parse (Device device, ContentDirectoryController content_directory)
        {
            RemoteContentDirectory remote_dir = new RemoteContentDirectory (content_directory);
            DateTime begin = DateTime.Now;
            bool recursive_browse = !content_directory.CanSearch;

            try {
                Container root = remote_dir.GetRootObject ();

                if (!recursive_browse) {
                    try {
                        Log.Debug ("Content directory is searchable, let's search");

                        HandleResults<MusicTrack> (remote_dir.Search<MusicTrack>(root, visitor => visitor.VisitDerivedFrom("upnp:class", "object.item.audioItem.musicTrack"), new ResultsSettings()),
                                                   remote_dir,
                                                   chunk => {
                                                                List<MusicTrack> music_tracks = new List<MusicTrack>();
                                                                foreach (var item in chunk) {
                                                                    music_tracks.Add (item as MusicTrack);
                                                                }

                                                                AddMusic (music_tracks);
                                                            });

                        HandleResults<VideoItem>  (remote_dir.Search<VideoItem>(root, visitor => visitor.VisitDerivedFrom("upnp:class", "object.item.videoItem"), new ResultsSettings()),
                                                   remote_dir,
                                                   chunk => {
                                                                List<VideoItem> video_tracks = new List<VideoItem>();
                                                                foreach (var item in chunk) {
                                                                    video_tracks.Add (item as VideoItem);
                                                                }

                                                                AddVideo (video_tracks);
                                                            });
                    } catch (System.InvalidCastException exception) {
                        Log.Exception (exception);
                        recursive_browse = true;
                    }
                }

                if (recursive_browse) {
                    Log.Debug ("Content directory is not searchable, let's browse recursively");
                    List<MusicTrack> music_tracks = new List<MusicTrack> ();
                    List<VideoItem> video_tracks = new List<VideoItem> ();

                    foreach (var hierarchy in FindBrowseQuirks (device)) {
                        TraverseContainer (remote_dir, root, hierarchy, 0, music_tracks, video_tracks);
                    }

                    if (music_tracks.Count > 0) {
                        AddMusic (music_tracks);
                    }
                    if (video_tracks.Count > 0) {
                        AddVideo (video_tracks);
                    }
                }
            } catch (Exception exception) {
                Log.Exception (exception);
            }

            Log.Debug ("Found all items on the service, took " + (DateTime.Now - begin).ToString());
        }

        void TraverseContainer (RemoteContentDirectory remote_dir, Container container, string[] hierarchy, int position, List<MusicTrack> music_tracks, List<VideoItem> video_tracks)
        {
            if (hierarchy != null && hierarchy.Length > position) {
                HandleResults<Mono.Upnp.Dcp.MediaServer1.ContentDirectory1.Object> (
                    remote_dir.GetChildren<Mono.Upnp.Dcp.MediaServer1.ContentDirectory1.Object> (container),
                    remote_dir,
                    chunk => {
                        foreach (var upnp_object in chunk) {
                            if (upnp_object is Container && upnp_object.Title == hierarchy[position]) {
                                TraverseContainer (remote_dir, upnp_object as Container, hierarchy, position + 1, music_tracks, video_tracks);
                            }
                        }
                    });
            } else {
                ParseContainer (remote_dir, container, 0, music_tracks, video_tracks);
            }
        }

        void ParseContainer (RemoteContentDirectory remote_dir, Container container, int depth, List<MusicTrack> music_tracks, List<VideoItem> video_tracks)
        {
            if (depth > 10 || (container.ChildCount != null && container.ChildCount == 0)) {
                return;
            }

            HandleResults<Mono.Upnp.Dcp.MediaServer1.ContentDirectory1.Object> (
                remote_dir.GetChildren<Mono.Upnp.Dcp.MediaServer1.ContentDirectory1.Object> (container),
                remote_dir,
                chunk => {
                    foreach (var upnp_object in chunk) {
                        if (upnp_object is Item) {
                            Item item = upnp_object as Item;

                            if (item.IsReference || item.Resources.Count == 0) {
                                continue;
                            }

                            if (item is MusicTrack) {
                                music_tracks.Add (item as MusicTrack);
                            } else if (item is VideoItem) {
                                video_tracks.Add (item as VideoItem);
                            }
                        } else if (upnp_object is Container) {
                            ParseContainer (remote_dir, upnp_object as Container, depth + 1, music_tracks, video_tracks);
                        }

                        if (music_tracks.Count > 500) {
                            AddMusic (music_tracks);
                            music_tracks.Clear ();
                        }
                        if (video_tracks.Count > 100) {
                            AddVideo (video_tracks);
                            video_tracks.Clear ();
                        }
                    }
                });
        }

        public void Disconnect ()
        {
            if (music_source != null) {
                music_source.Disconnect ();
            }

            if (video_source != null) {
                video_source.Disconnect ();
            }
        }

        private void AddMusic (List<MusicTrack> tracks)
        {
            if (music_source == null) {
                music_source = new UPnPMusicSource (udn);
                AddChildSource (music_source);
            }

            music_source.AddTracks (tracks);
        }

        private void AddVideo (List<VideoItem> tracks)
        {
            if (video_source == null) {
                video_source = new UPnPVideoSource (udn);
                AddChildSource (video_source);
            }

            video_source.AddTracks (tracks);
        }

        public override bool? AutoExpand {
            get { return expanded_schema.Get (); }
        }

        public override bool Expanded {
            get { return expanded_schema.Get (); }
            set { expanded_schema.Set (value); }
        }

        public override bool CanActivate {
            get { return false; }
        }

        public override bool CanRename {
            get { return false; }
        }
    }
}
