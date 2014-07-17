//
// MediaPlayer.cs
//
// Authors:
//   John Millikin <jmillikin@gmail.com>
//   Bertrand Lorentz <bertrand.lorentz@gmail.com>
//
// Copyright (C) 2009 John Millikin
// Copyright (C) 2010 Bertrand Lorentz
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
using System.Linq;
using System.Text;

using DBus;
using Hyena;

using Banshee.Gui;
using Banshee.MediaEngine;
using Banshee.PlaybackController;
using Banshee.Playlist;
using Banshee.ServiceStack;
using Banshee.Sources;

namespace Banshee.Mpris
{
    public class MediaPlayer : IMediaPlayer, IPlayer, IPlaylists, IProperties
    {
        private static string mediaplayer_interface_name = "org.mpris.MediaPlayer2";
        private static string player_interface_name = "org.mpris.MediaPlayer2.Player";
        private static string playlists_interface_name = "org.mpris.MediaPlayer2.Playlists";
        private PlaybackControllerService playback_service;
        private PlayerEngineService engine_service;
        private Gtk.ToggleAction fullscreen_action;
        private Dictionary<string, AbstractPlaylistSource> playlist_sources;
        private Dictionary<string, object> current_properties;
        private Dictionary<string, object> changed_properties;
        private List<string> invalidated_properties;

        private static ObjectPath path = new ObjectPath ("/org/mpris/MediaPlayer2");
        public static ObjectPath Path {
            get { return path; }
        }

        private event PropertiesChangedHandler properties_changed;
        event PropertiesChangedHandler IProperties.PropertiesChanged {
            add { properties_changed += value; }
            remove { properties_changed -= value; }
        }

        private event DBusPlayerSeekedHandler dbus_seeked;
        event DBusPlayerSeekedHandler IPlayer.Seeked {
            add { dbus_seeked += value; }
            remove { dbus_seeked -= value; }
        }

        private event PlaylistChangedHandler playlist_changed;
        event PlaylistChangedHandler IPlaylists.PlaylistChanged {
            add { playlist_changed += value; }
            remove { playlist_changed -= value; }
        }

        public MediaPlayer ()
        {
            playback_service = ServiceManager.PlaybackController;
            engine_service = ServiceManager.PlayerEngine;
            playlist_sources = new Dictionary<string, AbstractPlaylistSource> ();
            changed_properties = new Dictionary<string, object> ();
            current_properties = new Dictionary<string, object> ();
            invalidated_properties = new List<string> ();

            var interface_service = ServiceManager.Get<InterfaceActionService> ();
            fullscreen_action = interface_service.ViewActions["FullScreenAction"] as Gtk.ToggleAction;
        }

#region IMediaPlayer

        public bool CanQuit {
            get { return true; }
        }

        public bool CanRaise {
            get { return true; }
        }

        public bool Fullscreen {
            get {
                if (fullscreen_action != null) {
                    return fullscreen_action.Active;
                }
                return false;
            }
            set {
                if (fullscreen_action != null) {
                    fullscreen_action.Active = value;
                }
            }
        }

        public bool CanSetFullscreen {
            get { return true; }
        }

        public bool HasTrackList {
            get { return false; }
        }

        public string Identity {
            get { return "Banshee"; }
        }

        public string DesktopEntry {
            get { return "banshee"; }
        }

        // This is just a list of commonly supported MIME types.
        // We don't know exactly which ones are supported by the PlayerEngine
        private static string [] supported_mimetypes = { "application/ogg", "audio/flac",
            "audio/mp3", "audio/mp4", "audio/mpeg", "audio/ogg", "audio/vorbis", "audio/wav",
            "audio/x-flac", "audio/x-vorbis+ogg",
            "video/avi", "video/mp4", "video/mpeg" };
        public string [] SupportedMimeTypes {
            get { return supported_mimetypes; }
        }

