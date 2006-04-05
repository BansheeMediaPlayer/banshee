/***************************************************************************
 *  PlaylistView.cs
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@abock.org>
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Threading;
using System.Collections;
using System.Runtime.InteropServices;
using System.IO;
using Mono.Unix;
using Gtk;
using Gdk;
using Pango;

using Banshee.Base;
using Banshee.Dap;
using Banshee.Sources;

namespace Banshee
{
    public class LoaderAdditionArgs : EventArgs
    {
        public long Count;
    }
    
    public delegate void LoaderAdditionHandler(object o, 
        LoaderAdditionArgs args);
        
    public class PlaylistView : TreeView
    {
        private enum ColumnId : int {
            Track,
            Artist,
            Title,
            Album,
            Genre,
            Time,
            Rating,
            PlayCount,
            LastPlayed
        };
            
        private ArrayList columns;
        PlaylistModel model;
        
        PlaylistColumnChooserDialog columnChooser;
        Pixbuf nowPlayingPixbuf;
        Pixbuf songDrmedPixbuf;
        Pixbuf ripColumnPixbuf;

        public TreeViewColumn RipColumn;
        public PlaylistColumn RatingColumn;
        public PlaylistColumn PlaysColumn;
        public PlaylistColumn LastPlayedColumn;
        
        public PlaylistView(PlaylistModel model)
        {        
            // set up columns
            columns = new ArrayList();
            
            columns.Add(new PlaylistColumn(this, Catalog.GetString("Track"), "Track", 
                new TreeCellDataFunc(TrackCellTrack), new CellRendererText(),
                0, (int)ColumnId.Track));
            columns.Add(new PlaylistColumn(this, Catalog.GetString("Artist"), "Artist", 
                new TreeCellDataFunc(TrackCellArtist), new CellRendererText(),
                1, (int)ColumnId.Artist));
            columns.Add(new PlaylistColumn(this, Catalog.GetString("Title"), "Title", 
                new TreeCellDataFunc(TrackCellTitle), new CellRendererText(),
                2, (int)ColumnId.Title));
            columns.Add(new PlaylistColumn(this, Catalog.GetString("Album"), "Album", 
                new TreeCellDataFunc(TrackCellAlbum), new CellRendererText(),
                3, (int)ColumnId.Album));
            columns.Add(new PlaylistColumn(this, Catalog.GetString("Genre"), "Genre", 
                new TreeCellDataFunc(TrackCellGenre), new CellRendererText(),
                4, (int)ColumnId.Genre));
            columns.Add(new PlaylistColumn(this, Catalog.GetString("Time"), "Time", 
                new TreeCellDataFunc(TrackCellTime), new CellRendererText(),
                5, (int)ColumnId.Time));
            
            RatingColumn = new PlaylistColumn(this, 
                Catalog.GetString("Rating"), "Rating",
                new TreeCellDataFunc(TrackCellRating), new RatingRenderer(),
                6, (int)ColumnId.Rating);
            columns.Add(RatingColumn);
            
            PlaysColumn = new PlaylistColumn(this, 
                Catalog.GetString("Plays"), "Plays",
                new TreeCellDataFunc(TrackCellPlayCount), 
                new CellRendererText(),
                7, (int)ColumnId.PlayCount);
            columns.Add(PlaysColumn);
            
            LastPlayedColumn = new PlaylistColumn(this, 
                Catalog.GetString("Last Played"), "Last-Played",
                new TreeCellDataFunc(TrackCellLastPlayed), 
                new CellRendererText(),
                8, (int)ColumnId.LastPlayed);
            columns.Add(LastPlayedColumn);
            
            foreach(PlaylistColumn plcol in columns) {
                InsertColumn(plcol.Column, plcol.Order);
            }

            // FIXME: would be nice to have these as PlaylistColumns too...
            TreeViewColumn playIndColumn = new TreeViewColumn();
            Gtk.Image playIndImg = new Gtk.Image(
                Gdk.Pixbuf.LoadFromResource("blue-speaker.png"));
            playIndImg.Show();
            playIndColumn.Expand = false;
            playIndColumn.Resizable = false;
            playIndColumn.Clickable = false;
            playIndColumn.Reorderable = false;
            playIndColumn.Widget = playIndImg;
            
            nowPlayingPixbuf = Gdk.Pixbuf.LoadFromResource("now-playing-arrow.png");
            songDrmedPixbuf = Gdk.Pixbuf.LoadFromResource("song-drm.png");
            ripColumnPixbuf = Gdk.Pixbuf.LoadFromResource("cd-action-rip-16.png");
            
            CellRendererPixbuf indRenderer = new CellRendererPixbuf();
            playIndColumn.PackStart(indRenderer, true);
            playIndColumn.SetCellDataFunc(indRenderer, 
                new TreeCellDataFunc(TrackCellInd));
            InsertColumn(playIndColumn, 0);
            
            RipColumn = new TreeViewColumn();
            Gtk.Image ripImage = new Gtk.Image(ripColumnPixbuf);
            ripImage.Show();
            RipColumn.Expand = false;
            RipColumn.Resizable = false;
            RipColumn.Clickable = false;
            RipColumn.Reorderable = false;
            RipColumn.Visible = false;
            RipColumn.Widget = ripImage;
            
            CellRendererToggle ripRenderer = new CellRendererToggle();
            ripRenderer.Activatable = true;
            ripRenderer.Toggled += OnRipToggled;
            RipColumn.PackStart(ripRenderer, true);
            RipColumn.SetCellDataFunc(ripRenderer, new TreeCellDataFunc(RipCellInd));
            InsertColumn(RipColumn, 1);

            ColumnDragFunction = new TreeViewColumnDropFunc(CheckColumnDrop);

            Model = this.model = model;
            model.DefaultSortFunc = new TreeIterCompareFunc(DefaultTreeIterCompareFunc);
                
            // set up tree view
            RulesHint = true;
            HeadersClickable = true;
            HeadersVisible = true;
            Selection.Mode = SelectionMode.Multiple;
            
            model.SetSortFunc((int)ColumnId.Track, 
                    new TreeIterCompareFunc(TrackTreeIterCompareFunc));
            model.SetSortFunc((int)ColumnId.Artist, 
                new TreeIterCompareFunc(ArtistTreeIterCompareFunc));
            model.SetSortFunc((int)ColumnId.Title, 
                new TreeIterCompareFunc(TitleTreeIterCompareFunc));
            model.SetSortFunc((int)ColumnId.Album, 
                new TreeIterCompareFunc(AlbumTreeIterCompareFunc));
            model.SetSortFunc((int)ColumnId.Genre, 
                new TreeIterCompareFunc(GenreTreeIterCompareFunc));
            model.SetSortFunc((int)ColumnId.Time, 
                new TreeIterCompareFunc(TimeTreeIterCompareFunc));
            model.SetSortFunc((int)ColumnId.Rating, 
                new TreeIterCompareFunc(RatingTreeIterCompareFunc));
            model.SetSortFunc((int)ColumnId.PlayCount, 
                new TreeIterCompareFunc(PlayCountTreeIterCompareFunc));
            model.SetSortFunc((int)ColumnId.LastPlayed, 
                new TreeIterCompareFunc(LastPlayedTreeIterCompareFunc));
        }    

        private void OnRipToggled(object o, ToggledArgs args)
        {
            try {
                AudioCdTrackInfo ti = (AudioCdTrackInfo)model.PathTrackInfo(new TreePath(args.Path));
                CellRendererToggle renderer = (CellRendererToggle)o;
                ti.CanRip = !ti.CanRip;
                renderer.Active = ti.CanRip;
            } catch(Exception) {
            
            }   
        }

        private bool CheckColumnDrop(TreeView tree, TreeViewColumn col,
                                     TreeViewColumn prev, TreeViewColumn next)
        {
            // Don't allow moving other columns before the first column
            return prev != null;
        }

        private int StringFieldCompare(string a, string b)
        {
            if(a != null)
                a = a.ToLower();
                
            if(b != null)
                b = b.ToLower();
                
            return String.Compare(a, b);
        }
        
        private int LongFieldCompare(long a, long b)
        {
            return a < b ? -1 : (a == b ? 0 : 1);
        }
            
        public int TrackTreeIterCompareFunc(TreeModel _model, TreeIter a,
            TreeIter b)
        {
            return LongFieldCompare((long)model.IterTrackInfo(a).TrackNumber,
                (long)model.IterTrackInfo(b).TrackNumber);
        }
            
        public int ArtistTreeIterCompareFunc(TreeModel _model, TreeIter a, 
            TreeIter b)
        {
            return StringFieldCompare(model.IterTrackInfo(a).Artist, 
                model.IterTrackInfo(b).Artist);
        }
        
        public int TitleTreeIterCompareFunc(TreeModel _model, TreeIter a, 
            TreeIter b)
        {
            return StringFieldCompare(model.IterTrackInfo(a).Title, 
                model.IterTrackInfo(b).Title);
        }
        
        public int AlbumTreeIterCompareFunc(TreeModel _model, TreeIter a, 
            TreeIter b)
        {
            int v = StringFieldCompare(model.IterTrackInfo(a).Album, 
                model.IterTrackInfo(b).Album);

            if (v != 0)
                return v;

            return TrackTreeIterCompareFunc (_model, a, b);
        }
        
        public int GenreTreeIterCompareFunc(TreeModel _model, TreeIter a,
            TreeIter b)
        {
            return StringFieldCompare(model.IterTrackInfo(a).Genre, 
                model.IterTrackInfo(b).Genre);
        }
        
        public int TimeTreeIterCompareFunc(TreeModel _model, TreeIter a,
            TreeIter b)
        {
            //return LongFieldCompare(model.IterTrackInfo(a).Duration,
            //    model.IterTrackInfo(b).Duration);
                
            return model.IterTrackInfo(a).Duration.CompareTo(model.IterTrackInfo(b).Duration);
        }
        
        public int RatingTreeIterCompareFunc(TreeModel _model, TreeIter a,
            TreeIter b)
        {
            return LongFieldCompare((long)model.IterTrackInfo(a).Rating,
                (long)model.IterTrackInfo(b).Rating);
        }
        
        public int PlayCountTreeIterCompareFunc(TreeModel _model, TreeIter a,
            TreeIter b)
        {
            return LongFieldCompare((long)model.IterTrackInfo(a).PlayCount,
                (long)model.IterTrackInfo(b).PlayCount);
        }
        
        public int LastPlayedTreeIterCompareFunc(TreeModel _model, TreeIter a,
            TreeIter b)
        {
            return DateTime.Compare(model.IterTrackInfo(a).LastPlayed,
                model.IterTrackInfo(b).LastPlayed);
        }
        
        public int DefaultTreeIterCompareFunc(TreeModel _model, TreeIter a, 
            TreeIter b)
        {
            return 0;
        }
            
        public TrackInfo IterTrackInfo(TreeIter iter)
        {
            return Model.GetValue(iter, 0) as TrackInfo;
        }
        
        private void SaveColumns()
        {
            foreach(PlaylistColumn plcol in columns)
                plcol.Save(Columns);
        }
        
        public void ColumnChooser()
        {
            columnChooser = new PlaylistColumnChooserDialog(columns);
            columnChooser.ShowAll();
        }
        
        public void Shutdown()
        {
            SaveColumns();
        }
                    
        protected void SetRendererAttributes(CellRendererText renderer, 
            string text, TreeIter iter)
        {
            renderer.Text = text;
            renderer.Weight = iter.Equals(model.PlayingIter) 
                ? (int)Pango.Weight.Bold 
                : (int)Pango.Weight.Normal;
            
            renderer.Foreground = null;
            
            renderer.Sensitive = true;
            
            TrackInfo ti = model.IterTrackInfo(iter);
            if(ti == null) {
                return;
            }
          
            if(ti is AudioCdTrackInfo) {
                renderer.Sensitive = ti.CanPlay; 
            }
        }
        
        protected void TrackCellInd(TreeViewColumn tree_column,
            CellRenderer cell, TreeModel tree_model, TreeIter iter)
        {
            TrackInfo ti = tree_model.GetValue(iter, 0) as TrackInfo;
            CellRendererPixbuf renderer = (CellRendererPixbuf)cell;
            
            if(PlayerEngineCore.CurrentTrack == null) {
                model.PlayingIter = TreeIter.Zero;
                if(ti != null && !(ti is AudioCdTrackInfo)) {
                    renderer.Pixbuf = ti.CanPlay ? null : songDrmedPixbuf;
                } else {
                    renderer.Pixbuf = null;
                }
                
                return;
            }
        
            if(ti != null) {
                bool same_track = false;
                
                if(PlayerEngineCore.CurrentTrack != null) {
                    //same_track = PlayerEngineCore.CurrentTrack.Equals(ti);
                    same_track = PlayerEngineCore.CurrentTrack == ti;
                }
                
                if(same_track) {
                    renderer.Pixbuf = nowPlayingPixbuf;
                    model.PlayingIter = iter;
                } else if(ti is AudioCdTrackInfo) {
                    renderer.Pixbuf = null;
                } else {
                    renderer.Pixbuf = ti.CanPlay ? null : songDrmedPixbuf;
                }
            } else {
                renderer.Pixbuf = null;
            }
        }
        
        protected void RipCellInd(TreeViewColumn tree_column, CellRenderer cell, 
            TreeModel tree_model, TreeIter iter)
        {
            CellRendererToggle toggle = (CellRendererToggle)cell;
            AudioCdTrackInfo ti = model.IterTrackInfo(iter) as AudioCdTrackInfo;
 
            if(ti != null) {
                toggle.Sensitive = ti.CanPlay && !ti.IsRipped;
                toggle.Activatable = toggle.Sensitive;
                toggle.Active = ti.CanRip && !ti.IsRipped;
            } else {
                toggle.Active = false;
            }
        }
        
        protected void TrackCellTrack(TreeViewColumn tree_column,
            CellRenderer cell, TreeModel tree_model, TreeIter iter)
        {
            TrackInfo ti = model.IterTrackInfo(iter);
            if(ti == null) {
                return;
            }
            
            SetRendererAttributes((CellRendererText)cell, Convert.ToString(ti.TrackNumber), iter);
        }    
        
        protected void TrackCellArtist(TreeViewColumn tree_column,
            CellRenderer cell, TreeModel tree_model, TreeIter iter)
        {
            TrackInfo ti = model.IterTrackInfo(iter);
            if(ti == null) {
                return;
            }
            
            SetRendererAttributes((CellRendererText)cell, ti.Artist, iter);
        }
        
        protected void TrackCellTitle(TreeViewColumn tree_column,
            CellRenderer cell, TreeModel tree_model, TreeIter iter)
        {
            TrackInfo ti = model.IterTrackInfo(iter);
            if(ti == null) {
                return;
            }
            
            SetRendererAttributes((CellRendererText)cell, ti.Title, iter);
        }
        
        protected void TrackCellAlbum(TreeViewColumn tree_column,
            CellRenderer cell, TreeModel tree_model, TreeIter iter)
        {
            TrackInfo ti = model.IterTrackInfo(iter);
            if(ti == null) {
                return;
            }
            
            SetRendererAttributes((CellRendererText)cell, ti.Album, iter);
        }
        
        protected void TrackCellGenre(TreeViewColumn tree_column,
            CellRenderer cell, TreeModel tree_model, TreeIter iter)
        {
            TrackInfo ti = model.IterTrackInfo(iter);
            if(ti == null) {
                return;
            }
            
            SetRendererAttributes((CellRendererText)cell, ti.Genre, iter);
        }
        
        protected void TrackCellTime(TreeViewColumn tree_column,
            CellRenderer cell, TreeModel tree_model, TreeIter iter)
        {
            TrackInfo Track = model.IterTrackInfo(iter);
            if(Track == null) {
                return;
            }
            
            SetRendererAttributes((CellRendererText)cell, 
                Track.Duration.TotalSeconds < 0.0 ? Catalog.GetString("N/A") : 
                DateTimeUtil.FormatDuration((long)Track.Duration.TotalSeconds), iter);
        }
        
        protected void TrackCellPlayCount(TreeViewColumn tree_column,
            CellRenderer cell, TreeModel tree_model, TreeIter iter)
        {
            TrackInfo ti = model.IterTrackInfo(iter);
            if(ti == null) {
                return;
            }
            
            uint plays = ti.PlayCount;
            SetRendererAttributes((CellRendererText)cell, plays > 0 ? Convert.ToString(plays) : "", iter);
        }
        
        protected void TrackCellRating(TreeViewColumn tree_column,
            CellRenderer cell, TreeModel tree_model, TreeIter iter)
        {           
            TrackInfo ti = model.IterTrackInfo(iter);
            if(ti == null) {
                return;
            }
             
            ((RatingRenderer)cell).Track = ti;
        }
        
        protected void TrackCellLastPlayed(TreeViewColumn tree_column,
            CellRenderer cell, TreeModel tree_model, TreeIter iter)
        {
            TrackInfo ti = model.IterTrackInfo(iter);
            if(ti == null) {
                return;
            }
            
            DateTime lastPlayed = ti.LastPlayed;
            
            string disp = String.Empty;
            
            if(lastPlayed > DateTime.MinValue) {
                disp = lastPlayed.ToString();
            }
            
            SetRendererAttributes((CellRendererText)cell, String.Format("{0}", disp), iter);
        }
        
        public void PlayPath(TreePath path)
        {
            model.PlayPath(path);
            QueueDraw();
            ScrollToPlaying();
        }
        
        public void UpdateView()
        {
            QueueDraw();
            ScrollToPlaying();
        }
        
        public void ThreadedQueueDraw()
        {
            Application.Invoke(delegate {
                QueueDraw();
            });
        }

        public void ScrollToPlaying()
        {
            Gdk.Rectangle cellRect = GetCellArea (model.PlayingPath, Columns[0]);

            Point point = new Point ();
            WidgetToTreeCoords (cellRect.Left, cellRect.Top, out point.X, out point.Y);
            cellRect.Location = point;

            // we only care about vertical bounds
            if (cellRect.Location.Y < VisibleRect.Location.Y ||
                cellRect.Location.Y + cellRect.Size.Height > VisibleRect.Location.Y + VisibleRect.Size.Height) {
                ScrollToCell(model.PlayingPath, null, true, 0.5f, 0.0f);
            }
        }
        
        public bool PlaySelected()
        {
            TreeIter selIter;
            
            try {
                if(!model.GetIter(out selIter, Selection.GetSelectedRows()[0]))
                    return false;
            } catch(Exception) {
                return false;
            }
            
            model.PlayIter(selIter);
            return true;
        }
       
        public TrackInfo SelectedTrackInfo {
            get {
                TreePath [] paths = Selection.GetSelectedRows();
                TreeIter selIter;
                
                if(paths.Length <= 0)
                    return null;
                
                if(!model.GetIter(out selIter, paths[0]))
                    return null;
                    
                return model.IterTrackInfo(selIter);
            }
        }
        
        public TrackInfo [] SelectedTrackInfoMultiple {
            get {
                if(Selection.CountSelectedRows() == 0)
                    return null;
                
                ArrayList list = new ArrayList();
                
                foreach(TreePath path in Selection.GetSelectedRows())
                    list.Add(model.PathTrackInfo(path));
                
                return list.ToArray(typeof(TrackInfo)) as TrackInfo [];
            }
        }

        protected override void OnDragBegin(Gdk.DragContext ctx)
        {
            // This is just here to block GtkTreeView's
            // implementation, which would use an image of the row
            // being dragged as the drag icon, which we don't want
            // since we might actually be dragging multiple rows.
        }

        protected override bool OnDragMotion(Gdk.DragContext context, int x, int y, uint time)
        {
            if (!base.OnDragMotion(context, x, y, time))
                return false;

            // Force the drag highlight to be either before or after a
            // row, not on top of one.

            TreePath path;
            TreeViewDropPosition pos;
            if(GetDestRowAtPos(x, y, out path, out pos))
                SetDragDestRow(path, (TreeViewDropPosition)((int)pos & 0x1));

            return true;
        }
    }
}
