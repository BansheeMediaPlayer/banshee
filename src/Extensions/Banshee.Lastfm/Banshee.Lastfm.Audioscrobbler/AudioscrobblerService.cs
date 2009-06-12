//
// AudioscrobblerService.cs
//
// Authors:
//   Alexander Hixon <hixon.alexander@mediati.org>
//   Chris Toshok <toshok@ximian.com>
//   Ruben Vermeersch <ruben@savanne.be>
//   Aaron Bockover <aaron@abock.org>
//
// Copyright (C) 2005-2008 Novell, Inc.
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
using System.Text;
using System.Security.Cryptography;
using Gtk;
using Mono.Unix;

using Hyena;

using Lastfm;
using Lastfm.Gui;

using Banshee.MediaEngine;
using Banshee.Base;
using Banshee.Configuration;
using Banshee.ServiceStack;
using Banshee.Gui;
using Banshee.Networking;

using Banshee.Collection;

using Browser = Lastfm.Browser;

namespace Banshee.Lastfm.Audioscrobbler
{
    public class AudioscrobblerService : IExtensionService, IDisposable
    {
        private AudioscrobblerConnection connection;
        private ActionGroup actions;
        private uint ui_manager_id;
        private InterfaceActionService action_service;
        private Queue queue;        
        private Account account;
        
        private bool queued; /* if current_track has been queued */
        private bool now_playing_sent = false; /* self explanitory :) */
        private int iterate_countdown = 4 * 4; /* number of times to wait for iterate event before sending now playing */
        
        private DateTime song_start_time;
        private TrackInfo last_track;
    
        public AudioscrobblerService ()
        {
        }
        
        void IExtensionService.Initialize ()
        {
            account = LastfmCore.Account;
            
            if (account.UserName == null) {
                account.UserName = LastUserSchema.Get ();
                account.CryptedPassword = LastPassSchema.Get ();
                account.ScrobbleUrl = LastScrobbleUrlSchema.Get ();
            }
            
            if (LastfmCore.UserAgent == null) {
                LastfmCore.UserAgent = Banshee.Web.Browser.UserAgent;
            }
            
            Browser.Open = Banshee.Web.Browser.Open;
            
            queue = new Queue ();
            LastfmCore.AudioscrobblerQueue = queue;
            connection = LastfmCore.Audioscrobbler;
            
            Network network = ServiceManager.Get<Network> ();
            connection.UpdateNetworkState (network.Connected);
            network.StateChanged += delegate (object o, NetworkStateChangedArgs args) {
                connection.UpdateNetworkState (args.Connected);
            };
            
            // Update the Visit action menu item if we update our account info
            LastfmCore.Account.Updated += delegate (object o, EventArgs args) {
                actions["AudioscrobblerVisitAction"].Sensitive = LastfmCore.Account.UserName != null 
                    && LastfmCore.Account.CryptedPassword != null;
            };
            
            ServiceManager.PlayerEngine.ConnectEvent (OnPlayerEvent, 
                PlayerEvent.StartOfStream |
                PlayerEvent.EndOfStream |
                PlayerEvent.Seek |
                PlayerEvent.Iterate);
            
            action_service = ServiceManager.Get<InterfaceActionService> ("InterfaceActionService");
            InterfaceInitialize ();
        }
        
        public void InterfaceInitialize ()
        {
            actions = new ActionGroup ("Audioscrobbler");
            
            actions.Add (new ActionEntry [] {
                new ActionEntry ("AudioscrobblerAction", null,
                    Catalog.GetString ("_Last.fm"), null,
                    Catalog.GetString ("Configure the Audioscrobbler plugin"), null),
                    
                new ActionEntry ("AudioscrobblerVisitAction", null,
                    Catalog.GetString ("Visit _User Profile Page"), null,
                    Catalog.GetString ("Visit Your Last.fm Profile Page"), OnVisitOwnProfile),
                
                new ActionEntry ("AudioscrobblerConfigureAction", Stock.Properties,
                    Catalog.GetString ("_Configure..."), null,
                    Catalog.GetString ("Configure the Last.fm Extension"), OnConfigurePlugin)
            });
            
            actions.Add (new ToggleActionEntry [] { 
                new ToggleActionEntry ("AudioscrobblerEnableAction", null,
                    Catalog.GetString ("_Enable Song Reporting"), "<control>U",
                    Catalog.GetString ("Enable song reporting"), OnToggleEnabled, Enabled)
            });
            
            action_service.UIManager.InsertActionGroup (actions, 0);
            ui_manager_id = action_service.UIManager.AddUiFromResource ("AudioscrobblerMenu.xml");
            
            actions["AudioscrobblerVisitAction"].Sensitive = account.UserName != null && account.UserName != String.Empty;
        }
        
  
        public void Dispose ()
        {
            // Try and queue the currently playing track just in case it's queueable
            // but the user hasn't hit next yet and quit/disposed the service.
            if (ServiceManager.PlayerEngine.CurrentTrack != null) {
                Queue (ServiceManager.PlayerEngine.CurrentTrack);
            }
            
            ServiceManager.PlayerEngine.DisconnectEvent (OnPlayerEvent);
            
            // When we stop the connection, queue ends up getting saved too, so the
            // track we queued earlier should stay until next session.
            connection.Stop ();
        
            action_service.UIManager.RemoveUi (ui_manager_id);
            action_service.UIManager.RemoveActionGroup (actions);
            actions = null;
        }
        
