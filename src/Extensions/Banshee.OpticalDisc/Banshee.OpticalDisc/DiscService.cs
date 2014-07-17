//
// DiscService.cs
//
// Author:
//   Alex Launi <alex.launi@canonical.com>
//
// Copyright (C) 2010 Alex Launi
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

using Hyena;

using Banshee.ServiceStack;
using Banshee.Configuration;
using Banshee.Preferences;
using Banshee.Hardware;
using Banshee.Gui;

namespace Banshee.OpticalDisc
{
    public abstract class DiscService : IExtensionService, IDisposable
    {
        private List<DeviceCommand> unhandled_device_commands;

        public DiscService ()
        {
        }

        public virtual void Initialize ()
        {
            if (ServiceManager.HardwareManager == null) {
                throw new NotSupportedException ("DiscService cannot work when no HardwareManager is available");
            }

            lock (this) {
                Sources = new Dictionary<string, DiscSource> ();

                // This says Cdrom, but really it means Cdrom in the general optical disc device sense.
                foreach (ICdromDevice device in ServiceManager.HardwareManager.GetAllCdromDevices ()) {
                    MapDiscDevice (device);
                }

                ServiceManager.HardwareManager.DeviceAdded += OnHardwareDeviceAdded;
                ServiceManager.HardwareManager.DeviceRemoved += OnHardwareDeviceRemoved;
                ServiceManager.HardwareManager.DeviceCommand += OnDeviceCommand;
            }
        }

        public virtual void Dispose ()
        {
            lock (this) {
                ServiceManager.HardwareManager.DeviceAdded -= OnHardwareDeviceAdded;
                ServiceManager.HardwareManager.DeviceRemoved -= OnHardwareDeviceRemoved;
                ServiceManager.HardwareManager.DeviceCommand -= OnDeviceCommand;

                foreach (DiscSource source in Sources.Values) {
                    ServiceManager.SourceManager.RemoveSource (source);
                    source.Dispose ();
                }

                Sources.Clear ();
                Sources = null;
            }
        }

        protected Dictionary<string, DiscSource> Sources {
            get; private set;
        }

        protected virtual void MapDiscDevice (ICdromDevice device)
        {
            lock (this) {
                foreach (IVolume volume in device) {
                    if (volume is IDiscVolume) {
                        MapDiscVolume ((IDiscVolume) volume);
                    }
                }
            }
        }

        protected abstract DiscSource GetDiscSource  (IDiscVolume volume);

        protected virtual void MapDiscVolume (IDiscVolume volume)
        {
            DiscSource source = null;

            lock (this) {
                if (Sources.ContainsKey (volume.Uuid)) {
                    Log.Debug ("Already mapped");
                    return;
                }

                source =  GetDiscSource (volume);

                if (source == null)
                    return;

                Sources.Add (volume.Uuid, source);
                ServiceManager.SourceManager.AddSource (source);

                // If there are any queued device commands, see if they are to be
                // handled by this new volume (e.g. --device-activate-play=cdda://sr0/)
                try {
                    if (unhandled_device_commands != null) {
                        foreach (DeviceCommand command in unhandled_device_commands) {
                            if (DeviceCommandMatchesSource (source, command)) {
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

                Log.DebugFormat ("Mapping disc ({0})", volume.Uuid);
            }
        }

        internal void UnmapDiscVolume (string uuid)
        {
            lock (this) {
                if (Sources.ContainsKey (uuid)) {
                    DiscSource source = Sources[uuid];
                    source.StopPlayingDisc ();
                    ServiceManager.SourceManager.RemoveSource (source);
                    Sources.Remove (uuid);
                    Log.DebugFormat ("Unmapping disc ({0})", uuid);
                }
            }
        }

        private void OnHardwareDeviceAdded (object o, DeviceAddedArgs args)
        {
            lock (this) {
                if (args.Device is ICdromDevice) {
                    MapDiscDevice ((ICdromDevice)args.Device);
                } else if (args.Device is IDiscVolume) {
                    MapDiscVolume ((IDiscVolume)args.Device);
                }
            }
        }

        private void OnHardwareDeviceRemoved (object o, DeviceRemovedArgs args)
        {
            lock (this) {
                UnmapDiscVolume (args.DeviceUuid);
            }
        }

#region DeviceCommand Handling

        protected abstract bool DeviceCommandMatchesSource (DiscSource source, DeviceCommand command);

        protected virtual void OnDeviceCommand (object o, DeviceCommand command)
        {
            lock (this) {
                // Check to see if we have an already mapped disc volume that should
                // handle this incoming command; if not, queue it for later discs
                foreach (var source in Sources.Values) {
                    if (DeviceCommandMatchesSource (source, command)) {
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

        protected virtual void HandleDeviceCommand (DiscSource source, DeviceCommandAction action)
        {
            if ((action & DeviceCommandAction.Activate) != 0) {
                ServiceManager.SourceManager.SetActiveSource (source);
            }

            if ((action & DeviceCommandAction.Play) != 0) {
                ServiceManager.PlaybackController.NextSource = source;
                if (!ServiceManager.PlayerEngine.IsPlaying ()) {
                    ServiceManager.PlaybackController.Next ();
                }
            }
        }
#endregion
        string IService.ServiceName {
            get { return "DiscService"; }
        }
    }
}
