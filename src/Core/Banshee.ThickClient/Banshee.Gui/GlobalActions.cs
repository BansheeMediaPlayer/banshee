//
// GlobalActions.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2007 Novell, Inc.
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

using Hyena;
using Banshee.Base;
using Banshee.ServiceStack;
using Banshee.Streaming;
using Banshee.Gui.Dialogs;
using Banshee.Widgets;
using Banshee.Playlist;

namespace Banshee.Gui
{
    public class GlobalActions : BansheeActionGroup
    {
        public GlobalActions () : base ("Global")
        {
            Add (new ActionEntry [] {
                // Media Menu
                new ActionEntry ("MediaMenuAction", null, 
                    Catalog.GetString ("_Media"), null, null, null),

                new ActionEntry ("ImportAction", Stock.Open,
                    Catalog.GetString ("Import _Media..."), "<control>I",
                    Catalog.GetString ("Import media from a variety of sources"), OnImport),

                new ActionEntry ("ImportPlaylistAction", null,
                    Catalog.GetString ("Import _Playlist..."), null,
                    Catalog.GetString ("Import a playlist"), OnImportPlaylist),

                new ActionEntry ("RescanAction", null,
                    Catalog.GetString ("Rescan Music Library"), null,
                    Catalog.GetString ("Rescan the Music Library folder"), delegate {
                        new Banshee.Collection.RescanPipeline (ServiceManager.SourceManager.MusicLibrary);
                    }),

                new ActionEntry ("OpenLocationAction", null, 
                    Catalog.GetString ("Open _Location..."), "<control>L",
                    Catalog.GetString ("Open a remote location for playback"), OnOpenLocation),
                    
                new ActionEntry ("QuitAction", Stock.Quit,
                    Catalog.GetString ("_Quit"), "<control>Q",
                    Catalog.GetString ("Quit Banshee"), OnQuit),

                // Edit Menu
                new ActionEntry ("EditMenuAction", null, 
                    Catalog.GetString("_Edit"), null, null, null),

                new ActionEntry ("PreferencesAction", Stock.Preferences,
                    Catalog.GetString ("_Preferences"), null,
                    Catalog.GetString ("Modify your personal preferences"), OnPreferences),

                new ActionEntry ("ExtensionsAction", null, 
                    Catalog.GetString ("Manage _Extensions"), null,
                    Catalog.GetString ("Manage extensions to add new features to Banshee"), OnExtensions),
                
                // Tools menu
                new ActionEntry ("ToolsMenuAction", null,
                    Catalog.GetString ("_Tools"), null, null, null),
                
                // Help Menu
                new ActionEntry ("HelpMenuAction", null, 
                    Catalog.GetString ("_Help"), null, null, null),
                
                new ActionEntry ("WebMenuAction", null,
                    Catalog.GetString ("_Web Resources"), null, null, null),
                    
                new ActionEntry ("WikiGuideAction", Stock.Help,
                    Catalog.GetString ("Banshee _User Guide (Wiki)"), null,
                    Catalog.GetString ("Learn about how to use Banshee"), delegate {
                        Banshee.Web.Browser.Open ("http://banshee-project.org/support/guide/");
                    }),
                    
                new ActionEntry ("WikiSearchHelpAction", null,
                    Catalog.GetString ("Advanced Collection Searching"), null,
                    Catalog.GetString ("Learn advanced ways to search your media collection"), delegate {
                        Banshee.Web.Browser.Open ("http://banshee-project.org/support/guide/searching/");
                    }),
                    
                new ActionEntry ("WikiAction", null,
                    Catalog.GetString ("Banshee _Home Page"), null,
                    Catalog.GetString ("Visit the Banshee Home Page"), delegate {
                        Banshee.Web.Browser.Open ("http://banshee-project.org/");
                    }),
                    
                new ActionEntry ("WikiDeveloperAction", null,
                    Catalog.GetString ("_Get Involved"), null,
                    Catalog.GetString ("Become a contributor to Banshee"), delegate {
                        Banshee.Web.Browser.Open ("http://banshee-project.org/contribute/");
                    }),
                 
                new ActionEntry ("VersionInformationAction", null,
                    Catalog.GetString ("_Version Information"), null,
                    Catalog.GetString ("View detailed version and configuration information"), OnVersionInformation),
                    
                new ActionEntry("AboutAction", "gtk-about", OnAbout)
            });
            
            this["ExtensionsAction"].Visible = false;
        }
            
#region Media Menu Actions

