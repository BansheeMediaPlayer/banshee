//
// Solver.cs
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
using System.Collections.Generic;

using Mono.Unix;

using Hyena.Data.Sqlite;

using Banshee.ServiceStack;
using Banshee.Configuration;

namespace Banshee.Fixup
{
    public abstract class Solver : IDisposable
    {
        private string id;

        /* Find the highest TrackNumber for albums where not all tracks have it set */
        //SELECT AlbumID, Max(TrackCount) as MaxTrackNum FROM CoreTracks GROUP BY AlbumID HAVING MaxTrackNum > 0 AND MaxTrackNum != Min(TrackCount);

        public Solver ()
        {
        }

        internal protected virtual Banshee.Preferences.Section PreferencesSection {
            get { return null; }
        }

        // Total hack to work make unit tests work
        internal static bool EnableUnitTests;

        public string Id {
            get { return id; }
            set {
                if (id != null) {
                    throw new InvalidOperationException ("Solver's Id is already set; can't change it");
                }

                id = value;
                if (!EnableUnitTests) {
                    Generation = DatabaseConfigurationClient.Client.Get<int> ("MetadataFixupGeneration", id, 0);
                }
            }
        }

        public string Name { get; set; }
        public string Description { get; set; }
        public int Generation { get; private set; }

        internal protected virtual bool HasTrackDetails {
            get { return false; }
        }

        public void FindProblems ()
        {
            // Bump the generation number
            Generation++;
            DatabaseConfigurationClient.Client.Set<int> ("MetadataFixupGeneration", Id, Generation);

            // Identify the new issues
            IdentifyCore ();

            // Unselect any problems that the user had previously unselected
            ServiceManager.DbConnection.Execute (
                @"UPDATE MetadataProblems SET Selected = 0 WHERE ProblemType = ? AND Generation = ? AND ObjectIds IN
                    (SELECT ObjectIds FROM MetadataProblems WHERE ProblemType = ? AND Generation = ? AND Selected = 0)",
                Id, Generation, Id, Generation - 1
            );

            // Delete the previous generation's issues
            ServiceManager.DbConnection.Execute (
                "DELETE FROM MetadataProblems WHERE ProblemType = ? AND Generation = ?",
                Id, Generation - 1
            );
        }

        public virtual void Dispose () {}

        public void FixSelected ()
        {
            Fix (Problem.Provider.FetchAllMatching ("Selected = 1"));
        }

        protected abstract void IdentifyCore ();
        public abstract void Fix (IEnumerable<Problem> problems);
    }

    public abstract class DuplicateSolver : Solver
    {
        private List<HyenaSqliteCommand> find_cmds = new List<HyenaSqliteCommand> ();

        public void AddFinder (string value_column, string id_column, string from, string condition, string group_by)
        {
            /* The val result SQL gives us the first/highest value (in descending
             * sort order), so Foo Fighters over foo fighters.  Except it ignore all caps
             * ASCII values, so given the values Foo, FOO, and foo, they sort as
             * FOO, Foo, and foo, but we ignore FOO and pick Foo.  But because sqlite's
             * lower/upper functions only work for ASCII, our check for whether the
             * value is all uppercase involves ensuring that it doesn't also appear to be
             * lower case (that is, it may have zero ASCII characters).
             *
             * TODO: replace with a custom SQLite function
             *
             */
            find_cmds.Add (new HyenaSqliteCommand (String.Format (@"
                    INSERT INTO MetadataProblems (ProblemType, TypeOrder, Generation, SolutionValue, SolutionOptions, ObjectIds, ObjectCount)
                    SELECT
                        '{0}', {1}, {2},
                        COALESCE (
                            NULLIF (
                                MIN(CASE (upper({3}) = {3} AND NOT lower({3}) = {3})
                                    WHEN 1 THEN '~~~'
                                    ELSE {3} END),
                                '~~~'),
                            {3}) as val,
                        substr(group_concat({3}, ';;'), 1),
                        substr(group_concat({4}, ','), 1),
                        count(*) as num
                    FROM {5}
                    WHERE {6}
                    GROUP BY {7} HAVING num > 1
                    ORDER BY {3}",
                Id, 1, "?", // ? is for the Generation variable, which changes
                value_column, id_column, from, condition ?? "1=1", group_by))
            );
        }

