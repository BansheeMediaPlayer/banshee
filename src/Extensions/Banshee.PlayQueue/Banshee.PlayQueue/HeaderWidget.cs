//
// HeaderWidget.cs
//
// Author:
//   Alexander Kojevnikov <alexander@kojevnikov.com>
//
// Copyright (C) 2009 Alexander Kojevnikov
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

using Gtk;
using Mono.Unix;
using System;
using System.Collections.Generic;
using System.Linq;

using Banshee.PlaybackController;
using Banshee.Sources;

namespace Banshee.PlayQueue
{
    public class HeaderWidget : HBox
    {
        public event EventHandler<ModeChangedEventArgs> ModeChanged;
        public event EventHandler<SourceChangedEventArgs> SourceChanged;

        private readonly List<Widget> sensitive_widgets = new List<Widget> ();
        private readonly Dictionary<string, PlaybackShuffleMode> modes = new Dictionary<string, PlaybackShuffleMode> {
            { Catalog.GetString ("manually"), PlaybackShuffleMode.Linear },
            { Catalog.GetString ("by song"), PlaybackShuffleMode.Song },
            { Catalog.GetString ("by album"), PlaybackShuffleMode.Album },
            { Catalog.GetString ("by artist"), PlaybackShuffleMode.Artist },
            { Catalog.GetString ("by rating"), PlaybackShuffleMode.Rating },
            { Catalog.GetString ("by score"), PlaybackShuffleMode.Score },
        };

        public HeaderWidget (PlaybackShuffleMode mode, string source_name) : base ()
        {
            this.Spacing = 6;

            var fill_label = new Label (Catalog.GetString ("_Fill"));
            var mode_combo = new ComboBox (modes.Keys.OrderBy (s => modes[s]).ToArray ());
            fill_label.MnemonicWidget = mode_combo;
            mode_combo.Changed += delegate {
                var value = modes[mode_combo.ActiveText];
                foreach (var widget in sensitive_widgets) {
                    widget.Sensitive = value != PlaybackShuffleMode.Linear;
                }
                var handler = ModeChanged;
                if (handler != null) {
                    handler (this, new ModeChangedEventArgs (value));
                }
            };

            var from_label = new Label (Catalog.GetString ("f_rom"));
            sensitive_widgets.Add (from_label);
            var source_combo_box = new QueueableSourceComboBox (source_name);
            from_label.MnemonicWidget = source_combo_box;
            sensitive_widgets.Add (source_combo_box);
            source_combo_box.Changed += delegate {
                var handler = SourceChanged;
                if (handler != null) {
                    handler (this, new SourceChangedEventArgs (source_combo_box.Source));
                }
            };

            PackStart (fill_label, false, false, 0);
            PackStart (mode_combo, false, false, 0);
            PackStart (from_label, false, false, 0);
            PackStart (source_combo_box, false, false, 0);

            // Select the population mode.
            mode_combo.Model.Foreach (delegate (TreeModel model, TreePath path, TreeIter iter) {
                if (modes[(string)model.GetValue (iter, 0)] == mode) {
                    mode_combo.SetActiveIter (iter);
                    return true;
                }
                return false;
            });
        }
    }

    public sealed class ModeChangedEventArgs : EventArgs
    {
        private PlaybackShuffleMode value;

        public ModeChangedEventArgs (PlaybackShuffleMode value)
        {
            this.value = value;
        }

        public PlaybackShuffleMode Value {
            get { return this.value; }
        }
    }

    public sealed class SourceChangedEventArgs : EventArgs
    {
        private ITrackModelSource value;

        public SourceChangedEventArgs (ITrackModelSource value)
        {
            this.value = value;
        }

        public ITrackModelSource Value {
            get { return this.value; }
        }
    }
}
