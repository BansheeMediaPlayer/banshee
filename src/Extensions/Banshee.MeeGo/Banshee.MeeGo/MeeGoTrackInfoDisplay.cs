//
// MeeGoTrackDisplay.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright 2009 Novell, Inc.
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
using System.Text.RegularExpressions;

using Banshee.ServiceStack;

namespace Banshee.MeeGo
{
    public class MeeGoTrackInfoDisplay : Banshee.Gui.Widgets.ClassicTrackInfoDisplay
    {
        private Regex line_three_split;

        protected MeeGoTrackInfoDisplay (IntPtr raw) : base (raw)
        {
        }

        public MeeGoTrackInfoDisplay () : base ()
        {
        }

        protected override string GetSecondLineText (Banshee.Collection.TrackInfo track)
        {
            if (line_three_split == null) {
                line_three_split = new Regex (@"size=""small"">", RegexOptions.Compiled);
            }

            var text = base.GetSecondLineText (track);
            var splits = line_three_split.Split (text);
            string new_text = String.Empty;

            for (int i = 0; i < splits.Length; i++) {
                if (i == 2) {
                    new_text += "\n";
                }

                new_text += splits[i];

                if (i < 2) {
                    new_text += @"size=""small"">";
                }
            }

            return new_text;
        }
    }
}
