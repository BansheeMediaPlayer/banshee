/***************************************************************************
 *  StationSource.cs
 *
 *  Copyright (C) 2007 Novell, Inc.
 *  Written by Gabriel Burt <gabriel.burt@gmail.com>
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
using System.Data;
using System.Collections.Generic;
using System.Threading;
using Mono.Gettext;
using Gtk;

using Banshee.Base;
using Banshee.Widgets;
using Banshee.Database;
using Banshee.Sources;
using Banshee.Metadata;
using Banshee.MediaEngine;
using Banshee.Playlists.Formats.Xspf;
 
namespace Banshee.Plugins.LastFM
{
    public class StationSource : ChildSource
    {
        private static readonly Gdk.Pixbuf refresh_pixbuf = IconThemeUtils.LoadIcon (22, Stock.Refresh);
        private static readonly Gdk.Pixbuf error_pixbuf = IconThemeUtils.LoadIcon (22, Stock.DialogError);

        private List<TrackInfo> tracks = new List<TrackInfo> ();
        private HighlightMessageArea status_bar;

        private string station;
        public string Station {
            get { return station; }
            protected set { station = value; }
        }

        private StationType type;
        public StationType Type {
            get { return type; }
            set {
                type = value;
                if (icon != null) {
                    icon.Dispose ();
                    icon = null;
                }
            }
        }

        private string arg;
        public string Arg {
            get { return arg; }
            set { arg = value; }
        }

        private int play_count;
        public int PlayCount {
            get { return play_count; }
            set { play_count = value; }
        }

        private int dbid;
        
        // For StationSources that already exist in the db
        protected StationSource (int dbId, string name, string type, string arg, int playCount) : base (name, 150)
        {
            dbid = dbId;
            Type = StationType.FindByName (type);
            Arg = arg;
            PlayCount = playCount;
            Station = Type.GetStationFor (arg);

            PlayerEngineCore.StateChanged += OnPlayerStateChanged;
            Connection.Instance.StateChanged += HandleConnectionStateChanged;

            BuildInterface ();
        }

        public StationSource (string name, string type, string arg) : base (name, 150)
        {
            Type = StationType.FindByName (type);
            Arg = arg;
            Station = Type.GetStationFor (arg);

            DbCommand command = new DbCommand (@"
                INSERT INTO LastfmStations
                    (Creator, Name, Type, Arg, PlayCount)
                    VALUES (:creator, :name, :type, :arg, :play_count)",
                    "creator", Connection.Instance.Username, 
                    "name", Name, 
                    "type", Type.ToString (),
                    "arg", Arg,
                    "play_count", PlayCount
            );

            dbid = Globals.Library.Db.Execute (command);

            PlayerEngineCore.StateChanged += OnPlayerStateChanged;
            Connection.Instance.StateChanged += HandleConnectionStateChanged;

            BuildInterface ();
        }

        private void BuildInterface ()
        {
            //box = new VBox ();
            status_bar = new HighlightMessageArea ();
            status_bar.BorderWidth = 5;
            status_bar.LeftPadding = 15;
            status_bar.Hide ();
            UpdateUI (Connection.Instance.State);
        }

        protected override void OnDispose ()
        {
            PlayerEngineCore.StateChanged -= OnPlayerStateChanged;
            Connection.Instance.StateChanged -= HandleConnectionStateChanged;
        }

        public override void Commit ()
        {
            DbCommand command = new DbCommand (@"
                UPDATE LastfmStations
                SET 
                    Name = :name,
                    Type = :type,
                    Arg = :arg,
                    PlayCount = :play_count
                WHERE StationID = :station_id",
                "name", Name,
                "type", Type.ToString (),
                "arg", Arg,
                "play_count", PlayCount,
                "station_id", dbid
            );
            Globals.Library.Db.Execute (command);

            Station = Type.GetStationFor (Arg);
            OnUpdated ();
        }
        
        private bool shuffle;
        public override void Activate ()
        {
            shuffle = (Globals.ActionManager["ShuffleAction"] as ToggleAction).Active;
            (Globals.ActionManager["ShuffleAction"] as ToggleAction).Active = false;
            //Globals.ActionManager["ShuffleAction"].Sensitive = false;
 
            if (show_status)
                status_bar.Show ();
            else
                status_bar.Hide ();

            Globals.ActionManager["PreviousAction"].Sensitive = false;
            InterfaceElements.MainContainer.PackEnd (status_bar, false, false, 0);

            // We lazy load the Last.fm connection, so if we're not already connected, do it
            if (Connection.Instance.State == ConnectionState.Connected)
                TuneAndLoad ();
            else if (Connection.Instance.State == ConnectionState.Disconnected)
                Connection.Instance.Connect ();
        }

        private void TuneAndLoad ()
        {
            ThreadPool.QueueUserWorkItem (delegate {
                if (ChangeToThisStation ()) {
                    Thread.Sleep (250); // sleep for a bit to try to avoid Last.fm timeouts
                    if (TracksLeft < 2)
                        Refresh ();
                    else
                        HideStatus ();
                }
            });
        }

        public override void Deactivate ()
        {
            (Globals.ActionManager["ShuffleAction"] as ToggleAction).Active = shuffle;
            //Globals.ActionManager["ShuffleAction"].Sensitive = true;

            Globals.ActionManager["PreviousAction"].Sensitive = true;
            InterfaceElements.MainContainer.Remove (status_bar);
        }

        // Last.fm requires you to 'tune' to a station before requesting a track list/playing it
        public bool ChangeToThisStation ()
        {
            if (Connection.Instance.Station == Station)
                return false;

            LogCore.Instance.PushDebug (String.Format ("Tuning Last.fm to {0}", Name), null);
            SetStatus (Catalog.GetString ("Tuning Last.fm to {0}."), false);
            StationError error = Connection.Instance.ChangeStationTo (Station);

            if (error == StationError.None) {
                LogCore.Instance.PushDebug (String.Format ("Successfully tuned Last.fm to {0}", station), null);
                return true;
            } else {
                LogCore.Instance.PushDebug (String.Format ("Failed to tune Last.fm to {0}", Name), Connection.ErrorMessageFor (error));
                SetStatus (String.Format (
                    // Translators: {0} is an error message sentence from Connection.cs.
                    Catalog.GetString ("Failed to tune in station. {0}"), Connection.ErrorMessageFor (error)), true
                );
                return false;
            }
        }

        bool show_status = false;
        private void SetStatus (string message, bool error)
        {
            ThreadAssist.ProxyToMain (delegate {
                show_status = true;
                string status_name = String.Format ("<i>{0}</i>", GLib.Markup.EscapeText (Name));
                status_bar.Message = String.Format ("<big>{0}</big>", String.Format (GLib.Markup.EscapeText (message), status_name));
                status_bar.Pixbuf = error ? error_pixbuf : refresh_pixbuf;
                status_bar.ShowCloseButton = true;
                status_bar.Show ();
            });
        }

        private void HideStatus ()
        {
            ThreadAssist.ProxyToMain (delegate {
                show_status = false;
                status_bar.Hide ();
            });
        }

        public override void ShowPropertiesDialog ()
        {
            Editor ed = new Editor (this);
            ed.RunDialog ();
        }

        /*private bool playback_requested = false;
        public override void StartPlayback ()
        {
            if (CurrentTrack != null) {
                PlayerEngineCore.OpenPlay (CurrentTrack);
            } else if (playback_requested == false) {
                playback_requested = true;
                Refresh ();
            }
        }*/

        private int current_track = 0;
        public TrackInfo CurrentTrack {
            get { return GetTrack (current_track); }
            set {
                int i = tracks.IndexOf (value);
                if (i != -1)
                    current_track = i;
            }
        }

        public TrackInfo NextTrack {
            get { return GetTrack (current_track + 1); }
        }

        private TrackInfo GetTrack (int track_num) {
            return (track_num > tracks.Count - 1) ? null : tracks[track_num];
        }

        private int TracksLeft {
            get {
                int left = tracks.Count - current_track - 1;
                return (left < 0) ? 0 : left;
            }
        }

        private bool refreshing = false;
        public void Refresh ()
        {
            lock (this) {
                if (refreshing || Connection.Instance.Station != Station) {
                    return;
                }
                refreshing = true;
            }

            if (TracksLeft == 0) {
                SetStatus (Catalog.GetString ("Getting new songs for {0}."), false);
            }

            ThreadAssist.Spawn (delegate {
                Playlist playlist = Connection.Instance.LoadPlaylistFor (Station);
                if (playlist != null) {
                    if (playlist.TrackCount == 0) {
                        SetStatus (Catalog.GetString ("No new songs available for {0}."), true);
                    } else {
                        List<TrackInfo> new_tracks = new List<TrackInfo> ();
                        foreach (Track track in playlist.Tracks) {
                            TrackInfo ti = new LastFMTrackInfo (track, this);
                            new_tracks.Add (ti);
                            lock (TracksMutex) {
                                tracks.Add (ti);
                            }
                        }
                        HideStatus ();

                        ThreadAssist.ProxyToMain (delegate {
                            OnTrackAdded (null, new_tracks);
                            OnUpdated ();

                            /*if (playback_requested) {
                                StartPlayback ();
                                playback_requested = false;
                            }*/
                        });
                    }
                } else {
                    SetStatus (Catalog.GetString ("Failed to get new songs for {0}."), true);
                }

                refreshing = false;
            });
        }

        private void ClearTracks ()
        {
            lock (TracksMutex) {
                if (tracks.Count > 0) {
                    OnTrackRemoved (null, tracks);
                    tracks.Clear ();
                }
            }
        }

        private void OnPlayerStateChanged (object o, PlayerEngineStateArgs args)
        {
            if (args.State == PlayerEngineState.Loaded && tracks.Contains (PlayerEngineCore.CurrentTrack)) {
                CurrentTrack = PlayerEngineCore.CurrentTrack;

                lock (TracksMutex) {
                    foreach (TrackInfo track in tracks) {
                        if (track == CurrentTrack)
                            break;
                        if (track.CanPlay) {
                            track.CanPlay = false;
                        }
                    }
                    OnUpdated ();
                }

                if (TracksLeft <= 2) {
                    Refresh ();
                }
            }
        }
        
        private void HandleConnectionStateChanged (object sender, ConnectionStateChangedArgs args)
        {
            UpdateUI (args.State);
        }

        private void UpdateUI (ConnectionState state)
        {
            if (state == ConnectionState.Connected) {
                HideStatus ();
                if (this == SourceManager.ActiveSource) {
                    TuneAndLoad ();
                }
            } else {
                ClearTracks ();
                SetStatus (Connection.MessageFor (state), state != ConnectionState.Connecting);
            }
        }

        public override bool Unmap ()
        {
            Globals.Library.Db.Execute (String.Format (
                @"DELETE FROM LastfmStations
                    WHERE StationID = '{0}'",
                    dbid
            ));
            Parent.RemoveChildSource (this);
            SourceManager.RemoveSource (this);
            return true;
        }

        protected override bool UpdateName (string oldName, string newName)
        {
            Name = newName;
            Commit ();
            return true;
        }
        
        public override bool SearchEnabled {
            get { return false; }
        }
        
        public override bool CanWriteToCD {
            get { return false; }
        }
                
        public override bool ShowPlaylistHeader {
            get { return false; }
        }

        public override string ActionPath {
            get { return "/LastFMStationSourcePopup"; }
        }

        public override IEnumerable<TrackInfo> Tracks {
            get { return tracks; }
        }

        private static string unmap_label = Catalog.GetString ("Delete Last.fm Station");
        public override string UnmapLabel {
            get { return unmap_label; }
        }

        private static string generic_name = Catalog.GetString ("Last.fm Station");
        public override string GenericName {
            get { return generic_name; }
        }

        private static string properties_label = Catalog.GetString ("Edit Last.fm Station");
        public override string SourcePropertiesLabel {
            get { return properties_label; }
        }

        private Gdk.Pixbuf icon;
        public override Gdk.Pixbuf Icon {
            get {
                if (icon == null && Type != null && Type.IconName != null) {
                    icon = IconThemeUtils.LoadIcon (22, Type.IconName);
                    if (icon == null) {
                        icon = Gdk.Pixbuf.LoadFromResource (Type.IconName + ".png");
                    }
                }
                return icon;
            }
        }

        public static List<StationSource> LoadAll (string creator)
        {
            List<StationSource> stations = new List<StationSource> ();

            DbCommand command = new DbCommand (
                "SELECT StationID, Name, Type, Arg, PlayCount FROM LastfmStations WHERE Creator = :creator",
                "creator", creator
            );

            IDataReader reader = Globals.Library.Db.Query (command);

            while (reader.Read ()) {
                try {
                    stations.Add (new StationSource (
                        (int) reader[0], (string) reader[1], (string) reader[2], (string) reader[3], (int) reader[4]
                    ));
                } catch (Exception e) {
                    LogCore.Instance.PushWarning ("Error Loading Last.fm Station", e.ToString (), false);
                }
            }
            reader.Dispose ();

            // Create some default stations if the user has none
            if (stations.Count == 0) {
                stations.Add (new StationSource (Catalog.GetString ("Recommended"), "Recommended", creator));
                stations.Add (new StationSource (Catalog.GetString ("Personal"), "Personal", creator));
                stations.Add (new StationSource (Catalog.GetString ("Loved"), "Loved", creator));
                stations.Add (new StationSource (Catalog.GetString ("Neighbors"), "Neighbor", creator));
            }

            return stations;
        }

        static StationSource ()
        {
            if (!Globals.Library.Db.TableExists ("LastfmStations")) {
                Globals.Library.Db.Execute (@"
                    CREATE TABLE LastfmStations (
                        StationID           INTEGER PRIMARY KEY,
                        Creator             STRING NOT NULL,
                        Name                STRING NOT NULL,
                        Type                STRING NOT NULL,
                        Arg                 STRING NOT NULL,
                        PlayCount           INTEGER NOT NULL
                    )
                ");
            } else {
                try {
                    Globals.Library.Db.QuerySingle ("SELECT PlayCount FROM LastfmStations LIMIT 1");
                } catch {
                    LogCore.Instance.PushDebug ("Adding new database column", "Table: LastfmStations, Column: PlayCount INTEGER");
                    Globals.Library.Db.Execute ("ALTER TABLE LastfmStations ADD PlayCount INTEGER");
                    Globals.Library.Db.Execute ("UPDATE LastfmStations SET PlayCount = 0");
                }
            }
        }
    }
}
