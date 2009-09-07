//
// VendorProductDeviceNode.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Globalization;

using Mono.Addins;

namespace Banshee.Hardware
{
    [NodeAttribute ("vendor-name", true)]
    [NodeAttribute ("product-name", true)]
    [NodeAttribute ("vendor-id", true)]
    [NodeAttribute ("product-id", true)]
    public class VendorProductDeviceNode : TypeExtensionNode
    {
        private short [] vendor_ids;
        private short [] product_ids;
        
        private string vendor_name;
        public string VendorName {
            get { return vendor_name; }
        }
        
        private string product_name;
        public string ProductName {
            get { return product_name; }
        }
        
        public bool Matches (short vendorId, short productId)
        {
            return Match (vendor_ids, vendorId) && Match (product_ids, productId);
        }
        
        protected override void Read (NodeElement elem)
        {
            base.Read (elem);
            
            vendor_name = elem.GetAttribute ("vendor-name");
            product_name = elem.GetAttribute ("product-name");
            vendor_ids = ParseIds (elem.GetAttribute ("vendor-id"));
            product_ids = ParseIds (elem.GetAttribute ("product-id"));
        }        
        
        private static bool Match (short [] ids, short match)
        {
            for (int i = 0; i < ids.Length; i++) {
                if (ids[i] == match) {
                    return true;
                }
            }
            return false;
        }
        
        private static short [] ParseIds (string value)
        {
            string [] split = value.Split (',');
            short [] ids = new short[split.Length];
            for (int i = 0; i < split.Length; i++) {
                ids[i] = ParseId (split[i]);
            }
            return ids;
        }
        
        private static short ParseId (string value)
        {
            // Parse as an integer, then typecast, to avoid overflow issues
            // with regards to the sign bit.
            int result = 0;
            
            if (value.StartsWith ("0x")) {
                Int32.TryParse (value.Substring (2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out result);
            } else {
                Int32.TryParse (value, NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
                    CultureInfo.InvariantCulture, out result);
            }
            
            return (short)result;
        }
    }
}
