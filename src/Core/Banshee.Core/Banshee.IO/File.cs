//
// File.cs
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

using Hyena;

namespace Banshee.IO
{
    public static class File
    {
    	public static void Delete (SafeUri uri)
    	{
    	    Provider.File.Delete (uri);
    	}

    	public static bool Exists (SafeUri uri)
    	{
    	    return Provider.File.Exists (uri);
    	}

    	public static void Move (SafeUri from, SafeUri to)
    	{
    	    Provider.File.Move (from, to);
    	}

    	public static void Copy (SafeUri from, SafeUri to, bool overwrite)
    	{
    	    Provider.File.Copy (from, to, overwrite);
    	}

    	public static long GetSize (SafeUri uri)
    	{
    	    return Provider.File.GetSize (uri);
    	}

        public static long GetModifiedTime (SafeUri uri)
        {
            return Provider.File.GetModifiedTime (uri);
        }

    	public static System.IO.Stream OpenRead (SafeUri uri)
    	{
    	    return Provider.File.OpenRead (uri);
    	}

    	public static System.IO.Stream OpenWrite (SafeUri uri, bool overwrite)
    	{
    	    return Provider.File.OpenWrite (uri, overwrite);
    	}

        public static IFileSystemEntryPermissions GetPermissions (SafeUri uri)
        {
            return Provider.CreateDemuxVfs (uri);
        }
    }
}
