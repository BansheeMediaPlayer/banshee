//
// Problem.cs
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
using System.Linq;

using Hyena.Data.Sqlite;

using Banshee.ServiceStack;

namespace Banshee.Fixup
{
    public class Problem : CacheableItem<Problem>, IEquatable<Problem>
    {
        private static CacheableSqliteModelProvider<Problem> provider;
        public static CacheableSqliteModelProvider<Problem> Provider {
            get {
                return provider ?? (provider =
                    new CacheableSqliteModelProvider<Problem> (ServiceManager.DbConnection, "MetadataProblems", false));
            }
        }

        [DatabaseColumn ("ProblemID", Constraints = DatabaseColumnConstraints.PrimaryKey)]
        public long Id { get; private set; }

        public override long DbId {
            get { return Id; }
            protected set { Id = value; }
        }

        [DatabaseColumn ("ProblemType")]
        public string ProblemType { get; private set; }

        [DatabaseColumn]
        public bool Selected { get; set; }

        public bool SavedSelected {
            get { return Selected; }
            set {
                Selected = value;
                Provider.Save (this);
            }
        }

        [DatabaseColumn]
        public string SolutionValue { get; set; }

        [DatabaseColumn ("SolutionOptions")]
        private string options_field;

        [DatabaseColumn ("ObjectIds")]
        internal string object_ids_field;

        [DatabaseColumn]
        public int ObjectCount { get; private set; }

        [DatabaseColumn ("TrackDetails")]
        public string TrackDetails { get; private set; }

        private int [] object_ids;
        public int [] ObjectIds {
            get {
                if (object_ids == null && object_ids_field != null) {
                    object_ids = object_ids_field.Split (',')
                                                 .Select (i => Int32.Parse (i))
                                                 .ToArray ();
                }
                return object_ids;
            }
        }

        private string [] options;
        public string [] SolutionOptions {
            get {
                if (options == null && options_field != null) {
                    options = options_field.Split (splitter, StringSplitOptions.None);
                }
                return options;
            }
        }

        public override bool Equals (object b)
        {
            return Equals (b as Problem);
        }

        public bool Equals (Problem b)
        {
            return b != null && b.Id == this.Id;
        }

        public override int GetHashCode ()
        {
            return ((int)Id ^ (int)(Id >> 32));
        }

        public override string ToString ()
        {
            return String.Format ("<Problem Id={0} Type={1} Selected={2} SolutionValue={3}>", Id, ProblemType, Selected, SolutionValue);
        }

        public static void Initialize ()
        {
            ServiceManager.DbConnection.Execute (@"DROP TABLE IF EXISTS MetadataProblems");
            if (!ServiceManager.DbConnection.TableExists ("MetadataProblems")) {
                ServiceManager.DbConnection.Execute (@"
                    CREATE TABLE MetadataProblems (
                        ProblemID   INTEGER PRIMARY KEY,
                        ProblemType TEXT NOT NULL,
                        TypeOrder   INTEGER NOT NULL,
                        Generation  INTEGER NOT NULL,
                        Selected    INTEGER DEFAULT 1,

                        SolutionValue       TEXT,
                        SolutionOptions     TEXT,
                        ObjectIds   TEXT,
                        ObjectCount INTEGER,
                        TrackDetails TEXT,

                        UNIQUE (ProblemType, Generation, ObjectIds) ON CONFLICT IGNORE
                    )"
                );
            }
        }

        private static string [] splitter = new string [] { ";;" };
    }
}