        protected override void IdentifyCore ()
        {
            // Prune artists and albums that are no longer used
            ServiceManager.DbConnection.Execute (@"
                DELETE FROM CoreAlbums WHERE AlbumID NOT IN (SELECT DISTINCT(AlbumID) FROM CoreTracks);
                DELETE FROM CoreArtists WHERE
                    ArtistID NOT IN (SELECT DISTINCT(ArtistID) FROM CoreTracks) AND
                    ArtistID NOT IN (SELECT DISTINCT(ArtistID) FROM CoreAlbums WHERE ArtistID IS NOT NULL);"
            );

            foreach (HyenaSqliteCommand cmd in find_cmds) {
                ServiceManager.DbConnection.Execute (cmd, Generation);
            }
        }

    }

    public static class FixupExtensions
    {
        public static string NormalizeConjunctions (this string input)
        {
            return input.Replace (" & ", " and ");
        }

        public static string RemovePrefixedArticles (this string input)
        {
            foreach (var prefix in article_prefixes) {
                if (input.StartsWith (prefix)) {
                    input = input.Substring (prefix.Length, input.Length - prefix.Length);
                }
            }
            return input;
        }

        public static string RemoveSuffixedArticles (this string input)
        {
            foreach (var suffix in article_suffixes) {
                if (input.EndsWith (suffix)) {
                    input = input.Substring (0, input.Length - suffix.Length);
                }
            }
            return input;
        }

        static string [] article_prefixes;
        static string [] article_suffixes;
        static FixupExtensions ()
        {
            // Translators: These are articles that might be prefixed or suffixed
            // on artist names or album titles.  You can add as many as you need,
            // separated by a pipe (|)
            var articles = (Catalog.GetString ("a|an|the") + "|a|an|the").Split ('|').Distinct ();

            // Translators: This is the format commonly used in your langauge for
            // suffixing an article, eg in English: ", The"
            var suffix_format = Catalog.GetString (", {0}");

            article_prefixes = articles.Select (a => a + " ")
                                       .ToArray ();

            article_suffixes = articles.SelectMany (a =>
                new string [] { String.Format (suffix_format, a), ", " +  a }
            ).Distinct ().ToArray ();
        }

    }

    /*public class CompilationSolver : Solver
    {
        private HyenaSqliteCommand find_cmd;

        public CompilationSolver ()
        {
            Id = "make-compilation";
            Name = Catalog.GetString ("Compilation Albums");
            ShortDescription = Catalog.GetString ("Find albums that should be marked as compilation albums");
            LongDescription = Catalog.GetString ("Find albums that should be marked as compilation albums but are not");
            Action = Catalog.GetString ("Mark as compilation");

            find_cmd = new HyenaSqliteCommand (String.Format (@"
                INSERT INTO MetadataProblems (ProblemType, TypeOrder, Generation, SolutionValue, Options, Summary, Count)
                SELECT
                    '{0}', {1}, {2},
                    a.Title, a.Title, a.Title, count(*) as numtracks
                FROM
                    CoreTracks t,
                    CoreAlbums a
                WHERE
                    t.PrimarySourceID = 1 AND
                    a.IsCompilation = 0 AND
                    t.AlbumID = a.AlbumID
                GROUP BY
                    a.Title
                HAVING
                    numtracks > 1 AND
                    t.TrackCount = {3} AND
                    a.Title != 'Unknown Album' AND
                    a.Title != 'title' AND
                    a.Title != 'no title' AND
                    a.Title != 'Album' AND
                    a.Title != 'Music' AND (
                            {5} > 1 AND {5} = {4} AND (
                            {3} = 0 OR ({3} >= {5}
                                AND {3} >= numtracks))
                        OR lower(a.Title) LIKE '%soundtrack%'
                        OR lower(a.Title) LIKE '%soundtrack%'
                    )",
                Id, Order, Generation,
                "max(t.TrackCount)", "count(distinct(t.artistid))", "count(distinct(t.albumid))"
            ));
        }

        protected override void IdentifyCore ()
        {
            ServiceManager.DbConnection.Execute (find_cmd);
        }

        public override void Fix (IEnumerable<Problem> problems)
        {
            Console.WriteLine ("Asked to fix compilations..");
        }
    }*/
}
