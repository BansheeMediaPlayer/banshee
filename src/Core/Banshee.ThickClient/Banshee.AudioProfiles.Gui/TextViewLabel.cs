/***************************************************************************
 *  TextViewLabel.cs
 *
 *  Copyright (C) 2007 Novell, Inc.
 *  Written by Aaron Bockover <abockover@novell.com>
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

namespace Banshee.AudioProfiles.Gui
{
    public class TextViewLabel : TextView
    {
        public TextViewLabel() : base()
        {
            WrapMode = WrapMode.Word;
            Editable = false;
            CursorVisible = false;
        }
        
        private bool changing_style;
        
        protected override void OnStyleSet(Style previous_style)
        {
            if(changing_style) {
                return;
            }
            
            changing_style = true;
            
            base.OnStyleSet(previous_style);
            
            Window temp = new Window(WindowType.Toplevel);
            temp.EnsureStyle();
            ModifyBase(StateType.Normal, temp.Style.Background(StateType.Normal));
            
            changing_style = false;
        }
        
        public string Text {
            get { return Buffer.Text; }
            set { Buffer.Text = value; }
        }
    }
}
