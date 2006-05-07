/***************************************************************************
 *  FileTrackInfo.cs
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
using System.Text.RegularExpressions;
using System.IO;
using System.Data;
using System.Collections;
using System.Threading;
using Entagged;

namespace Banshee.Base
{
    public class FileTrackInfo : TrackInfo
    {
        public FileTrackInfo(Uri uri)
        {
            LoadFromUri(uri);
            PreviousTrack = Gtk.TreeIter.Zero;
            this.uri = uri;
        }
		
        private void LoadFromUri(Uri uri)
        {
            ParsePath(uri.LocalPath);
            track_id = 0;
   
            AudioFile af = new AudioFile(uri.LocalPath, Banshee.Gstreamer.Utilities.DetectMimeType(uri));

            mimetype = af.MimeType;

			artist = Choose (af.Artist, artist);
            album = Choose  (af.Album, album);
            title = Choose (af.Title, title);
            genre = Choose (af.Genre, genre);
            track_number = af.TrackNumber == 0 ? track_number : (uint)af.TrackNumber;
            track_count = 0;
            duration = af.Duration;
            year = af.Year;
            
            this.date_added = DateTime.Now;
        }

        private void ParsePath(string path)
        {
            artist = String.Empty;
            album = String.Empty;
            title = String.Empty;
            track_number = 0;
            Match match;

            string fileName = PathUtil.FileUriToPath(PathUtil.PathToFileUri(path));
            fileName = Path.GetFileNameWithoutExtension(fileName);
        
            match = Regex.Match(fileName, @"(\d+)\.? *(.*)$");
            if(match.Success) {
                track_number = Convert.ToUInt32(match.Groups[1].ToString());
                fileName = match.Groups[2].ToString().Trim();
            }

            /* Artist - Album - Title */
            match = Regex.Match(fileName, @"\s*(.*)-\s*(.*)-\s*(.*)$");
            if(match.Success) {
                artist = match.Groups[1].ToString();
                album = match.Groups[2].ToString();
                title = match.Groups[3].ToString();
            } else {
                /* Artist - Title */
                match = Regex.Match(fileName, @"\s*(.*)-\s*(.*)$");
                if(match.Success) {
                    artist = match.Groups[1].ToString();
                    title = match.Groups[2].ToString();
                } else {
                    /* Title */
                    title = fileName;
                }
            }

            while(path != null && path != String.Empty) {
                fileName = Path.GetFileName (path);
                path = Path.GetDirectoryName(path);
                if(album == String.Empty) {
                    album = fileName;
                    continue;
                }
                
                if(artist == String.Empty) {
                    artist = fileName;
                    continue;
                }
                break;
            }
            
            artist = artist.Trim();
            album = album.Trim();
            title = title.Trim();
            
            if(artist.Length == 0) {
                artist = /*"Unknown Artist"*/ null;
            }
            
            if(album.Length == 0) {
                album = /*"Unknown Album"*/ null;
            }
            
            if(title.Length == 0) {
                title = /*"Unknown Title"*/ null;
            }
        }
 
		private static string Choose(string priority, string fallback)
		{
			return (priority == null || priority.Length == 0) ? fallback : priority;
		}

        public override void Save()
        {
        }
        
        public override void IncrementPlayCount()
        {
            play_count++;
            last_played = DateTime.Now;
            Save();
        }
        
        protected override void SaveRating()
        {
            Save();
        }
    }
}
