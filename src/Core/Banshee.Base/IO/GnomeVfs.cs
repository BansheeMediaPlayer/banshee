/***************************************************************************
 *  GnomeVfs.cs
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
using System.Collections;

using Gnome.Vfs;

using Banshee.Base;

namespace Banshee.IO.GnomeVfs
{
    public class IOConfig : IIOConfig
    {
        public string Name           { get { return "gnomevfs";        } }
        public Type FileBackend      { get { return typeof(File);      } }
        public Type DirectoryBackend { get { return typeof(Directory); } }
        public Type DemuxVfsBackend  { get { return typeof(DemuxVfs);  } }
        
        public string DetectMimeType(SafeUri uri)
        {
            return _DetectMimeType(uri);
        }
        
        internal static string _DetectMimeType(SafeUri uri)
        {
            try {
                string mime = MimeType.GetMimeTypeForUri(uri.AbsoluteUri);
                if(mime != null && mime != "application/octet-stream") {
                    return FilterMimeType(mime);
                }
            } catch {
            }
            
            return null;
        }
        
        private static string FilterMimeType(string mime)
        {
            string [] parts = mime.Split(',');
            if(parts == null || parts.Length <= 0) {
                return mime.Trim();
            }
            
            return parts[0].Trim();
        }
    }

    public class File : IFile
    {
        public void Delete(SafeUri uri)
        {
            Gnome.Vfs.Unlink.FromUri(new Gnome.Vfs.Uri(uri.AbsoluteUri));
        }
        
        public bool Exists(SafeUri uri)
        {
            try {
                return Exists(new FileInfo(uri.AbsoluteUri));
            } catch {
                return false;
            }
        }
        
        public static bool Exists(FileInfo info)
        {
            return info != null && info.Size > 0 && 
                (info.Type & FileType.Regular) != 0 && 
                (info.Type & FileType.Directory) == 0;
        }
        
        public void Move(SafeUri from, SafeUri to)
        {
            Gnome.Vfs.Move.Uri(new Gnome.Vfs.Uri(from.AbsoluteUri), 
                               new Gnome.Vfs.Uri(to.AbsoluteUri), true);
        }
        
        public System.IO.Stream OpenRead(SafeUri uri)
        {
            return new VfsStream(uri.AbsoluteUri, System.IO.FileMode.Open);
        }
        
        public System.IO.Stream OpenWrite(SafeUri uri, bool overwrite)
        {
            // FIXME: This always overwrites, and I can't figure out how to 
            // not do it with GnomeVFS - this means tag writing is probably
            // completely broke with this backend
            
            return new VfsStream(uri.AbsoluteUri, System.IO.FileMode.Create);
        }
    }

    public class Directory : IDirectory
    {
        public void Create(string directory)
        {
            Gnome.Vfs.Directory.Create(directory, FilePermissions.UserAll);
        }
        
        public void Delete(string directory)
        {
            Delete(directory);
        }
        
        public void Delete(string directory, bool recursive)
        {
            System.IO.Directory.Delete(directory, recursive);
        }
        
        public bool Exists(string directory)
        {
            return Exists(new FileInfo(directory));
        }
        
        public bool Exists(FileInfo info)
        {
            return (info.Type & FileType.Directory) != 0;
        }
        
        public bool CanReadWrite(string directory)
        {
            // FIXME: Ugh
            return true;
        }
        
        public IEnumerable GetFiles(string directory)
        {
            foreach(FileInfo file in Gnome.Vfs.Directory.GetEntries(directory)) {
                if(Banshee.IO.GnomeVfs.File.Exists(file)) {
                    yield return System.IO.Path.Combine(directory, file.Name);
                }
            }
        }
        
        public IEnumerable GetDirectories(string directory)
        {
            foreach(FileInfo file in Gnome.Vfs.Directory.GetEntries(directory)) {
                if(Exists(file)) {
                    yield return System.IO.Path.Combine(directory, file.Name);
                }
            }
        }
        
        public void Move(SafeUri from, SafeUri to)
        {
            Gnome.Vfs.Move.Uri(new Gnome.Vfs.Uri(from.AbsoluteUri), 
                               new Gnome.Vfs.Uri(to.AbsoluteUri), true);
        }
    }
    
    public class DemuxVfs: IDemuxVfs
    {
        private FilePermissions permissions;
        private string name;
        
        public DemuxVfs(string path)
        {
            name = path;
            permissions = new FileInfo(name, FileInfoOptions.FollowLinks 
                | FileInfoOptions.GetAccessRights).Permissions;
        }
        
        public void CloseStream(System.IO.Stream stream)
        {
            stream.Close();
        }
        
        public string Name { 
            get { return name; }
        }

        public System.IO.Stream ReadStream {
            get { return new VfsStream(Name, System.IO.FileMode.Open); }
        }

        public System.IO.Stream WriteStream {
            get { return new VfsStream(Name, System.IO.FileMode.OpenOrCreate); }
        }

        public bool IsReadable {
            get { return (permissions | FilePermissions.AccessReadable) != 0; }
        }

        public bool IsWritable {
            get { return (permissions | FilePermissions.AccessWritable) != 0; }
        }
    }
}
