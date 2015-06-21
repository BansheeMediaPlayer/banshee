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
using System.IO;
using System.Collections.Generic;
using Mono.Unix;

using Hyena;
using Banshee.Base;

namespace Banshee.IO.Unix
{
    public class Directory : IDirectory
    {
        [System.Runtime.InteropServices.DllImport ("libglib-2.0-0.dll", CallingConvention = System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private static extern int g_mkdir_with_parents (IntPtr path, int mode);

        public void Create (string directory)
        {
            IntPtr path_ptr = IntPtr.Zero;

            try {
                path_ptr = GLib.Marshaller.StringToPtrGStrdup (directory);
                if (path_ptr == IntPtr.Zero) {
                    throw new Exception ("Failed to allocate native directory string");
                }

                // 493 == 0755 - C# doesn't do octal literals
                if (g_mkdir_with_parents (path_ptr, 493) == -1) {
                    Mono.Unix.UnixMarshal.ThrowExceptionForLastError ();
                }
            } catch (EntryPointNotFoundException) {
                Log.Warning ("g_mkdir_with_parents could not be found, falling back to System.IO");
                System.IO.Directory.CreateDirectory (directory);
            } finally {
                if (!path_ptr.Equals (IntPtr.Zero)) {
                    GLib.Marshaller.Free (path_ptr);
                }
            }
        }

        public void Delete (string directory)
        {
            Delete (directory, false);
        }

        public void Delete (string directory, bool recursive)
        {
            UnixDirectoryInfo unix_dir = new UnixDirectoryInfo (directory);
            unix_dir.Delete (recursive);
        }

        public bool Exists (string directory)
        {
            try {
                FileStat stat = new FileStat (directory);
                return stat.IsDirectory;
            } catch {
                return false;
            }
        }

        public IEnumerable<SafeUri> GetFiles (string directory)
        {
            var unix_dir = TraverseSymlink(new UnixDirectoryInfo (directory)) as UnixDirectoryInfo;
            if (unix_dir == null) {
                yield break;
            }
            foreach (var entry in unix_dir.GetFileSystemEntries ()) {
                if (entry != null && !entry.IsDirectory && entry.IsRegularFile && !entry.IsSocket && entry.Exists) {
                    yield return new SafeUri (entry.FullName, false);
                }
            }
        }

        public IEnumerable<SafeUri> GetDirectories (string directory)
        {
            var unix_dir = new UnixDirectoryInfo (directory);
            foreach (var entry in unix_dir.GetFileSystemEntries ()) {
                var info = TraverseSymlink (entry);
                if (info != null && info.IsDirectory && info.Exists && !info.IsSocket) {
                    yield return new SafeUri (info.FullName, false);
                }
            }
        }

        private readonly HashSet<string> visited_symlinks = new HashSet<string> ();
        private UnixFileSystemInfo TraverseSymlink (UnixFileSystemInfo info)
        {
            lock (visited_symlinks) {
                visited_symlinks.Clear ();
                while (info.IsSymbolicLink) {
                    if (visited_symlinks.Contains (info.FullName)) {
                        return null;
                    }
                    visited_symlinks.Add (info.FullName);
                    var target = new UnixSymbolicLinkInfo (info.FullName).GetContents ();
                    if (info.FullName.StartsWith (target.FullName)) {
                        return null;
                    }
                    if (!target.Exists) {
                        return null;
                    }
                    info = target;
                }

                return info;
            }
        }

        public void Move (SafeUri from, SafeUri to)
        {
            Mono.Unix.Native.Stdlib.rename (from.LocalPath, to.LocalPath);
        }
    }
}
