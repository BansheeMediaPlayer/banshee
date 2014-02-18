//
// Directory.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2006-2008 Novell, Inc.
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

using Hyena;

namespace Banshee.IO
{
    public static class Directory
    {
        public static bool Exists (string directory)
        {
            if (String.IsNullOrEmpty (directory)) {
                throw new ArgumentNullException ("directory");
            }

            return Provider.Directory.Exists (Provider.GetPath (directory));
        }

        public static void Create (string directory)
        {
            if (String.IsNullOrEmpty (directory)) {
                throw new ArgumentNullException ("directory");
            }

            Provider.Directory.Create (Provider.GetPath (directory));
        }

        public static void Move (SafeUri from, SafeUri to)
        {
            if (from == null) {
                throw new ArgumentNullException ("from");
            }
            if (to == null) {
                throw new ArgumentNullException ("to");
            }

            Provider.Directory.Move (from, to);
        }

        public static void Delete (string directory)
        {
            if (String.IsNullOrEmpty (directory)) {
                throw new ArgumentNullException ("directory");
            }

            Provider.Directory.Delete (Provider.GetPath (directory));
        }

        public static void Delete (string directory, bool recursive)
        {
            if (String.IsNullOrEmpty (directory)) {
                throw new ArgumentNullException ("directory");
            }

            Provider.Directory.Delete (Provider.GetPath (directory), recursive);
        }

        public static IEnumerable<SafeUri> GetFiles (string directory)
        {
            if (String.IsNullOrEmpty (directory)) {
                throw new ArgumentNullException ("directory");
            }

            return Provider.Directory.GetFiles (Provider.GetPath (directory));
        }

        public static IEnumerable<SafeUri> GetDirectories (string directory)
        {
            if (String.IsNullOrEmpty (directory)) {
                throw new ArgumentNullException ("directory");
            }

            return Provider.Directory.GetDirectories (Provider.GetPath (directory));
        }
    }
}
