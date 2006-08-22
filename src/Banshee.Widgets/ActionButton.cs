
/***************************************************************************
 *  ActionButton.cs
 *
 *  Copyright (C) 2005 Novell
 *  Written by Aaron Bockover (aaron@aaronbock.net)
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

namespace Banshee.Widgets
{
    public class ActionButton : Button
    {
        private static ArrayList buttons = new ArrayList();

        private Action action;
        private HBox box = new HBox();
        private Image image = new Image();
        private Label label = new Label();
        private Gdk.Pixbuf pixbuf;
        private IconSize default_size;
        
        public ActionButton(Action action) : this(action, IconSize.SmallToolbar)
        {
        }
        
        public ActionButton(Action action, IconSize defaultSize) : base()
        {
            this.action = action;
            default_size = defaultSize;
            
            BuildButton();
            Sync();
            
            buttons.Add(this);
            action.ConnectProxy(this);
        }
        
        private void BuildButton()
        {
            box.Spacing = 5;
            box.PackStart(image, false, false, 0);
            box.PackStart(label, true, true, 0);
            Add(box);
            box.ShowAll();
        }
        
        public void Sync()
        {
            if(action.StockId == null && pixbuf == null) {
                image.Hide();
            } else if(pixbuf == null) {
                image.Stock = action.StockId;
                image.IconSize = (int)default_size;
                image.Show();
            }
            
            label.Text = action.ShortLabel;
            
            Sensitive = action.IsSensitive;
            Visible = action.IsVisible;
        }
        
        public uint Padding {
            get {
                return box.BorderWidth;
            }
            
            set {
                box.BorderWidth = value;
            }
        }
        
        public new Action Action {
            get {
                return action;
            }
        }
        
        public IconSize IconSize {
            get {
                return (IconSize)image.IconSize;
            }
            
            set {
                image.IconSize = (int)value;
            }
        }
        
        public bool LabelVisible {
            get {
                return label.Visible;
            }
            
            set {
                label.Visible = value;
            }
        }
        
        public Gdk.Pixbuf Pixbuf {
            set {
                pixbuf = value;
                
                if(pixbuf == null) {
                    Sync();
                } else {
                    image.Pixbuf = pixbuf;
                    image.Show();
                }
            }
        }
        
        public static void SyncButtons()
        {
            foreach(ActionButton button in buttons) {
                button.Sync();
            }
        }
    }
    
    public class ActionToggleButton : ToggleButton
    {
        private static ArrayList buttons = new ArrayList();

        private Action action;
        private HBox box = new HBox();
        private Image image = new Image();
        private IconSize default_size;
        
        public ActionToggleButton(Action action) : this(action, IconSize.SmallToolbar)
        {
        }
    
        public ActionToggleButton(Action action, IconSize defaultSize) : base()
        {
            this.action = action;
            
            default_size = defaultSize;
            
            BuildButton();
            Sync();
            
            buttons.Add(this);
            action.ConnectProxy(this);
        }
        
        private void BuildButton()
        {
            box.Spacing = 5;
            box.PackStart(image, false, false, 0);
            Add(box);
            box.ShowAll();
        }
        
        public void Sync()
        {
            if(action.StockId == null) {
                image.Hide();
            } else {
                image.Stock = action.StockId;
                image.IconSize = (int)default_size;
                image.Show();
            }
            
            Sensitive = action.IsSensitive;
            Visible = action.IsVisible;
        }
        
        public uint Padding {
            get {
                return box.BorderWidth;
            }
            
            set {
                box.BorderWidth = value;
            }
        }
        
        public new Action Action {
            get {
                return action;
            }
        }        
        
        public IconSize IconSize {
            get {
                return (IconSize)image.IconSize;
            }
            
            set {
                image.IconSize = (int)value;
            }
        }
        
        public static void SyncButtons()
        {
            foreach(ActionButton button in buttons) {
                button.Sync();
            }
        }
    }
}
