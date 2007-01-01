/***************************************************************************
 *  StringUtil.cs
 *
 *  Copyright (C) 2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@abock.org>
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
using System.Globalization;
using System.Text.RegularExpressions;

namespace Banshee.Base
{    
    public static class StringUtil
    {
        private static CompareOptions compare_options = 
                CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace |
                CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth;

        public static int RelaxedIndexOf(string haystack, string needle)
        {
            return CultureInfo.CurrentCulture.CompareInfo.IndexOf(haystack, needle, compare_options);
        }
        
        public static int RelaxedCompare(string a, string b)
        {
            if(a == null && b == null) {
                return 0;
            } else if(a != null && b == null) {
                return 1;
            } else if(a == null && b != null) {
                return -1;
            }
            
            int a_offset = a.StartsWith("the ") ? 4 : 0;
            int b_offset = b.StartsWith("the ") ? 4 : 0;

            return CultureInfo.CurrentCulture.CompareInfo.Compare(a, a_offset, a.Length - a_offset, 
                b, b_offset, b.Length - b_offset, compare_options);
        }
        
        public static string CamelCaseToUnderCase(string s)
        {
            string undercase = String.Empty;
            string [] tokens = Regex.Split(s, "([A-Z]{1}[a-z]+)");
            
            for(int i = 0; i < tokens.Length; i++) {
                if(tokens[i] == String.Empty) {
                    continue;
                }

                undercase += tokens[i].ToLower();
                if(i < tokens.Length - 2) {
                    undercase += "_";
                }
            }
            
            return undercase;
        }
    }
}
