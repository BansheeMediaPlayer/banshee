/* -*- Mode: csharp; tab-width: 4; c-basic-offset: 4; indent-tabs-mode: t -*- */
/***************************************************************************
 *  Client.cs
 *
 *  Copyright (C) 2005 Novell
 *  Written by Aaron Bockover (aaron@aaronbock.net)
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
using System.Text;
using System.Runtime.InteropServices;

namespace MusicBrainz
{
    public class Client : IDisposable
    {
        private HandleRef handle;
        private string currentServer = "http://www.musicbrainz.org";
        
        private static readonly int MAX_STRING_LEN = 8192;
        //private static readonly int CDINDEX_ID_LEN = 28;
        //private static readonly int ID_LEN = 36;

        public Client()
        {
            handle = new HandleRef(this, mb_New());
            UseUtf8 = true;
            
            AutoSetProxy();
        }

        public bool AutoSetProxy()
        {
            try {
                Uri proxyAddress = System.Net.GlobalProxySelection.Select.GetProxy(new System.Uri(currentServer));
                if(proxyAddress != null) {
                    Console.WriteLine("Setting MusicBrainz proxy to {0}:{1}", proxyAddress.Host, proxyAddress.Port);
                    return this.SetProxy (proxyAddress.Host, (short)proxyAddress.Port);
                }
            } catch {
            }
            
            return false;
        }

        public void Dispose()
        {
            mb_Delete(handle);
        }
        
        public bool SetServer(string serverAddr, short serverPort)
        {
            if(mb_SetServer(handle, ToUtf8(serverAddr), serverPort) != 0) {
                currentServer = serverAddr + serverPort.ToString ();
                return true;
            }
            
            return false;
        }
        
        public bool SetProxy(string serverAddr, short serverPort)
        {
            return mb_SetProxy(handle, ToUtf8(serverAddr), serverPort) != 0;
        }
        
        public bool Authenticate(string username, string password)
        {
            return mb_Authenticate(handle, ToUtf8(username), ToUtf8(password)) != 0;
        }
        
        public bool Query(string rdfObject)
        {
            return mb_Query(handle, ToUtf8(rdfObject)) != 0;
        }
        
       /* public bool Query(string rdfObject, params string [] args)
        {
            return Query(rdfObject, args);
        } */
        
        public bool Query(string rdfObject, params string [] args)
        {
            IntPtr [] ptrs = ToUtf8PtrArray(args);
            
            try {
                return mb_QueryWithArgs(handle, ToUtf8(rdfObject), ptrs) != 0;
            } finally {
                FreeUtf8PtrArray(ptrs);
            }
        }
        
        public bool Select(string rdfObject)
        {
            return Select(rdfObject, 0);
        }
        
        public bool Select(string rdfObject, int index)
        {
            return mb_Select1(handle, ToUtf8(rdfObject), index) != 0;
        }
        
        public int GetResultInt(string rdfObject)
        {
            return mb_GetResultInt(handle, ToUtf8(rdfObject));
        }
        
        public int GetResultInt(string rdfObject, int index)
        {
            return mb_GetResultInt1(handle, ToUtf8(rdfObject), index);
        }
        
        public string GetResultData(string rdfObject)
        {
            return GetResultData(rdfObject, 0);
        }
        
        public string GetResultData(string rdfObject, int index)
        {
            byte [] buffer = new byte[MAX_STRING_LEN];
            int result = mb_GetResultData1(handle, ToUtf8(rdfObject), buffer, 
                buffer.Length, index);
                
            return result == 0 ? null : FromUtf8(buffer);
        }
        
        public string GetIDFromUrl(string url)
        {
            byte [] buffer = new byte[64];
            mb_GetIDFromURL(handle, ToUtf8(url), buffer, buffer.Length);
            string id = FromUtf8(buffer);
            int offset = id.IndexOf('#') + 1;
            return (offset >= 0) ? id.Substring(offset) : id;
        }
        
        public string GetID(string id)
        {
            if(id == null) {
                return String.Empty;
            }

            try {
                return System.Text.RegularExpressions.Regex.IsMatch(id, @"^[A-Za-z0-9\-]+$") ?
                    id : GetIDFromUrl(id);
            } catch {
                return String.Empty;
            }
        }
        
        public ClientVersion Version
        {
            get {
                int major, minor, revision;
                mb_GetVersion(handle, out major, out minor, out revision);
                return new ClientVersion(major, minor, revision);
            }
        }
        
        public bool Debug 
        { 
            set { 
                mb_SetDebug(handle, value ? 1 : 0); 
            } 
        }

        public string Device 
        { 
            set { 
                if(mb_SetDevice(handle, ToUtf8(value)) == 0) {
                    throw new ApplicationException("Could not set device");
                }
            }
        }
        
        private bool UseUtf8 
        { 
            set { 
                mb_UseUTF8(handle, value ? 1 : 0);
            }
        }
        
        public int Depth
        {
            set {
                mb_SetDepth(handle, value);
            }
        }
        
        public int MaxItems
        {
            set {
                mb_SetMaxItems(handle, value);
            }
        }
            
        private static readonly Encoding utf8encoding = new UTF8Encoding();
         
        private byte [] ToUtf8(string str)
        {
            if(str == null) {
                return null;
            }
            
            int length = utf8encoding.GetByteCount(str);
            byte [] result = new byte[length + 1];
            utf8encoding.GetBytes(str, 0, str.Length, result, 0);
            result[length] = 0;
            
            return result;
        }
        
        private IntPtr ToUtf8Ptr(string str)
        {
            if(str == null) {
                return IntPtr.Zero;
            }
            
            byte [] bytes = ToUtf8(str);
            IntPtr ptr = Marshal.AllocCoTaskMem(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            return ptr;
        }
        
        private void FreeUtf8Ptr(IntPtr ptr)
        {
            if(ptr == IntPtr.Zero) {
                return;
            }
               
            Marshal.FreeCoTaskMem(ptr);
        }
        
        private IntPtr [] ToUtf8PtrArray(string [] strs)
        {
            if(strs == null || strs.Length == 0) {
                return null;
            }
            
            IntPtr [] ptrs = new IntPtr[strs.Length];
            for(int i = 0; i < ptrs.Length; i++) {
                ptrs[i] = ToUtf8Ptr(strs[i]);
            }
            
            return ptrs;
        }
        
        private void FreeUtf8PtrArray(IntPtr [] ptrs)
        {
            for(int i = 0; i < ptrs.Length; i++) {
                FreeUtf8Ptr(ptrs[i]);
            }
            
            ptrs = null;
        }
        
        private string FromUtf8(byte [] buffer)
        {
            if(buffer == null || buffer.Length == 0) {
                return null;
            }
            
            int pos;
            for(pos = 0; pos < buffer.Length && buffer[pos] != 0; pos++);
            return utf8encoding.GetString(buffer, 0, pos); 
        }
        
        [DllImport("libmusicbrainz")]
        private static extern IntPtr mb_New();
        
        [DllImport("libmusicbrainz")]
        private static extern void mb_Delete(HandleRef o);
        
        [DllImport("libmusicbrainz")]
        private static extern void mb_GetVersion(HandleRef o, out int major, 
            out int minor, out int rev);
            
        [DllImport("libmusicbrainz")]
        private static extern int mb_SetServer(HandleRef o, byte [] serverAddr, 
            short serverPort);
            
        [DllImport("libmusicbrainz")]
        private static extern int mb_SetDebug(HandleRef o, int debug);
            
        [DllImport("libmusicbrainz")]
        private static extern int mb_SetProxy(HandleRef o, byte [] serverAddr, 
            short serverPort);
            
        [DllImport("libmusicbrainz")]
        private static extern int mb_Authenticate(HandleRef o, byte [] userName, 
            byte [] password);
        
        [DllImport("libmusicbrainz")]
        private static extern int mb_SetDevice(HandleRef o, byte [] device);
        
        [DllImport("libmusicbrainz")]
        private static extern int mb_UseUTF8(HandleRef o, int useUTF8);
        
        [DllImport("libmusicbrainz")]
        private static extern void mb_SetDepth(HandleRef o, int depth);
        
        [DllImport("libmusicbrainz")]
        private static extern void mb_SetMaxItems(HandleRef o, int maxItems);  
        
        [DllImport("libmusicbrainz")]
        private static extern int mb_Query(HandleRef o, byte [] rdfObject); 
        
        [DllImport("libmusicbrainz")]
        private static extern int mb_QueryWithArgs(HandleRef o, byte [] rdfObject, IntPtr [] args);
        
        [DllImport("libmusicbrainz")]
        private static extern int mb_Select1(HandleRef o, byte [] rdfObject, int index);
        
        [DllImport("libmusicbrainz")]
        private static extern int mb_GetResultInt(HandleRef o, byte [] rdfObject);
        
        [DllImport("libmusicbrainz")]
        private static extern int mb_GetResultInt1(HandleRef o, byte [] rdfObject, int index);
        
        [DllImport("libmusicbrainz")]
        private static extern int mb_GetResultData1(HandleRef o, byte [] rdfObject,
            byte [] data, int dataLen,  int index);
            
        [DllImport("libmusicbrainz")]
        private static extern void mb_GetIDFromURL(HandleRef o, byte [] url, 
            byte [] id, int idLen);
    }
}
