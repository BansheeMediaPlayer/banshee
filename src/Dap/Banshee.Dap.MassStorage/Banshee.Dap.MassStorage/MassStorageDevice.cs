//
// MassStorageDevice.cs
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
using System.IO;
using System.Collections.Generic;

using Hyena;

using Banshee.Base;
using Banshee.Hardware;
using Banshee.Collection.Database;

namespace Banshee.Dap.MassStorage
{
    public class MassStorageDevice : IDeviceMediaCapabilities
    {
        private MassStorageSource source;
        protected MassStorageSource Source {
            get { return source; }
        }
        
        public MassStorageDevice (MassStorageSource source)
        {
            this.source = source;
        }
        
        public virtual void SourceInitialize ()
        {
        }
        
        public virtual bool DeleteTrackHook (DatabaseTrackInfo track)
        {
            return true;
        }
        
        public virtual bool LoadDeviceConfiguration ()
        {
            string path = IsAudioPlayerPath;
            StreamReader reader = null;
            
            if (!File.Exists (path)) {
                return false;
            }
            
            try {
                foreach (var item in new KeyValueParser (reader = new StreamReader (path))) {
                    try {
                        switch (item.Key) {
                            case "name": name = item.Value[0]; break;
                            
                            case "cover_art_file_type": cover_art_file_type = item.Value[0].ToLower (); break;
                            case "cover_art_file_name": cover_art_file_name = item.Value[0]; break;
                            case "cover_art_size": Int32.TryParse (item.Value[0], out cover_art_size); break;
                            
                            case "folder_depth": Int32.TryParse (item.Value[0], out folder_depth); break;
                            case "audio_folders": audio_folders = item.Value; break;
                            
                            case "output_formats": playback_mime_types = item.Value; break;
                            
                            case "playlist_format": playlist_formats = item.Value; break;
                            case "playlist_path": playlist_path = item.Value[0]; break;
                            
                            default:
                                throw new ApplicationException ("unsupported key");    
                        }
                    } catch (Exception e) {
                        Log.Exception ("Invalid .is_audio_player item " + item.Key, e);
                    }
                }
            } catch (Exception e) {
                Log.Exception ("Error parsing " + path, e);
            } finally {
                if (reader != null) {
                    reader.Dispose ();
                }
            }
            
            has_is_audio_player_file = true;
            
            return true;
        }
        
        private bool has_is_audio_player_file;
        public bool HasIsAudioPlayerFile {
            get { return has_is_audio_player_file; }
        }
        
        private string IsAudioPlayerPath {
            get { return System.IO.Path.Combine (source.Volume.MountPoint, ".is_audio_player"); }
        }
        
        private string name;
        public virtual string Name {
            get { return name ?? source.Volume.Name; }
        }
        
        private int cover_art_size;
        public virtual int CoverArtSize { 
            get { return cover_art_size; } 
        }
        
        private int folder_depth;
        public virtual int FolderDepth { 
            get { return folder_depth; } 
        }
        
        private string [] audio_folders = new string[0];
        public virtual string [] AudioFolders { 
            get { return audio_folders; } 
        }
        
        private string cover_art_file_type;
        public virtual string CoverArtFileType { 
            get { return cover_art_file_type; } 
        }
        
        private string cover_art_file_name;
        public virtual string CoverArtFileName { 
            get { return cover_art_file_name; } 
        }
        
        private string [] playlist_formats;
        public virtual string [] PlaylistFormats { 
            get { return playlist_formats; } 
        }
        
        private string playlist_path;
        public virtual string PlaylistPath { 
            get { return playlist_path; } 
        }
        
        private string [] playback_mime_types;
        public virtual string [] PlaybackMimeTypes { 
            get { return playback_mime_types; } 
        }
        
        public virtual string DeviceType {
            get { return "mass-storage"; }
        }
        
        public bool IsType (string type)
        {
            return type == DeviceType;
        }
    }
}
