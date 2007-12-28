/***************************************************************************
 *  LibMtpManagement.cs
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
	internal class LibMtp
	{
		public static void ClearErrorStack(MtpDeviceHandle handle)
		{
			LIBMTP_Clear_Errorstack (handle);
		}
		
		public static void DeleteObject(MtpDeviceHandle handle, uint object_id)
		{
			if (LibMtp.LIBMTP_Delete_Object(handle, object_id) != 0)
			{
				LibMtpException.CheckErrorStack(handle);
				throw new LibMtpException(ErrorCode.LIBMTP_ERROR_GENERAL, "Could not delete the track");
			}
		}

		public static void GetBatteryLevel (MtpDeviceHandle handle, out ushort maxLevel, out ushort currentLevel)
		{
			int result = LIBMTP_Get_Batterylevel (handle, out maxLevel, out currentLevel);
			if (result != 0)
				throw new LibMtpException (ErrorCode.LIBMTP_ERROR_GENERAL, "Could not retrieve battery stats");
		}
		
		public static void GetConnectedDevices (out IntPtr list)
		{
			Error.CheckError (LIBMTP_Get_Connected_Devices (out list));
		}
		public static IntPtr GetErrorStack (MtpDeviceHandle handle)
		{
			return LIBMTP_Get_Errorstack(handle);
		}
		
		public static string GetFriendlyName(MtpDeviceHandle handle)
		{
			IntPtr ptr = LibMtp.LIBMTP_Get_Friendlyname(handle);
			if (ptr == IntPtr.Zero)
				return "Mtp Device";
			
			int i = 0;
			while (Marshal.ReadByte (ptr, i) != (byte) 0) ++i;
			byte[] s_buf = new byte [i];
			Marshal.Copy (ptr, s_buf, 0, s_buf.Length);
			string s = System.Text.Encoding.UTF8.GetString (s_buf);
			Marshal.FreeCoTaskMem(ptr);
			return s;
		}
		
		public static void GetStorage (MtpDeviceHandle handle, int sortMode)
		{
			LIBMTP_Get_Storage (handle, sortMode);
		}
		
		public static void Init ()
		{
			LIBMTP_Init ();
		}
		public static void ReleaseDevice (IntPtr handle)
		{
			LIBMTP_Release_Device(handle);
		}
		
		[DllImport("libmtp.dll")]
		private static extern void LIBMTP_Init ();
			
		// Clears out the error stack and frees any allocated memory.
		[DllImport("libmtp.dll")]
		private static extern void LIBMTP_Clear_Errorstack (MtpDeviceHandle handle);
		
		[DllImport("libmtp.dll")]
		private static extern int LIBMTP_Delete_Object (MtpDeviceHandle handle, uint object_id); 	
			
		// Gets the first connected device:
		[DllImport("libmtp.dll")]
		private static extern IntPtr LIBMTP_Get_First_Device (); // LIBMTP_mtpdevice_t *
		
		// Gets the storage information
		[DllImportAttribute("libmtp.dll")]
		private static extern int LIBMTP_Get_Storage (MtpDeviceHandle handle, int sortMode);
		
		// Formats the supplied storage device attached to the device
		[DllImportAttribute("libmtp.dll")]
		private static extern int LIBMTP_Format_Storage (MtpDeviceHandle handle, ref DeviceStorage storage);
		
		// Counts the devices in the list
		[DllImportAttribute("libmtp.dll")]
		private static extern uint LIBMTP_Number_Devices_In_List (MtpDeviceHandle handle);
		
		[DllImportAttribute("libmtp.dll")]
		private static extern ErrorCode LIBMTP_Get_Connected_Devices (out IntPtr list); //LIBMTP_mtpdevice_t **
		
		// Deallocates the memory for the device
		[DllImportAttribute("libmtp.dll")]
		private static extern void LIBMTP_Release_Device (IntPtr device);

		[DllImportAttribute("libmtp.dll")]
		private static extern int LIBMTP_Reset_Device (MtpDeviceHandle handle);
		
		[DllImport("libmtp.dll")]
		private static extern int LIBMTP_Get_Batterylevel (MtpDeviceHandle handle, out ushort maxLevel, out ushort currentLevel);
		
		[DllImportAttribute("libmtp.dll")]
		private static extern IntPtr LIBMTP_Get_Modelname (MtpDeviceHandle handle); // char *
		
		[DllImportAttribute("libmtp.dll")]
		private static extern IntPtr LIBMTP_Get_Serialnumber (MtpDeviceHandle handle); // char *
		
		[DllImportAttribute("libmtp.dll")]
		private static extern IntPtr LIBMTP_Get_Deviceversion (MtpDeviceHandle handle); // char *
		
		[DllImportAttribute("libmtp.dll")]
		private static extern IntPtr LIBMTP_Get_Friendlyname (MtpDeviceHandle handle); // char *
		
		//[DllImport("libmtp.dll")]
		//private static extern int LIBMTP_Set_Friendlyname (MtpDeviceHandle handle, char const *const)
		
		[DllImportAttribute("libmtp.dll")]
		private static extern IntPtr LIBMTP_Get_Errorstack (MtpDeviceHandle handle); // LIBMTP_error_t *
		
		[DllImportAttribute("libmtp.dll")]
		private static extern int LIBMTP_Get_Supported_Filetypes (MtpDeviceHandle handle, ref IntPtr types, ref ushort count); // uint16_t **const
		
		
		// void LIBMTP_Release_Device_List (LIBMTP_mtpdevice_t *)
				

		// int LIBMTP_Detect_Descriptor (uint16_t *, uint16_t *); 
		/*
				void 	LIBMTP_Dump_Device_Info (LIBMTP_mtpdevice_t *)
				
				char * 	LIBMTP_Get_Syncpartner (LIBMTP_mtpdevice_t *)
				int 	LIBMTP_Set_Syncpartner (LIBMTP_mtpdevice_t *, char const *const)
				int 	LIBMTP_Get_Secure_Time (LIBMTP_mtpdevice_t *, char **const)
				int 	LIBMTP_Get_Device_Certificate (LIBMTP_mtpdevice_t *, char **const)
		 */
	}
}
