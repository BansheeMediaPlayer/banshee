//
// FileChooserDialog.cs
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
using Gtk;

using Banshee.Configuration;
using Banshee.ServiceStack;
using Hyena;

namespace Banshee.Gui.Dialogs
{
    public class FileChooserDialog : Gtk.FileChooserDialog
    {
        public static FileChooserDialog CreateForImport (string title, bool files)
        {
            var chooser = new Banshee.Gui.Dialogs.FileChooserDialog (
                title,
                ServiceManager.Get<Banshee.Gui.GtkElementsService> ().PrimaryWindow,
                files ? FileChooserAction.Open : FileChooserAction.SelectFolder
            );

            chooser.DefaultResponse = ResponseType.Ok;
            chooser.SelectMultiple = true;

            chooser.AddButton (Stock.Cancel, ResponseType.Cancel);
            // Translators: verb
            chooser.AddButton (Mono.Unix.Catalog.GetString("I_mport"), ResponseType.Ok);

            Hyena.Gui.GtkUtilities.SetChooserShortcuts (chooser,
                ServiceManager.SourceManager.MusicLibrary.BaseDirectory,
                ServiceManager.SourceManager.VideoLibrary.BaseDirectory
            );

            return chooser;
        }

        public FileChooserDialog (string title, FileChooserAction action) : this (title, null, action)
        {
        }

        public FileChooserDialog (string title, Window parent, FileChooserAction action) :
            base (title, parent, action)
        {
            LocalOnly = Banshee.IO.Provider.LocalOnly;
            string fallback = SafeUri.FilenameToUri (Environment.GetFolderPath (Environment.SpecialFolder.Personal));
            SetCurrentFolderUri (LastFileChooserUri.Get (fallback));
            WindowPosition = WindowPosition.Center;
        }

        protected override void OnResponse (ResponseType response)
        {
            base.OnResponse (response);

            if (CurrentFolderUri != null) {
                LastFileChooserUri.Set (CurrentFolderUri);
            }
        }

        public static readonly SchemaEntry<string> LastFileChooserUri = new SchemaEntry<string> (
            "player_window", "last_file_chooser_uri",
            String.Empty,
            "URI",
            "URI of last file folder"
        );
    }
}
