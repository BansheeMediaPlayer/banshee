/***************************************************************************
 *  FileManagement.cs
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
	public delegate int ProgressFunction(ulong sent, ulong total, IntPtr data);
	
	internal class FileManagement
	{
		[DllImport("libmtp.dll")]
		private static extern IntPtr LIBMTP_new_file_t (); // LIBMTP_file_t *
		[DllImport("libmtp.dll")]
		private static extern void LIBMTP_destroy_file_t (ref File file); // LIBMTP_file_t *
		[DllImport("libmtp.dll")]
		private static extern string LIBMTP_Get_Filetype_Description (FileType type); // char const *
		[DllImport("libmtp.dll")]
		private static extern IntPtr LIBMTP_Get_Filelisting (MtpDeviceHandle handle); // LIBMTP_file_t *
		[DllImport("libmtp.dll")]
		private static extern IntPtr LIBMTP_Get_Filelisting_With_Callback (MtpDeviceHandle handle, ProgressFunction function, IntPtr data); // LIBMTP_file_t *
		[DllImport("libmtp.dll")]
		private static extern IntPtr LIBMTP_Get_Filemetadata (MtpDeviceHandle handle, uint fileid); // LIBMTP_file_t *
		[DllImport("libmtp.dll")]
		private static extern int LIBMTP_Get_File_To_File (MtpDeviceHandle handle, uint fileId, string path, ProgressFunction function, IntPtr data);
		[DllImport("libmtp.dll")]
		private static extern int LIBMTP_Send_File_From_File (MtpDeviceHandle handle, string path, ref File fileData, ProgressFunction function, IntPtr data, uint parentHandle);
		//int LIBMTP_Get_File_To_File_Descriptor (MtpDeviceHandle handle, uint32_t const, int const, ProgressFunction function, void const *const)
		//int 	LIBMTP_Send_File_From_File_Descriptor (MtpDeviceHandle handle, int const, LIBMTP_file_t *const, ProgressFunction function, void const *const, uint32_t const)
		//IntPtr LIBMTP_new_filesampledata_t (); // LIBMTP_filesampledata_t *
		//void LIBMTP_destroy_filesampledata_t (ref FileSampleData data); // LIBMTP_filesampledata_t *
		//int LIBMTP_Get_Representative_Sample_Format (MtpDeviceHandle handle, LIBMTP_filetype_t const, LIBMTP_filesampledata_t **)
		//int LIBMTP_Send_Representative_Sample (MtpDeviceHandle handle, uint32_t const, LIBMTP_filesampledata_t *)
		}
}
