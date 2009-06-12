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

using Banshee.Web;
using Banshee.Base;
using Banshee.Sources;
using Banshee.MediaEngine;
using Banshee.ServiceStack;
using Banshee.Configuration;
using Banshee.Collection.Database;

using Banshee.Paas.Aether;
using Banshee.Paas.Gui;

// remove
using Banshee.Paas.Data;
using Hyena.Json;
using Hyena;
//remove

namespace Banshee.Paas
{
    public partial class PaasService : IExtensionService, IDisposable, IDelayedInitializeService
    {
        private static AetherClient aether_client;
        
        private PaasSource source;
        private PaasActions actions = null;

        private bool disposing, disposed;
        private AutoResetEvent clientHandle;
        private readonly object sync = new object ();

        public string ServiceName {
            get { return "PaasService"; } 
        }

        private bool Disposed { 
            get { lock (sync) { return (disposed | disposing); } }
        }

        public PaasService ()
        {
            clientHandle = new AutoResetEvent (true);
            
            aether_client = new AetherClient () {
                Timeout    = (60 * 1000), // one minute.
                ServiceUri = "http://127.0.0.1:8000",
                ClientID   = ClientID.Get (),
                SessionID  = SessionID.Get (),
                UserAgent  = Banshee.Web.Browser.UserAgent,
            };

            aether_client.StateChanged += (sender, e) => {
                if (e.NewState == AetherClientState.Idle) {
                    clientHandle.Set ();
                    source.HideStatus ();
                } else {
                    clientHandle.Reset ();
                }
            };

            aether_client.RequestDeltasCompleted += ClientUpdatedHandler;

            //System.Threading.Timer t = new System.Threading.Timer (delegate { Update (); });
            //t.Change (10, 10);
        }

        public void UpdateAsync ()
        {
            if (!Disposed) {
                source.SetStatus ("Updating...", false, true, null);
                aether_client.RequestDeltasAsync ();                
            }
        }

        public void Initialize ()
        {        
        }
        
        public void DelayedInitialize ()
        {
            MigrateLegacyIfNeeded ();
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

            aether_client.CancelAsync ();
            clientHandle.WaitOne ();
            
            aether_client.RequestDeltasCompleted -= ClientUpdatedHandler;
            aether_client = null;

            clientHandle.Close ();
            clientHandle = null;

            lock (sync) {
                disposing = false;            
                disposed = true;
            }
        }

        private void InitializeInterface ()
        {
            source = new PaasSource ();

            ServiceManager.SourceManager.AddSource (source);
            actions = new PaasActions (this);
        }
        
        private void DisposeInterface ()
        {
            if (source != null) {
                ServiceManager.SourceManager.RemoveSource (source);
                source = null;
            }
            
            if (actions != null) {
                actions.Dispose ();
                actions = null;
            }            
        }

        private void ApplyUpdate (AetherDelta delta)
        {
            ServiceManager.DbConnection.BeginTransaction ();

            try {
                foreach (PaasChannel pc in delta.NewChannels) {
                    try {
                        PaasChannel.Provider.Save (pc);
                    } catch (Exception e) {
                        Hyena.Log.Exception (e);
                        continue;
                    }
                }
                
                foreach (PaasItem pi in delta.NewItems) {
                    try {
                        AddItem (pi);
                    } catch (Exception e) {
                        Hyena.Log.Exception (e);
                        continue;
                    }                
                }

                string[] cids = delta.RemovedChannels.Select (id => id.ToString () ).ToArray<string> ();
                string[] iids = delta.RemovedItems.Select (id => id.ToString () ).ToArray<string> ();

                // Talk to Scott about adding, or add, a '*.Provider.Delete (IEnumerable<int> ids)'

                if (cids.Length > 0) {
                    List<PaasChannel> removed_channels = new List<PaasChannel> (
                        PaasChannel.Provider.FetchAllMatching (
                            String.Format ("MiroGuideID IN ({0})", String.Join (",", cids))
                        )
                    );
    
                    PaasChannel.Provider.Delete (removed_channels);
                }
                
                if (iids.Length > 0) {
                    List<PaasItem> removed_items = new List<PaasItem> (
                        PaasItem.Provider.FetchAllMatching (
                            String.Format ("MiroGuideID IN ({0})", String.Join (",", iids))
                        )
                    );
    
                    PaasItem.Provider.Delete (removed_items);                    
                }
                
                ServiceManager.DbConnection.CommitTransaction ();                
            } catch (Exception e) {
                Log.Exception (e);
                ServiceManager.DbConnection.RollbackTransaction  ();            
            }            
        }
        
        private void AddItem (PaasItem item)
        {
            if (item != null) {
                item.Save ();            
                DatabaseTrackInfo track = new DatabaseTrackInfo ();
                track.ExternalId = item.DbId;
                track.PrimarySource = source;
                (track.ExternalObject as PaasTrackInfo).SyncWithItem ();
                track.Save (false);
                //RefreshArtworkFor (item.Feed);
            } 
        }
        
        private void ClientUpdatedHandler (object sender, AetherAsyncCompletedEventArgs e)
        {
            if (Log.Debugging) {
                Log.Debug ("ClientUpdatedHandler - Completed.");
                Log.Debug (String.Format ("ClientUpdatedHandler - Timed Out:  {0}", e.Timedout));
                Log.Debug (String.Format ("ClientUpdatedHandler - Cancelled:  {0}", e.Cancelled));            
            }

            if (Disposed) {
                if (Log.Debugging) {
                    Log.Debug ("ClientUpdatedHandler - Dispose called.");
                }
                
                return;
            }
    
            if (e.Timedout) {
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
                if (Log.Debugging) {
                    Log.Exception (ex);
                }
                
                source.ErrorSource.AddMessage (                    
                    Catalog.GetString ("Update Error"),
                    Catalog.GetString ("Parsing Error:  ") + ex.Message
                );
            }
        }

        private void MigrateLegacyIfNeeded ()
        {
            // Not yet.
            // Upload an OPML file of all currently subscribed feeds to MG.
        }

        public static readonly SchemaEntry<string> ClientID = new SchemaEntry<string> (
            "plugins.paas", "mg_client_id", String.Empty, "Miro Guide Client ID", ""
        );
        
        public static readonly SchemaEntry<string> SessionID = new SchemaEntry<string> (
            "plugins.paas", "mg_session_id", String.Empty, "Miro Guide Session ID", ""
        );         
    }
}
