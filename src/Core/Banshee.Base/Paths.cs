
/***************************************************************************
 *  Paths.cs
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
using System.IO;
using Mono.Unix;
using Gtk;
 
namespace Banshee.Base
{
    public class Paths
    {
        public static string ApplicationData {
            get {
                return Environment.GetFolderPath(Environment.SpecialFolder.Personal)
                    + Path.DirectorySeparatorChar
                    + ".gnome2"
                    + Path.DirectorySeparatorChar
                    + "banshee"
                    + Path.DirectorySeparatorChar;
            }
        }
        
        public static string SystemApplicationData {
            get { return ConfigureDefines.SystemDataDir; }
        }
        
        public static string CoverArtDirectory {
            get {
                string path = Path.Combine(ApplicationData, "covers") 
                    + Path.DirectorySeparatorChar;
                    
                if(!Directory.Exists(path)) {
                    Directory.CreateDirectory(path);
                }
                
                return path;
            }
        }
        
        public static string SystemPluginDirectory {
            get {
                return Path.Combine(ConfigureDefines.InstallDir, "Banshee.Plugins") + Path.DirectorySeparatorChar;
            }
        }
        
        public static string UserPluginDirectory {
            get {
                return Path.Combine(ApplicationData, "plugins") + Path.DirectorySeparatorChar;
            }
        }
        
        public static string GetCoverArtPath(string artist_album_id)
        {
            return GetCoverArtPath(artist_album_id, ".jpg");
        }
        
        public static string GetCoverArtPath(string artist_album_id, string extension)
        {
            return CoverArtDirectory + artist_album_id + extension;
        }
        
        public static string DefaultLibraryPath {
            get {
                return Environment.GetFolderPath(Environment.SpecialFolder.Personal)
                    + Path.DirectorySeparatorChar  
                    + "Music"
                    + Path.DirectorySeparatorChar;
            }
        }
        
        public static string TempDir {
            get {
                string dir = Paths.ApplicationData 
                    + Path.DirectorySeparatorChar 
                    + "temp";
        
                if(File.Exists(dir))
                    File.Delete(dir);

                Directory.CreateDirectory(dir);
                return dir;
            }
        }
    }
}
