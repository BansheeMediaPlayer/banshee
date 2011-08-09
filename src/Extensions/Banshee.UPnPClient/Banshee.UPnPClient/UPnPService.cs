//
// UPnPClientSource.cs
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

using Mono.Addins;

using Mono.Upnp;
using Mono.Upnp.Dcp.MediaServer1.ContentDirectory1;
using Mono.Upnp.Dcp.MediaServer1.ContentDirectory1.AV;

using Banshee.Base;
using Banshee.Sources.Gui;
using Banshee.ServiceStack;
using Banshee.Preferences;
using Banshee.MediaEngine;
using Banshee.PlaybackController;

namespace Banshee.UPnPClient
{
    public class UPnPService : IExtensionService, IDisposable
    {
        private Mono.Upnp.Client client;
        private UPnPContainerSource container;

        void IExtensionService.Initialize ()
        {
            container = new UPnPContainerSource();
            ServiceManager.SourceManager.AddSource(container);

            client = new Mono.Upnp.Client ();
            client.DeviceAdded += DeviceAdded;

            client.Browse(Mono.Upnp.Dcp.MediaServer1.MediaServer.DeviceType);
        }
    
        public void Dispose ()
        {
            if (container != null)
            {
                foreach (UPnPMusicSource source in container.Children)
                    source.Disconnect();

                ServiceManager.SourceManager.RemoveSource(container);
                container = null;
            }
        }

        void DeviceAdded (object sender, DeviceEventArgs e)
        {
            Hyena.Log.Debug ("UPnPService.DeviceAdded (" + e.Device.ToString() + ") (" + e.Device.Type + ")");
            Device device = e.Device.GetDevice();
            
            UPnPServerSource source = new UPnPServerSource(device);
            container.AddChildSource (source);
        }

        string IService.ServiceName {
            get { return "uPnP Client service"; }
        }
  }
}
