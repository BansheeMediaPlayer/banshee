// 
// MiroGuideLoginForm.cs
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

using System;

using Mono.Unix;

using Gtk;

using Banshee.Paas.Aether.MiroGuide;

namespace Banshee.Paas.MiroGuide.Gui
{
    public class MiroGuideLoginForm : Gtk.Table
    {
        private MiroGuideAccountInfo account_info;
        private Entry username_entry;
        private Entry password_entry;
        
        private bool save_on_edit = false;
        
        private bool save_on_enter = false;
        private Gtk.Dialog parentDialog;

        public MiroGuideLoginForm (MiroGuideAccountInfo accountInfo) : base (2, 2, false)
        {
            this.account_info = accountInfo;

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
                
            username_entry.Text = account_info.Username ?? String.Empty;
            password_entry.Text = account_info.PasswordHash ?? String.Empty;
        }
        
        protected override void OnDestroyed ()
        {
            if (save_on_edit) {
                Save ();
            }
            
            base.OnDestroyed ();
        }
        
        public void Save ()
        {
            string trimmed_user = username_entry.Text.Trim ();
            string trimmed_password_hash = password_entry.Text.Trim ();

            if (!Hyena.CryptoUtil.IsMd5Encoded (trimmed_password_hash)) {
                trimmed_password_hash = Hyena.CryptoUtil.Md5Encode (trimmed_password_hash);
            }

            if (account_info.Username != trimmed_user || account_info.PasswordHash != trimmed_password_hash) {
                account_info.Username = trimmed_user;
                account_info.PasswordHash = trimmed_password_hash;
                account_info.Notify ();
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
