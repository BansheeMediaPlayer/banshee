// 
// PaasService.cs
//  
// Authors:
//   Mike Urbanski <michael.c.urbanski@gmail.com>
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2009 Michael C. Urbanski
// Copyright (C) 2008 Novell, Inc.
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

#define MIRO_GUIDE

using System;
using System.IO;

using System.Linq;
using System.Xml.Linq;

using System.Threading;
using System.Collections.Generic;

using Mono.Unix;

using Hyena;

using Banshee.IO;
using Banshee.Web;
using Banshee.Base;
using Banshee.Sources;
using Banshee.MediaEngine;
using Banshee.ServiceStack;
using Banshee.Configuration;
using Banshee.Collection.Database;

using Banshee.Paas.Gui;
using Banshee.Paas.Data;
using Banshee.Paas.Utils;

using Banshee.Paas.Aether;
using Banshee.Paas.Aether.MiroGuide;
using Banshee.Paas.Aether.MiroGuide.Gui;

using Banshee.Paas.Aether.Syndication;

using Banshee.Paas.DownloadManager;
using Banshee.Paas.DownloadManager.Gui;

using Migo2.Async;
using Migo2.DownloadService;

namespace Banshee.Paas
{
    delegate void Updater ();
    
    public partial class PaasService : IExtensionService, IDisposable, IDelayedInitializeService
    {
        private int update_count;
        private bool disposing, disposed;
                
        private readonly string tmp_download_path = Paths.Combine (
            Paths.ExtensionCacheRoot, "paas", "partial-downloads"
        );

