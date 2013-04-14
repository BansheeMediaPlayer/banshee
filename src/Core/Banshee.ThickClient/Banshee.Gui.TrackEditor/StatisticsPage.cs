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
        private CellRendererText value_renderer;
        private ListStore model;
        private TreeView view;

        public StatisticsPage ()
        {
            ShadowType = ShadowType.In;
            VscrollbarPolicy = PolicyType.Automatic;
            HscrollbarPolicy = PolicyType.Never;
            BorderWidth = 2;

            view = new FixedTreeView (model);
            view.HeadersVisible = false;
            view.RowSeparatorFunc = new TreeViewRowSeparatorFunc (RowSeparatorFunc);
            view.HasTooltip = true;
            view.QueryTooltip += HandleQueryTooltip;

            name_renderer = new CellRendererText () {
                Alignment = Pango.Alignment.Right,
                Weight = (int)Pango.Weight.Bold,
                Xalign = 1.0f,
                Yalign = 0.0f,
                Scale = Pango.Scale.Small
            };

            value_renderer = new CellRendererText ();
            value_renderer.Editable = true;
            value_renderer.Scale = Pango.Scale.Small;
            value_renderer.Ellipsize = Pango.EllipsizeMode.End;
            value_renderer.WrapMode = Pango.WrapMode.Word;
            value_renderer.EditingStarted += delegate(object o, EditingStartedArgs args) {
                var entry = args.Editable as Entry;
                if (entry != null) {
                    entry.IsEditable = false;
                }
            };

            view.SizeAllocated += delegate { UpdateWrapWidth (); };
            view.Realized += delegate { UpdateWrapWidth (); };

            view.AppendColumn (Catalog.GetString ("Name"), name_renderer, "text", 0);
            view.AppendColumn (Catalog.GetString ("Value"), value_renderer, "text", 1, "ellipsize", 3, "ellipsize-set", 4, "wrap-width", 5);

            Add (view);
            ShowAll ();
        }

        public void UpdateWrapWidth ()
        {
            if (view.IsRealized && model != null) {
                var width = GetValueWidth ();

                model.Foreach ((TreeModel m, TreePath path, TreeIter iter) => {
                    if ((Pango.EllipsizeMode) model.GetValue (iter, 3) != Pango.EllipsizeMode.End) {
                        model.SetValue (iter, 5, width);
                    }
                    return false;
                });
            }
        }

        public CellRendererText NameRenderer { get { return name_renderer; } }
        public CellRendererText ValueRenderer { get { return value_renderer; } }

        private bool RowSeparatorFunc (TreeModel model, TreeIter iter)
        {
            return (bool)model.GetValue (iter, 2);
        }

        private int GetValueWidth ()
        {
            var column = view.GetColumn (1);
            var column_width = column.Width - 2 * value_renderer.Xpad -
                (int)view.StyleGetProperty ("horizontal-separator") -
                2 * (int)view.StyleGetProperty ("focus-line-width");
            return (int) column_width;
        }

        private void HandleQueryTooltip(object o, QueryTooltipArgs args)
        {
            TreePath path;
            TreeIter iter;
            if (view.GetPathAtPos (args.X, args.Y, out path) && view.Model.GetIter (out iter, path)) {
                string text = (string)view.Model.GetValue (iter, 1);
                if (!String.IsNullOrEmpty (text)) {
                    using (var layout = new Pango.Layout (view.PangoContext)) {
                        layout.FontDescription = value_renderer.FontDesc;
                        layout.SetText (text);
                        layout.Attributes = new Pango.AttrList ();
                        layout.Attributes.Insert (new Pango.AttrScale (value_renderer.Scale));

                        int width, height;
                        layout.GetPixelSize (out width, out height);

                        var column_width = GetValueWidth ();
                        if (width > column_width) {
                            var column = view.GetColumn (1);
                            args.Tooltip.Text = text;
                            view.SetTooltipCell (args.Tooltip, path, column, value_renderer);
                            args.RetVal = true;
                        }
                    }
                }
            }

            // Work around ref counting SIGSEGV, see http://bugzilla.gnome.org/show_bug.cgi?id=478519#c9
            if (args.Tooltip != null) {
                args.Tooltip.Dispose ();
            }
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
            model = null;
            CreateModel ();

            TagLib.File file = track.GetTaglibFile ();

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
                if (file.Properties.BitsPerSample > 0) {
                    AddItem (Catalog.GetString ("Bits Per Sample:"), String.Format ("{0} bits",
                        file.Properties.BitsPerSample));
                }

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
                file.Dispose ();
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

        private void CreateModel ()
        {
            if (model == null) {
                model = new ListStore (typeof (string), typeof (string), typeof (bool), typeof (Pango.EllipsizeMode), typeof(bool), typeof (int));
                view.Model = model;
            }
        }

        public void AddItem (string name, object value)
        {
            AddItem (name, value, false);
        }

        public void AddItem (string name, object value, bool wrapText)
        {
            CreateModel ();
            if (name != null && value != null) {
                model.AppendValues (name, value.ToString (), false, wrapText ? Pango.EllipsizeMode.None : Pango.EllipsizeMode.End, true, wrapText ? 10 : -1);
            }
        }

        public void AddSeparator ()
        {
            CreateModel ();
            model.AppendValues (String.Empty, String.Empty, true, Pango.EllipsizeMode.End, false, -1);
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
