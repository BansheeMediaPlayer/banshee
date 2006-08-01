
/***************************************************************************
 *  MMKeysConfigPage.cs
 *
 *  Copyright (C) 2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@aaronbock.net>
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
using Gtk;
using GConf;
using Mono.Unix;

using Banshee.Base;
using Banshee.Widgets;

namespace Banshee.Plugins.MMKeys 
{
    public class MMKeysConfigPage : VBox
    {
        public MMKeysConfigPage()
        {    
            Spacing = 10;
            
            Label title = new Label();
            title.Markup = String.Format("<big><b>{0}</b></big>", 
                GLib.Markup.EscapeText(Catalog.GetString("Multimedia Keyboard Shortcuts")));
            title.Xalign = 0.0f;

            Label label = new Label(Catalog.GetString(
                "Configuration of multimedia keyboard shortcuts is done through " + 
                "the Gnome Keyboard Shortcuts configuration applet."));
            label.Wrap = true;
            label.Xalign = 0.0f;

            Button button = new Button(Catalog.GetString("Configure keyboard shortcuts"));
            button.Clicked += delegate(object o, EventArgs args) {
                System.Diagnostics.Process.Start("gnome-keybinding-properties");
            };
            
            PackStart(title, false, false, 0);
            PackStart(label, false, false, 0);
            PackStart(button, false, false, 0);
            
            ShowAll();
        }
    }
}
