// 
// PaasService.cs
//  
// Author:
//   Mike Urbanski <michael.c.urbanski@gmail.com>
//
// Copyright (C) 2009 Michael C. Urbanski
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

using Mono.Unix;

using Banshee.IO;
using Banshee.Web;
using Banshee.Base;
using Banshee.Sources;
using Banshee.MediaEngine;
using Banshee.ServiceStack;
using Banshee.Configuration;
using Banshee.Collection.Database;

using Banshee.Paas.Gui;
using Banshee.Paas.Utils;
using Banshee.Paas.Aether;

using Banshee.Paas.DownloadManager;
using Banshee.Paas.DownloadManager.Gui;

using Migo2.Async;
using Migo2.DownloadService;

// remove

using Banshee.Paas.Data;
using Hyena.Json;
using Hyena;
//remove

namespace Banshee.Paas
{
    delegate void Updater ();
    
    public partial class PaasService : IExtensionService, IDisposable, IDelayedInitializeService
    {
        private bool disposing, disposed;
                
        private readonly string tmp_download_path = Paths.Combine (
            Paths.ExtensionCacheRoot, "paas", "partial-downloads"
        );

        private PaasSource source;
        
        //private MiroGuideClient mg_client;
        private SyndicationClient syndication_client;
        
        private AutoResetEvent client_handle;
        
        private PaasDownloadManager download_manager;
        private DownloadManagerInterface download_manager_interface;

        private Updater redraw, reload;
        
        private readonly object sync = new object ();

        // There needs to be a DownloadManager service in the service manager.
        // There are issues adding a service to the service manager from an extension...
        // It shouldn't be in the extension anyway.
        public PaasDownloadManager DownloadManager {
            get { return download_manager; }
        }

        public string ServiceName {
            get { return "PaasService"; } 
        }

        public PaasSource Source {
            get { return source; }
        }

        public SyndicationClient SyndicationClient {
            get { return syndication_client; }
        }

        private bool Disposed { 
            get { 
                return (disposed | disposing); 
            }
        }

        public PaasService ()
        {
            reload = delegate {
                lock (sync) {
                    if (Disposed) {
                        return;
                    }
                    
                    source.Reload ();
                }
            };

            redraw = delegate {
                lock (sync) {
                    if (Disposed) {
                        return;
                    }
                    
                    source.QueueDraw ();
                }
            };

            client_handle = new AutoResetEvent (true);
/*            
            mg_client = new MiroGuideClient () {
                Timeout    = (60 * 1000), // one minute.
                ServiceUri = "http://127.0.0.1:8000",
                ClientID   = ClientID.Get (),
                SessionID  = SessionID.Get (),
                UserAgent  = Banshee.Web.Browser.UserAgent,
            };

            mg_client.StateChanged += (sender, e) => {
                if (e.NewState == AetherClientState.Idle) {
                    client_handle.Set ();
                } else {
                    client_handle.Reset ();
                }
            };

            mg_client.RequestDeltasCompleted += ClientUpdatedHandler;
*/
            syndication_client = new SyndicationClient ();
            syndication_client.StateChanged += (sender, e) => {
                lock (sync) {
                    if (!Disposed) {
                        if (e.NewState == AetherClientState.Busy) { 
                            source.SetStatus ("Updating...", false, true, null);                            
                        } else {
                            source.HideStatus ();
                        }
                    }
                }
            };

            syndication_client.ChannelAdded += (sender, e) => { 
                PaasChannel channel = e.Channel;
                
                string escaped = Hyena.StringUtil.EscapeFilename (channel.Name);
                channel.LocalEnclosurePath = Path.Combine (source.BaseDirectory, escaped);
                channel.Save ();
                
                reload ();
            };
            
            syndication_client.ChannelAdded += (sender, e) => {
                lock (sync) {
                    if (Disposed) {
                        return;
                    }

                    syndication_client.QueueUpdate (e.Channel);
                }
                
                reload ();
            };
            
            syndication_client.ChannelRemoved += (sender, e) => { reload (); };

            syndication_client.ChannelUpdateCompleted += OnChannelUpdatedHandler;

            syndication_client.ItemsAdded += OnItemsAddedHandler;
            syndication_client.ItemsRemoved += OnItemsRemovedHandler;
            
            download_manager = new PaasDownloadManager (2, tmp_download_path);
            download_manager.TaskCompleted += OnDownloadTaskCompletedHandler;
            download_manager.TaskStateChanged += (sender, e) => { redraw (); };
        }