        // We can't use the PlayerEngine.SourceCapabilities property here, because
        // the OpenUri method only supports "file" and "http".
        private static string [] supported_uri_schemes = { "file", "http" };
        public string [] SupportedUriSchemes {
            get { return supported_uri_schemes; }
        }

        public void Raise ()
        {
            if (!CanRaise) {
                return;
            }

            ServiceManager.Get<GtkElementsService> ().PrimaryWindow.SetVisible (true);
        }

        public void Quit ()
        {
            if (!CanQuit) {
                return;
            }

            Application.Shutdown ();
        }

#endregion

#region IPlayer

        public bool CanControl {
            get { return true; }
        }

        // We don't really know if we can actually go next or previous
        public bool CanGoNext {
            get { return CanControl; }
        }

        public bool CanGoPrevious {
            get { return CanControl; }
        }

        public bool CanPause {
            get { return engine_service.CanPause; }
        }

        public bool CanPlay {
            get { return CanControl; }
        }

        public bool CanSeek {
            get { return engine_service.CanSeek; }
        }

        public double MaximumRate {
            get { return 1.0; }
        }

        public double MinimumRate {
            get { return 1.0; }
        }

        public double Rate {
            get { return 1.0; }
            set {}
        }

        public bool Shuffle {
            get { return !(playback_service.ShuffleMode == "off"); }
            set { playback_service.ShuffleMode = value ? "song" : "off"; }
        }

        public string LoopStatus {
            get {
                string loop_status;
                switch (playback_service.RepeatMode) {
                    case PlaybackRepeatMode.None:
                        loop_status = "None";
                        break;
                    case PlaybackRepeatMode.RepeatSingle:
                        loop_status = "Track";
                        break;
                    case PlaybackRepeatMode.RepeatAll:
                        loop_status = "Playlist";
                        break;
                    default:
                        loop_status = "None";
                        break;
                }
                return loop_status;
            }
            set {
                switch (value) {
                    case "None":
                        playback_service.RepeatMode = PlaybackRepeatMode.None;
                        break;
                    case "Track":
                        playback_service.RepeatMode = PlaybackRepeatMode.RepeatSingle;
                        break;
                    case "Playlist":
                        playback_service.RepeatMode = PlaybackRepeatMode.RepeatAll;
                        break;
                }
            }
        }

        public string PlaybackStatus {
            get {
                string status;
                switch (engine_service.CurrentState) {
                    case PlayerState.Playing:
                        status = "Playing";
                        break;
                    case PlayerState.Paused:
                        status = "Paused";
                        break;
                    default:
                        status = "Stopped";
                        break;
                }
                return status;
            }
        }

        public IDictionary<string, object> Metadata {
            get {
                var metadata = new Metadata (engine_service.CurrentTrack);
                return metadata.DataStore;
            }
        }

        public double Volume {
            get { return engine_service.Volume / 100.0; }
            set { engine_service.Volume = (ushort)Math.Round (value * 100); }
        }

        // Position is expected in microseconds
        public long Position {
            get { return (long)engine_service.Position * 1000; }
        }

        public void Next ()
        {
            playback_service.Next ();
        }

        public void Previous ()
        {
            playback_service.Previous ();
        }

        public void Pause ()
        {
            engine_service.Pause ();
        }

        public void PlayPause ()
        {
            engine_service.TogglePlaying ();
        }

        public void Stop ()
        {
            engine_service.Close ();
        }

        public void Play ()
        {
            engine_service.Play ();
        }

        public void SetPosition (ObjectPath trackid, long position)
        {
            if (!CanSeek) {
                return;
            }

            if (trackid == null || trackid != (ObjectPath)Metadata["mpris:trackid"]) {
                return;
            }

            // position is in microseconds, we speak in milliseconds
            long position_ms = position / 1000;
            if (position_ms < 0 || position_ms > engine_service.CurrentTrack.Duration.TotalMilliseconds) {
                return;
            }

            engine_service.Position = (uint)position_ms;
        }

