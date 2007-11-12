/***************************************************************************
 *  Import.cs
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
using Mono.Unix;
using Banshee.Base;
 
namespace Banshee.Library
{
    public static class Import
    {
        static Import()
        {
            ImportManager.Instance.ImportRequested += OnImportManagerImportRequested;
        }
    
        public static void QueueSource(UriList uris)
        {
            ImportManager.Instance.QueueSource(uris);
        }
        
        public static void QueueSource(Gtk.SelectionData selection)
        {
            ImportManager.Instance.QueueSource(selection);
        }
        
        public static void QueueSource(string source)
        {
            ImportManager.Instance.QueueSource(source);
        }
        
        public static void QueueSource(string [] paths)
        {
            ImportManager.Instance.QueueSource(paths);
        }
        
        public static bool IsImportInProgress()
        {
            return ImportManager.Instance.IsImportingInProgress();
        }

        private static void OnImportManagerImportRequested(object o, ImportEventArgs args)
        {
            try {
                TrackInfo ti = new LibraryTrackInfo(args.FileName);
                args.ReturnMessage = String.Format("{0} - {1}", ti.Artist, ti.Title);
            } catch(Exception e) {
                args.ReturnMessage = Catalog.GetString("Scanning") + "...";
                
                if(e is TagLib.UnsupportedFormatException) {
                    return;
                }
                
                Banshee.Sources.ImportErrorsSource.Instance.AddError(args.FileName, e.Message, e);
            }
        }
    }
}
