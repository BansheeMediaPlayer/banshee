/***************************************************************************
 *  SystemIO.cs
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

using Banshee.Base;

namespace Banshee.IO.SystemIO
{
    public class IOConfig : IIOConfig
    {
        public string Name           { get { return "systemio";        } }
        public Type FileBackend      { get { return typeof(File);      } }
        public Type DirectoryBackend { get { return typeof(Directory); } }
        public Type DemuxVfsBackend  { get { return typeof(DemuxVfs);  } }
        
        public string DetectMimeType(SafeUri uri)
        {
            return Banshee.IO.GnomeVfs.IOConfig._DetectMimeType(uri);
        }
    }

    public class File : IFile
    {
        public void Delete(SafeUri uri)
        {
            System.IO.File.Delete(uri.LocalPath);
        }

        public bool Exists(SafeUri uri)
        {
            return System.IO.File.Exists(uri.LocalPath);
        }

        public void Move(SafeUri from, SafeUri to)
        {
            System.IO.File.Move(from.LocalPath, to.LocalPath);
        }
        
        public System.IO.Stream OpenRead(SafeUri uri)
        {
            return System.IO.File.OpenRead(uri.LocalPath);
        }
        
        public System.IO.Stream OpenWrite(SafeUri uri, bool overwrite)
        {
            return overwrite 
                ? System.IO.File.Open(uri.LocalPath, FileMode.Create, FileAccess.ReadWrite)
                : System.IO.File.OpenWrite(uri.LocalPath);
        }
    }

    public class Directory : IDirectory
    {
        public void Create(string directory)
        {
            System.IO.Directory.CreateDirectory(directory);
        }
        
        public void Delete(string directory)
        {
            Delete(directory, false);
        }
        
        public void Delete(string directory, bool recursive)
        {
            System.IO.Directory.Delete(directory, recursive);
        }
        
        public bool Exists(string directory)
        {
            return System.IO.Directory.Exists(directory);
        }
        
        public bool CanReadWrite(string directory)
        {
            // FIXME: This will need porting to System.IO - no clue how to do this
            try {
                return new Mono.Unix.UnixDirectoryInfo(directory).CanAccess(
                    Mono.Unix.Native.AccessModes.R_OK | Mono.Unix.Native.AccessModes.W_OK);
            } catch {
                return false;
            }
        }
        
        public IEnumerable GetFiles(string directory)
        {
            return System.IO.Directory.GetFiles(directory);
        }
        
        public IEnumerable GetDirectories(string directory)
        {
            return System.IO.Directory.GetDirectories(directory);
        }

        public void Move(SafeUri from, SafeUri to)
        {
            System.IO.Directory.Move(from.LocalPath, to.LocalPath);
        }
    }
    
    public class DemuxVfs: IDemuxVfs
    {   
        private FileInfo file_info;
        
        public DemuxVfs(string path)
        {
            file_info = new FileInfo(path);
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
            get { return file_info.Open(FileMode.Open, FileAccess.ReadWrite); }
        }
   
        public bool IsReadable {
            get {
                try {
                    ReadStream.Close();
                    return true;
                } catch { 
                    return false;
                }
            }
        }

        public bool IsWritable {
            get {
                try {
                    WriteStream.Close();
                    return true;
                } catch { 
                    return false;
                }
            }
        }
    }
}
