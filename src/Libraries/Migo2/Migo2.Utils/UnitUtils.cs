// 
// UnitUtils.cs
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

namespace Migo2.Utils 
{
    public static class UnitUtils
    {
        public static string ToString (long bytes)
        {
            if (bytes == -1) {
                return String.Empty;
            } else if (bytes < -1) {
                throw new ArgumentException ("Must be >= 0", "bytes");
            }

            int divisor = 1;
            string formatString = String.Empty;
            string unit = String.Empty;
    
            if (bytes >= 0 && bytes < 1000) {
                unit = "B";                
                formatString = "{0:F0} {1}";
            } else if (bytes > 1000 && bytes < 1000000) {
                // Not all those who wander are lost.
                
                // 1 kB == 1000 bytes.  1 KiB == 1024 bytes.
                // Do not, change this unless you also want to change the abbreviations!
                
                unit = "kB";    
                divisor = 1000;
                formatString = "{0:F0} {1}";
            } else if (bytes > 1000000 && bytes < 1000000000) {
                unit = "MB";    
                divisor = 1000000;
                formatString = "{0:F1} {1}";            
            } else if (bytes > 1000000000) {
                unit = "GB";    
                divisor = 1000000000;
                formatString = "{0:F2} {1}";             
            }
            
            return String.Format (formatString, ((double)bytes/divisor), unit);
        }
    }
}