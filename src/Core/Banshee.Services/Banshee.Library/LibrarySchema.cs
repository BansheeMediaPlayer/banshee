/***************************************************************************
 *  LibrarySchema.cs
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
using Banshee.Configuration;
using Banshee.Library;

namespace Banshee.Configuration.Schema
{
    public static class LibrarySchema
    {
        // Deprecated, don't use in new code
        internal static readonly SchemaEntry<string> Location = new SchemaEntry<string> ("library", "base_location", null, null, null);

        public static readonly SchemaEntry<string> FolderPattern = new SchemaEntry<string>(
            "library", "folder_pattern",
            "%album_artist%%path_sep%%album%",
            "Library Folder Pattern",
            "Format for creating a track folder inside the library. Do not create an absolute path. " +
                "Location here is relative to the Banshee music directory. See LibraryLocation. Legal tokens: " +
                "%album_artist%, %track_artist%, %album%, %genre%, %title%, %track_number%, %track_count%, " +
                "%track_number_nz% (No prefixed zero), %track_count_nz% (No prefixed zero), %album_artist_initial%," +
                "%path_sep% (portable directory separator (/)), %artist% (deprecated, use %album_artist%)."
        );

        public static readonly SchemaEntry<string> FilePattern = new SchemaEntry<string>(
            "library", "file_pattern",
            "{%track_number%. }%title%",
            "Library File Pattern",
            "Format for creating a track filename inside the library. Do not use path tokens/characters here. " +
                "See LibraryFolderPattern. Legal tokens: %album_artist%, %track_artist%, %album%, %genre%, %title%, %track_number%, " +
                "%track_count%, %track_number_nz% (No prefixed zero), %track_count_nz% (No prefixed zero), " +
                "%album_artist_initial%, %artist% (deprecated, use %album_artist%)."
        );

        public static readonly SchemaEntry<bool> CopyOnImport = new SchemaEntry<bool>(
            "library", "copy_on_import",
            false,
            "Copy music on import",
            "Copy and rename music to banshee music library directory when importing"
        );

        public static readonly SchemaEntry<bool> MoveOnInfoSave = new SchemaEntry<bool>(
            "library", "move_on_info_save",
            false,
            "Move music on info save",
            "Move music within banshee music library directory when saving track info"
        );

        public static readonly SchemaEntry<bool> WriteMetadata = new SchemaEntry<bool>(
            "library", "write_metadata",
            false,
            "Write metadata back to audio files",
            "If enabled, metadata (tags) will be written back to audio files when using the track metadata editor."
        );

        public static readonly SchemaEntry<bool> WriteRatingsAndPlayCounts = new SchemaEntry<bool>(
            "library", "write_rating",
            false,
            "Store ratings within supported files",
            "If enabled, rating and playcount metadata will be written back to audio files."
        );

        public static readonly SchemaEntry<bool> SortByAlbumYear = new SchemaEntry<bool>(
            "library", "sort_albums_by_year",
            false,
            "Sort tracks by album year",
            "If set the tracks will be sorted by album year instead of by album name"
        );
    }
}
