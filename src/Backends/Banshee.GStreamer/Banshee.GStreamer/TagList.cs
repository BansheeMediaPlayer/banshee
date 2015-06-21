//
// TagList.cs
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
using System.Runtime.InteropServices;

using Banshee.Streaming;
using Banshee.Collection;

namespace Banshee.GStreamer
{
    public class TagList : IDisposable
    {
        private HandleRef handle;

        public TagList ()
        {
            handle = new HandleRef (this, bt_tag_list_new ());
        }

        public TagList (TrackInfo track) : this ()
        {
            Merge (track);
        }

        public void Merge (TrackInfo track)
        {
            AddTag (CommonTags.Artist, track.ArtistName);
            AddTag (CommonTags.Album, track.AlbumTitle);
            AddTag (CommonTags.Title, track.TrackTitle);
            AddTag (CommonTags.Genre, track.Genre);

            AddTag (CommonTags.TrackNumber, (uint)track.TrackNumber);
            AddTag (CommonTags.TrackCount, (uint)track.TrackCount);
            AddTag (CommonTags.AlbumDiscNumber, (uint)track.DiscNumber);
            AddTag (CommonTags.AlbumDiscCount, (uint)track.DiscCount);

            AddYear (track.Year);
            AddDate (track.ReleaseDate);

            AddTag (CommonTags.Composer, track.Composer);
            AddTag (CommonTags.Copyright, track.Copyright);
            AddTag (CommonTags.Comment, track.Comment);

            AddTag (CommonTags.MusicBrainzTrackId, track.MusicBrainzId);
            AddTag (CommonTags.MusicBrainzArtistId, track.ArtistMusicBrainzId);
            AddTag (CommonTags.MusicBrainzAlbumId, track.AlbumMusicBrainzId);

        }

        public void AddTag (string tagName, string value)
        {
            if (!String.IsNullOrEmpty (value)) {
                AddTag (tagName, (object)value);
            }
        }

        public void AddTag (string tagName, uint value)
        {
            if (value > 0) {
                AddTag (tagName, (object)value);
            }
        }

        public void AddDate (DateTime date)
        {
            bt_tag_list_add_date (Handle, date.Year, date.Month, date.Day);
        }

        public void AddYear (int year)
        {
            if (year > 1) {
                bt_tag_list_add_date (Handle, year, 1, 1);
            }
        }

        public void AddTag (string tagName, object value)
        {
            GLib.Value g_value = new GLib.Value (value);
            bt_tag_list_add_value (Handle, tagName, ref g_value);
        }

        public void Dispose ()
        {
            if (handle.Handle != IntPtr.Zero) {
                bt_tag_list_free (handle);
                handle = new HandleRef (this, IntPtr.Zero);

                GC.SuppressFinalize (this);
            }
        }

        public HandleRef Handle {
            get { return handle; }
        }

        [DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr bt_tag_list_new ();

        [DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void bt_tag_list_free (HandleRef tag_list);

        [DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void bt_tag_list_add_value (HandleRef tag_list, string tag_name, ref GLib.Value value);

        [DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void bt_tag_list_add_date (HandleRef tag_list, int year, int month, int day);
    }
}
