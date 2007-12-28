/***************************************************************************
 *  TrackManagement.cs
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
	internal class TrackManagement
	{
		internal static void DestroyTrack (IntPtr track)
		{
			LIBMTP_destroy_track_t (track);
		}
		internal static void GetTrack (MtpDeviceHandle handle, uint trackId, string destPath, ProgressFunction callback, IntPtr data)
		{
			if (LIBMTP_Get_Track_To_File (handle, trackId, destPath, callback, data) != 0)
			{
				LibMtpException.CheckErrorStack(handle);
				throw new LibMtpException (ErrorCode.LIBMTP_ERROR_GENERAL, "Could not download track from the device");
			}
		}
		internal static IntPtr GetTrackListing (MtpDeviceHandle handle, ProgressFunction function, IntPtr data)
		{
			return LIBMTP_Get_Tracklisting_With_Callback(handle, function, data);
		}
		internal static void SendTrack (MtpDeviceHandle handle, string path, ref TrackStruct metadata, ProgressFunction callback, IntPtr data, uint parent)
		{
			Console.WriteLine("Sending: {0}", metadata.item_id);
			if (LIBMTP_Send_Track_From_File (handle, path, ref metadata, callback, data, parent) != 0)
			{
				LibMtpException.CheckErrorStack(handle);
				throw new LibMtpException (ErrorCode.LIBMTP_ERROR_GENERAL, "Could not upload the track");
			}
			Console.WriteLine("Got: {0}", metadata.item_id);
			Console.ReadLine();
		}
		internal static void UpdateTrackMetadata(MtpDeviceHandle handle, ref TrackStruct metadata)
		{
			if (LIBMTP_Update_Track_Metadata (handle, ref metadata) != 0)
				throw new LibMtpException(ErrorCode.LIBMTP_ERROR_GENERAL);
		}
		
		[DllImport("libmtp.dll")]
		private static extern IntPtr LIBMTP_new_track_t (); // LIBMTP_track_t *
		[DllImport("libmtp.dll")]
		private static extern void LIBMTP_destroy_track_t (IntPtr track); // LIBMTP_track_t *
		//[DllImport("libmtp.dll")]
		//private static extern IntPtr LIBMTP_Get_Tracklisting (MtpDeviceHandle handle); //LIBMTP_track_t *
		[DllImport("libmtp.dll")]
		private static extern IntPtr LIBMTP_Get_Tracklisting_With_Callback (MtpDeviceHandle handle, ProgressFunction callback, IntPtr data); // LIBMTP_track_t *
		[DllImport("libmtp.dll")]
		private static extern IntPtr LIBMTP_Get_Trackmetadata (MtpDeviceHandle handle, uint trackId); // LIBMTP_track_t *
		[DllImport("libmtp.dll")]
		private static extern int LIBMTP_Get_Track_To_File (MtpDeviceHandle handle, uint trackId, string path, ProgressFunction callback, IntPtr data);
		[DllImport("libmtp.dll")]
		private static extern int LIBMTP_Send_Track_From_File (MtpDeviceHandle handle, string path, ref TrackStruct track, ProgressFunction callback, IntPtr data, uint parentHandle);
		[DllImport("libmtp.dll")]
	    private static extern int LIBMTP_Update_Track_Metadata (MtpDeviceHandle handle, ref TrackStruct metadata);
		[DllImport("libmtp.dll")]
	    private static extern int LIBMTP_Track_Exists (MtpDeviceHandle handle, uint trackId);
		//int 	LIBMTP_Get_Track_To_File_Descriptor (MtpDeviceHandle handle, uint trackId, int const, LIBMTP_progressfunc_t const, void const *const)
		//int 	LIBMTP_Send_Track_From_File_Descriptor (MtpDeviceHandle handle, int const, LIBMTP_track_t *const, LIBMTP_progressfunc_t const, void const *const, uint32_t const)
	}
}