        public void UpdateAsync ()
        {
            lock (sync) {
                if (!Disposed) {
                    //mg_client.RequestDeltasAsync ();
                    syndication_client.UpdateAsync ();
                }
            }
        }

        public void Initialize ()
        {        
        }

        public void DelayedInitialize ()
        {
            InitializeInterface ();
        }

        public void Dispose ()
        {                        
            lock (sync) {
                if (Disposed) {
                    return;
                }
                
                disposing = true;               
            }

            DisposeInterface ();
            
            //mg_client.CancelAsync ();
            client_handle.WaitOne ();

            //mg_client.RequestDeltasCompleted -= ClientUpdatedHandler;
            //mg_client = null;

            client_handle.Close ();
            client_handle = null;

            syndication_client.Dispose ();
            syndication_client.ItemsAdded -= OnItemsAddedHandler;
            syndication_client.ItemsRemoved -= OnItemsRemovedHandler;
            syndication_client.ChannelUpdateCompleted -= OnChannelUpdatedHandler;            
            
            syndication_client = null;

            download_manager.Dispose ();
            download_manager.TaskCompleted -= OnDownloadTaskCompletedHandler;
            download_manager = null;
            
            lock (sync) {
                disposing = false;            
                disposed  = true;
            }
        }

        private void InitializeInterface ()
        {
            source = new PaasSource (this);            
            ServiceManager.SourceManager.AddSource (source);

            download_manager_interface = new DownloadManagerInterface (source, download_manager);
        }
        
