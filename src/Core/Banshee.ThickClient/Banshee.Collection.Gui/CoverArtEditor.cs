//
// CoverArtEditor.cs
//
// Author:
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
using System.Text;
using System.Collections.Generic;

using Mono.Unix;
using Mono.Addins;
using Gtk;

using Hyena;
using Hyena.Gui;
using Hyena.Widgets;

using Banshee.Base;
using Banshee.Kernel;
using Banshee.Sources;
using Banshee.ServiceStack;
using Banshee.Collection;
using Banshee.Collection.Database;
using Banshee.Configuration.Schema;

using Banshee.Widgets;
using Banshee.Gui.DragDrop;

namespace Banshee.Collection.Gui
{
    public class CoverArtEditor
    {
        public static Widget For (Widget widget, Func<int, int, bool> is_sensitive, Func<TrackInfo> get_track, System.Action on_updated)
        {
            return new EditorBox (widget) {
                IsBoxSensitive = is_sensitive,
                GetTrack = get_track,
                OnUpdated = on_updated
            };
        }

        private class EditorBox : EventBox
        {
            public Func<int, int, bool> IsBoxSensitive;
            public Func<TrackInfo> GetTrack;
            public System.Action OnUpdated;

            public EditorBox (Widget child)
            {
                Child = child;
                VisibleWindow = false;

                ButtonPressEvent += (o, a) => {
                    if (a.Event.Button == 3 && IsBoxSensitive ((int)a.Event.X, (int)a.Event.Y)) {
                        var menu = new Menu ();

                        var choose = new MenuItem (Catalog.GetString ("Choose New Cover Art..."));
                        choose.Activated += delegate {
                            try {
                                var track = GetTrack ();
                                if (track != null) {
                                    var dialog = new Banshee.Gui.Dialogs.ImageFileChooserDialog ();
                                    var resp = dialog.Run ();
                                    string filename = dialog.Filename;
                                    dialog.Destroy ();
                                    if (resp == (int)Gtk.ResponseType.Ok) {
                                        SetCoverArt (track, filename);
                                    }
                                }
                            } catch (Exception e) {
                                Log.Error (e);
                            }
                        };

                        var delete = new MenuItem (Catalog.GetString ("Delete This Cover Art"));
                        delete.Activated += delegate {
                            try {
                                var track = GetTrack ();
                                if (track != null) {
                                    DeleteCoverArt (track);
                                    NotifyUpdated (track);
                                }
                            } catch (Exception e) {
                                Log.Error (e);
                            }
                        };

                        var uri = GetCoverArtPath (GetTrack ());
                        choose.Sensitive = uri != null;
                        if (uri == null || !Banshee.IO.File.Exists (uri)) {
                            delete.Sensitive = false;
                        }

                        menu.Append (choose);
                        menu.Append (delete);
                        menu.ShowAll ();
                        menu.Popup ();
                    }
                };

                Gtk.Drag.SourceSet (this, Gdk.ModifierType.Button1Mask, new TargetEntry [] { DragDropTarget.UriList }, Gdk.DragAction.Copy | Gdk.DragAction.Move);
                DragDataGet += (o, a) => {
                    try {
                        var uri = GetCoverArtPath (GetTrack ());
                        if (uri != null) {
                            if (Banshee.IO.File.Exists (uri)) {
                                a.SelectionData.Set (
                                    Gdk.Atom.Intern (DragDropTarget.UriList.Target, false), 8,
                                    Encoding.UTF8.GetBytes (uri.AbsoluteUri)
                                );
                            }
                        }
                    } catch (Exception e) {
                        Log.Error (e);
                    }
                };

                Gtk.Drag.DestSet (this, Gtk.DestDefaults.All, new TargetEntry [] { DragDropTarget.UriList }, Gdk.DragAction.Copy);
                DragDataReceived += (o, a) => {
                    try {
                        SetCoverArt (GetTrack (), Encoding.UTF8.GetString (a.SelectionData.Data));
                    } catch (Exception e) {
                        Log.Error (e);
                    }
                };
            }

            private void SetCoverArt (TrackInfo track, string path)
            {
                if (track == null)
                    return;

                var from_uri = new SafeUri (new System.Uri (path));

                var to_uri = new SafeUri (CoverArtSpec.GetPathForNewFile (track.ArtworkId, from_uri.AbsoluteUri));
                if (to_uri != null) {
                    // Make sure it's not the same file we already have
                    if (from_uri.Equals (to_uri)) {
                        return;
                    }

                    // Make sure the incoming file exists
                    if (!Banshee.IO.File.Exists (from_uri)) {
                        Hyena.Log.WarningFormat ("New cover art file not found: {0}", path);
                        return;
                    }

                    DeleteCoverArt (track);
                    Banshee.IO.File.Copy (from_uri, to_uri, true);
                    NotifyUpdated (track);

                    Hyena.Log.DebugFormat ("Got new cover art file for {0}: {1}", track.DisplayAlbumTitle, path);
                }
            }

            private void NotifyUpdated (TrackInfo track)
            {
                var cur = ServiceManager.PlayerEngine.CurrentTrack;
                if (cur != null && cur.TrackEqual (track)) {
                    ServiceManager.PlayerEngine.TrackInfoUpdated ();
                }

                OnUpdated ();
            }

            private void DeleteCoverArt (TrackInfo track)
            {
                if (track != null) {
                    var uri = new SafeUri (CoverArtSpec.GetPath (track.ArtworkId));
                    if (Banshee.IO.File.Exists (uri)) {
                        Banshee.IO.File.Delete (uri);
                    }
                    var artwork_id = track.ArtworkId;
                    if (artwork_id != null) {
                        ServiceManager.Get<ArtworkManager> ().ClearCacheFor (track.ArtworkId);
                    }

                    // Deleting it from this table means the cover art downloader extension will
                    // attempt to redownload it on its next run.
                    var db = ServiceManager.DbConnection;
                    var db_track = track as DatabaseTrackInfo;
                    if (db_track != null && db.TableExists ("CoverArtDownloads")) {
                        db.Execute ("DELETE FROM CoverArtDownloads WHERE AlbumID = ?", db_track.AlbumId);
                    }
                }
            }

            private SafeUri GetCoverArtPath (TrackInfo track)
            {
                if (track != null) {
                    return new SafeUri (CoverArtSpec.GetPath (track.ArtworkId));
                }

                return null;
            }
        }
    }
}
