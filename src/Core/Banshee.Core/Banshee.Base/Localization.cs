/***************************************************************************
 *  Localization.cs
 *
 *  Copyright (C) 2007 Novell, Inc.
 *  Written by Aaron Bockover <abockover@novell.com>
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
using System.Xml;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Banshee.Base
{
    public static class Localization
    {        
        private static string [] default_languages = { "C" };
        private static string [] instance_languages = null;
        private static string [] instance_xml_languages = null;
        
        public static string [] Languages {
            get {
                if(instance_languages == null) {
                    instance_languages = GetLanguageNames();
                }
                
                return instance_languages;
            }
        }
        
        public static string [] XmlLanguages {
            get { 
                if(instance_xml_languages != null) {
                    return instance_xml_languages;
                }
                
                List<string> xml_langs = new List<string>();
                bool prepend_empty = false;
                bool first = true;
                
                foreach(string lang in Languages) {
                    string xml_lang = lang.Replace("_", "-");
                    if(first && (xml_lang == "C" || xml_lang.StartsWith("en"))) {
                        prepend_empty = true;
                    }
                    
                    first = false;
                    xml_langs.Add(xml_lang);
                }
                
                if(prepend_empty) {
                    xml_langs.Insert(0, "");
                } else {
                    xml_langs.Add("");
                }
                
                instance_xml_languages = xml_langs.ToArray();
                
                return instance_xml_languages;
            }
        }
        
        public static XmlNode SelectSingleNode(XmlNode parent, string query)
        {
            XmlNodeList list = parent.SelectNodes(query);
            XmlNode result = null;
            
            foreach(string language in XmlLanguages) {
                foreach(XmlNode child in list) {
                    XmlNode lang_child = child.SelectSingleNode(String.Format("self::node()[lang('{0}')]", language));
                    if(lang_child != null) {
                        result = lang_child;
                        break;
                    }
                }
                
                if(result != null) {
                    break;
                }
            }
            
            return result;
        }
        
        public static List<XmlNode> SelectNodes(XmlNode parent, string query)
        {
            XmlNodeList list = parent.SelectNodes(query);
            List<XmlNode> result = new List<XmlNode>();
            
            foreach(string language in XmlLanguages) {
                foreach(XmlNode child in list) {
                    XmlNode lang_child = child.SelectSingleNode(String.Format("self::node()[lang('{0}')]", language));
                    if(lang_child != null) {
                        result.Add(lang_child);
                    }
                }
            }
            
            return result;
        }
        
        [DllImport("libglib-2.0.so.0")]
        private static extern IntPtr g_get_language_names();

        private static string [] GetLanguageNames()
        {
            IntPtr languages = g_get_language_names();
            if(languages == IntPtr.Zero) {
                return default_languages;
            }

            try {
                string [] marshalled_languages = Mono.Unix.UnixMarshal.PtrToStringArray(languages);
                if(marshalled_languages == null || marshalled_languages.Length == 0) {
                    return default_languages;
                }

                return marshalled_languages;
            } catch {
                return default_languages;
            }
        }
    }
}