        private PaasSource source;
#if MIRO_GUIDE
        private MiroGuideClient mg_client;
        public static MiroGuideAccountInfo MiroGuideAccount;        
#endif        
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

#if MIRO_GUIDE
        static PaasService ()
        {
            MiroGuideAccount = new MiroGuideAccountInfo (
                MiroGuideServiceUri.Get (), MiroGuideClientID.Get (), 
                MiroGuideUsername.Get (), MiroGuidePasswordHash.Get ()
            );

            MiroGuideAccount.Updated += (sender, e) => {
                MiroGuideUsername.Set (MiroGuideAccount.Username);
                MiroGuidePasswordHash.Set (MiroGuideAccount.PasswordHash);
                MiroGuideClientID.Set (MiroGuideAccount.ClientID);
                MiroGuideServiceUri.Set (MiroGuideAccount.ServiceUri);
            };
        }
#endif
        public PaasService ()
        {
            reload = delegate {
                lock (sync) {
                    if (Disposed) {
                        return;
                    }
                    
                    // Performance will be crap on updates with large podcast collections 
                    // if you hold sync while reloading.  Investigate and CHANGE it.
                    source.Reload ();
                }
                
                //source.Reload ();
            };

            redraw = delegate {
                lock (sync) {
                    if (Disposed) {
                        return;
                    }
                    
                    source.QueueDraw ();
                }
            };
            
            source = new PaasSource (this);

            if (source.DbId < 1) {
                source.Save ();                 
            }
            
            client_handle = new AutoResetEvent (true);

#if MIRO_GUIDE
            mg_client = new MiroGuideClient (MiroGuideAccount) {
                UserAgent = Banshee.Web.Browser.UserAgent,
            };

            source.AddChildSource (new TestSource (mg_client));
            
            mg_client.StateChanged += (sender, e) => {
                if (e.NewState == AetherClientState.Idle) {
                    DecrementUpdateCount ();
                    client_handle.Set ();
                } else {
                    IncrementUpdateCount ();                
                    client_handle.Reset ();
                }
            };

            mg_client.ClientIDChanged += (sender, e) => { MiroGuideAccount.ClientID = e.ClientID; };

            mg_client.ChannelsAdded += (sender, e) => { reload (); };
            mg_client.ChannelsRemoved += (sender, e) => { reload (); };
            mg_client.ItemsAdded += OnItemsAddedHandler;
            mg_client.ItemsRemoved += OnItemsRemovedHandler;
            mg_client.Completed += (sender, e) => {  
                if (e.Method == MiroGuideClientMethod.Unsubscribe) {
                    if ((e.Cancelled & e.Timedout) == false && e.Error == null) {
                        mg_client.UpdateAsync ();
                    }
                }

                foreach (PaasChannel c in PaasChannel.Provider.FetchAll ()) {
                    RefreshArtworkFor (c);
                }
            };

            mg_client.DownloadRequested += (sender, e) => {
                lock (sync) {
                    if (Disposed) {
                        return;
                    }            
                    
                    IEnumerable<PaasItem> items = PaasItem.Provider.FetchAllMatching (
                        String.Format ("ClientID = {0} AND ExternalID IN ({1})", 
                        (long)AetherClientID.MiroGuide, String.Join (",", e.IDs.Select (id => id.ToString ()).ToArray ()))
                    );

                    download_manager.QueueDownload (items);
                }                
            };

            mg_client.DownloadCancelled += (sender, e) => {
                lock (sync) {
                    if (Disposed) {
                        return;
                    }            
                    
                    IEnumerable<PaasItem> items = PaasItem.Provider.FetchAllMatching (
                        String.Format ("ClientID = {0} AND ExternalID IN ({1})", 
                        (long)AetherClientID.MiroGuide, String.Join (",", e.IDs.Select (id => id.ToString ()).ToArray ()))
                    );

                    download_manager.CancelDownload (items);
                }                
            };

            mg_client.SubscriptionRequested += (sender, e) => {
                if (e.Uri != null) {
                    SubscribeToChannel (e.Uri);
                } else if (e.Uris != null) {
                    SubscribeToChannels (e.Uris);
                }
            };
#endif
            syndication_client = new SyndicationClient ();
            syndication_client.StateChanged += (sender, e) => {
                if (e.NewState == AetherClientState.Idle) {
                    DecrementUpdateCount ();
                } else {
                    IncrementUpdateCount ();                                    
                }
            };

            syndication_client.ChannelsAdded += (sender, e) => {
                lock (sync) {
                    if (Disposed) {
                        return;
                    }

                    syndication_client.QueueUpdate (e.Channel);
                    source.Reload ();
                }
            };
            
            syndication_client.ChannelsRemoved += (sender, e) => { reload (); };
            syndication_client.ChannelUpdating += (sender, e) => { redraw (); };
            syndication_client.ChannelUpdateCompleted += OnChannelUpdatedHandler;

            syndication_client.ItemsAdded += OnItemsAddedHandler;
            syndication_client.ItemsRemoved += OnItemsRemovedHandler;
            
            download_manager = new PaasDownloadManager (source.DbId, 2, tmp_download_path);
            download_manager.TaskAdded += (sender, e) => { redraw (); };
            
            download_manager.TaskCompleted += OnDownloadTaskCompletedHandler;
            download_manager.TaskStateChanged += (sender, e) => { redraw (); };
        }

