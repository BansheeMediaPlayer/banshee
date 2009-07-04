//
// AccountLoginForm.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2006 Novell, Inc.
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

using Lastfm;

namespace Lastfm.Gui
{
    public class AccountLoginForm : Gtk.Table
    {
        private Account account;
        private Entry username_entry;
        private Entry password_entry;
        private LinkButton signup_button;
        
        private bool save_on_edit = false;
        
        private bool save_on_enter = false;
        private Gtk.Dialog parentDialog;

        public AccountLoginForm (Account account) : base (2, 2, false)
        {
            this.account = account;

            BorderWidth = 5;
            RowSpacing = 5;
            ColumnSpacing = 5;
        
            Label username_label = new Label (Catalog.GetString ("Username:"));
            username_label.Xalign = 1.0f;
            username_label.Show ();
            
            username_entry = new Entry ();
            username_entry.Show ();
            
            Label password_label = new Label (Catalog.GetString ("Password:"));
            password_label.Xalign = 1.0f;
            password_label.Show ();
            
            password_entry = new Entry ();
            password_entry.Visibility = false;

            //When the user presses enter in the password field: save, and then destroy the AcountLoginDialog
            password_entry.Activated += delegate {
                    if (save_on_enter) {
                        this.Save ();
                        parentDialog.Destroy ();
                    }
                };
            
            password_entry.Show ();
            
            Attach (username_label, 0, 1, 0, 1, AttachOptions.Fill, 
                AttachOptions.Shrink, 0, 0);
            
            Attach (username_entry, 1, 2, 0, 1, AttachOptions.Fill | AttachOptions.Expand, 
                AttachOptions.Shrink, 0, 0);
            
            Attach (password_label, 0, 1, 1, 2, AttachOptions.Fill, 
                AttachOptions.Shrink, 0, 0);
            
            Attach (password_entry, 1, 2, 1, 2, AttachOptions.Fill | AttachOptions.Expand, 
                AttachOptions.Shrink, 0, 0);
                
            username_entry.Text = account.UserName ?? String.Empty;
            password_entry.Text = account.Password ?? String.Empty;
        }
        
        protected override void OnDestroyed ()
        {
            if (save_on_edit) {
                Save ();
            }
            
            base.OnDestroyed ();
        }
        
        public void AddSignUpButton ()
        {
            if (signup_button != null) {
                return;
            }
            
            Resize (3, 2);
            signup_button = new LinkButton (account.SignUpUrl, Catalog.GetString ("Sign up for Last.fm"));
            signup_button.Show ();
            Attach (signup_button, 1, 2, 2, 3, AttachOptions.Shrink, AttachOptions.Shrink, 0, 0);
        }
        
        public void Save ()
        {
            if (account.UserName != username_entry.Text.Trim () || account.Password != password_entry.Text.Trim ()) {
                account.UserName = username_entry.Text.Trim ();
                account.Password = password_entry.Text.Trim ();
                account.Save ();
            }
        }

        public bool SaveOnEdit {
            get { return save_on_edit; }
            set { save_on_edit = value; }
        }
        
        //enable save on Enter and destruction of the parentDialog.
        public void SaveOnEnter (Gtk.Dialog parentDialog) 
        {
           save_on_enter = true;
           this.parentDialog = parentDialog;
        }
                
        public string Username {
            get { return username_entry.Text; }
        }
        
        public string Password {
            get { return password_entry.Text; }
        }
    }
}

