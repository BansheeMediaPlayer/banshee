//
// PaasItemPage.cs
//
// Author:
//   Gabriel Burt <gburt@novell.com>
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
using System.Collections.Generic;

using Mono.Unix;
using Gtk;

using Hyena.Widgets;

using Banshee.Gui.TrackEditor;

using Banshee.Paas.Data;

namespace Banshee.Paas.Gui
{
    public class PaasItemPage : Gtk.ScrolledWindow, ITrackEditorPage
    {
        private VBox box;

        private WrapLabel podcast       = new WrapLabel ();
        private WrapLabel author        = new WrapLabel ();
        private WrapLabel published     = new WrapLabel ();
        private WrapLabel description   = new WrapLabel ();

        public PaasItemPage ()
        {
            BorderWidth = 2;
            ShadowType = ShadowType.None;
            HscrollbarPolicy = PolicyType.Never;
            VscrollbarPolicy = PolicyType.Automatic;
            
            box = new VBox ();
            box.BorderWidth = 6;
            box.Spacing = 12;

            box.PackStart (podcast,     false, false, 0);
            box.PackStart (author,      false, false, 0);
            box.PackStart (published,   false, false, 0);
            box.PackStart (description, true, true, 0);
            
            AddWithViewport (box);
            ShowAll ();
        }
        
        public void Initialize (TrackEditorDialog dialog)
        {
        }
        
        public void LoadTrack (EditorTrackInfo track)
        {
            BorderWidth = 2;

            PaasTrackInfo info = PaasTrackInfo.From (track.SourceTrack);

            if (info == null) {
                Hide ();
                return;
            }
            // HACK to keep the save TrackInfo dialog from appearing when closing the TED.
            // Investigate / talk to Gabe / Aaron about this later.
            track.ReleaseDate = info.PubDate;
            
            podcast.Markup      = SetInfo (Catalog.GetString ("Podcast"), track.SourceTrack.AlbumTitle);
            author.Markup       = SetInfo (Catalog.GetString ("Author"), track.SourceTrack.ArtistName);
            published.Markup    = SetInfo (Catalog.GetString ("Published"), info.PubDate.ToLongDateString ());
            description.Markup  = SetInfo (Catalog.GetString ("Description"), info.Description);

            Show ();
        }

        private static string info_str = "<b>{0}</b>\n{1}";
        private static string SetInfo (string title, string info)
        {
            return String.Format (info_str,
                GLib.Markup.EscapeText (title),
                GLib.Markup.EscapeText (info)
            );
        }
        
        public int Order {
            get { return 40; }
        }
        
        public string Title {
            get { return Catalog.GetString ("Episode Details"); }
        }
        
        public PageType PageType { 
            get { return PageType.View; }
        }
        
        public Gtk.Widget TabWidget { 
            get { return null; }
        }
        
        public Gtk.Widget Widget {
            get { return this; }
        }
    }
}
