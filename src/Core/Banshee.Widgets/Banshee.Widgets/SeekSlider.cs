/***************************************************************************
 *  SeekSlider.cs
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

namespace Banshee.Widgets
{
    public class SeekSlider : HScale
    {
        private uint timeout;
        private uint timeout_delay = 500;
        private bool can_seek;
        private bool raise_seek_requested;
        private bool can_set_value;
        private double pressed_x;
        
        public event EventHandler SeekRequested;
        public event EventHandler DurationChanged;

        public SeekSlider() : base(0.0, 0.0, 0.0)
        {
            UpdatePolicy = UpdateType.Continuous;
            DrawValue = false;
            
            raise_seek_requested = true;
            can_set_value = true;
            
            Adjustment.Lower = 0;
            Adjustment.Upper = 0;
            
            SetIdle();
        }
        
        protected override bool OnButtonPressEvent(Gdk.EventButton evnt)
        {
            can_set_value = false;
            if (evnt.Button == 1) {
                pressed_x = evnt.X;
            }
            return base.OnButtonPressEvent(evnt);
        }

        protected override bool OnScrollEvent(Gdk.EventScroll evnt) {
            if (can_seek) {
                SeekValue += (evnt.Direction.Equals (Gdk.ScrollDirection.Down) ? -1 : 1) * 10000; // skip 10s
                OnSeekRequested();
            }
            
            return base.OnScrollEvent (evnt);
        }

        protected override bool OnButtonReleaseEvent(Gdk.EventButton evnt)
        {
            can_set_value = true;
            
            if(timeout > 0) {
                GLib.Source.Remove(timeout);
            }
            
            if (can_seek) {
                if (evnt.Button == 1 && Math.Abs (pressed_x - evnt.X) <= 3.0) {
                    SeekValue = (long) (evnt.X / Allocation.Width * Duration); // seek to clicked position
                }
                OnSeekRequested();
            }
            
            return base.OnButtonReleaseEvent(evnt);
        }
        
        protected override void OnValueChanged()
        {
            if(timeout == 0 && raise_seek_requested) {
                timeout = GLib.Timeout.Add(timeout_delay, OnSeekRequested);
            }
            
            base.OnValueChanged();
        }
        
        private bool OnSeekRequested()
        {   
            if(raise_seek_requested) {
                EventHandler handler = SeekRequested;
                if(handler != null) {
                    handler(this, new EventArgs());
                }
            }
            
            timeout = 0;
            return false;
        }
        
        public long SeekValue {
            get { return (long)Value; }
            set {
                if(!can_set_value) {
                    return;
                }
                
                raise_seek_requested = false;
                
                if(value > Duration) {
                    Duration = Int64.MaxValue;
                    Value = value;
                } else {
                    Value = value;
                }

                raise_seek_requested = true;
            }
        }
        
        public double Duration {
            get { return Adjustment.Upper; }
            set {
                Adjustment.Upper = value;
                EventHandler handler = DurationChanged;
                if(handler != null) {
                    handler(this, new EventArgs());
                }
            }
        }
        
        public void SetIdle()
        {
            Sensitive = false;
            SeekValue = 0;
            Duration = 0;
        }
        
        public uint SeekRequestDelay {
            get { return timeout_delay; }
            set { timeout_delay = value; }
        }
        
        public bool CanSeek {
            get { return can_seek; }
            set { 
                can_seek = value; 
                Sensitive = value;
            }
        }

        public new bool Sensitive {
            get { return base.Sensitive; }
            set {
                if (!value) {
                    can_set_value = true;
                }
                base.Sensitive = value;
            }
        } 
    }
}
