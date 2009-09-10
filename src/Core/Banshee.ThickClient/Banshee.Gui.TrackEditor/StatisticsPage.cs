//
// StatisticsPage.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
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
using Mono.Unix;
using Gtk;

namespace Banshee.Gui.TrackEditor
{
    internal class FixedTreeView : TreeView
    {
        public FixedTreeView (ListStore model) : base (model)
        {
        }

        protected override bool OnKeyPressEvent (Gdk.EventKey evnt)
        {
            if ((evnt.State & Gdk.ModifierType.ControlMask) != 0 &&
                (evnt.Key == Gdk.Key.Page_Up || evnt.Key == Gdk.Key.Page_Down)) {
                return false;
            }
            return base.OnKeyPressEvent (evnt);
        }
    }

    public class StatisticsPage : ScrolledWindow, ITrackEditorPage
    {
        private CellRendererText name_renderer;
        private ListStore model;
        private TreeView view;
        
        public StatisticsPage ()
        {
            ShadowType = ShadowType.In;
            VscrollbarPolicy = PolicyType.Automatic;
            HscrollbarPolicy = PolicyType.Never;
            
            view = new FixedTreeView (model);
            view.HeadersVisible = false;
            view.RowSeparatorFunc = new TreeViewRowSeparatorFunc (RowSeparatorFunc);
            
            name_renderer = new CellRendererText ();
            name_renderer.Alignment = Pango.Alignment.Right;
            name_renderer.Weight = (int)Pango.Weight.Bold;
            name_renderer.Xalign = 1.0f;
            name_renderer.Scale = Pango.Scale.Small;
            
            CellRendererText value_renderer = new CellRendererText ();
            value_renderer.Ellipsize = Pango.EllipsizeMode.End;
            value_renderer.Editable = true;
            value_renderer.Scale = Pango.Scale.Small;
            value_renderer.EditingStarted += delegate(object o, EditingStartedArgs args) {
                var entry = args.Editable as Entry;
                if (entry != null) {
                    entry.IsEditable = false;
                }
            };
            
            view.AppendColumn (Catalog.GetString ("Name"), name_renderer, "text", 0);
            view.AppendColumn (Catalog.GetString ("Value"), value_renderer, "text", 1);
            
            Add (view);
            ShowAll ();
        }
        
        private bool RowSeparatorFunc (TreeModel model, TreeIter iter)
        {
            return (bool)model.GetValue (iter, 2);
        }
        
        protected override void OnStyleSet (Style previous_style)
        {
            base.OnStyleSet (previous_style);
            name_renderer.CellBackgroundGdk = Style.Background (StateType.Normal);
        }

        public void Initialize (TrackEditorDialog dialog)
        {
        }
        
        public void LoadTrack (EditorTrackInfo track)
        {
            BorderWidth = 2;
            model = new ListStore (typeof (string), typeof (string), typeof (bool));
            view.Model = model;
            
            TagLib.File file = track.TaglibFile;
            
            if (track.Uri.IsLocalPath) {
                string path = track.Uri.AbsolutePath;
                AddItem (Catalog.GetString ("File Name:"), System.IO.Path.GetFileName (path));
                AddItem (Catalog.GetString ("Directory:"), System.IO.Path.GetDirectoryName (path));
                AddItem (Catalog.GetString ("Full Path:"), path);
                try {
                    AddFileSizeItem (Banshee.IO.File.GetSize (track.Uri));
                } catch {
                }
            } else {
                AddItem (Catalog.GetString ("URI:"), track.Uri.AbsoluteUri);
                AddFileSizeItem (track.FileSize);
            }
            
            AddSeparator ();
            
            if (file != null) {
                System.Text.StringBuilder builder = new System.Text.StringBuilder ();
                Banshee.Sources.DurationStatusFormatters.ConfusingPreciseFormatter (builder, file.Properties.Duration);
                AddItem (Catalog.GetString ("Duration:"), String.Format ("{0} ({1}ms)", 
                    builder, file.Properties.Duration.TotalMilliseconds));
                
                AddItem (Catalog.GetString ("Audio Bitrate:"), String.Format ("{0} KB/sec", 
                    file.Properties.AudioBitrate));
                AddItem (Catalog.GetString ("Audio Sample Rate:"), String.Format ("{0} Hz", 
                    file.Properties.AudioSampleRate)); 
                AddItem (Catalog.GetString ("Audio Channels:"), file.Properties.AudioChannels);
                
                if ((file.Properties.MediaTypes & TagLib.MediaTypes.Video) != 0) {
                    AddItem (Catalog.GetString ("Video Dimensions:"), String.Format ("{0}x{1}", 
                        file.Properties.VideoWidth, file.Properties.VideoHeight));
                }
                
                foreach (TagLib.ICodec codec in file.Properties.Codecs) {
                    if (codec != null) {
                        /* Translators: {0} is the description of the codec */
                        AddItem (String.Format (Catalog.GetString ("{0} Codec:"), 
                            codec.MediaTypes.ToString ()), codec.Description);
                    }
                }
                
                AddItem (Catalog.GetString ("Container Formats:"), file.TagTypes.ToString ());
                AddSeparator ();
            }
            
            AddItem (Catalog.GetString ("Imported On:"), track.DateAdded > DateTime.MinValue 
                ? track.DateAdded.ToString () : Catalog.GetString ("Unknown"));
            AddItem (Catalog.GetString ("Last Played:"), track.LastPlayed > DateTime.MinValue 
                ? track.LastPlayed.ToString () : Catalog.GetString ("Unknown"));
            AddItem (Catalog.GetString ("Last Skipped:"), track.LastSkipped > DateTime.MinValue 
                ? track.LastSkipped.ToString () : Catalog.GetString ("Unknown"));
            AddItem (Catalog.GetString ("Play Count:"), track.PlayCount);
            AddItem (Catalog.GetString ("Skip Count:"), track.SkipCount);
            AddItem (Catalog.GetString ("Score:"), track.Score);
        }
        
        private void AddFileSizeItem (long bytes)
        {
            Hyena.Query.FileSizeQueryValue value = new Hyena.Query.FileSizeQueryValue (bytes);
            AddItem (Catalog.GetString ("File Size:"), String.Format ("{0} ({1} {2})", 
                value.ToUserQuery (), bytes, Catalog.GetString ("bytes")));
        }
        
        public void AddItem (string name, object value)
        {
            if (name != null && value != null) {
                model.AppendValues (name, value.ToString (), false);
            }
        }
        
        public void AddSeparator ()
        {
            model.AppendValues (String.Empty, String.Empty, true);
        }
        
        public int Order {
            get { return 40; }
        }
        
        public string Title {
            get { return Catalog.GetString ("Properties"); }
        }
        
        public PageType PageType { 
            get { return PageType.View; }
        }
        
        public Gtk.Widget TabWidget { 
            get { return null; }
        }
        
        public Gtk.Widget Widget {
            get { return this; }
        }
    }
}
