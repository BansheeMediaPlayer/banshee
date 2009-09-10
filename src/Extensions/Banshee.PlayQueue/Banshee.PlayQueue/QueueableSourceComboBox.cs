// QueueableSourceComboBox.cs
//
// Authors:
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

using Banshee.Library;
using Banshee.ServiceStack;
using Banshee.Sources;
using Banshee.Sources.Gui;

namespace Banshee.PlayQueue
{
    public class QueueableSourceComboBox : ComboBox
    {
        private readonly TreeModelFilter filter;

        public QueueableSourceComboBox (string source_name)
        {
            SourceRowRenderer renderer = new SourceRowRenderer ();
            renderer.ParentWidget = this;
            PackStart (renderer, true);
            SetCellDataFunc (renderer, new CellLayoutDataFunc (SourceRowRenderer.CellDataHandler));

            var store = new SourceModel ();
            filter = new TreeModelFilter (store, null);
            filter.VisibleFunc = (model, iter) => IsQueueable (((SourceModel)model).GetSource (iter));
            Model = filter;

            store.Refresh ();

            SetActiveSource (source_name);
        }

        private void SetActiveSource (string name)
        {
            TreeIter first;
            if (filter.GetIterFirst (out first)) {
                TreeIter iter = FindSource (name, first);
                if (!iter.Equals (TreeIter.Zero)) {
                    SetActiveIter (iter);
                }
            }
        }

        private bool IsQueueable (Source source)
        {
            return source != null && (
                source is MusicLibrarySource || source.Parent is MusicLibrarySource ||
                source is VideoLibrarySource || source.Parent is VideoLibrarySource);
        }

        private TreeIter FindSource (string name, TreeIter iter)
        {
            do {
                var source = filter.GetValue (iter, 0) as ISource;
                if (source != null && source.Name == name) {
                    return iter;
                }

                TreeIter citer;
                if (filter.IterChildren (out citer, iter)) {
                    var yiter = FindSource (name, citer);
                    if (!yiter.Equals (TreeIter.Zero)) {
                        return yiter;
                    }
                }
            } while (filter.IterNext (ref iter));

            return TreeIter.Zero;
        }

        public ITrackModelSource Source {
            get {
                TreeIter iter;
                if (GetActiveIter (out iter)) {
                    return filter.GetValue(iter, 0) as ITrackModelSource;
                }
                return null;
            }
        }
    }
}