        public void Seek (long position)
        {
            if (!CanSeek) {
                return;
            }

            // position is in microseconds, relative to the current position and can be negative
            long new_pos = (int)engine_service.Position + (position / 1000);
            if (new_pos < 0) {
                engine_service.Position = 0;
            } else {
                engine_service.Position = (uint)new_pos;
            }
        }

        public void OpenUri (string uri)
        {
            Banshee.Streaming.RadioTrackInfo.OpenPlay (uri);
        }

#endregion

#region IPlaylists
        public uint PlaylistCount {
            get {
                return (uint)ServiceManager.SourceManager.FindSources<AbstractPlaylistSource> ().Count ();
            }
        }

        private static string [] supported_playlist_orderings = { "Alphabetical", "UserDefined" };
        public string [] Orderings {
            get { return supported_playlist_orderings; }
        }

        private static Playlist dummy_playlist = new Playlist {
            Id = new ObjectPath (DBusServiceManager.ObjectRoot),
            Name = "",
            Icon = "" };
        public MaybePlaylist ActivePlaylist {
            get {
                // We want the source that is currently playing
                var playlist_source = ServiceManager.PlaybackController.Source as AbstractPlaylistSource;
                if (playlist_source == null) {
                    return new MaybePlaylist { Valid = false, Playlist = dummy_playlist };
                } else {
                    return new MaybePlaylist { Valid = true,
                        Playlist = BuildPlaylistFromSource (playlist_source) };
                }
            }
        }

        private ObjectPath MakeObjectPath (AbstractPlaylistSource playlist)
        {
            StringBuilder object_path_builder = new StringBuilder ();

            object_path_builder.Append (DBusServiceManager.ObjectRoot);
            if (playlist.Parent != null) {
                object_path_builder.AppendFormat ("/{0}", DBusServiceManager.MakeDBusSafeString (playlist.Parent.TypeName));
            }
            object_path_builder.Append ("/Playlists/");

            object_path_builder.Append (DBusServiceManager.MakeDBusSafeString (playlist.UniqueId));

            string object_path = object_path_builder.ToString ();
            playlist_sources[object_path] = playlist;

            return new ObjectPath (object_path);
        }

        private string GetIconPath (Source source)
        {
            string icon_name = "image-missing";
            Type icon_type = source.Properties.GetType ("Icon.Name");

            if (icon_type == typeof (string)) {
                icon_name = source.Properties.Get<string> ("Icon.Name");
            } else if (icon_type == typeof (string [])) {
                icon_name = source.Properties.Get<string[]> ("Icon.Name")[0];
            }

            string icon_path = Paths.Combine (Paths.GetInstalledDataDirectory ("icons"),
                                       "hicolor", "22x22", "categories",
                                       String.Concat (icon_name, ".png"));

            return String.Concat ("file://", icon_path);
        }

        private Playlist BuildPlaylistFromSource (AbstractPlaylistSource source)
        {
            var mpris_playlist = new Playlist ();
            mpris_playlist.Name = source.Name;
            mpris_playlist.Id = MakeObjectPath (source);
            mpris_playlist.Icon = GetIconPath (source);

            return mpris_playlist;
        }

        public void ActivatePlaylist (ObjectPath playlist_id)
        {
            // TODO: Maybe try to find the playlist if it's not in the dictionary ?
            var playlist = playlist_sources[playlist_id.ToString ()];

            if (playlist != null) {
                Log.DebugFormat ("MPRIS activating playlist {0}", playlist.Name);
                ServiceManager.SourceManager.SetActiveSource (playlist);
                ServiceManager.PlaybackController.Source = playlist;
                ServiceManager.PlaybackController.First ();
            }
        }

