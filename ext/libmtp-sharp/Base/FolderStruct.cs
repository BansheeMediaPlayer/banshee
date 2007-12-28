/***************************************************************************
 *  FolderStruct.cs
 *
 *  Copyright (C) 2006-2007 Alan McGovern
 *  Authors:
 *  Alan McGovern (alan.mcgovern@gmail.com)
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
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace libmtpsharp
{
	internal class FolderHandle : SafeHandle
	{
		private FolderHandle()
			: base(IntPtr.Zero, true)
		{
			
		}
		
		internal FolderHandle(IntPtr ptr)
			: this(ptr, true)
		{
			
		}
		
		internal FolderHandle(IntPtr ptr, bool ownsHandle)
			: base (IntPtr.Zero, ownsHandle)
		{
			SetHandle (ptr);
		}
		
		public override bool IsInvalid
		{
			get { return handle == IntPtr.Zero; }
		}

		protected override bool ReleaseHandle ()
		{
			FolderManagement.DestroyFolder (handle);
			return true;
		}
	}
	
	[StructLayout(LayoutKind.Sequential)]
	internal struct FolderStruct
	{
		public uint folder_id;
		public uint parent_id;
		[MarshalAs(UnmanagedType.LPStr)] public string name;
		public IntPtr sibling; // LIBMTP_folder_t*
		public IntPtr child;   // LIBMTP_folder_t*
		/*
		public object NextSibling
		{
			get 
			{
				if(sibling == IntPtr.Zero)
					return null;
				return (FolderStruct)Marshal.PtrToStructure(sibling, typeof(Folder));
			}
		}
		
		public object NextChild
		{
			get 
			{
				if(child == IntPtr.Zero)
					return null;
				return (FolderStruct)Marshal.PtrToStructure(child, typeof(Folder));
			}
		}
		
		public Folder? Sibling
		{
			get
			{
				if (sibling == IntPtr.Zero)
					return null;
				return (Folder)Marshal.PtrToStructure(sibling, typeof(Folder));
			}
		}
		
		public Folder? Child
		{
			get
			{
				if (child == IntPtr.Zero)
					return null;
				return (Folder)Marshal.PtrToStructure(child, typeof(Folder));
			}
		}*/

		/*public IEnumerable<Folder> Children()
		{
			Folder? current = Child;
			while(current.HasValue)
			{
				yield return current.Value;
				current = current.Value.Child;
			}
		}*/
		
		/*public IEnumerable<Folder> Siblings()
		{
			Folder? current = Sibling;
			while(current.HasValue)
			{
				yield return current.Value;
				current = current.Value.Sibling;
			}
		}*/
	}
}