        private void OnImport (object o, EventArgs args)
        {
            Banshee.Library.Gui.ImportDialog dialog = new Banshee.Library.Gui.ImportDialog ();            
            try {
                if (dialog.Run () != Gtk.ResponseType.Ok) {
                    return;
                }
                    
                dialog.ActiveSource.Import ();
            } finally {
                dialog.Destroy ();
            }
        }
        
        private void OnOpenLocation (object o, EventArgs args)
        {
            OpenLocationDialog dialog = new OpenLocationDialog ();
            ResponseType response = dialog.Run ();
            string address = dialog.Address;
            dialog.Destroy ();
            
            if (response == ResponseType.Ok) {
                RadioTrackInfo.OpenPlay (address);
            }
        }

        private void OnImportPlaylist (object o, EventArgs args)
        {
            // Prompt user for location of the playlist.
            Banshee.Gui.Dialogs.FileChooserDialog chooser = new Banshee.Gui.Dialogs.FileChooserDialog (
                Catalog.GetString("Import Playlist"),
                PrimaryWindow,
                FileChooserAction.Open
            );
                         
            chooser.DefaultResponse = ResponseType.Ok;
            chooser.SelectMultiple = true;
            chooser.AddFilter (Hyena.Gui.GtkUtilities.GetFileFilter (Catalog.GetString ("Playlists"), PlaylistFileUtil.PlaylistExtensions));
            // TODO library-location add the BaseDirectory for the library being imported to (or all of them)
            /*try {
                chooser.AddShortcutFolder (Paths.LibraryLocation);
            } catch {}*/

            chooser.AddButton (Stock.Cancel, ResponseType.Cancel);
            // Translators: verb
            chooser.AddButton (Catalog.GetString("I_mport"), ResponseType.Ok);
            
            int response = chooser.Run();            

            string [] uris = null;
            if (response == (int) ResponseType.Ok) {
                uris = chooser.Uris;
                chooser.Destroy();
            } else {
                chooser.Destroy();
                return;
            }
            
            if (uris == null || uris.Length == 0) {
                return;
            }

            Banshee.Kernel.Scheduler.Schedule (new Banshee.Kernel.DelegateJob (delegate {
                foreach (string uri in uris) {
                    PlaylistFileUtil.ImportPlaylistToLibrary (uri);
                }
            }));
        }
        
        private void OnQuit (object o, EventArgs args)
        {
            Banshee.ServiceStack.Application.Shutdown ();
        }
        
#endregion

#region Edit Menu Actions

        private void OnPreferences (object o, EventArgs args)
        {
            try {
                Banshee.Preferences.Gui.PreferenceDialog dialog = new Banshee.Preferences.Gui.PreferenceDialog ();
                dialog.Run ();
                dialog.Destroy ();
            } catch (ApplicationException) {
            }
        }

        private void OnExtensions (object o, EventArgs args)
        {
            Mono.Addins.Gui.AddinManagerWindow.Run (PrimaryWindow);
        }

#endregion
        
#region Help Menu Actions
        
        private void OnVersionInformation (object o, EventArgs args)
        {
            Hyena.Gui.Dialogs.VersionInformationDialog dialog = new Hyena.Gui.Dialogs.VersionInformationDialog ();
            dialog.Run ();
            dialog.Destroy ();
        }
        
        private void OnAbout (object o, EventArgs args)
        {
            Banshee.Gui.Dialogs.AboutDialog dialog = new Banshee.Gui.Dialogs.AboutDialog ();
            dialog.Show ();
        }

#endregion
            
    }
}