        public Playlist [] GetPlaylists (uint index, uint max_count, string order, bool reverse_order)
        {
            var playlist_sources = ServiceManager.SourceManager.FindSources<AbstractPlaylistSource> ();

            switch (order) {
                case "Alphabetical":
                    playlist_sources = playlist_sources.OrderBy (p => p.Name);
                    break;
                case "UserDefined":
                    playlist_sources = playlist_sources.OrderBy (p => p.Order);
                    break;
            }
            if (reverse_order) {
                playlist_sources = playlist_sources.Reverse ();
            }

            var playlists = new List<Playlist> ();
            foreach (var pl in playlist_sources.Skip ((int)index).Take ((int)max_count)) {
                playlists.Add (BuildPlaylistFromSource (pl));
            }
            return playlists.ToArray ();
        }
#endregion

#region Signals

        private void HandlePropertiesChange (string interface_name)
        {
            PropertiesChangedHandler handler = properties_changed;
            if (handler != null) {
                lock (changed_properties) {
                    try {
                        handler (interface_name, changed_properties, invalidated_properties.ToArray ());
                    } catch (Exception e) {
                        Log.Error (e);
                    }
                    changed_properties.Clear ();
                    invalidated_properties.Clear ();
                }
            }
        }

        public void HandleSeek ()
        {
            DBusPlayerSeekedHandler dbus_handler = dbus_seeked;
            if (dbus_handler != null) {
                dbus_handler (Position);
            }
        }

        public void HandlePlaylistChange (AbstractPlaylistSource source)
        {
            PlaylistChangedHandler playlist_handler = playlist_changed;
            if (playlist_handler != null) {
                try {
                    playlist_handler (BuildPlaylistFromSource (source));
                } catch (Exception e) {
                    Log.Error (e);
                }
            }
        }

        public void AddPropertyChange (params PlayerProperties [] properties)
        {
            AddPropertyChange (player_interface_name, properties.Select (p => p.ToString()));
        }

        public void AddPropertyChange (params MediaPlayerProperties [] properties)
        {
            AddPropertyChange (mediaplayer_interface_name, properties.Select (p => p.ToString()));
        }

        public void AddPropertyChange (params PlaylistProperties [] properties)
        {
            AddPropertyChange (playlists_interface_name, properties.Select (p => p.ToString()));
        }

        private void AddPropertyChange (string interface_name, IEnumerable<string> property_names)
        {
            lock (changed_properties) {
                foreach (string prop_name in property_names) {
                    object current_value = null;
                    current_properties.TryGetValue (prop_name, out current_value);
                    var new_value = Get (interface_name, prop_name);
                    if ((current_value == null) || !(current_value.Equals (new_value))) {
                        changed_properties [prop_name] = new_value;
                        current_properties [prop_name] = new_value;
                    }
                }
                if (changed_properties.Count > 0) {
                    HandlePropertiesChange (interface_name);
                }
            }
        }

#endregion

#region Dbus.Properties

        private static string [] mediaplayer_properties = { "CanQuit", "CanRaise", "CanSetFullscreen", "Fullscreen",
            "HasTrackList", "Identity", "DesktopEntry", "SupportedMimeTypes", "SupportedUriSchemes" };

        private static string [] player_properties = { "CanControl", "CanGoNext", "CanGoPrevious", "CanPause",
            "CanPlay", "CanSeek", "LoopStatus", "MaximumRate", "Metadata", "MinimumRate", "PlaybackStatus",
            "Position", "Rate", "Shuffle", "Volume" };

        private static string [] playlist_properties = { "Orderings", "PlaylistCount", "ActivePlaylist" };

