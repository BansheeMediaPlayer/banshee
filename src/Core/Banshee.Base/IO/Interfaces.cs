/***************************************************************************
 *  Interfaces.cs
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

using Banshee.Base;

namespace Banshee.IO
{
    public interface IIOConfig
    {
        string Name { get; }
        Type FileBackend { get; }
        Type DirectoryBackend { get; }
        Type DemuxVfsBackend { get; }
        
        string DetectMimeType(SafeUri uri);
    }

    public interface IFile
    {
        void Delete(SafeUri uri);
        bool Exists(SafeUri uri);
        void Move(SafeUri from, SafeUri to);
        System.IO.Stream OpenRead(SafeUri uri);
        System.IO.Stream OpenWrite(SafeUri uri, bool overwrite);
    }
    
    public interface IDirectory
    {
        void Create(string directory);
        void Delete(string directory);
        void Delete(string directory, bool recursive);
        bool Exists(string directory);
        bool CanReadWrite(string directory);
        IEnumerable GetFiles(string directory);
        IEnumerable GetDirectories(string directory);
        void Move(SafeUri from, SafeUri to);
    }
    
    public interface IDemuxVfs : TagLib.File.IFileAbstraction
    {
    }
}
