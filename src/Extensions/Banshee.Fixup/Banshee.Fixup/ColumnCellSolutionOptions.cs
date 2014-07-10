//
// ColumnCellSolutionOptions.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2009-2010 Novell, Inc.
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
using System.Linq;

using Mono.Unix;

using Hyena;
using Hyena.Gui.Canvas;
using Hyena.Data.Gui;
using System.Text;
using System.Collections.Generic;

namespace Banshee.Fixup
{
    public class ColumnCellSolutionOptions : ColumnCellText, IInteractiveCell
    {
        bool measure;
        List<int> solution_value_widths = new List<int> ();

        public ColumnCellSolutionOptions () : base (null, true)
        {
            UseMarkup = true;
        }

        public override void Render (CellContext context, double cellWidth, double cellHeight)
        {
            base.Render (context, cellWidth, cellHeight);

            if (measure) {
                solution_value_widths.Clear ();
                var sb = new StringBuilder ();
                int w, h;
                foreach (var str in SolutionOptions) {
                    sb.Append (str);
                    context.Layout.SetMarkup (sb.ToString ());
                    context.Layout.GetPixelSize (out w, out h);
                    solution_value_widths.Add (w);
                    sb.Append (solution_joiner);
                }
            }
        }

        private IEnumerable<string> SolutionOptions {
            get {
                var problem = (Problem)BoundObject;
                return problem.SolutionOptions
                               .Select (o => o == problem.SolutionValue
                                   ? String.Format ("<b>{0}</b>", GLib.Markup.EscapeText (o))
                                   : GLib.Markup.EscapeText (o));
            }
        }

        const string solution_joiner = ", ";

        protected override string GetText (object obj)
        {
            return SolutionOptions.Join (solution_joiner);
        }

        private int GetSolutionValueFor (int x)
        {
            if (solution_value_widths.Count == 0)
                return -1;

            int cur_x = 0;
            for (int i = 0; i < solution_value_widths.Count; i++) {
                cur_x = solution_value_widths[i];
                if (x < cur_x)
                    return i;
            }
            return -1;
        }

        public override bool ButtonEvent (Point cursor, bool pressed, uint button)
        {
            if (button == 1 && !pressed) {
                int sol = GetSolutionValueFor ((int)cursor.X);
                if (sol != -1) {
                    var problem = ((Problem)BoundObject);
                    problem.SolutionValue = problem.SolutionOptions.Skip (sol).First ();
                    Problem.Provider.Save (problem);
                    ProblemModel.Instance.Reload ();
                    return true;
                }
            }
            return false;
        }

        public override bool CursorMotionEvent (Point cursor)
        {
            measure = true;
            return false;
        }

        public override bool CursorLeaveEvent ()
        {
            solution_value_widths.Clear ();
            measure = false;
            return false;
        }
    }
}