        public void UpdateAsync ()
        {
            lock (sync) {
                if (!Disposed) {
#if MIRO_GUIDE
                    mg_client.UpdateAsync ();
#endif                    
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
            ServiceManager.Get<DBusCommandService> ().ArgumentPushed += OnCommandLineArgument;

            download_manager.RestoreQueuedDownloads ();
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
            ServiceManager.Get<DBusCommandService> ().ArgumentPushed -= OnCommandLineArgument;            
            
#if MIRO_GUIDE                    
            mg_client.CancelAsync ();
            client_handle.WaitOne ();

            mg_client.ItemsAdded -= OnItemsAddedHandler;
            mg_client.ItemsRemoved -= OnItemsRemovedHandler;

            mg_client = null;
#endif            

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

        public void DeleteChannels (IEnumerable<PaasChannel> channels, bool deleteFiles)
        {
            lock (sync) {
                if (Disposed) {
                    return;
                }
            
                syndication_client.DeleteChannels (
                    channels.Where (c => c.ClientID == (int)AetherClientID.Syndication), deleteFiles
                );
#if MIRO_GUIDE                                    
                mg_client.Unsubscribe (
                    channels.Where (c => c.ClientID == (int)AetherClientID.MiroGuide)//, deleteFiles
                );
#endif                
            }
        }

        public void ExportChannelsToOpml (string path, IEnumerable<PaasChannel> channels)
        {
            if (String.IsNullOrEmpty (path)) {
                throw new ArgumentNullException ("path");
            } else if (channels == null) {
                throw new ArgumentNullException ("channels");
            }

            if (channels.Count () == 0) {
                return;
            }

            try {
                // Move this to a helper class
                XDocument doc = new XDocument (
                    new XDeclaration ("1.0", "utf-8", "yes"),
                    new XElement ("opml",
                        new XAttribute ("version", "2.0"),
                        new XElement ("head", 
                                new XElement ("title", Catalog.GetString ("Banshee Podcast Subscriptions")),
                                new XElement ("dateCreated", DateTime.UtcNow.ToString ("ddd, dd MMM yyyy HH:mm:ss K"))
                        ), new XElement ("body",
                            from channel in channels select new XElement (
                                "outline",
                                new XAttribute ("type", "rss"),                                
                                new XAttribute ("text", channel.Name),
                                new XAttribute ("xmlUrl", channel.Url)
                            )
                        )
                    )
                );
    
                doc.Save (path);
            } catch (Exception e) {
                Log.Exception (e);
                throw;
            }            
        }

        public void ImportOpml (string path)
        {
            if (String.IsNullOrEmpty (path)) {
                throw new ArgumentNullException ("path");
            }
            
            lock (sync) {
                if (Disposed) {
                    return;
                }

                try {
                    OpmlParser opml_parser = new OpmlParser (path, true);
                    
                    foreach (string channel in opml_parser.Feeds) {
                        try {
                            syndication_client.SubscribeToChannel (channel, DownloadPreference.One);
                            source.NotifyUser ();                            
                        } catch (Exception e) {
                            Log.Exception (e);
                        }
                    }
                } catch (Exception e) {
                    Log.Exception (e);
                    throw;
                }
            }
        }

        public void SubscribeToChannels (IEnumerable<Uri> uris)
        {
            if (uris == null) {
                throw new ArgumentNullException ("uris");            
            }
            
            lock (sync) {
                if (Disposed) {
                    return;
                }

                int sub_count = 0;
                
                foreach (Uri uri in uris) {
                    try {
                        if (uri != null) {
                            syndication_client.SubscribeToChannel (uri.ToString (), DownloadPreference.One);
                            sub_count++;
                        }                    
                    } catch (Exception e) {
                        Log.Exception (e);
                        continue;
                    }
                }

                if (sub_count > 0) {
                    source.NotifyUser ();                                                
                }
            }
        }

        public void SubscribeToChannel (Uri uri)
        {
            if (uri == null) {
                throw new ArgumentNullException ("uri");
            }
            
            lock (sync) {
                if (Disposed) {
                    return;
                }

                try {
                    syndication_client.SubscribeToChannel (uri.ToString (), DownloadPreference.One);
                    source.NotifyUser ();                            
                } catch (Exception e) {
                    Log.Exception (e);
                }
            }
        }

        private void InitializeInterface ()
        {
            ServiceManager.SourceManager.AddSource (source);
            download_manager_interface = new DownloadManagerInterface (source, download_manager);
        }
        
        private void DisposeInterface ()
        {
            if (source != null) {
                ServiceManager.SourceManager.RemoveSource (source);
                source.Dispose ();
            }

            if (download_manager_interface != null) {
                download_manager_interface.Dispose ();
            }
        }

        private DatabaseTrackInfo GetTrackByItemId (long item_id)
        {
           return DatabaseTrackInfo.Provider.FetchFirstMatching (
                "PrimarySourceID = ? AND ExternalID = ?", source.DbId, item_id
            );
        }

        private void OnChannelUpdatedHandler (object sender, ChannelUpdateCompletedEventArgs e)
        {
            lock (sync) {
                if (Disposed) {
                    return;
                }

                if (e.Succeeded) {                
                    PaasChannel channel = e.Channel;
#if EDIT_DIR_TEST                    
                    if (String.IsNullOrEmpty (channel.LocalEnclosurePath)) {
                        string escaped = Hyena.StringUtil.EscapeFilename (channel.Name);
                        
                        channel.LocalEnclosurePath = Path.Combine (source.BaseDirectory, escaped);
                        channel.Save ();
                        
                        try {
                            Banshee.IO.Directory.Create (channel.LocalEnclosurePath);
                        } catch (Exception ex) {
                            Hyena.Log.Exception (ex);
                        }
                    }
#endif                                            
                    IEnumerable<PaasItem> items = channel.Items.OrderByDescending (i => i.PubDate);

                    switch (e.Channel.DownloadPreference) {
                    case DownloadPreference.One:
                        items = items.Take (1);
                        break;
                    case DownloadPreference.None:
                        items = items.Take (0);
                        break;
                    }
                    
                    RefreshArtworkFor (channel);
                    download_manager.QueueDownload (items.Where (i => i.Active && !i.IsDownloaded));
                } else if (e.Error != null) {
                    Log.Exception (e.Error);
                    
                    source.ErrorSource.AddMessage (                    
                        String.Format (Catalog.GetString (@"Error while updating channel ""{0}"""), e.Channel.Name),
                        e.Error.Message
                    );                    
                }

                source.Reload ();
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
#if MIRO_GUIDE
                    PaasChannel channel;
                    
                    if (e.Item != null) {
                        source.AddItem (e.Item);
                        channel = e.Item.Channel;
                    } else {
                        source.AddItems (e.Items);
                        var item = e.Items.First ();
                        channel = (item == null) ? null : item.Channel;
                    }

                    if (channel != null) {
                        channel.LastDownloadTime = DateTime.Now;
                    }
#else
                    if (e.Item != null) {
                        source.AddItem (e.Item);                        
                    } else {
                        source.AddItems (e.Items);
                    }
#endif

                    ServiceManager.DbConnection.CommitTransaction ();
                } catch (Exception ex) {
                    ServiceManager.DbConnection.RollbackTransaction ();
                    Hyena.Log.Exception (ex);                           
                    throw;
                }
                
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
                        
                        ThreadAssist.ProxyToMain (delegate {
                            source.RemoveItem (e.Item);
                        });
                    } else {
                        foreach (PaasItem item in e.Items) {
                            if (download_manager.Contains (item)) {
                                download_manager.CancelDownload (item);
                            }
                        }
                        
                        ThreadAssist.ProxyToMain (delegate {                                                        
                            source.RemoveItems (e.Items);
                        });
                    }

                    ServiceManager.DbConnection.CommitTransaction ();
                } catch (Exception ex) {
                    ServiceManager.DbConnection.RollbackTransaction ();
                    Hyena.Log.Exception (ex);                           
                    throw;
                }
                
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
                    if (e.Error != null) {
                        source.ErrorSource.AddMessage (                                                
                            String.Format (
                                Catalog.GetString ("Error Downloading:  {0}"), (e.Task as HttpFileDownloadTask).Name
                            ), e.Error.Message
                        );
                    }
                
                    source.QueueDraw ();
                    return;
                }   

                string path = Path.GetDirectoryName (e.Task.LocalPath);                
                string filename = Path.GetFileName (e.Task.LocalPath);             
                string full_path = path;
                string tmp_local_path;                   

#if EDIT_DIR_TEST
                string local_enclosure_path = item.Channel.LocalEnclosurePath;        
#else

                string escaped = Hyena.StringUtil.EscapeFilename (item.Channel.Name);
                string local_enclosure_path = Path.Combine (source.BaseDirectory, escaped);        
#endif

                if (!local_enclosure_path.EndsWith (Path.DirectorySeparatorChar.ToString ())) {
                    local_enclosure_path += Path.DirectorySeparatorChar;
                }
                
                if (!full_path.EndsWith (Path.DirectorySeparatorChar.ToString ())) {
                    full_path += Path.DirectorySeparatorChar;
                }           
                
                full_path += filename;
                tmp_local_path = local_enclosure_path+StringUtils.DecodeUrl (filename);            
    
                try {
                    // This is crap, non-i18n-iz-iz-ized strings will be displayed to the users.
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

                    DatabaseTrackInfo track = GetTrackByItemId (item.DbId);                   

                    if (track != null) {
                        new PaasTrackInfo (track, item).Track.Save ();
                    }                    
                } catch (Exception ex) {
                    source.ErrorSource.AddMessage (                    
                        String.Format (Catalog.GetString ("Error Saving File:  {0}"), tmp_local_path), ex.Message
                    );

                    Hyena.Log.Exception (ex);
                    Hyena.Log.Error (ex.StackTrace);
                }

                source.Reload ();
                source.NotifyUser ();
            }            
        }

