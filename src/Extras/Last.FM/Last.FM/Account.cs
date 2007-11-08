/***************************************************************************
 *  Account.cs
 *
 *  Copyright (C) 2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@abock.org>
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
using System.Text;
using System.Security.Cryptography;

using Gnome.Keyring;

namespace Last.FM
{
    public delegate void AccountEventHandler(AccountEventArgs args);
    
    public class AccountEventArgs : EventArgs
    {
        private Exception exception;

        public bool Success {
            get { return exception == null; }
        }

        public Exception Exception {
            get { return exception; }
            internal set { exception = value; }
        }
    }

    public static class Account
    {
        private const string keyring_item_name = "Last.fm Account";
        private static Hashtable request_attributes = new Hashtable();

        private static string username;
        private static string password;
        
        public static event AccountEventHandler LoginRequestFinished;
        public static event AccountEventHandler LoginCommitFinished;

        static Account()
        {
            request_attributes["name"] = keyring_item_name;
        }
        
        public static void RequestLoginSync()
        {    
            foreach(ItemData result in Ring.Find(ItemType.NetworkPassword, request_attributes)) {
                if(result.Attributes["name"] as string != keyring_item_name) {
                    continue;
                }

                username = ((string)result.Attributes["user"]).Trim();
                password = result.Secret.Trim();

                return;
            }

            throw new ApplicationException("Last.fm account information not found in default keyring");
        }

        public static void CommitLoginSync()
        {
            Hashtable update_request_attributes = request_attributes.Clone() as Hashtable;
            update_request_attributes["user"] = username;

            ItemData [] items = Ring.Find(ItemType.NetworkPassword, request_attributes);
            string keyring = Ring.GetDefaultKeyring();

            if(items.Length == 0) {
                Ring.CreateItem(keyring, ItemType.NetworkPassword, keyring_item_name, 
                   update_request_attributes, password, true);
            } else {
                Ring.SetItemInfo(keyring, items[0].ItemID, ItemType.NetworkPassword, 
                    keyring_item_name, password);
                Ring.SetItemAttributes(keyring, items[0].ItemID, update_request_attributes);
            }
        }
        
        private static void RequestLoginAsync(AccountEventArgs args)
        {
            try {
                RequestLoginSync();
            } catch(Exception e) {
                args.Exception = e;
            }
            
            AccountEventHandler handler = LoginRequestFinished;
            if(handler != null) {
                handler(args);
            }
        }
        
        private static void CommitLoginAsync(AccountEventArgs args) 
        {
            try {
                CommitLoginSync();
            } catch(Exception e) {
                args.Exception = e;
            }
            
            AccountEventHandler handler = LoginCommitFinished;
            if(handler != null) {
                handler(args);
            }
        }

        public static void RequestLogin()
        {
            AccountEventHandler handler = new AccountEventHandler(RequestLoginAsync);
            handler.BeginInvoke(new AccountEventArgs(), null, null);
        }
        
        public static void CommitLogin()
        {
            AccountEventHandler handler = new AccountEventHandler(CommitLoginAsync);
            handler.BeginInvoke(new AccountEventArgs(), null, null);
        }
        
        public static void CommitLogin(string username, string password)
        {
            Username = username;
            Password = password;
            CommitLogin();
        }
        
        public static void SignUp()
        {
            Browser.Open("http://www.last.fm/join");
        }
        
        public static void VisitUserProfile(string username)
        {
            Browser.Open(String.Format("http://last.fm/user/{0}", username));
        }
        
        public static void VisitUserProfile()
        {
            VisitUserProfile(Username);
        }
        
        public static void VisitHomePage()
        {
            Browser.Open("http://last.fm/");
        }
        
        public static string Username {
            get { return username; }
            set { username = value; }
        }
        
        public static string Password {
            get { return password; }
            set { password = value; }
        }

        public static string Md5Password {
            get { return password == null ? null : Md5Encode (password); }
            set { password = value; }
        }

        public static string Md5Encode (string text)
        {
            if(text == null || text == String.Empty)
                return String.Empty;
                
            MD5 md5 = MD5.Create ();
            byte[] hash = md5.ComputeHash (Encoding.ASCII.GetBytes (text));

            StringBuilder shash = new StringBuilder ();
            for (int i = 0; i < hash.Length; ++i) {
                shash.Append (hash[i].ToString ("x2"));
            }

            return shash.ToString ();
        }
    }
}