        // We need to time how long the song has played
        internal class SongTimer
        {
            private long playtime = 0;  // number of msecs played
            public long PlayTime {
                get { return playtime; }
            }
            
            private long previouspos = 0;
            
            // number of events to ignore to get sync (since events may be fired in wrong order)
            private int ignorenext = 0;
                        
            public void IncreasePosition ()
            {
                long increase = 0;
                
                if (ignorenext == 0) {
                    increase = (ServiceManager.PlayerEngine.Position - previouspos);
                    if (increase > 0) {
                        playtime += increase;
                    }
                } else {
                    ignorenext--;
                }
                
                previouspos = ServiceManager.PlayerEngine.Position;
            }
            
            public void SkipPosition ()
            {                
                // Set newly seeked position
                previouspos = ServiceManager.PlayerEngine.Position;
                ignorenext = 2; // allow 2 iterates to sync
            }
            
            public void Reset ()
            {
                playtime = 0;
                previouspos = 0;
                ignorenext = 0;
            }
        }
        
        SongTimer st = new SongTimer ();
        
        private void Queue (TrackInfo track) {
            if (track == null || st.PlayTime == 0 || 
                !(actions["AudioscrobblerEnableAction"] as ToggleAction).Active) {
                
                return;
            }
            
            Log.DebugFormat ("Track {3} had playtime of {0} msec ({4}sec), duration {1} msec, queued: {2}",
                st.PlayTime, track.Duration.TotalMilliseconds, queued, track, st.PlayTime / 1000);
            
            if (!queued && track.Duration.TotalSeconds > 30 && 
                (track.MediaAttributes & TrackMediaAttributes.Music) != 0 && 
                !String.IsNullOrEmpty (track.ArtistName) && !String.IsNullOrEmpty (track.TrackTitle) &&
                (st.PlayTime > track.Duration.TotalMilliseconds / 2 || st.PlayTime > 240 * 1000)) {
                    if (!connection.Started) {
                        connection.Start ();
                    }
                    
                    queue.Add (track, song_start_time);
                    queued = true;
            }
        }
        
        private void OnPlayerEvent (PlayerEventArgs args)
        {
            switch (args.Event) {
                case PlayerEvent.StartOfStream:
                    // Queue the previous track in case of a skip
                    Queue (last_track);
                
                    st.Reset ();
                    song_start_time = DateTime.Now;
                    last_track = ServiceManager.PlayerEngine.CurrentTrack;
                    queued = false;
                    now_playing_sent = false;
                    iterate_countdown = 4 * 4;  /* we get roughly 4 events/sec */
                    
                    break;
                
                case PlayerEvent.Seek:
                    st.SkipPosition ();
                    break;
                
                case PlayerEvent.Iterate:
                    // Queue as now playing
                    if (!now_playing_sent && iterate_countdown == 0) {
                        if (last_track != null && last_track.Duration.TotalSeconds > 30 &&
                            (actions["AudioscrobblerEnableAction"] as ToggleAction).Active &&
                            (last_track.MediaAttributes & TrackMediaAttributes.Music) != 0) {
                            
                            connection.NowPlaying (last_track.ArtistName, last_track.TrackTitle,
                                last_track.AlbumTitle, last_track.Duration.TotalSeconds, last_track.TrackNumber);
                        }
                        
                        now_playing_sent = true;
                    } else if (iterate_countdown > 0) {
                        iterate_countdown --;
                    }
                    
                    st.IncreasePosition ();
                    break;
                
                case PlayerEvent.EndOfStream:
                    Queue (ServiceManager.PlayerEngine.CurrentTrack);
                    iterate_countdown = 4 * 4; 
                    break;
            }
        }
        
        private void OnConfigurePlugin (object o, EventArgs args)
        {
            AccountLoginDialog dialog = new AccountLoginDialog (account, true);
            dialog.SaveOnEdit = true;
            if (account.UserName == null) {
                dialog.AddSignUpButton ();
            }
            dialog.Run ();
            dialog.Destroy ();
        }
        
        private void OnVisitOwnProfile (object o, EventArgs args)
        {
            account.VisitUserProfile (account.UserName);
        }
        
        private void OnToggleEnabled (object o, EventArgs args)
        {
            Enabled = (o as ToggleAction).Active;
        }
        
        internal bool Enabled {
            get { return EngineEnabledSchema.Get (); }
            set { 
                EngineEnabledSchema.Set (value);
                (actions["AudioscrobblerEnableAction"] as ToggleAction).Active = value;
            }
        }
           
        public static readonly SchemaEntry<string> LastUserSchema = new SchemaEntry<string> (
            "plugins.lastfm", "username", "", "Last.fm user", "Last.fm username"
        );

        public static readonly SchemaEntry<string> LastPassSchema = new SchemaEntry<string> (
            "plugins.lastfm", "password_hash", "", "Last.fm password", "Last.fm password (hashed)"
        );
   
        public static readonly SchemaEntry<string> LastScrobbleUrlSchema = new SchemaEntry<string> (
            "plugins.audioscrobbler", "api_url",
            null,
            "AudioScrobbler API URL",
            "URL for the AudioScrobbler API (supports turtle.libre.fm, for instance)"
        );

        public static readonly SchemaEntry<bool> EngineEnabledSchema = new SchemaEntry<bool> (
            "plugins.audioscrobbler", "engine_enabled",
            false,
            "Engine enabled",
            "Audioscrobbler reporting engine enabled"
        );
        
        string IService.ServiceName {
            get { return "AudioscrobblerService"; }
        }
    }
}
