/***************************************************************************
 *  ToggleStates.cs
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
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
using Mono.Unix;
using Banshee.Widgets;

namespace Banshee.Gui
{
    public class RepeatNoneToggleState : ToggleState
    {
        public RepeatNoneToggleState()
        {
            Icon = Gdk.Pixbuf.LoadFromResource("media-repeat-none.png");
            Label = Catalog.GetString("Repeat None");
        }
    }

    public class RepeatSingleToggleState : ToggleState
    {
        public RepeatSingleToggleState()
        {
            Icon = Gdk.Pixbuf.LoadFromResource("media-repeat-single.png");
            Label = Catalog.GetString("Repeat Single");
        }
    }

    public class RepeatAllToggleState : ToggleState
    {
        public RepeatAllToggleState()
        {
            Icon = Gdk.Pixbuf.LoadFromResource("media-repeat-all.png");
            Label = Catalog.GetString("Repeat All");
        }
    }
    
    public class ShuffleEnabledToggleState : ToggleState
    {
        public ShuffleEnabledToggleState()
        {
            Icon = Gdk.Pixbuf.LoadFromResource("media-playlist-shuffle.png");
            Label = Catalog.GetString("Shuffle");
            MatchActive = true;
            MatchValue = true;
        }
    }
    
    public class ShuffleDisabledToggleState : ToggleState
    {
        public ShuffleDisabledToggleState()
        {
            Icon = Gdk.Pixbuf.LoadFromResource("media-playlist-continuous.png");
            Label = Catalog.GetString("Continuous");
            MatchActive = true;
            MatchValue = false;
        }
    }
}