        private void DisposeInterface ()
        {
            if (source != null) {
                ServiceManager.SourceManager.RemoveSource (source);
                source.Dispose ();
                source = null;
            }

            if (download_manager_interface != null) {
                download_manager_interface.Dispose ();
            }
        }
/*
        private void ApplyUpdate (AetherDelta delta)
        {
            Dictionary<long, PaasChannel> channel_cache = new Dictionary<long, PaasChannel> ();
            
            ServiceManager.DbConnection.BeginTransaction ();

            try {
                foreach (PaasChannel pc in delta.NewChannels) {
                    try {
                        PaasChannel.Provider.Save (pc);
                        channel_cache.Add (pc.ExternalID, pc);
                    } catch (Exception e) {
                        Hyena.Log.Exception (e);
                        continue;
                    }
                }
                
                foreach (PaasItem pi in delta.NewItems) {
                    try {
                        pi.ChannelID = GetChannelIDFromExternalID (channel_cache, pi.ExternalChannelID);
                        pi.Save ();
                        AddItem (pi);
                    } catch (Exception e) {
                        Hyena.Log.Exception (e);
                        continue;
                    }                
                }

                string[] cids = delta.RemovedChannels.Select (id => id.ToString ()).ToArray<string> ();
                string[] iids = delta.RemovedItems.Select (id => id.ToString ()).ToArray<string> ();

                // Talk to Scott about adding, or add, a '*.Provider.Delete (IEnumerable<int> ids)'

                if (cids.Length > 0) {
                    List<PaasChannel> removed_channels = new List<PaasChannel> (
                        PaasChannel.Provider.FetchAllMatching (
                            String.Format ("ClientID = {0} ExternalID IN ({1})", 
                            (long)AetherClientID.MiroGuide, String.Join (",", cids))
                        )
                    );
    
                    PaasChannel.Provider.Delete (removed_channels);
                }
                
                if (iids.Length > 0) {
                    List<PaasItem> removed_items = new List<PaasItem> (
                        PaasItem.Provider.FetchAllMatching (
                            String.Format ("ClientID = {0} AND ExternalID IN ({1})", 
                            (long)AetherClientID.MiroGuide, String.Join (",", iids))
                        )
                    );
    
                    PaasItem.Provider.Delete (removed_items);                    
                }
                
                ServiceManager.DbConnection.CommitTransaction ();                
            } catch {
                ServiceManager.DbConnection.RollbackTransaction  ();
                throw;
            }            
        }

        private long GetChannelIDFromExternalID (Dictionary<long, PaasChannel> channel_cache, long external_id)
        {
            PaasChannel c;

            if (channel_cache.ContainsKey (external_id)) {
                c = channel_cache[external_id];
            } else {
                c = PaasChannel.Provider.FetchFirstMatching ("ExternalID = ?", external_id);
                channel_cache.Add (external_id, c);
            }

            return c.DbId;
        }
   
        private void ClientUpdatedHandler (object sender, AetherAsyncCompletedEventArgs e)
        {        
            if (Disposed) {
                return;
            }
    
            if (e.Timedout) {
                Log.Error ("AetherClient:  Timeouted while updating.");

                source.ErrorSource.AddMessage (
                    Catalog.GetString ("Update Error"), 
                    Catalog.GetString ("Request timed out while attempting to contact server.")
                );
                
                return;
            } else if (e.Error != null) {
                Log.Exception ("PaasService:  ", e.Error);
                source.ErrorSource.AddMessage ("Update Error", e.Error.Message);
                return;
            }

            try {
                ApplyUpdate (new AetherDelta (e.Data));
                source.Reload (); 
            } catch (Exception ex) {
                Log.Exception (ex);                
                source.ErrorSource.AddMessage (                    
                    Catalog.GetString ("Update Error"),
                    Catalog.GetString ("Parsing Error:  ") + ex.Message
                );
            }
                source.ErrorSource.Reload ();            
        }
*/
        private DatabaseTrackInfo GetTrackByItemId (long item_id)
        {
           return DatabaseTrackInfo.Provider.FetchFirstMatching (
                "PrimarySourceID = ? AND ExternalID = ?", source.DbId, item_id
            );
        }

        private void OnChannelUpdatedHandler (object sender, ChannelUpdateCompletedEventArgs e)
        {
            lock (sync) {
                if (!Disposed && e.Succeeded) {
                    IEnumerable<PaasItem> items = e.Channel.Items.OrderByDescending (i => i.PubDate);

                    switch (e.Channel.DownloadPreference) {
                    case DownloadPreference.One:
                        items = items.Take (1);
                        break;
                    case DownloadPreference.None:
                        items = items.Take (0);
                        break;
                    }

                    download_manager.QueueDownload (items.Where (i => i.Active && !i.IsDownloaded));
                }
            }
        }

        private void OnItemsAddedHandler (object sender, ItemEventArgs e)
        {
            lock (sync) {
                if (Disposed) {
                    return;
                }
    
                ServiceManager.DbConnection.BeginTransaction ();
                
                try {
                    if (e.Item != null) {
                        source.AddItem (e.Item);                        
                    } else {
                        source.AddItems (e.Items);
                    }
                } catch (Exception ex) {
                    ServiceManager.DbConnection.RollbackTransaction ();
                    Hyena.Log.Exception (ex);                           
                    throw;
                }
                
                ServiceManager.DbConnection.CommitTransaction ();
                source.Reload ();
            }
        }

        private void OnItemsRemovedHandler (object sender, ItemEventArgs e)
        {
            lock (sync) {
                if (Disposed) {
                    return;
                }
                
                ServiceManager.DbConnection.BeginTransaction ();
                
                try {
                    if (e.Item != null) {
                        if (download_manager.Contains (e.Item)) {
                            download_manager.CancelDownload (e.Item);
                        }
                        source.RemoveItem (e.Item);
                    } else {
                        foreach (PaasItem item in e.Items) {
                            if (download_manager.Contains (item)) {
                                download_manager.CancelDownload (item);
                            }
                        }
                        
                        source.RemoveItems (e.Items);
                    }
                } catch (Exception ex) {
                    ServiceManager.DbConnection.RollbackTransaction ();
                    Hyena.Log.Exception (ex);                           
                    throw;
                }
                
                ServiceManager.DbConnection.CommitTransaction ();
                source.Reload ();
            }
        }

