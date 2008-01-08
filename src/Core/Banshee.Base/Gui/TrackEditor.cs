/***************************************************************************
 *  TrackEditor.cs
 *
 *  Copyright (C) 2005-2007 Novell, Inc.
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
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Gtk;
using Glade;
using Mono.Unix;

using Banshee.Base;
using Banshee.Widgets;
using Banshee.Configuration.Schema;

using Hyena.Gui;

namespace Banshee.Gui.Dialogs
{
    internal class EditorTrack
    {
        private TrackInfo track;
        
        public string Artist;
        public string Album;
        public string Title;
        public string Genre;
        public SafeUri Uri;
        
        public uint TrackNumber;
        public uint TrackCount;
        public int Year;
        public uint Rating;
        
        // temp properties
        public bool ProcessedStream = false;
        public string CoverArtFilename;
        public bool EmbedCoverArt;
        public bool CopyCoverArt;
        public Gdk.Pixbuf CoverArtThumbnail;
        public int Channels;
        public int Bitrate;
        public int SampleRate;
        public long FileSize;
    
        public EditorTrack(TrackInfo track)
        {
            this.track = track;
            Revert();
        }
        
        public void Revert()
        {
            Artist = track.Artist == null ? String.Empty : track.Artist;
            Album = track.Album == null ? String.Empty : track.Album;
            Title = track.Title == null ? String.Empty : track.Title;
            Genre = track.Genre == null ? String.Empty : track.Genre;
            TrackNumber = track.TrackNumber;
            TrackCount = track.TrackCount;
            Year = track.Year;
            Uri = track.Uri;
            Rating = track.Rating;
        }
        
        public void Save()
        {
            track.Artist = Artist;
            track.Album = Album;
            track.Title = Title;
            track.Genre = Genre;
            track.TrackNumber = TrackNumber;
            track.TrackCount = TrackCount;
            track.Uri = Uri;
            track.Year = Year;
            track.Rating = Rating;
        }
        
        public TrackInfo Track {
            get { return track; }
        }
    }

    public class TrackEditor : GladeWindow
    {
        private struct PasteParseItem
        {
            public int TrackNumber;
            public string Title;
            
            public PasteParseItem (int trackNumber, string title)
            {
                this.TrackNumber = trackNumber;
                this.Title = title;
            }
        }
    
        [Widget] private Button CancelButton;
        [Widget] private Button SaveButton;
        [Widget] private Button Previous;
        [Widget] private Button Next;
        [Widget] private Button TrackNumberIterator;
        [Widget] private Button TrackCountSync;
        [Widget] private Button ArtistSync;
        [Widget] private Button AlbumSync;
        [Widget] private Button YearSync;
        [Widget] private Button GenreSync;
        [Widget] private Button RatingSync;
        [Widget] private Button EnterNextTitle;
        [Widget] private SpinButton TrackCount;
        [Widget] private SpinButton TrackNumber;
        [Widget] private Entry Year;
        [Widget] private Entry Artist;
        [Widget] private Entry Album;
        [Widget] private Entry Title;
        [Widget] private ComboBoxEntry Genre;
        [Widget] private Entry Uri;
        [Widget] private Entry Location;
        [Widget] private Label BitRate;
        [Widget] private Label SampleRate;
        [Widget] private Label Channels;
        [Widget] private Label FileSize;
        [Widget] private Button SyncAll;
        [Widget] private Image CoverImage;
        [Widget] private Button CoverButton;
        //[Widget] private Button ClearCoverArt;
        //[Widget] private Button SyncCoverArt;
        [Widget] private CheckButton EmbedCoverArt;
        [Widget] private CheckButton CopyCoverArt;
        [Widget] private Box RatingContainer;
        
        private Tooltips tips = new Tooltips();
        private RatingEntry rating_entry = new RatingEntry();
        private List<EntryUndoAdapter> entry_undo_adapters = new List<EntryUndoAdapter>();
        
        private List<EditorTrack> TrackSet = new List<EditorTrack>();
        private int currentIndex = 0;
        private bool first_load = false;

        public event EventHandler Saved;

        public TrackEditor(IList<TrackInfo> selection) : base("TrackEditorWindow")
        {
            if(selection == null) {
                return;
            }
        
            foreach(TrackInfo track in selection) {
                TrackSet.Add(new EditorTrack(track));
            }
            
            rating_entry.Show();
            (Glade["RatingLabel"] as Label).MnemonicWidget = rating_entry;
            RatingContainer.PackStart(rating_entry, false, false, 0);

            TrackNumberIterator.ExposeEvent += OnTrackNumberIteratorExpose;

            CancelButton.Clicked += OnCancelButtonClicked;
            SaveButton.Clicked += OnSaveButtonClicked;
            Previous.Clicked += OnPreviousClicked;
            Next.Clicked += OnNextClicked;
            
            TrackNumberIterator.Clicked += OnTrackNumberIteratorClicked;
            TrackCountSync.Clicked += OnTrackCountSyncClicked;
            ArtistSync.Clicked += OnArtistSyncClicked;
            AlbumSync.Clicked += OnAlbumSyncClicked;
            GenreSync.Clicked += OnGenreSyncClicked;
            YearSync.Clicked += OnYearSyncClicked;
            SyncAll.Clicked += OnSyncAllClicked;
            RatingSync.Clicked += OnRatingSyncClicked;
            EnterNextTitle.Clicked += OnEnterNextTitleClicked;
            CoverButton.Clicked += OnCoverButtonClicked;
            
            Artist.Changed += OnValueEdited;
            Album.Changed += OnValueEdited;
            Title.Changed += OnValueEdited;
            Title.Activated += OnTitleActivated;
            Year.Changed += OnValueEdited;
            Genre.Entry.Changed += OnValueEdited;
            rating_entry.Changed += OnValueEdited;
            
            Title.TextInserted += OnTitleTextInsertedEvent;
            
            ListStore genre_model = new ListStore(typeof(string));
            Genre.Model = genre_model;
            Genre.TextColumn = 0;
            
            foreach(string genre in Globals.Library.GetGenreList()) {
                genre_model.AppendValues(genre);            
            }
            
            Next.Visible = TrackSet.Count > 1;
            Previous.Visible = TrackSet.Count > 1;

            Glade["MultiTrackButtons"].Visible = TrackSet.Count > 1;
            TrackNumberIterator.Visible = TrackSet.Count > 1;
            TrackCountSync.Visible = TrackSet.Count > 1;
            ArtistSync.Visible = TrackSet.Count > 1;
            AlbumSync.Visible = TrackSet.Count > 1;
            GenreSync.Visible = TrackSet.Count > 1;
            YearSync.Visible = TrackSet.Count > 1;
            RatingSync.Visible = TrackSet.Count > 1;
            EnterNextTitle.Visible = TrackSet.Count > 1;
            Glade["SyncAllAlignment"].Visible = TrackSet.Count > 1;
            
            (Glade["Notebook"] as Gtk.Notebook).RemovePage(1);

            tips.SetTip(TrackNumberIterator, Catalog.GetString("Automatically set all track numbers in increasing order"), "track iterator");
            tips.SetTip(TrackCountSync, Catalog.GetString("Set all track counts to this value"), "track counts");
            tips.SetTip(ArtistSync, Catalog.GetString("Set all artists to this value"), "artists");
            tips.SetTip(AlbumSync, Catalog.GetString("Set all albums to this value"), "albums");
            tips.SetTip(GenreSync, Catalog.GetString("Set all genres to this value"), "genres"); 
            tips.SetTip(YearSync, Catalog.GetString("Set all years to this value"), "years");
            tips.SetTip(SyncAll, Catalog.GetString("Apply the values of this track set for the Artist, Album Title, Genre, Track count, Year, and Rating fields to the rest of the selected tracks in this editor."), "all");
            tips.SetTip(RatingSync, Catalog.GetString("Set all ratings to this value"), "ratings");
            
            LoadTrack(0);
            
            foreach(Entry entry in new Entry [] { Artist, Album, Title, Year, Genre.Entry }) {
                entry_undo_adapters.Add(new EntryUndoAdapter(entry));
            }
            
            Window.Show();
        }
        
        private void OnTrackNumberIteratorExpose(object o, ExposeEventArgs args)
        {
            Gdk.Rectangle alloc = TrackNumberIterator.Allocation;
            Gdk.GC gc = TrackNumberIterator.Style.DarkGC(StateType.Normal);
            Gdk.Drawable drawable = TrackNumberIterator.GdkWindow;
            
            int x_pad = (int)((double)alloc.Width * 0.15);
            int y_pad = (int)((double)alloc.Height * 0.15);
            
            int left_x = alloc.X + x_pad;
            int top_y = alloc.Y + y_pad;
            int mid_x = alloc.X + ((alloc.Width / 2) - x_pad) + 2; 
            int mid_y = alloc.Y + (alloc.Height / 2);
            int bottom_y = (alloc.Y + alloc.Height) - y_pad;
            
            drawable.DrawLine(gc, left_x, top_y, mid_x, top_y);
            drawable.DrawLine(gc, mid_x + 1, top_y + 1, mid_x + 1, mid_y - 15);
            
            drawable.DrawLine(gc, left_x, bottom_y, mid_x, bottom_y);
            drawable.DrawLine(gc, mid_x + 1, bottom_y - 1, mid_x + 1, mid_y + 13);
        }
        
        private void LoadTrack(int index)
        {
            if(index < 0 || index >= TrackSet.Count) {
                return;
            }
                
            SetCoverImage(null);
            
            foreach(EntryUndoAdapter undo_adapter in entry_undo_adapters) {
                undo_adapter.UndoManager.Clear();
            }
                
            EditorTrack track = TrackSet[index] as EditorTrack;
            
            TrackNumber.Value = track.TrackNumber;
            TrackCount.Value = track.TrackCount;
            Year.Text = track.Year.ToString();
            rating_entry.Value = (int)track.Rating;
        
            (Glade["Artist"] as Entry).Text = track.Artist;
            (Glade["Album"] as Entry).Text = track.Album;
            (Glade["Title"] as Entry).Text = track.Title;
            (Glade["Genre"] as ComboBoxEntry).Entry.Text = track.Genre;
            
            (Glade["DurationLabel"] as Label).Text = String.Format("{0}:{1}", 
                track.Track.Duration.Minutes, (track.Track.Duration.Seconds).ToString("00"));
            (Glade["PlayCountLabel"] as Label).Text = track.Track.PlayCount.ToString();
            (Glade["LastPlayedLabel"] as Label).Text = track.Track.LastPlayed == DateTime.MinValue ?
                Catalog.GetString("Never played") : track.Track.LastPlayed.ToString();
            (Glade["ImportedLabel"] as Label).Text = track.Track.DateAdded == DateTime.MinValue ?
                Catalog.GetString("Unknown") : track.Track.DateAdded.ToString();
                
            if(first_load) {
                EmbedCoverArt.Active = true;
                CopyCoverArt.Active = true;
                first_load = false;
            } else {
                EmbedCoverArt.Active = track.EmbedCoverArt;
                CopyCoverArt.Active = track.CopyCoverArt;
            }
            
            Window.Title = TrackSet.Count > 1 
                ? String.Format(Catalog.GetString("Editing song {0} of {1}"), index + 1, TrackSet.Count)
                : String.Format(Catalog.GetString("Editing {0}"), track.Title);
       
            if(track.Uri.IsLocalPath) {
                Uri.Text = System.IO.Path.GetFileName(track.Uri.LocalPath);
                Location.Text = System.IO.Path.GetDirectoryName(track.Uri.LocalPath);
            } else {
                Uri.Text = track.Uri.ToString();
                Location.Text = String.Empty;
            }
            
            FileSize.Text = Catalog.GetString("Unknown");
                
            if(!(track.Track is AudioCdTrackInfo) && !track.ProcessedStream) {
                track.ProcessedStream = true;
                
                if(track.Uri.Scheme == System.Uri.UriSchemeFile) {
                    try {
                        System.IO.FileInfo info = new System.IO.FileInfo(track.Uri.LocalPath);
                        track.FileSize = info.Length;
                    } catch {
                    }
                }
                
                try {
                    TagLib.File file = StreamTagger.ProcessUri(track.Uri);
                
                    /*try {
                        TagLib.IPicture [] pictures = file.Tag.Pictures;
                        
                        if(pictures != null && pictures.Length > 0) {
                            TagLib.IPicture cover_picture = null;
                            foreach(TagLib.IPicture picture in pictures) {
                                if(cover_picture == null) {
                                    cover_picture = picture;
                                }
                                
                                if(picture.Type == TagLib.PictureType.FrontCover) {
                                    cover_picture = picture;
                                    break;
                                }
                            }
                            
                            if(cover_picture != null) {
                                Gdk.Pixbuf pixbuf = new Gdk.Pixbuf(cover_picture.Data.Data);
                                track.CoverArtThumbnail = pixbuf.ScaleSimple(100, 100, Gdk.InterpType.Bilinear);
                            }
                        }
                    } catch {
                    }*/
                
                    track.Bitrate = file.Properties.AudioBitrate;
                    track.SampleRate = file.Properties.AudioSampleRate;
                    track.Channels = file.Properties.AudioChannels;
                } catch(Exception) {
                    track.Bitrate = -1;
                }
            } 
            
            if(track.ProcessedStream) {
                FileSize.Text = String.Format("{0:0.0} MB", (double)track.FileSize / 1024.0 / 1024.0);
                
                if(track.Bitrate > 0) {
                    BitRate.Text = String.Format("{0} kbps", track.Bitrate);
                    SampleRate.Text = String.Format("{0} Hz", track.SampleRate);
                    Channels.Text = String.Format("{0}", track.Channels);
                } else {
                    BitRate.Text = String.Format("{0} kbps", track.Bitrate);
                    SampleRate.Text = String.Format("{0} Hz", track.SampleRate);
                    Channels.Text = String.Format("{0}", track.Channels);
                }
                
                SetCoverImage(track.CoverArtThumbnail);
            }
            
            Previous.Sensitive = index > 0;
            Next.Sensitive = index < TrackSet.Count - 1;
            EnterNextTitle.Sensitive = Next.Sensitive;
        }
        
        private static Gdk.Pixbuf no_cover_image = Gdk.Pixbuf.LoadFromResource("editor-cover-album.png");
        
        private void SetCoverImage(Gdk.Pixbuf pixbuf)
        {
            if(pixbuf == null) {
                CoverImage.Pixbuf = no_cover_image;
            } else {
                CoverImage.Pixbuf = pixbuf;
            }
        }
        
        private void OnPreviousClicked(object o, EventArgs args)
        {
            UpdateCurrent();
            LoadTrack(--currentIndex);
        }
        
        private void OnNextClicked(object o, EventArgs args)
        {
            UpdateCurrent();
            LoadTrack(++currentIndex);
        }

        private void OnTrackNumberIteratorClicked(object o, EventArgs args)
        {
            int i = 1;
            foreach(EditorTrack track in TrackSet) {
                track.TrackNumber = (uint)i++;
                track.TrackCount = (uint)TrackSet.Count;
            }

            EditorTrack current_track = TrackSet[currentIndex] as EditorTrack;
            TrackNumber.Value = (int)current_track.TrackNumber;
            TrackCount.Value = (int)current_track.TrackCount;
        }
        
        private void OnValueEdited(object o, EventArgs args)
        {
            if(currentIndex < 0 || currentIndex >= TrackSet.Count) {
                return;
            }
        }
        
        private void OnTrackCountSyncClicked(object o, EventArgs args)
        {
            foreach(EditorTrack track in TrackSet) {
                track.TrackCount = (uint)TrackCount.Value;
            }
        }
        
        private void OnRatingSyncClicked(object o, EventArgs args)
        {
            foreach(EditorTrack track in TrackSet) {
                track.Rating = (uint)rating_entry.Value;
            }
        }
        
        private void OnYearSyncClicked(object o, EventArgs args)
        {
            foreach(EditorTrack track in TrackSet) {
                try {
                    track.Year = Convert.ToInt32(Year.Text);
                } catch {
                    track.Year = 0;
                }
            }
        }
        
        private void OnArtistSyncClicked(object o, EventArgs args)
        {
            foreach(EditorTrack track in TrackSet) {
                track.Artist = Artist.Text;
            }
        }

        private void OnAlbumSyncClicked(object o, EventArgs args)
        {
            foreach(EditorTrack track in TrackSet) {
                track.Album = Album.Text;
            }
        }
        
        private void OnSyncAllClicked(object o, EventArgs args)
        {
            OnTrackCountSyncClicked(o, args);
            OnGenreSyncClicked(o, args);
            OnAlbumSyncClicked(o, args);
            OnArtistSyncClicked(o, args);
            OnYearSyncClicked(o, args);
            OnRatingSyncClicked(o, args);
        }
        
        private void OnGenreSyncClicked(object o, EventArgs args)
        {
            foreach(EditorTrack track in TrackSet) {
                track.Genre = Genre.Entry.Text;
            }
        }
        
        private void OnEnterNextTitleClicked(object o, EventArgs args)
        {
            OnNextClicked(o, args);
            Title.HasFocus = true;
            Title.SelectRegion(0, Title.Text.Length);
        }
        
        private void OnTitleActivated(object o, EventArgs args)
        {
            if(EnterNextTitle.Sensitive) {
                OnEnterNextTitleClicked(o, args);
            }
        }
        
        private string pasted_text = null;
        
        private void OnTitleTextInsertedEvent (object o, TextInsertedArgs args)
        {
            if (currentIndex == 0 && args.Text.Length > 2 && (args.Text.Contains ("\n") || 
                args.Text.Contains ("\t") || args.Text.Contains ("\r"))) {
                int index = 0;
                foreach (PasteParseItem item in ParsePaste (args.Text)) {
                    if (index < TrackSet.Count) {
                        if (index == 0) {
                            Title.Text = item.Title;
                        }
                        
                        TrackSet[index].Title = item.Title;
                        TrackSet[index].TrackNumber = (uint)(item.TrackNumber > 0 ? item.TrackNumber : index + 1);
                        index++;    
                    }
                }
            }
        }
        
        private string last_path = null;
        
        private void OnCoverButtonClicked(object o, EventArgs args)
        {
            Banshee.Gui.Dialogs.ImageFileChooserDialog chooser = new Banshee.Gui.Dialogs.ImageFileChooserDialog();
            chooser.LocalOnly = true;
            
            try {
                string path = (TrackSet[currentIndex] as EditorTrack).CoverArtFilename;
                path = System.IO.Path.GetDirectoryName(path);
                
                if(path != null && path != String.Empty) {
                    chooser.SetCurrentFolder(path);
                } else if(last_path != null && last_path != String.Empty) {
                    path = System.IO.Path.GetDirectoryName(last_path);
                    chooser.SetCurrentFolder(path);
                }
            } catch {
            }
            
            if(chooser.Run() == (int)ResponseType.Ok) {
                last_path = chooser.Filename;
                
                try {
                    Gdk.Pixbuf pixbuf = new Gdk.Pixbuf(chooser.Filename).ScaleSimple(100, 100, Gdk.InterpType.Bilinear);
                    SetCoverImage(pixbuf);
                    (TrackSet[currentIndex] as EditorTrack).CoverArtFilename = chooser.Filename;
                    (TrackSet[currentIndex] as EditorTrack).CoverArtThumbnail = pixbuf;
                } catch {
                    SetCoverImage(null);
                }
            }
            
            chooser.Destroy();
        }
        
        private EditorTrack UpdateCurrent()
        {
            if(currentIndex < 0 || currentIndex >= TrackSet.Count) {
                return null;
            }
                
            EditorTrack track = TrackSet[currentIndex] as EditorTrack;
            
            track.TrackNumber = (uint)TrackNumber.Value;
            track.TrackCount = (uint)TrackCount.Value;
            track.Artist = Artist.Text;
            track.Album = Album.Text;
            track.Title = Title.Text;
            track.Genre = Genre.Entry.Text;
            track.CopyCoverArt = CopyCoverArt.Active;
            track.EmbedCoverArt = EmbedCoverArt.Active;
            track.Rating = (uint)rating_entry.Value;
            
            try {
                track.Year = Convert.ToInt32(Year.Text);
            } catch {
                track.Year = 0;
            }
            
            return track;
        }

        private void OnCancelButtonClicked(object o, EventArgs args)
        {
            Window.Destroy();
        }
        
        private void OnSaveButtonClicked(object o, EventArgs args)
        {
            UpdateCurrent();
            
            foreach(EditorTrack track in TrackSet) {
                SaveTrack(track, true);
            }

            EventHandler handler = Saved;
            if(handler != null) {
                handler(this, new EventArgs());
            }
            
            Window.Destroy();
        }
        
        private static IEnumerable<PasteParseItem> ParsePaste (string input)
        {
            foreach (string track in Regex.Split (input, @"[\t\n\r]")) {
                int track_number = 0;
                string title = track.Trim ();
                
                try {
                    Match match = Regex.Match (title, @"^([0-9]+)\.(.*)$");
                    if (match.Groups.Count == 3) {
                        track_number = Int32.Parse (match.Groups[1].Captures[0].Value);
                        title = match.Groups[2].Captures[0].Value;
                    }
                } catch {
                }
                
                yield return new PasteParseItem (track_number, title.Trim ());
            }
        }
        
        private void SaveTrack(EditorTrack track, bool writeToDatabase)
        {
            track.Save();
            
            if(writeToDatabase) {
                track.Track.Save();
                
                if(LibrarySchema.WriteMetadata.Get()) {
                    SaveToFile(track);
                }
            }
                
            if(track.Track == PlayerEngineCore.CurrentTrack) {
                PlayerEngineCore.TrackInfoUpdated();
            }
        }
        
        private void SaveToFile(EditorTrack track)
        {
            Banshee.Kernel.Scheduler.Schedule(new SaveTrackMetadataJob(track.Track), 
                Banshee.Kernel.JobPriority.Highest);
        }
    }
}
