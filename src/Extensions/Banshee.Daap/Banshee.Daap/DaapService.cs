//
// DaapService.cs
//
// Authors:
//   Alexander Hixon <hixon.alexander@mediati.org>
//
// Copyright (C) 2008 Alexander Hixon
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
using System.Net;
using System.Net.Sockets;
using Mono.Unix;
using NativeDaap = Daap;
using Daap;
using Gtk;

using Hyena;

using Banshee.Collection;
using Banshee.Gui;
using Banshee.Sources;
using Banshee.ServiceStack;

namespace Banshee.Daap
{
    public class DaapService : IExtensionService, IDisposable, IDelayedInitializeService
    {
        private ServiceLocator locator;
        private DateTime locator_started;
        private static DaapProxyWebServer proxy_server;

        private DaapContainerSource container;
        private Dictionary<string, DaapSource> source_map;

        private uint actions_id;

        internal static DaapProxyWebServer ProxyServer {
            get { return proxy_server; }
        }

        void IExtensionService.Initialize ()
        {
        }

        public void Dispose ()
        {
            if (locator != null) {
                locator.Stop ();
                locator.Found -= OnServiceFound;
                locator.Removed -= OnServiceRemoved;
                locator = null;
            }

            if (proxy_server != null) {
                proxy_server.Stop ();
                proxy_server = null;
            }

            var uia_service = ServiceManager.Get<InterfaceActionService> ();
            if (uia_service != null) {
                uia_service.UIManager.RemoveUi (actions_id);
                uia_service.GlobalActions.Remove ("AddRemoteDaapServerAction");
            }

            // Dispose any remaining child sources
            if (source_map != null) {
                foreach (KeyValuePair <string, DaapSource> kv in source_map) {
                    if (kv.Value != null) {
                        kv.Value.Disconnect (true);
                        kv.Value.Dispose ();
                    }
                }

                source_map.Clear ();
            }

            if (container != null) {
                ServiceManager.SourceManager.RemoveSource (container, true);
                container = null;
            }
        }

        private void OnServiceFound (object o, ServiceArgs args)
        {
            AddDaapServer (args.Service);
        }

        private void AddDaapServer (Service service)
        {
            ThreadAssist.ProxyToMain (delegate {
                DaapSource source = new DaapSource (service);
                string key = String.Format ("{0}:{1}", service.Name, service.Port);

                if (source_map.Count == 0) {
                    ServiceManager.SourceManager.AddSource (container);
                }

                if (source_map.ContainsKey (key)) {
                    // Received new connection info for service
                    container.RemoveChildSource (source_map [key]);
                    source_map [key] = source;
                } else {
                    // New service information
                    source_map.Add (key, source);
                }

                container.AddChildSource (source);

                // Don't flash shares we find on startup (well, within 5s of startup)
                if ((DateTime.Now - locator_started).TotalSeconds > 5) {
                    source.NotifyUser ();
                }
            });
        }

        private void OnServiceRemoved (object o, ServiceArgs args)
        {
            ThreadAssist.ProxyToMain (delegate {
                string key = String.Format ("{0}:{1}", args.Service.Name, args.Service.Port);
                DaapSource source = source_map [key];

                source.Disconnect (true);
                container.RemoveChildSource (source);
                source_map.Remove (key);

                if (source_map.Count == 0) {
                    ServiceManager.SourceManager.RemoveSource (container);
                }
            });
        }

        public void DelayedInitialize ()
        {
            ThreadAssist.SpawnFromMain (ThreadedInitialize);
        }

        public void ThreadedInitialize ()
        {
            source_map = new Dictionary<string, DaapSource> ();
            container = new DaapContainerSource ();

            try {
                // Now start looking for services.
                // We do this after creating the source because if we do it before
                // there's a race condition where we get a service before the source
                // is added.
                locator = new ServiceLocator ();
                locator.Found += OnServiceFound;
                locator.Removed += OnServiceRemoved;
                locator.ShowLocalServices = true;
                locator_started = DateTime.Now;
                locator.Start ();

                proxy_server = new DaapProxyWebServer ();
                proxy_server.Start ();
            } catch (Exception e) {
                Log.Error ("Failed to start DAAP client", e);
            }

            var uia_service = ServiceManager.Get<InterfaceActionService> ();
            if (uia_service != null) {
                ThreadAssist.ProxyToMain ( () => {
                    uia_service.GlobalActions.Add (
                        new ActionEntry ("AddRemoteDaapServerAction", Stock.Add,
                            Catalog.GetString ("Add Remote DAAP Server"), null,
                            Catalog.GetString ("Add a new remote DAAP server"),
                            OnAddRemoteServer)
                    );
                    actions_id = uia_service.UIManager.AddUiFromResource ("GlobalUI.xml");
                });

            }
        }

        private void OnAddRemoteServer (object o, EventArgs args)
        {
            ResponseType response;
            string s_address;
            ushort port;

            using (OpenRemoteServer dialog = new OpenRemoteServer ()) {
                response = (ResponseType) dialog.Run ();
                s_address = dialog.Address;
                port = (ushort) dialog.Port;
                dialog.Destroy ();
            }

            if (response != ResponseType.Ok)
                return;

            Log.DebugFormat ("Trying to add DAAP server on {0}:{1}", s_address, port);
            IPHostEntry hostEntry = null;
            try {
                hostEntry = Dns.GetHostEntry (s_address);
            } catch (SocketException) {
                Log.Warning ("Unable to resolve host " + s_address);
                return;
            }

            IPAddress address = hostEntry.AddressList[0];
            foreach (IPAddress curAdd in hostEntry.AddressList) {
                if (curAdd.AddressFamily == AddressFamily.InterNetwork) {
                    address = curAdd;
                }
            }
            Log.DebugFormat (String.Format("Resolved {0} to {1}", s_address, address));
            Log.Debug ("Spawning daap resolving thread");

            DaapResolverJob job = new DaapResolverJob(s_address, address, port);

            job.Finished += delegate {
                Service service = job.DaapService;

                if (service != null) {
                    AddDaapServer (service);
                    Log.DebugFormat ("Created server {0}", service.Name);
                } else {
                    Log.DebugFormat ("Unable to create service for {0}", s_address);
                }
            };

            ServiceManager.JobScheduler.Add (job);
        }

        string IService.ServiceName {
            get { return "DaapService"; }
        }
    }
}
