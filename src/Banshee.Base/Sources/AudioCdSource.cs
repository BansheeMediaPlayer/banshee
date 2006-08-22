/***************************************************************************
 *  AudioCdSource.cs
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
using System.Data;
using System.Collections;
using Mono.Unix;
using Gtk;

using Banshee.Base;
using Banshee.Widgets;

namespace Banshee.Sources
{
    public class AudioCdSource : Source, IImportSource
    {
        private AudioCdDisk disk;
        private VBox box;
        private Alignment container;
        private AudioCdRipper ripper;
        
        private HighlightMessageArea audiocd_statusbar;
        
        public AudioCdSource(AudioCdDisk disk) : base(disk.Title, 200)
        {
            this.disk = disk;
            disk.Updated += OnDiskUpdated;
            
            container = new Alignment(0.0f, 0.0f, 1.0f, 1.0f);
            
            audiocd_statusbar = new HighlightMessageArea();
            audiocd_statusbar.BorderWidth = 5;
            audiocd_statusbar.LeftPadding = 15;
            audiocd_statusbar.ButtonClicked += delegate { disk.QueryMetadata(); };
            
            box = new VBox();
            box.Spacing = 5;
            box.PackStart(container, true, true, 0);
            box.PackStart(audiocd_statusbar, false, false, 0);
            
            container.Show();
            box.Show();
            
            SourceManager.SourceRemoved += OnSourceRemoved;
        }
        
        private void UpdateAudioCdStatus()
        {
            string status = null;
            Gdk.Pixbuf icon = null;
            
            switch(disk.Status) {
                case AudioCdLookupStatus.ReadingDisk:
                    status = Catalog.GetString("Reading table of contents from CD...");
                    icon = IconThemeUtils.LoadIcon(22, "media-cdrom", "gnome-dev-cdrom-audio", "source-cd-audio");
                    audiocd_statusbar.ShowCloseButton = false;
                    break;
                case AudioCdLookupStatus.SearchingMetadata:
                    status = Catalog.GetString("Searching for CD metadata...");
                    icon = IconThemeUtils.LoadIcon(22, "system-search", Stock.Find);
                    audiocd_statusbar.ShowCloseButton = false;
                    break;
                case AudioCdLookupStatus.SearchingCoverArt:
                    status = Catalog.GetString("Searching for CD cover art...");
                    icon = IconThemeUtils.LoadIcon(22, "system-search", Stock.Find);
                    audiocd_statusbar.ShowCloseButton = false;
                    break;
                case AudioCdLookupStatus.ErrorNoConnection:
                    status = Catalog.GetString("Cannot search for CD metadata: " + 
                        "there is no available Internet connection");
                    icon = IconThemeUtils.LoadIcon(22, "network-wired", Stock.Network);
                    audiocd_statusbar.ShowCloseButton = true;
                    break;
                case AudioCdLookupStatus.ErrorLookup:
                    status = Catalog.GetString("Could not fetch metadata for CD.");
                    icon = IconThemeUtils.LoadIcon(22, Stock.DialogError);
                    audiocd_statusbar.ShowCloseButton = true;
                    break;
                case AudioCdLookupStatus.Success:
                default:
                    status = null;
                    icon = null;
                    break;
            }
            
            if(disk.Status == AudioCdLookupStatus.ErrorLookup) {
                audiocd_statusbar.ButtonLabel = Stock.Refresh;
                audiocd_statusbar.ButtonUseStock = true;
            } else {
                audiocd_statusbar.ButtonLabel = null;
            }
            
            audiocd_statusbar.Visible = status != null;
            audiocd_statusbar.Message = String.Format("<big>{0}</big>", GLib.Markup.EscapeText(status));
            audiocd_statusbar.Pixbuf = icon;
        }
        
        public override bool Eject()
        {
            if(!disk.Eject()) {
                return false;
            }
            
            SourceManager.RemoveSource(this);
            return true;
        }
        
        public override void Activate()
        {
            InterfaceElements.DetachPlaylistContainer();
            container.Add(InterfaceElements.PlaylistContainer);
            UpdateAudioCdStatus();
        }
        
        public void Import()
        {
            SourceManager.SetActiveSource(this);
            ImportDisk();
            OnUpdated();
        }
        
        private void ImportDisk()
        {
            if(disk.IsRipping) {
                Console.WriteLine("CD is already ripping");
                return;
            }
            
            disk.IsRipping = true;
        
            ArrayList list = new ArrayList();
            
            foreach(AudioCdTrackInfo track in disk.Tracks) {
                if(track.CanRip) {
                    list.Add(track);
                }
            }
            
            if(list.Count > 0) {
                ripper = new AudioCdRipper();
                ripper.Finished += OnRipperFinished;
                ripper.HaveTrackInfo += OnRipperHaveTrackInfo;
                foreach(AudioCdTrackInfo track in list) {
                    ripper.QueueTrack(track);
                }
                
                AudioCdTrackInfo playing_track = PlayerEngineCore.CurrentTrack as AudioCdTrackInfo;
                if(playing_track != null && playing_track.Disk == disk) {
                    PlayerEngineCore.Close();
                }
                
                ripper.Start();
            } else {
                HigMessageDialog dialog = new HigMessageDialog(InterfaceElements.MainWindow, DialogFlags.Modal, 
                    MessageType.Info, ButtonsType.Ok, 
                    Catalog.GetString("Invalid Selection"),
                    Catalog.GetString("You must select at least one track to import.")
                );
                dialog.Run();
                dialog.Destroy();
                disk.IsRipping = false;
            }
        }
        
        private void OnSourceRemoved(SourceEventArgs args)
        {
            if(args.Source == this && ripper != null) {
                ripper.Cancel();
            }
        }
        
        private void OnRipperHaveTrackInfo(object o, HaveTrackInfoArgs args)
        {
            OnUpdated();
        }
        
        private void OnRipperFinished(object o, EventArgs args)
        {
            disk.IsRipping = false;
            ripper = null;
            OnUpdated();
        }
        
        private void OnDiskUpdated(object o, EventArgs args)
        {
            ThreadAssist.ProxyToMain(delegate {
                Name = disk.Title;
                UpdateAudioCdStatus();
                OnUpdated();
            });
        }
                
        private Gdk.Pixbuf icon = IconThemeUtils.LoadIcon(22, "media-cdrom", "gnome-dev-cdrom-audio", "source-cd-audio");
        public override Gdk.Pixbuf Icon {
            get { return icon; }
        }
                
        public override int Count {
            get { return disk.TrackCount; }
        }
        
        public AudioCdDisk Disk {
            get { return disk; }
        }
        
        public override IEnumerable Tracks {
            get { return disk.Tracks; }
        }
        
        public override bool SearchEnabled {
            get { return false; }
        }
        
        public override Gtk.Widget ViewWidget {
            get { return box; }
        }
    }
}
