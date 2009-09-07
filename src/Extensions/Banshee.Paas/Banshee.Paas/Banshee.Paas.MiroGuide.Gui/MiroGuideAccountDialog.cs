// 
// MiroGuideAccountDialog.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//   Mike Urbanski <michael.c.urbanski@gmail.com>
//
// Copyright (C) 2006 Novell, Inc.
// Copyright (c) 2009 Michael C. Urbanski
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

// This code has been copied/pasted so many times that it should probably just 
// be abstracted.

using System;
using Mono.Unix;

using Gtk;
using Banshee.Gui;

using Banshee.Paas.Aether.MiroGuide;

namespace Banshee.Paas.MiroGuide.Gui
{
    public class MiroGuideAccountDialog : Gtk.Dialog
    {
        private AccelGroup accel_group;
        private MiroGuideLoginForm login_form;
        private Label message;
    
        public MiroGuideAccountDialog (MiroGuideAccountInfo accountInfo) : this (accountInfo, true)
        {
        }
    
        public MiroGuideAccountDialog (MiroGuideAccountInfo accountInfo, bool addCloseButton) : base ()
        {
            Title = Catalog.GetString ("Miro Guide Account");
            HasSeparator = false;
            BorderWidth = 5;
            
            IconName = "gtk-dialog-authentication";
            
            accel_group = new AccelGroup ();
            AddAccelGroup (accel_group);
            
            HBox hbox = new HBox (false, 12);
            VBox vbox = new VBox (false, 0);
            hbox.BorderWidth = 5;
            vbox.Spacing = 5;
            hbox.Show ();
            vbox.Show ();
            
            Image image = new Image ();
            image.Yalign = 0.0f;
            image.Pixbuf = IconThemeUtils.LoadIcon (48, "miroguide");
            image.IconSize = (int)IconSize.Dialog;
            image.Show ();
        
            hbox.PackStart (image, false, false, 0);
            hbox.PackStart (vbox, true, true, 0);
        
            Label header = new Label ();
            header.Xalign = 0.0f;
            header.Markup = String.Format ("<big><b>{0}</b></big>", Title);
            header.Show ();
            
            message = new Label (Catalog.GetString ("Please enter your Miro Guide account information."));
            message.Xalign = 0.0f;
            message.Show ();
            
            vbox.PackStart (header, false, false, 0);
            vbox.PackStart (message, false, false, 0);
        
            login_form = new MiroGuideLoginForm (accountInfo);
            login_form.Show ();
            
            vbox.PackStart (login_form, true, true, 0);
            
            VBox.PackStart (hbox, true, true, 0);
            VBox.Remove (ActionArea);
            VBox.Spacing = 10;
            
            HBox bottom_box = new HBox ();

            bottom_box.PackStart (ActionArea, true, true, 0);
            bottom_box.ShowAll ();
            
            VBox.PackEnd (bottom_box, false, false, 0);
            
            if (addCloseButton) {
                AddButton (Stock.Cancel, ResponseType.Cancel);
                Button button = new Button (Stock.Save);
                button.ShowAll ();
                
                button.Activated += delegate {
                    login_form.Save ();
                };
                
                button.Clicked += delegate {
                    login_form.Save ();
                };
                
                AddActionWidget (button, ResponseType.Ok);
                login_form.SaveOnEnter (this);
            }
        }
        
        public void AddButton (string message, ResponseType response, bool isDefault)
        {
            Button button = (Button)AddButton (message, response);
            
            if (isDefault) {
                DefaultResponse = response;
                button.AddAccelerator ("activate", accel_group, (uint)Gdk.Key.Return, 0, Gtk.AccelFlags.Visible);
            }
        }

        public string Message {
            get { return message.Text; }
            set { message.Text = value; }
        }
        
        public bool SaveOnEdit {
            get { return login_form.SaveOnEdit; }
            set { login_form.SaveOnEdit = value; }
        }
        
        public string Username {
            get { return login_form.Username; }
        }
        
        public string Password {
            get { return login_form.Password; }
        }
    }
}
