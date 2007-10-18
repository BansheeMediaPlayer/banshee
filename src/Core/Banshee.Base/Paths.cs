/***************************************************************************
 *  Paths.cs
 *
 *  Copyright (C) 2005-2007 Novell, Inc.
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
using System.IO;
using Mono.Unix;
using Gtk;
 
namespace Banshee.Base
{
    public class Paths
    {
        // TODO: A corlib version of this method will be committed to Mono's Environment.GetFolderPath,
        // so many many Mono versions in the future we will be able to drop this private copy and
        // use the pure .NET API - but it's here to stay for now (compat!)
        private static string ReadXdgUserDir(string key, string fallback)
        {
            string home_dir = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            string config_dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            
            string env_path = Environment.GetEnvironmentVariable(key);
            if(!String.IsNullOrEmpty(env_path)) {
                return env_path;
            }

            string user_dirs_path = Path.Combine (config_dir, "user-dirs.dirs");

            if(!File.Exists(user_dirs_path)) {
                return Path.Combine(home_dir, fallback);
            }

            try {
                using(StreamReader reader = new StreamReader (user_dirs_path)) {
                    string line;
                    while((line = reader.ReadLine ()) != null) {
                        line = line.Trim();
                        int delim_index = line.IndexOf('=');
                        if(delim_index > 8 && line.Substring (0, delim_index) == key) {
                            string path = line.Substring(delim_index + 1).Trim('"');
                            bool relative = false;

                            if(path.StartsWith("$HOME/")) {
                                relative = true;
                                path = path.Substring(6);
                            } else if(path.StartsWith("~")) {
                                relative = true;
                                path = path.Substring(1);
                            } else if(!path.StartsWith("/")) {
                                relative = true;
                            }

                            return relative ? Path.Combine(home_dir, path) : path;
                        }
                    }
                }
            } catch(FileNotFoundException) {
            }
            
            return Path.Combine(home_dir, fallback);
        }
    
        public static string LegacyApplicationData {
            get {
                return Environment.GetFolderPath(Environment.SpecialFolder.Personal)
                    + Path.DirectorySeparatorChar
                    + ".gnome2"
                    + Path.DirectorySeparatorChar
                    + "banshee"
                    + Path.DirectorySeparatorChar;
            }
        }
        
        private static string application_data = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData), "banshee");
        
        public static string ApplicationData {
            get { return application_data; }
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
                string dir = ReadXdgUserDir("XDG_MUSIC_DIR", "Music");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }
        
        private static string dap_cache_path;
        public static string DapCachePath {
            get { 
                if(dap_cache_path != null) {
                    return dap_cache_path;
                }
            
                string path = Globals.Library.Location;
                
                if(Banshee.IO.IOProxy.Directory.CanReadWrite(path)) {
                    path = Path.Combine(path, "DAP Cache");
                } else {
                    path = Path.Combine(TempDir, "dap_cache");
                }
                
                Banshee.IO.IOProxy.Directory.Create(path);
                dap_cache_path = path;
                
                return path;
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
