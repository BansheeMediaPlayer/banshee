/***************************************************************************
 *  Editor.cs
 *
 *  Copyright (C) 2007 Novell, Inc.
 *  Written by Gabriel Burt <gabriel.burt@gmail.com>
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
using System.Collections;
using Gtk;
using Glade;
using Mono.Gettext;

using Banshee.Base;
using Banshee.Widgets;
using Banshee.Sources;
using Banshee.Database;

namespace Banshee.Plugins.LastFM
{
    public class Editor : Banshee.Gui.GladeDialog
    {
        const string dialog_name = "StationSourceEditorDialog";
        const string dialog_resource = "lastfm.glade";

        private StationSource source;

        [Widget] private Gtk.Entry name_entry;
        [Widget] private Gtk.ComboBox type_combo;
        [Widget] private Gtk.Entry arg_entry;
        [Widget] private Gtk.Label arg_label;
        [Widget] private Gtk.Button ok_button;

        public Editor (StationSource source) : base (dialog_name, new Glade.XML (
            System.Reflection.Assembly.GetExecutingAssembly (), dialog_resource, dialog_name, "banshee"))
        {
            this.source = source;
            SourceName = source.Name;
            Arg = source.Arg;
            Initialize ();
            Dialog.Title = Catalog.GetString ("Edit Station");
        }
    
        public Editor () : base (dialog_name, new Glade.XML (
            System.Reflection.Assembly.GetExecutingAssembly (), dialog_resource, dialog_name, "banshee"))
        {
            Initialize ();
            Dialog.Title = Catalog.GetString ("New Station");
        }

        private void Initialize ()
        {
            // Pressing enter should save and close the dialog
            //Dialog.DefaultResponse = Gtk.ResponseType.Ok;
            ok_button.HasDefault = true;

            Gdk.Geometry limits = new Gdk.Geometry ();
            limits.MinWidth = Dialog.SizeRequest ().Width;
            limits.MaxWidth = Gdk.Screen.Default.Width;
            limits.MinHeight = -1;
            limits.MaxHeight = -1;
            Dialog.SetGeometryHints (Dialog, limits, Gdk.WindowHints.MaxSize | Gdk.WindowHints.MinSize);

            type_combo.RemoveText (0);
            int active_type = 0;
            int i = 0;
            foreach (StationType type in StationType.Types) {
                if (!type.SubscribersOnly || Connection.Instance.Subscriber) {
                    type_combo.AppendText (type.Label);
                    if (source != null && type == source.Type) {
                        active_type = i;
                    }
                    i++;
                }
            }

            type_combo.Changed += HandleTypeChanged;
            type_combo.Active = active_type;
            arg_entry.Changed += HandleArgChanged;
            ok_button.Sensitive = true;
            name_entry.GrabFocus ();
        }

        public void RunDialog ()
        {
            Run ();
            Dialog.Destroy ();
        }

        public override ResponseType Run ()
        {
            Dialog.ShowAll ();

            ResponseType response = (ResponseType)Dialog.Run ();

            if (response == ResponseType.Ok) {
                string name = SourceName;
                StationType type = Type;
                string arg = Arg;

                ThreadAssist.Spawn (delegate {
                    if (source == null) {
                        source = new StationSource (name, type.Name, arg);
                        LastFMPlugin.Instance.Source.AddChildSource (source);
                    } else {
                        source.Rename (name);
                        source.Type = type;
                        source.Arg = arg;
                        source.Commit ();
                        //source.Refresh ();
                    }
                });
            }

            return response;
        }

        private void HandleTypeChanged (object sender, EventArgs args)
        {
            StationType type = StationType.FindByLabel (type_combo.ActiveText);
            if (type == null)
                Console.WriteLine ("got null type for text: {0}", type_combo.ActiveText);
            else
                arg_label.Text = type.ArgLabel;
        }

        private string last_arg = String.Empty;
        private void HandleArgChanged (object sender, EventArgs args)
        {
            if (name_entry.Text == last_arg) {
                name_entry.Text = arg_entry.Text;
                last_arg = arg_entry.Text;
            }
        }

        private string SourceName {
            get { return name_entry.Text; }
            set { name_entry.Text = value; }
        }

        private StationType Type {
            get { return StationType.FindByLabel (type_combo.ActiveText); }
        }

        private string Arg {
            get { return arg_entry.Text; }
            set { arg_entry.Text = value; }
        }
    }
}
