//
// DetailsSource.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2009 Novell, Inc.
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
using System.Linq;

using Mono.Unix;

using Hyena;
using Hyena.Collections;
using Hyena.Data.Sqlite;

using Banshee.Base;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Configuration;
using Banshee.Database;
using Banshee.Gui;
using Banshee.Library;
using Banshee.MediaEngine;
using Banshee.PlaybackController;
using Banshee.Playlist;
using Banshee.Preferences;
using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Sources.Gui;

using IA=InternetArchive;

namespace Banshee.InternetArchive
{
    public class DetailsSource : Banshee.Sources.Source, ITrackModelSource, IDurationAggregator, IFileSizeAggregator, IUnmapableSource
    {
        private Item item;
        private MemoryTrackListModel track_model;
        private LazyLoadSourceContents<DetailsView> gui;

        public Item Item {
            get { return item; }
        }

        public DetailsSource (string id, string title, string mediaType) : this (Item.LoadOrCreate (id, title, mediaType)) {}

        public DetailsSource (Item item) : base (item.Title, item.Title, 195, "internet-archive-" + item.Id)
        {
            this.item = item;
            track_model = new MemoryTrackListModel ();
            track_model.Reloaded += delegate { OnUpdated (); };

            Properties.SetString ("ActiveSourceUIResource", "DetailsSourceActiveUI.xml");
            Properties.SetString ("GtkActionPath", "/IaDetailsSourcePopup");
            Properties.SetString ("UnmapSourceActionLabel", Catalog.GetString ("Close Item"));
            Properties.SetString ("UnmapSourceActionIconName", "gtk-close");

            SetIcon ();

            gui = new LazyLoadSourceContents<DetailsView> (this, item);
            Properties.Set<ISourceContents> ("Nereid.SourceContents", gui);
        }

        private bool details_loaded;
        internal void LoadDetails ()
        {
            if (details_loaded) {
                return;
            }

            details_loaded = true;

            if (item.Details == null) {
                SetStatus (Catalog.GetString ("Getting item details from the Internet Archive"), false, true, null);
                Load ();
            } else {
                gui.Contents.UpdateDetails ();
            }
        }

        private void SetIcon ()
        {
            if (item.MediaType == null)
                return;

            var media_type = IA.MediaType.Options.FirstOrDefault (mt =>
                item.MediaType.Contains (mt.Id) || mt.Children.Any (c => item.MediaType.Contains (c.Id))
            );

            if (media_type == null)
                return;

            if (media_type.Id == "audio") {
                Properties.SetStringList ("Icon.Name", "audio-x-generic", "audio");
            } else if (media_type.Id == "movies") {
                Properties.SetStringList ("Icon.Name", "video-x-generic", "video");
            } else if (media_type.Id == "texts") {
                Properties.SetStringList ("Icon.Name", "x-office-document", "document");
            } else if (media_type.Id == "education") {
                Properties.SetStringList ("Icon.Name", "video-x-generic", "video");
            }
        }

        private void Load ()
        {
            ThreadAssist.SpawnFromMain (ThreadedLoad);
        }

        private void ThreadedLoad ()
        {
            try {
                item.LoadDetails ();
                ThreadAssist.ProxyToMain (delegate {
                    ClearMessages ();
                    if (item.Details != null) {
                        gui.Contents.UpdateDetails ();
                    }
                });
            } catch (Exception e) {
                Log.Error ("Error loading IA item details", e);

                ThreadAssist.ProxyToMain (delegate {
                    var web_e = e as System.Net.WebException;
                    if (web_e != null && web_e.Status == System.Net.WebExceptionStatus.Timeout) {
                        SetStatus (Catalog.GetString ("Timed out getting item details from the Internet Archive"), true);
                        CurrentMessage.AddAction (new MessageAction (Catalog.GetString ("Try Again"), (o, a) => Load ()));
                    } else {
                        SetStatus (Catalog.GetString ("Error getting item details from the Internet Archive"), true);
                    }
                });
            }
        }

        public void Reload ()
        {
        }

        public override string PreferencesPageId {
            get { return Parent.PreferencesPageId; }
        }

        public override int Count {
            get { return 0; }
        }

        public override int FilteredCount {
            get { return track_model.Count; }
        }

        public TimeSpan Duration {
            get {
                TimeSpan duration = TimeSpan.Zero;
                foreach (var t in track_model) {
                    duration += t.Duration;
                }
                return duration;
            }
        }

        public long FileSize {
            get {
                // Mono on openSUSE 11.0 doesn't like this
                // return track_model.Sum (t => t.FileSize);
                long result = 0;
                foreach (var t in track_model)
                    result += t.FileSize;
                return result;
            }
        }

#region ITrackModelSource

        public TrackListModel TrackModel {
            get { return track_model; }
        }

        public bool HasDependencies { get { return false; } }

        public void RemoveTracks (Selection selection) {}
        public void DeleteTracks (Selection selection) {}

        public bool ConfirmRemoveTracks { get { return false; } }

        public bool Indexable { get { return false; } }

        public bool ShowBrowser {
            get { return false; }
        }

        public bool CanPlay {
            get { return true; }
        }

        public bool CanShuffle {
            get { return false; }
        }

        public bool CanRepeat {
            get { return true; }
        }

        public bool CanAddTracks {
            get { return false; }
        }

        public bool CanRemoveTracks {
            get { return false; }
        }

        public bool CanDeleteTracks {
            get { return false; }
        }

#endregion

        public bool CanUnmap { get { return true; } }
        public bool ConfirmBeforeUnmap { get { return false; } }

        public bool Unmap ()
        {
            // If we were the active source, switch to the Search source
            if (this == ServiceManager.SourceManager.ActiveSource) {
                ServiceManager.SourceManager.SetActiveSource ((Parent as HomeSource).SearchSource);
            }

            item.Delete ();
            Parent.RemoveChildSource (this);
            return true;
        }

        public override bool HasEditableTrackProperties {
            get { return false; }
        }

        public override bool HasViewableTrackProperties {
            get { return false; }
        }

        public override bool CanSearch {
            get { return false; }
        }
    }
}
