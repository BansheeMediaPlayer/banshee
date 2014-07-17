//
// ProblemModel.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright 2010 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;

using Mono.Unix;

using Mono.Addins;

using Gtk;

using Hyena;
using Hyena.Data;
using Hyena.Data.Sqlite;

using Banshee.ServiceStack;
using Banshee.Preferences;
using Banshee.Configuration;

using Banshee.Gui;

namespace Banshee.Fixup
{
    public class ProblemModel : BaseListModel<Problem>
    {
        private static ProblemModel instance;
        public static ProblemModel Instance {
            get { return instance; }
        }

        private int count;
        private int selected_count;
        private List<Solver> solvers = new List<Solver> ();
        private Dictionary<string, Solver> solvers_hash = new Dictionary<string, Solver> ();

        public ProblemModel ()
        {
            instance = this;
            Selection = new Hyena.Collections.Selection ();

            Problem.Initialize ();

            AddSolvers ();
        }

        public void Dispose ()
        {
            foreach (var solver in Solvers) {
                solver.Dispose ();
            }
        }

        public IEnumerable<Solver> Solvers { get { return solvers; } }

        private Solver solver;
        public Solver Solver {
            get { return solver; }
            set {
                if (value == solver)
                    return;

                solver = value;
                Clear ();
                Refresh ();
                Log.DebugFormat ("Metadata Solver changed to {0}", solver.Name);
            }
        }

        private void AddSolvers ()
        {
            Solver solver = null;
            foreach (TypeExtensionNode node in AddinManager.GetExtensionNodes ("/Banshee/MetadataFix/Solver")) {
                try {
                    solver = (Solver) node.CreateInstance (typeof (Solver));
                } catch (Exception e) {
                    Log.Error (e);
                    continue;
                }

                Add (solver);
            }
        }

        private void Add (Solver solver)
        {
            solvers.Add (solver);
            solvers_hash[solver.Id] = solver;
        }

        public Solver GetSolverFor (Problem fixable)
        {
            if (solvers_hash.ContainsKey (fixable.ProblemType)) {
                return solvers_hash[fixable.ProblemType];
            }
            return null;
        }

        public void Refresh ()
        {
            Solver.FindProblems ();
            Reload ();
        }

        public void Fix ()
        {
            Solver.FixSelected ();
            ServiceManager.SourceManager.MusicLibrary.NotifyTracksChanged ();
            Refresh ();
        }

#region BaseListModel implementation

        public override void Clear ()
        {
            count = 0;
            selected_count = 0;
            ServiceManager.DbConnection.Execute ("DELETE FROM MetadataProblems");
            Problem.Provider.ClearCache ();
            OnCleared ();
        }

        public void ToggleSelection ()
        {
            foreach (var range in Selection.Ranges) {
                ServiceManager.DbConnection.Execute (
                    @"UPDATE MetadataProblems SET Selected = NOT(Selected) WHERE ProblemID IN
                        (SELECT ProblemID FROM MetadataProblems ORDER BY ProblemID LIMIT ?, ?)",
                    range.Start, range.End - range.Start + 1);
            }
            Reload ();
        }

        public override void Reload ()
        {
            count = ServiceManager.DbConnection.Query<int> ("SELECT count(*) FROM MetadataProblems");
            selected_count = ServiceManager.DbConnection.Query<int> ("SELECT count(*) FROM MetadataProblems WHERE Selected = 1");
            OnReloaded ();
        }

        public override Problem this[int index] {
            get {
                lock (this) {
                    foreach (Problem fixable in Problem.Provider.FetchRange (index, 1)) {
                        return fixable;
                    }

                    return null;
                }
            }
        }

        public override int Count {
            get { return count; }
        }

#endregion

        public int SelectedCount {
            get { return selected_count; }
        }

    }
}
