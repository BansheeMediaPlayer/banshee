//
// FileImportSource.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2006-2007 Novell, Inc.
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
using Mono.Unix;
using Gtk;

using Banshee.ServiceStack;

namespace Banshee.Library.Gui
{
    public class FileImportSource : IImportSource
    {
        public FileImportSource ()
        {
        }
    
        public void Import()
        {
            Banshee.Gui.Dialogs.FileChooserDialog chooser = new Banshee.Gui.Dialogs.FileChooserDialog (
                Catalog.GetString ("Import Files to Library"),
                FileChooserAction.Open
            );
            
            chooser.AddButton (Stock.Cancel, ResponseType.Cancel);
            chooser.AddButton (Stock.Open, ResponseType.Ok);
            chooser.AddFilter (Hyena.Gui.GtkUtilities.GetFileFilter (Catalog.GetString ("Media Files"), Banshee.Collection.Database.DatabaseImportManager.WhiteListFileExtensions.List));
            chooser.SelectMultiple = true;
            chooser.DefaultResponse = ResponseType.Ok;
            
            SetChooserShortcuts (chooser);
            
            if (chooser.Run () == (int)ResponseType.Ok) {
                Banshee.ServiceStack.ServiceManager.Get<LibraryImportManager> ().Enqueue (chooser.Uris);
            }
            
            chooser.Destroy ();
        }
        
        public string Name {
            get { return Catalog.GetString ("Local Files"); }
        }
        
        public string [] IconNames {
            get { return new string [] { "gtk-open" }; }
        }
        
        public bool CanImport {
            get { return true; }
        }
        
        public int SortOrder {
            get { return 5; }
        }
        
        public static void SetChooserShortcuts (Gtk.FileChooserDialog chooser)
        {
            Hyena.Gui.GtkUtilities.SetChooserShortcuts (chooser,
                ServiceManager.SourceManager.MusicLibrary.BaseDirectory,
                ServiceManager.SourceManager.VideoLibrary.BaseDirectory
            );
        }

        // Reserve strings in preparation for the forthcoming string freeze.
        public void ReservedStrings ()
        {
            Catalog.GetString ("_Files to import:");
            Catalog.GetString ("Select Files");
            Catalog.GetString ("(none selected)");
        }
    }
}
