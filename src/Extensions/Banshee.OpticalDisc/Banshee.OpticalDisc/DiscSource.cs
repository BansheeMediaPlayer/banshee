//
// DiscSource.cs
//
// Author:
//   Alex Launi <alex.launi@canonical.com>
//
// Copyright 2010 Alex Launi
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
using System.Threading;

using Hyena;
using Mono.Unix;

using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.ServiceStack;
using Banshee.Sources;

using Selection = Hyena.Collections.Selection;

namespace Banshee.OpticalDisc
{
    public abstract class DiscSource : Source, ITrackModelSource, IUnmapableSource, IDisposable
    {
        public DiscSource (DiscService service, DiscModel model, string genericName, string name, int order)
            : base (genericName, name, order)
        {
            Service = service;
            Model = model;
        }

        protected DiscService Service { get; set; }
        protected DiscModel Model { get; set; }

        public DiscModel DiscModel {
            get { return Model; }
            protected set { Model = value; }
        }

        public bool DiscIsPlaying {
            get {
                DiscTrackInfo playing_track = ServiceManager.PlayerEngine.CurrentTrack as DiscTrackInfo;
                return playing_track != null && playing_track.Model == Model;
            }
        }

        public virtual void StopPlayingDisc ()
        {
            if (DiscIsPlaying) {
                ServiceManager.PlayerEngine.Close (true);
            }
        }

        public virtual void Dispose ()
        {
        }

#region ITrackModelSource Implementation

        public TrackListModel TrackModel {
            get { return Model; }
        }

        public AlbumListModel AlbumModel {
            get { return null; }
        }

        public ArtistListModel ArtistModel {
            get { return null; }
        }

        public void Reload ()
        {
            Model.Reload ();
        }

        public void RemoveTracks (Selection selection)
        {
        }

        public void DeleteTracks (Selection selection)
        {
        }

        public abstract bool CanRepeat { get; }

        public abstract bool CanShuffle { get; }

        public bool CanAddTracks {
            get { return false; }
        }

        public bool CanRemoveTracks {
            get { return false; }
        }

        public bool CanDeleteTracks {
            get { return false; }
        }

        public bool ConfirmRemoveTracks {
            get { return false; }
        }

        public bool ShowBrowser {
            get { return false; }
        }

        public bool HasDependencies {
            get { return false; }
        }

        public bool Indexable {
            get { return false; }
        }
#endregion

#region IUnmapableSource Implementation

        public bool Unmap ()
        {
            StopPlayingDisc ();

            foreach (TrackInfo track in DiscModel) {
                track.CanPlay = false;
            }

            OnUpdated ();

            SourceMessage eject_message = new SourceMessage (this);
            eject_message.FreezeNotify ();
            eject_message.IsSpinning = true;
            eject_message.CanClose = false;
            // Translators: {0} is the type of disc, "Audio CD" or "DVD"
            eject_message.Text = String.Format (Catalog.GetString ("Ejecting {0}..."), GenericName.ToLower ());
            eject_message.ThawNotify ();
            PushMessage (eject_message);

            ThreadPool.QueueUserWorkItem (delegate {
                try {
                    DiscModel.Volume.Unmount ();
                    DiscModel.Volume.Eject ();

                    ThreadAssist.ProxyToMain (delegate {
                        Service.UnmapDiscVolume (DiscModel.Volume.Uuid);
                        Dispose ();
                    });
                } catch (Exception e) {
                    ThreadAssist.ProxyToMain (delegate {
                        ClearMessages ();
                        eject_message.IsSpinning = false;
                        eject_message.SetIconName ("dialog-error");
                        // Translators: {0} is the type of disc, "Audio CD" or "DVD". {1} is the error message.
                        eject_message.Text = String.Format (Catalog.GetString ("Could not eject {0}: {1}"), GenericName.ToLower (), e.Message);
                        PushMessage (eject_message);

                        foreach (TrackInfo track in Model) {
                            track.CanPlay = true;
                        }
                        OnUpdated ();
                    });

                    Log.Error (e);
                }
            });

            return true;
        }

        public bool CanUnmap {
            get { return DiscModel != null ? !DiscModel.IsDoorLocked : true; }
        }

        public bool ConfirmBeforeUnmap {
            get { return false; }
        }

#endregion

    }
}

