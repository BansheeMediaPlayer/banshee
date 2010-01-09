/***************************************************************************
 *  FileSampleData.cs
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

namespace Mtp
{
    internal static class FileSample
    {
        [DllImport("libmtp.dll")]
        public static extern void LIBMTP_destroy_filesampledata_t (ref FileSampleData data); // LIBMTP_filesampledata_t *

        [DllImport("libmtp.dll")]
        public static extern int LIBMTP_Get_Representative_Sample_Format (MtpDeviceHandle handle, FileType type, IntPtr data_array);

        [DllImport("libmtp.dll")]
        public static extern int LIBMTP_Send_Representative_Sample (MtpDeviceHandle handle, uint id, ref FileSampleData sample);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FileSampleData
    {
        public uint width;
        public uint height;
        public uint duration;
        public FileType filetype;
        public ulong size;
        public IntPtr data;
    }
}