        public object Get (string interface_name, string propname)
        {
            if (interface_name == mediaplayer_interface_name) {
                switch (propname) {
                    case "CanQuit":
                        return CanQuit;
                    case "CanRaise":
                        return CanRaise;
                    case "Fullscreen":
                        return Fullscreen;
                    case "CanSetFullscreen":
                        return CanSetFullscreen;
                    case "HasTrackList":
                        return HasTrackList;
                    case "Identity":
                        return Identity;
                    case "DesktopEntry":
                        return DesktopEntry;
                    case "SupportedMimeTypes":
                        return SupportedMimeTypes;
                    case "SupportedUriSchemes":
                        return SupportedUriSchemes;
                    default:
                        return null;
                }
            } else if (interface_name == player_interface_name) {
                switch (propname) {
                    case "CanControl":
                        return CanControl;
                    case "CanGoNext":
                        return CanGoNext;
                    case "CanGoPrevious":
                        return CanGoPrevious;
                    case "CanPause":
                        return CanPause;
                    case "CanPlay":
                        return CanPlay;
                    case "CanSeek":
                        return CanSeek;
                    case "MinimumRate":
                        return MinimumRate;
                    case "MaximumRate":
                        return MaximumRate;
                    case "Rate":
                        return Rate;
                    case "Shuffle":
                        return Shuffle;
                    case "LoopStatus":
                        return LoopStatus;
                    case "PlaybackStatus":
                        return PlaybackStatus;
                    case "Position":
                        return Position;
                    case "Metadata":
                        return Metadata;
                    case "Volume":
                        return Volume;
                    default:
                        return null;
                }
            } else if (interface_name == playlists_interface_name) {
                switch (propname) {
                    case "Orderings":
                        return Orderings;
                    case "PlaylistCount":
                        return PlaylistCount;
                    case "ActivePlaylist":
                        return ActivePlaylist;
                    default:
                        return null;
                }
            } else {
                return null;
            }
        }

        public void Set (string interface_name, string propname, object value)
        {
            if (interface_name == player_interface_name) {
                switch (propname) {
                case "LoopStatus":
                    string s = value as string;
                    if (!String.IsNullOrEmpty (s)) {
                        LoopStatus = s;
                    }
                    break;
                case "Shuffle":
                    if (value is bool) {
                        Shuffle = (bool)value;
                    }
                    break;
                case "Volume":
                    if (value is double) {
                        Volume = (double)value;
                    }
                    break;
                }
            }  else if (interface_name == mediaplayer_interface_name) {
                switch (propname) {
                case "Fullscreen":
                    if (value is bool) {
                        Fullscreen = (bool)value;
                    }
                    break;
                }
            }
        }

        public IDictionary<string, object> GetAll (string interface_name)
        {
            var props = new Dictionary<string, object> ();

            if (interface_name == mediaplayer_interface_name) {
                foreach (string prop in mediaplayer_properties) {
                    props.Add (prop, Get (interface_name, prop));
                }
            } else if (interface_name == player_interface_name) {
                foreach (string prop in player_properties) {
                    props.Add (prop, Get (interface_name, prop));
                }
            } else if (interface_name == playlists_interface_name) {
                foreach (string prop in playlist_properties) {
                    props.Add (prop, Get (interface_name, prop));
                }
            }

            return props;
        }
    }

#endregion

    // Those are all the properties from the Player interface that can trigger the PropertiesChanged signal
    // The names must match exactly the names of the properties
    public enum PlayerProperties
    {
        CanControl,
        CanGoNext,
        CanGoPrevious,
        CanPause,
        CanPlay,
        CanSeek,
        MinimumRate,
        MaximumRate,
        Rate,
        Shuffle,
        LoopStatus,
        PlaybackStatus,
        Metadata,
        Volume
    }

    // Those are all the properties from the MediaPlayer interface that can trigger the PropertiesChanged signal
    // The names must match exactly the names of the properties
    public enum MediaPlayerProperties
    {
        Fullscreen
    }

    // Those are all the properties from the Playlist interface that can trigger the PropertiesChanged signal
    // The names must match exactly the names of the properties
    public enum PlaylistProperties
    {
        PlaylistCount,
        ActivePlaylist
    }
}
