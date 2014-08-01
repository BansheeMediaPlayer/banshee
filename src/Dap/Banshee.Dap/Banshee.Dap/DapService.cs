//
// DapService.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//   Aaron Bockover <abockover@novell.com>
//   Ruben Vermeersch <ruben@savanne.be>
//
// Copyright (C) 2007-2008 Novell, Inc.
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
using System.Linq;
using System.Collections.Generic;

using Mono.Unix;
using Mono.Addins;

using Hyena;
using Banshee.Kernel;
using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Hardware;

namespace Banshee.Dap
{
    public class DapService : IExtensionService, IDelayedInitializeService, IDisposable
    {
        private Dictionary<string, DapSource> sources;
        private List<DeviceCommand> unhandled_device_commands;
        private List<DapPriorityNode> supported_dap_types;
        private bool initialized;
        private object sync = new object ();

        public void Initialize ()
        {
        }

        public void DelayedInitialize ()
        {
            // This group source gives us a separator for DAPs in the source view.
            SourceManager.GroupSource dap_group = new SourceManager.GroupSource (Catalog.GetString ("Devices"), 400);
            ThreadAssist.ProxyToMain (delegate {
                ServiceManager.SourceManager.AddSource (dap_group);
            });

            lock (sync) {
                if (initialized || ServiceManager.HardwareManager == null)
                    return;

                sources = new Dictionary<string, DapSource> ();
                supported_dap_types = new List<DapPriorityNode> ();

                AddinManager.AddExtensionNodeHandler ("/Banshee/Dap/DeviceClass", OnExtensionChanged);

                ServiceManager.HardwareManager.DeviceAdded += OnHardwareDeviceAdded;
                ServiceManager.HardwareManager.DeviceChanged += OnHardwareDeviceChanged;
                ServiceManager.HardwareManager.DeviceRemoved += OnHardwareDeviceRemoved;
                ServiceManager.HardwareManager.DeviceCommand += OnDeviceCommand;
                initialized = true;

                // Now that we've loaded all the enabled DAP providers, load the devices
                foreach (IDevice device in ServiceManager.HardwareManager.GetAllDevices ()) {
                    MapDevice (device);
                }
            }
        }

        private void OnExtensionChanged (object o, ExtensionNodeEventArgs args)
        {
            lock (sync) {
                var node = (DapPriorityNode)args.ExtensionNode;
                if (!node.Type.IsSubclassOf (typeof (DapSource)))
                    return;

                if (args.Change == ExtensionChange.Add) {
                    Log.DebugFormat ("Dap support extension loaded: {0}", node.Addin.Id);

                    supported_dap_types.Add (node);
                    supported_dap_types.Sort ((left, right) => right.Priority.CompareTo (left.Priority));

                    if (initialized) {
                        // See if any existing devices are handled by this new DAP support
                        foreach (IDevice device in ServiceManager.HardwareManager.GetAllDevices ()) {
                            MapDevice (device);
                        }
                    }
                } else if (args.Change == ExtensionChange.Remove) {
                    supported_dap_types.Remove ((DapPriorityNode) args.ExtensionNode);

                    Queue<DapSource> to_remove = new Queue<DapSource> ();
                    foreach (DapSource source in sources.Values) {
                        if (source.AddinId == node.Addin.Id) {
                            to_remove.Enqueue (source);
                        }
                    }

                    while (to_remove.Count > 0) {
                        UnmapDevice (to_remove.Dequeue ().Device.Uuid);
                    }
                }
            }
        }

        public void Dispose ()
        {
            Scheduler.Unschedule (typeof(MapDeviceJob));
            lock (sync) {
                if (!initialized)
                    return;

                AddinManager.RemoveExtensionNodeHandler ("/Banshee/Dap/DeviceClass", OnExtensionChanged);

                ServiceManager.HardwareManager.DeviceAdded -= OnHardwareDeviceAdded;
                ServiceManager.HardwareManager.DeviceRemoved -= OnHardwareDeviceRemoved;
                ServiceManager.HardwareManager.DeviceCommand -= OnDeviceCommand;

                List<DapSource> dap_sources = new List<DapSource> (sources.Values);
                foreach (DapSource source in dap_sources) {
                    UnmapDevice (source.Device.Uuid);
                }

                sources.Clear ();
                sources = null;
                supported_dap_types.Clear ();
                supported_dap_types = null;
                initialized = false;
            }
        }

        private DapSource FindDeviceSource (IDevice device)
        {
            foreach (TypeExtensionNode node in supported_dap_types) {
                try {
                    DapSource source = (DapSource)node.CreateInstance ();
                    source.DeviceInitialize (device, false);
                    source.LoadDeviceContents ();
                    source.AddinId = node.Addin.Id;
                    return source;
                } catch (InvalidDeviceStateException) {
                    Log.WarningFormat (
                        "Dap.DapService: invalid state, mapping potential source for {0}",
                        device.Name
                    );
                    DapSource source = new PotentialSource (this, node, device);
                    return source;
                } catch (InvalidDeviceException) {
                } catch (InvalidCastException e) {
                    Log.Warning ("Extension is not a DapSource as required", e);
                } catch (Exception e) {
                    Log.Error (e);
                }
            }

            return null;
        }

        private void MapDevice (IDevice device)
        {
            Scheduler.Schedule (new MapDeviceJob (this, device));
        }

