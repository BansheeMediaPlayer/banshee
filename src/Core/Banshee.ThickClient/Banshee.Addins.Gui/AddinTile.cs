//
// AddinTile.cs
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
using Gtk;
using Mono.Unix;
using Mono.Addins;

using Hyena.Widgets;

namespace Banshee.Addins.Gui
{    
    public class AddinTile : Table
    {
        private Addin addin;
        private Button activate_button;
        private Button details_button;
        private Box button_box;
        
        private Label title;
        private WrapLabel description;
        private WrapLabel authors;
        
        private bool last;
        public bool Last {
            get { return last; }
            set { last = value; }
        }
        
        public event EventHandler ActiveChanged;
        
        public AddinTile (Addin addin) : base (3, 3, false)
        {
            this.addin = addin;
            BuildTile ();
        }
        
        private void BuildTile ()
        {
            BorderWidth = 5;
            RowSpacing = 1;
            ColumnSpacing = 5;
            
            Image image = new Image ();
            image.IconName = "package-x-generic";
            image.IconSize = (int)IconSize.Dnd;
            image.Yalign = 0.0f;
            image.Show ();
            Attach (image, 0, 1, 0, 3, AttachOptions.Shrink, AttachOptions.Fill | AttachOptions.Expand, 0, 0);
            
            title = new Label ();
            title.Show ();
            title.Xalign = 0.0f;
            title.Markup = String.Format ("<b>{0}</b>", GLib.Markup.EscapeText (addin.Name));
            
            Attach (title, 1, 3, 0, 1, 
                AttachOptions.Expand | AttachOptions.Fill, 
                AttachOptions.Expand | AttachOptions.Fill, 0, 0);
            
            description = new WrapLabel ();
            description.Show ();
            description.Text = addin.Description.Description;
            description.Wrap = false;
            
            Attach (description, 1, 3, 1, 2,
                AttachOptions.Expand | AttachOptions.Fill, 
                AttachOptions.Expand | AttachOptions.Fill, 0, 0);
                
            authors = new WrapLabel ();
            authors.Markup = String.Format (
                "<small><b>{0}</b> <i>{1}</i></small>", 
                Catalog.GetString ("Authors:"),
                GLib.Markup.EscapeText (addin.Description.Author)
            );
            
            Attach (authors, 1, 2, 2, 3,
                AttachOptions.Expand | AttachOptions.Fill, 
                AttachOptions.Expand | AttachOptions.Fill,  0, 4);
            
            button_box = new VBox ();
            HBox box = new HBox ();
            box.Spacing = 3;
            
            button_box.PackEnd (box, false, false, 0);
            
            Pango.FontDescription font = PangoContext.FontDescription.Copy ();
            font.Size = (int)(font.Size * Pango.Scale.Small);
            
            Label label = new Label ("Details");
            label.ModifyFont (font);
            details_button = new Button ();
            details_button.Add (label);
            details_button.Clicked += OnDetailsClicked;
            box.PackStart (details_button, false, false, 0);
            
            label = new Label ();
            label.ModifyFont (font);
            activate_button = new Button ();
            activate_button.Add (label);
            activate_button.Clicked += OnActivateClicked;
            box.PackStart (activate_button, false, false, 0);
            
            Attach (button_box, 2, 3, 2, 3, AttachOptions.Shrink, AttachOptions.Expand | AttachOptions.Fill, 0, 0);
                
            Show ();
            
            UpdateState ();
        }
        
        protected override void OnRealized ()
        {
            WidgetFlags |= WidgetFlags.NoWindow;
            GdkWindow = Parent.GdkWindow;
            base.OnRealized ();
        }
        
        protected override bool OnExposeEvent (Gdk.EventExpose evnt)
        {
            if (State == StateType.Selected) {
                Gtk.Style.PaintFlatBox (Style, evnt.Window, State, ShadowType.None, evnt.Area, 
                    this, "cell_odd", Allocation.X, Allocation.Y, 
                    Allocation.Width, Allocation.Height - (last ? 0 : 1));
            }
            
            if (!last) {            
                Gtk.Style.PaintHline (Style, evnt.Window, StateType.Normal, evnt.Area, this, null, 
                    Allocation.X, Allocation.Right, Allocation.Bottom - 1);
            }
            
            return base.OnExposeEvent (evnt);
        }
        
        private void OnActivateClicked (object o, EventArgs args)
        {
            addin.Enabled = !addin.Enabled;
            ActiveChanged (this, EventArgs.Empty);
        }
        
        private void OnDetailsClicked (object o, EventArgs args)
        {
            AddinDetailsDialog dialog = new AddinDetailsDialog (addin, Toplevel as Window);
            dialog.Run ();
            dialog.Destroy ();
        }
        
        public void UpdateState ()
        {
            bool enabled = addin.Enabled;
            bool sensitive = enabled || (!enabled && State == StateType.Selected);
            
            title.Sensitive = sensitive;
            description.Sensitive = sensitive;
            description.Wrap = State == StateType.Selected;
            authors.Visible = State == StateType.Selected;
            
            ((Label)activate_button.Child).Text = enabled 
                ? Catalog.GetString ("Disable") 
                : Catalog.GetString ("Enable");
        }
        
        public void Select (bool select)
        {
            State = select ? StateType.Selected : StateType.Normal;
            if (select) {
                button_box.ShowAll ();
            } else {
                button_box.Hide ();
            }
            button_box.State = StateType.Normal;
            UpdateState ();
            QueueResize ();
        }
    }
}
