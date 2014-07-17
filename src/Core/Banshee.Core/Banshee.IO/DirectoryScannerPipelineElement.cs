//
// DirectoryScannerPipelineElement.cs
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
using System.IO;

using Hyena;
using Hyena.Collections;

namespace Banshee.IO
{
    public class DirectoryScannerPipelineElement : QueuePipelineElement<string>
    {
        public bool Debug { get; set; }

        public DirectoryScannerPipelineElement ()
        {
            SkipHiddenChildren = true;
        }

        protected override string ProcessItem (string item)
        {
            try {
                if (Debug) Log.InformationFormat ("DirectoryScanner element processing {0}", item);
                ScanForFiles (item, false);
            }
            finally {
                visited_dirs.Clear ();
            }
            return null;
        }

        public bool SkipHiddenChildren { get; set; }

        private readonly HashSet<string> visited_dirs = new HashSet<string> ();
        private void ScanForFiles (string source, bool skip_hidden)
        {
            CheckForCanceled ();

            bool is_regular_file = false;
            bool is_directory = false;

            SafeUri source_uri = new SafeUri (source);

            try {
                is_regular_file = Banshee.IO.File.Exists (source_uri);
                is_directory = !is_regular_file && Banshee.IO.Directory.Exists (source);
                if (Debug) Log.InformationFormat ("  > item {0} is reg file? {1} is dir? {2}", source, is_regular_file, is_directory);
            } catch (Exception e) {
                if (Debug) Log.Error ("Testing if path is file or dir", e);
                if (Debug) Log.InformationFormat ("  > item {0} is reg file? {1} is dir? {2}", source, is_regular_file, is_directory);
                return;
            }

            if (is_regular_file) {
                try {
                    if (!skip_hidden || !Path.GetFileName (source).StartsWith (".")) {
                        EnqueueDownstream (source);
                    }
                } catch (System.ArgumentException) {
                    // If there are illegal characters in path
                }
            } else if (is_directory) {
                try {
                    if (!skip_hidden || !Path.GetFileName (source).StartsWith (".")) {
                        visited_dirs.Add (source);
                        try {
                            foreach (string file in Banshee.IO.Directory.GetFiles (source)) {
                                ScanForFiles (file, SkipHiddenChildren);
                            }

                            foreach (string directory in Banshee.IO.Directory.GetDirectories (source)) {
                                if (!visited_dirs.Contains (directory)) {
                                    ScanForFiles (directory, SkipHiddenChildren);
                                }
                            }
                        } catch (Exception e) {
                            Log.Error (e);
                        }
                    }
                } catch (ArgumentException ex) {
                    Log.Warning (String.Format ("There are illegal characters in the path '{0}'", source), ex);
                }
            }
        }
    }
}
