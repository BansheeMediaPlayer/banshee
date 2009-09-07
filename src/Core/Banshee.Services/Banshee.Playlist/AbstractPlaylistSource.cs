//
// AbstractPlaylistSource.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2005-2008 Novell, Inc.
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
using System.Data;
using System.Collections;
using System.Collections.Generic;

using Mono.Unix;

using Hyena.Data;
using Hyena.Data.Sqlite;
using Hyena.Collections;
using Hyena.Query;

using Banshee.Base;
using Banshee.ServiceStack;
using Banshee.Database;
using Banshee.Sources;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Query;

namespace Banshee.Playlist
{
    public abstract class AbstractPlaylistSource : DatabaseSource
    {
        protected int? dbid;

        protected abstract string SourceTable { get; }
        protected abstract string SourcePrimaryKey { get; }
        protected abstract string TrackJoinTable { get; }

        protected DateTime last_added = DateTime.MinValue;
        protected DateTime last_updated = DateTime.MinValue;
        protected DateTime last_removed = DateTime.MinValue;

        protected virtual string TrackCondition {
            get {
                return String.Format (
                    " {0}.TrackID = CoreTracks.TrackID AND {0}.{2} = {1}",
                    TrackJoinTable, "{0}", SourcePrimaryKey
                );
            }
        }

        protected virtual string JoinPrimaryKey {
            get { return "EntryID"; }
        }

        protected virtual bool CachesJoinTableEntries {
            get { return true; }
        }

        private bool is_temporary = false;
        public bool IsTemporary {
            get { return is_temporary; }
            set { is_temporary = value; }
        }

        public int? DbId {
            get { return dbid; }
            protected set {
                if (value != null && value != dbid) {
                    dbid = value;
                }
            }
        }

        protected int primary_source_id;
        public int PrimarySourceId {
            get { return primary_source_id; }
        }

        public PrimarySource PrimarySource {
            get { return PrimarySource.GetById (primary_source_id); }
            set { primary_source_id = value.DbId; }
        }

        private HyenaSqliteCommand count_updated_command;
        protected HyenaSqliteCommand CountUpdatedCommand {
            get {
                return count_updated_command ??
                    (count_updated_command = new HyenaSqliteCommand (String.Format (
                        @"SELECT COUNT(*) FROM {0} WHERE {1} = {2} AND TrackID IN (
                            SELECT TrackID FROM CoreTracks WHERE DateUpdatedStamp > ?)",
                        TrackJoinTable, SourcePrimaryKey, dbid
                    )));
            }
        }

        private HyenaSqliteCommand count_removed_command;
        protected HyenaSqliteCommand CountRemovedCommand {
            get {
                return count_removed_command ??
                    (count_removed_command = new HyenaSqliteCommand (String.Format (
                        @"SELECT COUNT(*) FROM {0} WHERE {1} = {2} AND TrackID IN (
                            SELECT TrackID FROM CoreRemovedTracks WHERE DateRemovedStamp > ?)",
                        TrackJoinTable, SourcePrimaryKey, dbid
                    )));
            }
        }

        public AbstractPlaylistSource (string generic_name, string name, PrimarySource parent) : base ()
        {
            GenericName = generic_name;
            Name = name;
            if (parent != null) {
                primary_source_id = parent.DbId;
                SetParentSource (parent);
            }
        }

        public AbstractPlaylistSource (string generic_name, string name, int dbid, int sortColumn, int sortType, PrimarySource parent, bool is_temp)
            : base (generic_name, name, dbid.ToString (), 500, parent)
        {
            primary_source_id = parent.DbId;
            DbId = dbid;
            IsTemporary = is_temp;
            AfterInitialized ();
        }

        protected override void AfterInitialized ()
        {
            DatabaseTrackModel.JoinTable = TrackJoinTable;
            DatabaseTrackModel.JoinPrimaryKey = JoinPrimaryKey;
            DatabaseTrackModel.JoinColumn = "TrackID";
            DatabaseTrackModel.CachesJoinTableEntries = CachesJoinTableEntries;
            DatabaseTrackModel.AddCondition (String.Format (TrackCondition, dbid));
            base.AfterInitialized ();
        }

        public override void Rename (string newName)
        {
            base.Rename (newName);
            Save ();
        }

        public override bool CanRename {
            get { return (Parent is PrimarySource) ? !(Parent as PrimarySource).PlaylistsReadOnly : true; }
        }

        public override bool CanSearch {
            get { return true; }
        }

        public virtual bool CanUnmap {
            get { return (Parent is PrimarySource) ? !(Parent as PrimarySource).PlaylistsReadOnly : true; }
        }

        public bool ConfirmBeforeUnmap {
            get { return true; }
        }

        // We can delete tracks only if our parent can
        public override bool CanDeleteTracks {
            get {
                DatabaseSource ds = Parent as DatabaseSource;
                return ds != null && ds.CanDeleteTracks;
            }
        }

        public override void Save ()
        {
            if (dbid == null || dbid <= 0) {
                Create ();
                TypeUniqueId = dbid.ToString ();
                Initialize ();
                AfterInitialized ();
            } else {
                Update ();
            }
        }

        // Have our parent handle deleting tracks
        public override void DeleteSelectedTracks ()
        {
            if (Parent is PrimarySource) {
                (Parent as PrimarySource).DeleteSelectedTracksFromChild (this);
            }
        }

        public override bool ShowBrowser {
            get { return (Parent is DatabaseSource) ? (Parent as DatabaseSource).ShowBrowser : base.ShowBrowser; }
        }

        protected abstract void Create ();
        protected abstract void Update ();

        // Reserve strings in preparation for the forthcoming string freeze.
        public void ReservedStrings ()
        {
            Catalog.GetString ("The track's rating was set differently on the device and in Banshee");
        }
    }
}
