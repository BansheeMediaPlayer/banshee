//
// RemovableSource.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
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
using System.Threading;
using Mono.Unix;

using Hyena;
using Banshee.Base;
using Banshee.Library;
using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Hardware;

namespace Banshee.Dap
{
    public abstract class RemovableSource : PrimarySource, IUnmapableSource, IImportSource
    {
        protected RemovableSource () : base ()
        {
        }

        protected override void Initialize ()
        {
            base.Initialize ();

            Order = 410;
            Properties.SetString ("UnmapSourceActionIconName", "media-eject");
            Properties.SetString ("UnmapSourceActionLabel", Catalog.GetString ("Disconnect"));
            Properties.SetString ("GtkActionPath", "/RemovableSourceContextMenu");
            AfterInitialized ();

            // Things are usually slower on removable disks, so don't bother trying to
            // delay the add/remove jobs from showing.
            DelayAddJob = false;
            DelayDeleteJob = false;
        }

        public override string Name {
            get { return base.Name; }
            set {
                base.Name = value;
                StorageName = value;
            }
        }

        public override bool CanRemoveTracks {
            get { return false; }
        }

        public override bool CanDeleteTracks {
            get { return !IsReadOnly; }
        }

        public override bool CanAddTracks {
            get { return !IsReadOnly; }
        }

        public virtual bool CanImport {
            get { return true; }
        }

        string IImportSource.ImportLabel {
            get { return null; }
        }

        int IImportSource.SortOrder {
            get { return 20; }
        }

#region IUnmapableSource Implementation

        public bool Unmap ()
        {
            DatabaseTrackInfo track = ServiceManager.PlayerEngine.CurrentTrack as DatabaseTrackInfo;
            if (track != null) {
                if (track.PrimarySourceId == this.DbId ||
                    (!String.IsNullOrEmpty (BaseDirectory) && track.LocalPath.StartsWith (BaseDirectory))) {
                    ServiceManager.PlayerEngine.Close ();
                }
            }

            SetStatus (String.Format (Catalog.GetString ("Disconnecting {0}..."), GenericName), false);

            ThreadPool.QueueUserWorkItem (delegate {
                try {
                    Eject ();
                } catch (Exception e) {
                    ThreadAssist.ProxyToMain (delegate {
                        SetStatus (String.Format (Catalog.GetString ("Could not disconnect {0}: {1}"),
                            GenericName, e.Message), true);
                    });

                    Log.Exception (e);
                }
            });
            return true;
        }

        public override bool AcceptsInputFromSource (Source source)
        {
            return (source is DatabaseSource) && this != source.Parent && !IsReadOnly;
        }

        private bool syncing = false;
        public bool CanUnmap {
            get { return !syncing; }
        }

        public bool ConfirmBeforeUnmap {
            get { return false; }
        }

#endregion

#region Members Subclasses Should Override

        protected virtual void Eject ()
        {
        }

        public abstract bool IsReadOnly { get; }

        public abstract long BytesUsed { get; }
        public abstract long BytesCapacity { get; }
        public virtual long BytesAvailable {
            get { return BytesCapacity - BytesUsed; }
        }

        public abstract void Import ();

        string [] IImportSource.IconNames {
            get { return Properties.GetStringList ("Icon.Name"); }
        }

#endregion

    }
}
