/***************************************************************************
 *  EqualizerBandScale.cs
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
using Gtk;

namespace Banshee.Equalizer.Gui
{
    public class EqualizerBandScale : HBox
    {
        private uint frequency;
        private Scale scale;
        private Label label;
        
        public event EventHandler ValueChanged;
    
        public EqualizerBandScale(uint frequency, string labelText)
        {
            this.frequency = frequency;
            
            label = new Label();
            label.Markup = String.Format("<small>{0}</small>", GLib.Markup.EscapeText(labelText));
            label.Xalign = 0.0f;
            label.Yalign = 1.0f;
            label.Angle = 90.0;

            scale = new VScale(new Adjustment(0, -100, 100, 15, 15, 1));
            scale.DrawValue = false;
            scale.Inverted = true;
            scale.ValueChanged += OnValueChanged;
            
            scale.Show();
            label.Show();
            
            PackStart(scale, false, false, 0);
            PackStart(label, false, false, 0);
        }
        
        private void OnValueChanged(object o, EventArgs args)
        {
            EventHandler handler = ValueChanged;
            if(handler != null) {
                handler(this, new EventArgs());
            }
        }
        
        public int Value {
            get { return (int)scale.Value; }
            set { scale.Value = (double)value; }
        }
        
        public bool LabelVisible {
            get { return label.Visible; }
            set { label.Visible = value; }
        }
        
        public uint Frequency {
            get { return frequency; }
        }
    }   
}
