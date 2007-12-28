/***************************************************************************
 *  MtpDeviceStruct.cs
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
using System.Collections.Generic;

namespace libmtpsharp
{
	internal class MtpDeviceHandle : SafeHandle
	{
		private MtpDeviceHandle()
			: base(IntPtr.Zero, true)
		{
			
		}
		
		internal MtpDeviceHandle(IntPtr ptr, bool ownsHandle)
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
			LibMtp.ReleaseDevice(handle);
			return true;
		}
	}
	
	internal struct MtpDeviceStruct
	{
		public byte object_bitsize;
		public IntPtr parameters;  // void*
		public IntPtr usbinfo;     // void*
		public IntPtr storage;     // LIBMTP_devicestorage_t*
		public IntPtr errorstack;  // LIBMTP_error_t*
		public byte maximum_battery_level;
		public uint default_music_folder;
		public uint default_playlist_folder;
		public uint default_picture_folder;
		public uint default_video_folder;
		public uint default_organizer_folder;
		public uint default_zencast_folder;
		public uint default_album_folder;
		public uint default_text_folder;
		public IntPtr cd; // void*
		public IntPtr next; // LIBMTP_mtpdevice_t*
	}
}