        private void OnCommandLineArgument (string uri, object value, bool isFile)
        {
            if (!isFile || String.IsNullOrEmpty (uri)) {
                return;
            }

            lock (sync) {
                if (Disposed) {
                    return;
                }

                if (uri.Contains ("opml") || uri.EndsWith (".miro") || uri.EndsWith (".democracy")) {
                    try {
                        OpmlParser opml_parser = new OpmlParser (uri, true);
                        
                        foreach (string channel in opml_parser.Feeds) {
                            ServiceManager.Get<DBusCommandService> ().PushFile (channel);
                        }
                    } catch (Exception e) {
                        Log.Exception (e);
                    }
                } else if (uri.Contains ("xml") || uri.Contains ("rss") || uri.Contains ("feed") || uri.StartsWith ("itpc") || uri.StartsWith ("pcast")) {
                    if (uri.StartsWith ("feed://") || uri.StartsWith ("itpc://")) {
                        uri = String.Format ("http://{0}", uri.Substring (7));
                    } else if (uri.StartsWith ("pcast://")) {
                        uri = String.Format ("http://{0}", uri.Substring (8));
                    }
    
                    syndication_client.SubscribeToChannel (uri, DownloadPreference.One);                
                    source.NotifyUser ();
                } else if (uri.StartsWith ("itms://")) {
                    System.Threading.ThreadPool.QueueUserWorkItem (delegate {
                        try {
                            string feed_url = new ItmsPodcast (uri).FeedUrl;
                            
                            if (feed_url != null) {
                                ThreadAssist.ProxyToMain (delegate {
                                    syndication_client.SubscribeToChannel (feed_url, DownloadPreference.None);
                                    source.NotifyUser ();
                                });
                            }
                        } catch (Exception e) {
                            Hyena.Log.Exception (e);
                        }
                    });
                }
            }            
        }

