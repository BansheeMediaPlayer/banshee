/***************************************************************************
 *  FolderManagement.cs
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
using System.Runtime.InteropServices;

namespace libmtpsharp
{
	internal class FolderManagement
	{
		public static uint CreateFolder (MtpDeviceHandle handle, string name, uint parentId)
		{
			uint result = LIBMTP_Create_Folder (handle, name, parentId);
			if (result == 0)
			{
				LibMtpException.CheckErrorStack(handle);
				throw new LibMtpException(ErrorCode.LIBMTP_ERROR_GENERAL, "Could not create folder on the device");
			}
			
			return result;
		}
		public static void DestroyFolder (IntPtr folder)
		{
			LIBMTP_destroy_folder_t (folder);
		}
		
		public static IntPtr Find (IntPtr folderList, uint folderId )
		{
			return LIBMTP_Find_Folder (folderList, folderId);
		}
		public static FolderHandle GetFolderList (MtpDeviceHandle handle)
		{
			IntPtr ptr = LIBMTP_Get_Folder_List (handle);
			return new FolderHandle(ptr);
		}
		
		[DllImport("libmtp.dll")]
		private static extern IntPtr LIBMTP_new_folder_t (); // LIBMTP_folder_t*
		[DllImport("libmtp.dll")]
		private static extern void LIBMTP_destroy_folder_t (IntPtr folder);
		[DllImport("libmtp.dll")]
		private static extern IntPtr LIBMTP_Get_Folder_List (MtpDeviceHandle handle); // LIBMTP_folder_t*
		[DllImport("libmtp.dll")]
		private static extern IntPtr LIBMTP_Find_Folder (IntPtr folderList, uint folderId); // LIBMTP_folder_t*
		[DllImport("libmtp.dll")]
		private static extern uint LIBMTP_Create_Folder (MtpDeviceHandle handle, string name, uint parentId);
	}
}
