/***************************************************************************
 *  DragDrop.cs
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
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
using System.Text.RegularExpressions;
using System.Collections;
using Gtk;

using Banshee.Base;

namespace Banshee 
{
	public sealed class Dnd
	{
		public enum TargetType {
			Source,
			PlaylistRows,
			LibraryTrackIds,
			UriList
		};
		
		public static readonly TargetEntry TargetSource = 
			new TargetEntry("application/x-banshee-source", TargetFlags.App, 
				(uint)TargetType.Source);

		public static readonly TargetEntry TargetPlaylistRows = 
			new TargetEntry("application/x-banshee-playlist-rows", TargetFlags.App, 
				(uint)TargetType.PlaylistRows);

		public static readonly TargetEntry TargetLibraryTrackIds = 
			new TargetEntry("application/x-banshee-library-track-ids", TargetFlags.App, 
				(uint)TargetType.LibraryTrackIds);

		public static readonly TargetEntry TargetUriList = 
			new TargetEntry("text/uri-list", 0, (uint)TargetType.UriList);

		public static string SelectionDataToString(Gtk.SelectionData data)
		{
			if(data.Length <= 0) {
				return String.Empty;
			}

			try {
				return System.Text.Encoding.UTF8.GetString(data.Data);
			} catch {
				return String.Empty;
			}
		}

		public static string [] SplitSelectionData(Gtk.SelectionData data)
		{
			string str = SelectionDataToString(data);
			return SplitSelectionData(str);
		}

		public static string [] SplitSelectionData(string data)
		{
			return Regex.Split(data, "\r\n");
		}
		
		public static TreePath [] SelectionDataToTreePaths(Gtk.SelectionData data)
		{
			string rawData = String.Empty;
			
			try {
				rawData = SelectionDataToString(data);
				return SelectionDataToTreePaths(rawData);
			} catch(Exception) {
				return null;
			}
		}
		
		public static TreePath [] SelectionDataToTreePaths(string data)
		{
			ArrayList pathList = new ArrayList();
			string [] strPaths = SplitSelectionData(data);
			
			foreach(string strPath in strPaths) {
				try {
					string finalStrPath = strPath.Trim();
					if(!finalStrPath.Equals(String.Empty))
						pathList.Add(new TreePath(finalStrPath));
				} catch(Exception) { }
			}
		
			return pathList.ToArray(typeof(TreePath)) as TreePath [];
		}
		
		public static byte [] TreeViewSelectionPathsToBytes(TreeView view)
		{
			if(view.Selection.CountSelectedRows() <= 0)
				return null;
				
			string selData = null;

			foreach(TreePath p in view.Selection.GetSelectedRows())
				selData += p.ToString() + "\r\n";
		
			return System.Text.Encoding.ASCII.GetBytes(selData);
		}
		
		public static byte [] PlaylistViewSelectionUrisToBytes(PlaylistView view)
		{
			if(view.Selection.CountSelectedRows() <= 0)
				return null;
				
			string selData = null;
			foreach(TreePath p in view.Selection.GetSelectedRows()) {
				PlaylistModel model = view.Model as PlaylistModel;
				TrackInfo ti = model.PathTrackInfo(p);
				selData += ti.Uri + "\r\n";
			}
			
			return System.Text.Encoding.ASCII.GetBytes(selData);
		}
		
		public static byte [] PlaylistSelectionTrackIdsToBytes(PlaylistView view)
		{
			if(view.Selection.CountSelectedRows() <= 0)
				return null;
				
			string selData = null;
				
			foreach(TreePath p in view.Selection.GetSelectedRows()) {
				PlaylistModel model = view.Model as PlaylistModel;
				TrackInfo ti = model.PathTrackInfo(p);
				selData += ti.TrackId + "\r\n";
			}
			
			return System.Text.Encoding.ASCII.GetBytes(selData);
		}
	}
}
