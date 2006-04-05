/***************************************************************************
 *  ErrorListDialog.cs
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
using Mono.Unix;
using Gtk;
using Glade;

namespace Banshee.Gui
{
    public class ErrorListDialog : GladeDialog
    {
        [Widget] private Label header_label;
        [Widget] private Label message_label;
        [Widget] private TreeView list_view;
        [Widget] private Image icon_image;
        
        private ListStore simple_model;
        
        public ErrorListDialog() : base("ErrorListDialog")
        {
            list_view.SetSizeRequest(-1, 100);
        }
        
        public void AddButton(string message, ResponseType response)
        {
            Dialog.AddButton(message, response);
        }
        
        public void AddStockButton(string stock, ResponseType response)
        {
            Button button = new Gtk.Button(stock);
            button.UseStock = true;
            button.Show();
            Dialog.AddActionWidget(button, response);
        }
        
        public string Header {
            set { 
                Dialog.Title = value;
                header_label.Markup = String.Format("<b><big>{0}</big></b>", 
                    GLib.Markup.EscapeText(value));
            }
        }
        
        public void AppendString(string item)
        {
            if(list_view.Model == null) {
                CreateSimpleModel();
            }
            
            if(list_view.Model != simple_model) {
                throw new ApplicationException("A custom model is in use");
            }
            
            simple_model.AppendValues(item);
        }
        
        private void CreateSimpleModel()
        {
            simple_model = new ListStore(typeof(string));
            list_view.Model = simple_model;
            list_view.AppendColumn("Error", new CellRendererText(), "text", 0);
            list_view.HeadersVisible = false;
        }
        
        public string Message {
            set { message_label.Text = value; }
        }
        
        public string IconName {
            set { icon_image.SetFromIconName(value, IconSize.Dialog); }
        }
        
        public TreeView ListView {
            get { return list_view; }
        }
    }
}
