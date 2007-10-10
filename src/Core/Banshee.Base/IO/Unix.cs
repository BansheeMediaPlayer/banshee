/***************************************************************************
 *  Unix.cs
 *
 *  Copyright (C) 2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@abock.org>
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */

using System;
using System.IO;
using System.Collections;

using Mono.Unix;
using Mono.Unix.Native;

using Banshee.Base;

namespace Banshee.IO.Unix
{
    public class IOConfig : IIOConfig
    {
        public string Name           { get { return "unix";            } }
        public Type FileBackend      { get { return typeof(File);      } }
        public Type DirectoryBackend { get { return typeof(Directory); } }
        public Type DemuxVfsBackend  { get { return typeof(DemuxVfs);  } }
        
        public string DetectMimeType(SafeUri uri)
        {
            return Banshee.IO.GnomeVfs.IOConfig._DetectMimeType(uri);
        }
    }

    internal struct FileStat
    {
        private Stat buf;
        private bool is_directory;
        private bool is_regular_file;
        
        internal FileStat(string path)
        {
            buf = new Stat();
            is_directory = is_regular_file = Syscall.stat(path, out buf) == 0;
            is_regular_file &= (buf.st_mode & FilePermissions.S_IFREG) == FilePermissions.S_IFREG;
            is_directory &= (buf.st_mode & FilePermissions.S_IFDIR) == FilePermissions.S_IFDIR;
            // FIXME: workaround for http://bugzilla.ximian.com/show_bug.cgi?id=76966
            is_directory &= ! ((buf.st_mode & FilePermissions.S_IFSOCK) == FilePermissions.S_IFSOCK);
        }
        
        internal bool IsDirectory {
            get { return is_directory; }
        }
        
        internal bool IsRegularFile {
            get { return is_regular_file; }
        }
    }

    public class File : IFile
    {
        public void Delete(SafeUri uri)
        {
            UnixFileInfo info = new UnixFileInfo(uri.LocalPath);
            info.Delete();
        }

        public bool Exists(SafeUri uri)
        {
            FileStat stat = new FileStat(uri.LocalPath);
            return stat.IsRegularFile && !stat.IsDirectory;
        }
        
        public void Move(SafeUri from, SafeUri to)
        {
            Mono.Unix.Native.Stdlib.rename(from.LocalPath, to.LocalPath);
        }
        
        public Stream OpenRead(SafeUri uri)
        {
            return new UnixFileInfo(uri.LocalPath).OpenRead();
        }
        
        public Stream OpenWrite(SafeUri uri, bool overwrite)
        {
            return overwrite 
                ? new UnixFileInfo(uri.LocalPath).Open(FileMode.Create, FileAccess.ReadWrite, FilePermissions.DEFFILEMODE)
                : new UnixFileInfo(uri.LocalPath).OpenWrite();
        }
    }

    public class Directory : IDirectory
    {
        
        [System.Runtime.InteropServices.DllImport("libglib-2.0.so")]
        private static extern int g_mkdir_with_parents(IntPtr path, int mode);
    
        public void Create(string directory)
        {
            IntPtr path_ptr = IntPtr.Zero;
            
            try {
                path_ptr = GLib.Marshaller.StringToPtrGStrdup(directory);
                if(path_ptr == IntPtr.Zero) {
                    throw new Exception("Failed to allocate native directory string");
                }
            
                if(g_mkdir_with_parents(path_ptr, 493 /*0755 - C# doesn't do octal literals*/) == -1) {
                    Mono.Unix.UnixMarshal.ThrowExceptionForLastError();
                }
            } catch(EntryPointNotFoundException) {
                Console.WriteLine("g_mkdir_with_parents could not be found");
                System.IO.Directory.CreateDirectory(directory);
            } finally {
                if(!path_ptr.Equals(IntPtr.Zero)) {
                    GLib.Marshaller.Free(path_ptr);
                }
            }
        }
        
        public void Delete(string directory)
        {
            Delete(directory, false);
        }
        
        public void Delete(string directory, bool recursive)
        {
            UnixDirectoryInfo unix_dir = new UnixDirectoryInfo(directory);
            unix_dir.Delete(recursive);
        }
        
        public bool Exists(string directory)
        {
            try {
                FileStat stat = new FileStat(directory);
                return stat.IsDirectory;
            } catch {
                return false;
            }
        }
        
        public IEnumerable GetFiles(string directory)
        {
            UnixDirectoryInfo unix_dir = new UnixDirectoryInfo(directory);
            foreach(UnixFileSystemInfo entry in unix_dir.GetFileSystemEntries()) {
                if(!entry.IsDirectory && entry.IsRegularFile && !entry.IsSocket && entry.Exists) {
                    yield return entry.FullName;
                }
            }
        }
        
        public IEnumerable GetDirectories(string directory)
        {
            UnixDirectoryInfo unix_dir = new UnixDirectoryInfo(directory);
            foreach(UnixFileSystemInfo entry in unix_dir.GetFileSystemEntries()) {
                if(entry.IsDirectory && entry.Exists && !entry.IsSocket) {
                    yield return entry.FullName;
                }
            }
        }
        
        public void Move(SafeUri from, SafeUri to)
        {
            Mono.Unix.Native.Stdlib.rename(from.LocalPath, to.LocalPath);
        }
    }
    
    public class DemuxVfs: IDemuxVfs
    {   
        private UnixFileInfo file_info;
        
        public DemuxVfs(string path)
        {
            file_info = new UnixFileInfo(path);
        }
        
        public void CloseStream(Stream stream)
        {
            stream.Close();
        }
        
        public string Name { 
            get { return file_info.FullName; }
        }
        
        public Stream ReadStream {
            get { return file_info.Open(FileMode.Open, FileAccess.Read); }
        }
        
        public Stream WriteStream {
            get { return file_info.OpenWrite(); }
        }
   
        public bool IsReadable {
            get { return file_info.CanAccess(AccessModes.R_OK); }
        }
   
        public bool IsWritable {
            get { return file_info.CanAccess(AccessModes.W_OK); }
        }
    }
}
