// 
// XmlUtils.cs
//  
// Author:
//       Mike Urbanski <michael.c.urbanski@gmail.com>
// 
// Copyright (c) 2009 Michael C. Urbanski
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Xml;

namespace Migo2.Utils
{
    public static class XmlUtils
    {
        public static string GetXmlNodeText (XmlNode node, string tag)
        {
            return GetXmlNodeText (node, tag, null);            
        }

        public static string GetXmlNodeText (XmlNode node, string tag, XmlNamespaceManager mgr)
        {
            XmlNode n = node.SelectSingleNode (tag, mgr);
            return (n == null) ? null : n.InnerText.Trim ();
        }

        public static int GetInt32 (XmlNode node, string tag)
        {
            return GetInt32 (node, tag, null);            
        }

        public static int GetInt32 (XmlNode node, string tag, XmlNamespaceManager mgr)
        {
            int ret = 0;
            string result = GetXmlNodeText (node, tag, mgr);

            if (!String.IsNullOrEmpty (result)) {
                Int32.TryParse (result, out ret);
            }
                    
            return ret;              
        }

        public static long GetInt64 (XmlNode node, string tag)
        {
            return GetInt64 (node, tag, null);            
        }
        
        public static long GetInt64 (XmlNode node, string tag, XmlNamespaceManager mgr)
        {
            long ret = 0;
            string result = GetXmlNodeText (node, tag, mgr);

            if (!String.IsNullOrEmpty (result)) {
                Int64.TryParse (result, out ret);
            }
                    
            return ret;              
        }

        public static DateTime GetRfc822DateTime (XmlNode node, string tag)
        {
            return GetRfc822DateTime (node, tag, null);            
        }

        public static DateTime GetRfc822DateTime (XmlNode node, string tag, XmlNamespaceManager mgr)
        {
            DateTime ret;
            string result = GetXmlNodeText (node, tag, mgr);

            if (!String.IsNullOrEmpty (result)) {
                if (Rfc822DateTime.TryParse (result, out ret)) {
                    return ret;
                }

                if (DateTime.TryParse (result, out ret)) {
                    return ret;
                }
            }
                    
            return DateTime.MinValue;              
        }
    }
}
