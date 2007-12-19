
/***************************************************************************
 *  HighlightMessageArea.cs
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
using Gtk;

namespace Banshee.Widgets
{
    public class HighlightMessageArea : Alignment
    {   
        private HBox box;
        private Image image;
        private Label label;
        private Button button;
        private Button close_button;
        
        public event EventHandler ButtonClicked;
        
        public HighlightMessageArea() : base(0.0f, 0.5f, 1.0f, 0.0f)
        {
            HBox shell_box = new HBox();
            shell_box.Spacing = 10;
        
            box = new HBox();
            box.Spacing = 10;
            
            image = new Image();
            
            label = new Label();
            label.Xalign = 0.0f;
            label.Show();
            
            button = new Button();
            button.Clicked += delegate(object o, EventArgs args) {
                EventHandler handler = ButtonClicked;
                if(handler != null) {
                    handler(this, new EventArgs());
                }
            };
            
            box.PackStart(image, false, false, 0);
            box.PackStart(label, true, true, 0);
            box.Show();
            
            close_button = new Button(new Image(Stock.Close, IconSize.Menu));
            close_button.Relief = ReliefStyle.None;
            close_button.Clicked += delegate(object o, EventArgs args) {
                Hide();
            };
            close_button.ShowAll();
            close_button.Hide();
            
            shell_box.PackStart(box, true, true, 0);
            shell_box.PackStart(close_button, false, false, 0);
            shell_box.Show();
            
            Add(shell_box);
            
            EnsureStyle();
        }

        protected override bool OnExposeEvent(Gdk.EventExpose evnt)
        {
            GdkWindow.DrawRectangle(Style.BackgroundGC(StateType.Normal), true, Allocation);

            GdkWindow.DrawRectangle (Style.BackgroundGC(StateType.Selected), false,
                Allocation.X, Allocation.Y, Allocation.Width - 1, Allocation.Height - 1);
                
            base.OnExposeEvent(evnt);
            return true;
        }
      
        private bool changing_style = false;
        protected override void OnStyleSet(Gtk.Style previousStyle)
        {
            if(changing_style) {
                return;
            }
            
            changing_style = true;
            Window win = new Window(WindowType.Popup);
            win.Name = "gtk-tooltips";
            win.EnsureStyle();
            Style = win.Style;
            changing_style = false;
        }
        
        public string ButtonLabel {
            set {
                bool should_remove = false;
                foreach(Widget child in box.Children) {
                    if(child == button) {
                        should_remove = true;
                        break;
                    }
                }
                
                if(should_remove && value == null) {
                    box.Remove(button);
                }
                
                if(value != null) {
                    button.Label = value;
                    button.Show();
                    box.PackStart(button, false, false, 0);
                }
                
                QueueDraw();
            }
        }
        
        public bool ShowCloseButton {
            set {
                close_button.Visible = value;
                QueueDraw();
            }
        }
        
        public bool ButtonUseStock {
            set {
                button.UseStock = value;
                QueueDraw();
            }
        }
        
        public string Message {
            set {
                label.Markup = value;
                QueueDraw();
            }
        }
        
        public Gdk.Pixbuf Pixbuf {
            set {
                image.Pixbuf = value;
                image.Visible = value != null;
                QueueDraw();
            }
        }
    }
}
