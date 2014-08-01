//
// PotentialSource.cs
//
// Author:
//   Nicholas Little <arealityfarbetween@googlemail.com>
//
// Copyright (C) 2014 Nicholas Little
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

using Mono.Unix;

using Banshee.Collection.Database;
using Banshee.Dap.Gui;
using Banshee.Hardware;
using Banshee.Sources;
using Banshee.Sources.Gui;

using Hyena;

using Mono.Addins;

namespace Banshee.Dap
{
    internal class PotentialSource : DapSource
    {
        private readonly TypeExtensionNode Claimant;
        private readonly DapService Service;

        private object lock_object = new object();
        private bool initialized;

        internal PotentialSource (DapService service, TypeExtensionNode claimant, IDevice device)
        {
            Claimant = claimant;
            Service = service;

            IsTemporary = true;

            SupportsPlaylists = false;
            SupportsPodcasts = false;
            SupportsVideo = false;

            DeviceInitialize (device, false);
            Initialize ();
        }

        #region overridden members of Source

        protected override void Initialize ()
        {
            base.Initialize ();
            ThreadAssist.ProxyToMain (() => {
                ClearChildSources ();
                Properties.Set<ISourceContents> ("Nereid.SourceContents", new InactiveDapContent (this));
            });
        }

        #endregion

        #region implemented abstract members of RemovableSource

        public override void Import ()
        {
            throw new NotSupportedException ();
        }

        public override bool CanUnmap {
            get { return false; }
        }

        public override bool CanImport {
            get { return false; }
        }

        public override bool IsReadOnly {
            get { return true; }
        }

        public override long BytesUsed {
            get { return 0L; }
        }

        public override long BytesCapacity {
            get { return 0L; }
        }

        #endregion

        #region implemented abstract members of DapSource

        public override void AddChildSource (Source child)
        {
        }

        public override void RemoveChildSource (Source child)
        {
        }

        protected override void AddTrackToDevice (DatabaseTrackInfo track, SafeUri fromUri)
        {
            throw new NotSupportedException ();
        }

        private bool TryDeviceInitialize (bool force, out DapSource source)
        {
            lock (lock_object) {
                source = null;

                if (initialized) {
                    return false;
                }

                SetStatus (
                    Catalog.GetString ("Trying to claim your device..."),
                    false,
                    true,
                    "dialog-information"
                );

                Log.Debug ("PotentialSource: Creating Instance");
                try {
                    DapSource src = (DapSource) Claimant.CreateInstance ();
                    Log.Debug ("PotentialSource: Initializing Device");
                    src.DeviceInitialize (Device, force);
                    Log.Debug ("PotentialSource: Loading Contents");
                    src.LoadDeviceContents ();

                    Log.DebugFormat ("PotentialSource: Success, new Source {0}", src.Name);
                    src.AddinId = Claimant.Addin.Id;
                    source = src;
                    initialized = true;
                } catch (InvalidDeviceStateException e) {
                    Log.Warning (e);
                } catch (InvalidDeviceException e) {
                    Log.Warning (e);
                } catch (Exception e) {
                    Log.Error (e);
                }

                bool success = (source != null);

                SetStatus (
                    success ? Catalog.GetString ("Connection successful. Please wait...")
                            : Catalog.GetString ("Connection failed"),
                    !success,
                    success,
                    success ? "dialog-information"
                            : "dialog-warning"
                );

                return success;
            }
        }
        #endregion

        internal void TryClaim ()
        {
            Log.DebugFormat ("PotentialSource: TryClaim {0} as {1}", Device.Name, Claimant.Type);
            ThreadAssist.SpawnFromMain (() => {
                DapSource source;
                if (TryDeviceInitialize (true, out source)) {
                    Service.SwapSource (this, source, true);
                }
            });
        }

        internal void TryInitialize ()
        {
            Log.DebugFormat ("PotentialSource: TryInitialize {0} as {1}", Device.Name, Claimant.Type);
            ThreadAssist.SpawnFromMain (() => {
                DapSource source;
                if (TryDeviceInitialize (false, out source)) {
                    Service.SwapSource (this, source, false);
                }
            });
        }
    }
}

