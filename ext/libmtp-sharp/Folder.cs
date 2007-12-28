/***************************************************************************
 *  Folder.cs
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
	public class Folder
	{
		private MtpDevice device;
		private uint folderId;
		private uint parentId;
		private string name;

		internal uint FolderId
		{
			get { return folderId; }
		}
		
		public string Name
		{
			get { return name; }
		}
		
		internal uint ParentId
		{
			get { return parentId; }
		}
		
		internal Folder (uint folderId, uint parentId, string name, MtpDevice device)
		{
			this.device = device;
			this.folderId = folderId;
			this.parentId = parentId;
			this.name = name;
		}
		
		internal Folder (FolderStruct folder, MtpDevice device)
					: this (folder.folder_id, folder.parent_id, folder.name, device)
		{

		}
		
		public Folder AddChild(string name)
		{
			if (string.IsNullOrEmpty(name))
			    throw new ArgumentNullException("name");
			    
			// First create the folder on the device and check for error
			uint id = FolderManagement.CreateFolder (device.handle, name, FolderId);
			
			FolderStruct f = new FolderStruct();
			f.folder_id = id;
			f.parent_id = FolderId;
			f.name = name;
			
			return new Folder(f, device);
		}
		
		public List<Folder> GetChildren ()
		{
			using (FolderHandle handle = FolderManagement.GetFolderList(device.handle))
			{
				// Find the pointer to the folderstruct representing this folder
				IntPtr ptr = handle.DangerousGetHandle();
				ptr = FolderManagement.Find (ptr, folderId);
				
				FolderStruct f = (FolderStruct)Marshal.PtrToStructure(ptr, typeof(FolderStruct));
				
				ptr = f.child;
				List<Folder> folders = new List<Folder>();				
				while (ptr != IntPtr.Zero)
				{
					FolderStruct folder = (FolderStruct)Marshal.PtrToStructure(ptr, typeof(FolderStruct));
					folders.Add(new Folder(folder, device));
					ptr = folder.sibling;
				}
				
				return folders;
			}
		}
		
		public void Remove()
		{
			LibMtp.DeleteObject(device.handle, FolderId);
		}
		
		internal static List<Folder> GetRootFolders (MtpDevice device)
		{
			List<Folder> folders = new List<Folder>();
			using (FolderHandle handle = FolderManagement.GetFolderList (device.handle))
			{
				for (IntPtr ptr = handle.DangerousGetHandle(); ptr != IntPtr.Zero;)
				{
					FolderStruct folder = (FolderStruct)Marshal.PtrToStructure(ptr, typeof(FolderStruct));
					folders.Add(new Folder (folder, device));
					ptr = folder.sibling;
				}
				return folders;
			}
		}
	}
}