        internal void SwapSource (DapSource oldSource, DapSource newSource, bool makeActive)
        {
            if (oldSource.Device.Uuid != newSource.Device.Uuid) {
                Log.ErrorFormat (
                    "Dap.DapService: swap ignored from {0} to {1}.",
                    oldSource.Device.Uuid, newSource.Device.Uuid
                );
                return;
            }
            Log.DebugFormat (
                "Dap.DapService: Swapping {0} with UUID {1} for {2}",
                oldSource.GetType ().Name, oldSource.Device.Uuid,
                newSource.GetType ().Name
            );

            Unmap (oldSource.Device.Uuid);
            MapSource (newSource, makeActive);
        }

        private class MapDeviceJob : IJob
        {
            IDevice device;
            DapService service;

            public MapDeviceJob (DapService service, IDevice device)
            {
                this.device = device;
                this.service = service;
            }

            public string Uuid {
                get { return device.Uuid; }
            }

            public void Run ()
            {
                DapSource source = null;
                lock (service.sync) {
                    try {
                        if (service.sources.ContainsKey (device.Uuid)) {
                            return;
                        }

                        if (device is ICdromDevice || device is IDiscVolume) {
                            return;
                        }

                        if (device is IVolume && (device as IVolume).ShouldIgnore) {
                            return;
                        }

                        if (device.MediaCapabilities == null && !(device is IBlockDevice) && !(device is IVolume)) {
                            return;
                        }

                        source = service.FindDeviceSource (device);
                        if (source != null) {
                            Log.DebugFormat ("Found DAP support ({0}) for device {1} and Uuid {2}", source.GetType ().FullName,
                                             source.Name, device.Uuid);
                            service.sources.Add (device.Uuid, source);
                        }
                    } catch (Exception e) {
                        Log.Error (e);
                    }
                }

                if (source != null) {
                    service.MapSource (source, false);
                }
            }
        }

        private void MapSource (DapSource source, bool active)
        {
            lock (sync) {
                sources [source.Device.Uuid] = source;
                source.RequestUnmap += OnRequestUnmap;
            }

            ThreadAssist.ProxyToMain (() => {
            
                ServiceManager.SourceManager.AddSource (source);
                source.NotifyUser ();

                if (active)
                {
                    ServiceManager.SourceManager.SetActiveSource (source);
                }

                // If there are any queued device commands, see if they are to be
                // handled by this new DAP (e.g. --device-activate=file:///media/disk)
                try {
                    if (unhandled_device_commands != null) {
                        foreach (DeviceCommand command in unhandled_device_commands) {
                            if (source.CanHandleDeviceCommand (command)) {
                                HandleDeviceCommand (source, command.Action);
                                unhandled_device_commands.Remove (command);
                                if (unhandled_device_commands.Count == 0) {
                                    unhandled_device_commands = null;
                                }
                                break;
                            }
                        }
                    }
                } catch (Exception e) {
                    Log.Error (e);
                }
            });
        }

        private void OnRequestUnmap (object sender, EventArgs e)
        {
            DapSource source = sender as DapSource;
            if (source != null) {
                Log.DebugFormat ("DapService: unmap request from {0}", source.Device.Uuid);
                UnmapDevice (source.Device.Uuid);
            }
        }

        internal void UnmapDevice (string uuid)
        {
            ThreadAssist.SpawnFromMain (() => Unmap (uuid));
        }

        private void Unmap (string uuid)
        {
            DapSource source = null;
            lock (sync) {
                if (sources == null) {
                    // DapService already disposed...
                    return;
                }

                if (sources.ContainsKey (uuid)) {
                    Log.DebugFormat ("Unmapping DAP source ({0})", uuid);
                    source = sources[uuid];
                    sources.Remove (uuid);
                }
            }

            if (source != null) {
                source.RequestUnmap -= OnRequestUnmap;
                source.Dispose ();
                ThreadAssist.ProxyToMain (delegate {
                    try {
                        ServiceManager.SourceManager.RemoveSource (source);
                    } catch (Exception e) {
                        Log.Error (e);
                    }
                });
            }
        }

        private void OnHardwareDeviceAdded (object o, DeviceAddedArgs args)
        {
            MapDevice (args.Device);
        }

        private void OnHardwareDeviceChanged (object o, DeviceChangedEventArgs args)
        {
            DapSource source;
            if (!sources.TryGetValue (args.Device.Uuid, out source)) {
                MapDevice (args.Device);
                return;
            }

            PotentialSource potential = source as PotentialSource;
            if (potential != null) {
                potential.TryInitialize ();
            }
        }

        private void OnHardwareDeviceRemoved (object o, DeviceRemovedArgs args)
        {
            UnmapDevice (args.DeviceUuid);
        }


#region DeviceCommand Handling

        private void HandleDeviceCommand (DapSource source, DeviceCommandAction action)
        {
            if ((action & DeviceCommandAction.Activate) != 0) {
                ServiceManager.SourceManager.SetActiveSource (source);
            }
        }

        private void OnDeviceCommand (object o, DeviceCommand command)
        {
            lock (this) {
                // Check to see if we have an already mapped disc volume that should
                // handle this incoming command; if not, queue it for later devices
                foreach (DapSource source in sources.Values) {
                    if (source.CanHandleDeviceCommand (command)) {
                        HandleDeviceCommand (source, command.Action);
                        return;
                    }
                }

                if (unhandled_device_commands == null) {
                    unhandled_device_commands = new List<DeviceCommand> ();
                }
                unhandled_device_commands.Add (command);
            }
        }

#endregion

        string IService.ServiceName {
            get { return "DapService"; }
        }
    }
}
