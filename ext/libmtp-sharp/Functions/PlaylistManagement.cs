/***************************************************************************
 *  PlaylistManagement.cs
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
	internal class PlaylistManagement
	{
		[DllImport("libmtp.dll")]
		private static extern IntPtr LIBMTP_new_playlist_t (); // LIBMTP_playlist_t*
		[DllImport("libmtp.dll")]
		private static extern void LIBMTP_destroy_playlist_t (ref Playlist playlist);
		[DllImport("libmtp.dll")]
		private static extern IntPtr LIBMTP_Get_Playlist_List (MtpDeviceHandle handle); // LIBMTP_playlist_t*
		[DllImport("libmtp.dll")]
		private static extern IntPtr LIBMTP_Get_Playlist (MtpDeviceHandle handle, uint playlistId); // LIBMTP_playlist_t*
		[DllImport("libmtp.dll")]
		private static extern int LIBMTP_Create_New_Playlist (MtpDeviceHandle handle, ref Playlist metadata, uint parentHandle);
		[DllImport("libmtp.dll")]
		private static extern int LIBMTP_Update_Playlist (MtpDeviceHandle handle, ref Playlist playlist);
	}
}
