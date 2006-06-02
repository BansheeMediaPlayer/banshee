/***************************************************************************
 *  Branding.cs
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
using System.IO;
using System.Reflection;

using Mono.Unix;

namespace Banshee.Base
{
    public static class Branding
    {
        private static ICustomBranding default_branding;
        private static ICustomBranding custom_branding;
        
        public static bool Initialize()
        {
            if(default_branding != null) {
                return true;
            }
            
            default_branding = new BansheeBranding();
            default_branding.Initialize();
            
            try {
                Assembly entry_assembly = Assembly.GetEntryAssembly();
                string branding_asm_path = Path.Combine(Path.GetDirectoryName(
                    entry_assembly.Location), "branding.dll");
                
                if(File.Exists(branding_asm_path)) {
                    Assembly branding_assembly = Assembly.LoadFrom(branding_asm_path);
                    if(branding_assembly != null) {
                        LoadCustomBranding(branding_assembly);
                    }
                }
            } catch(Exception e) {
                LogCore.Instance.PushDebug("Failed to load custom branding assembly", e.Message);
            }
            
            if(custom_branding == null) {
                custom_branding = default_branding;
            }
            
            return custom_branding.Initialize();
        }
        
        private static void LoadCustomBranding(Assembly assembly)
        {
            foreach(Type type in assembly.GetTypes()) {
                foreach(Type interface_type in type.GetInterfaces()) {
                    if(interface_type == typeof(ICustomBranding)) {
                        ICustomBranding branding = Activator.CreateInstance(type) as ICustomBranding;
                        if(branding != null) {
                            custom_branding = branding;
                        }
                        
                        break;
                    }
                }
            }
        }
        
        public static string ApplicationLongName {
            get { 
                string name = custom_branding.ApplicationLongName;
                return name == null ? default_branding.ApplicationLongName : name;
            }
        }
        
        public static string ApplicationName {
            get { 
                string name = custom_branding.ApplicationName;
                return name == null ? default_branding.ApplicationName : name;
            }
        }
        
        public static string ApplicationIconName {
            get {
                string name = custom_branding.ApplicationIconName;
                return name == null ? default_branding.ApplicationIconName : name;
            }
        }
        
        public static Gdk.Pixbuf AboutBoxLogo {
            get { 
                Gdk.Pixbuf logo = custom_branding.AboutBoxLogo;
                return logo == null ? default_branding.AboutBoxLogo : logo;
            }
        }
    }
    
    public interface ICustomBranding
    {
        bool Initialize();
        string ApplicationLongName { get; }
        string ApplicationName { get; }
        string ApplicationIconName { get; }
        Gdk.Pixbuf AboutBoxLogo { get; }
    }
    
    public class BansheeBranding : ICustomBranding
    {
        private Gdk.Pixbuf about_box_logo;
    
        public bool Initialize()
        {
            about_box_logo = new Gdk.Pixbuf(Assembly.GetEntryAssembly(), "banshee-logo.png");
            return true;
        }
        
        public string ApplicationLongName {
            get { return Catalog.GetString("Banshee Music Player"); }
        }
        
        public string ApplicationName {
            get { return Catalog.GetString("Banshee"); }
        }
        
        public string ApplicationIconName {
            get { return "music-player-banshee"; }
        }
        
        public Gdk.Pixbuf AboutBoxLogo {
            get { return about_box_logo; }
        }
    }
}