        private void OnDownloadTaskCompletedHandler (object sender, TaskCompletedEventArgs<HttpFileDownloadTask> e)
        {
            PaasItem item = e.UserState as PaasItem;

            if (item == null) {
                return;
            }

            lock (sync) {                
                if (Disposed) {
                    return;
                } else if (e.State != TaskState.Succeeded) {
                    source.QueueDraw ();
                    return;
                }   

                string path = Path.GetDirectoryName (e.Task.LocalPath);                
                string filename = Path.GetFileName (e.Task.LocalPath);             
                string full_path = path;
                string tmp_local_path;                   
                
                string local_enclosure_path = item.Channel.LocalEnclosurePath;
                
                if (!local_enclosure_path.EndsWith (Path.DirectorySeparatorChar.ToString ())) {
                    local_enclosure_path += Path.DirectorySeparatorChar;
                }
                
                if (!full_path.EndsWith (Path.DirectorySeparatorChar.ToString ())) {
                    full_path += Path.DirectorySeparatorChar;
                }           
                
                full_path += filename;
                tmp_local_path = local_enclosure_path+StringUtils.DecodeUrl (filename);            
    
                try {
                    if (!Banshee.IO.Directory.Exists (path)) {
                        throw new InvalidOperationException ("Directory specified by path does not exist");             
                    } else if (!Banshee.IO.File.Exists (new SafeUri (full_path))) {
                        throw new InvalidOperationException (
                            String.Format ("File:  {0}, does not exist", full_path)
                        );
                    }
    
                    if (!Banshee.IO.Directory.Exists (local_enclosure_path)) {
                        Banshee.IO.Directory.Create (local_enclosure_path);
                    }
    
                    if (Banshee.IO.File.Exists (new SafeUri (tmp_local_path))) {
                        int last_dot = tmp_local_path.LastIndexOf (".");
                        
                        if (last_dot == -1) {
                            last_dot = tmp_local_path.Length-1;
                        }
                        
                        string rep = String.Format (
                            "-{0}", 
                            Guid.NewGuid ().ToString ()
                                           .Replace ("-", String.Empty)
                                           .ToLower ()
                        );
                        
                        tmp_local_path = tmp_local_path.Insert (last_dot, rep);
                    }
                
                    Banshee.IO.File.Move (new SafeUri (full_path), new SafeUri (tmp_local_path));
                    
                    try {
                        Banshee.IO.Directory.Delete (path, true);
                    } catch {}

                    item.IsNew = true;
                    item.LocalPath = tmp_local_path;
                    item.MimeType = e.Task.MimeType;
                    item.DownloadedAt = DateTime.Now;
                    
                    item.Save ();

                    DatabaseTrackInfo track = GetTrackByItemId (item.TrackID);

                    if (track != null) {
                        PaasTrackInfo pti = PaasTrackInfo.From (track);

                        if (pti != null) {
                            pti.SyncWithItem ();
                            pti.Track.Save (true);
                        }
                    }                    
                } catch (Exception ex) {
                    source.ErrorSource.AddMessage (                    
                        Catalog.GetString (String.Format ("Error Saving File:  {0}", tmp_local_path)), ex.Message
                    );

                    Hyena.Log.Exception (ex);
                    Hyena.Log.Error (ex.StackTrace);
                }

                source.Reload ();
                source.NotifyUser ();
            }            
        }

        public static readonly SchemaEntry<string> ClientID = new SchemaEntry<string> (
            "plugins.paas", "mg_client_id", String.Empty, "Miro Guide Client ID", ""
        );
        
        public static readonly SchemaEntry<string> SessionID = new SchemaEntry<string> (
            "plugins.paas", "mg_session_id", String.Empty, "Miro Guide Session ID", ""
        );         
    }
}