        private void IncrementUpdateCount ()
        {
            lock (sync) {
                if (!Disposed) {
                   if (update_count++ == 0) {
                        ThreadAssist.ProxyToMain (delegate { 
                            source.SetStatus ("Updating...", false, true, null); 
                        });
                    }
                }
            }
        }

        private void DecrementUpdateCount ()
        {
            lock (sync) {
                if (!Disposed) {
                    if (--update_count == 0) {
                        ThreadAssist.ProxyToMain (delegate { 
                            source.HideStatus (); 
                        });
                    }
                }
            }
        }

        private void RefreshArtworkFor (PaasChannel channel)
        {
            if (channel.LastDownloadTime != DateTime.MinValue && !CoverArtSpec.CoverExists (ArtworkIdFor (channel))) {
                Banshee.Kernel.Scheduler.Schedule (new PaasImageFetchJob (channel), Banshee.Kernel.JobPriority.BelowNormal);
            }
        }
        
        public static string ArtworkIdFor (PaasChannel channel)
        {
            return String.Format ("paas-{0}", Banshee.Base.CoverArtSpec.EscapePart (channel.Name));
        }

        public static readonly SchemaEntry<string> MiroGuideUsername = new SchemaEntry<string> (
            "plugins.paas.miroguide", "username", String.Empty, "Miro Guide Username", ""
        );
        
        public static readonly SchemaEntry<string> MiroGuidePasswordHash = new SchemaEntry<string> (
            "plugins.paas.miroguide", "miroguide.password_hash", String.Empty, "Miro Guide Password Hash", ""
        );

        public static readonly SchemaEntry<string> MiroGuideClientID = new SchemaEntry<string> (
            "plugins.paas.miroguide", "miroguide.client_id", String.Empty, "Miro Guide Client ID", ""
        );

        public static readonly SchemaEntry<string> MiroGuideServiceUri = new SchemaEntry<string> (
            "plugins.paas.miroguide", "miroguide.service_uri", "http://miroguide.com", "Miro Guide Service URI", ""
        ); 
    }
}
