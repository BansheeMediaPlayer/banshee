// 
// SortPreferenceMenuButton.cs
//  
// Author:
//       Mike Urbanski <michael.c.urbanski@gmail.com>
// 
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

using Gtk;
using System;

using Mono.Unix;

using Hyena.Widgets;

using Banshee.Gui;
using Banshee.ServiceStack;

using Banshee.Paas.Data;

namespace Banshee.Paas.MiroGuide.Gui
{
    public class SortPreferenceActionButton : EventBox
    {
        private MenuButton button;
        private HBox box = new HBox ();
        private Label label = new Label ();
        private MiroGuideActions actions;
        
        public SortPreferenceActionButton ()
        {
            UIManager ui_manager = ServiceManager.Get <InterfaceActionService> ().UIManager;        
            actions = ServiceManager.Get <InterfaceActionService> ().FindActionGroup ("MiroGuide") as MiroGuideActions;

            actions.SortPreferenceChanged += OnActionChanged;
            
            box.Spacing = 0;
            
            label.UseUnderline = true;
            box.PackStart (label, true, true, 6);
            
            button = new MenuButton (box, ui_manager.GetWidget ("/MiroGuideSortPreferencePopup") as Menu, true);
            Add (button);
            
            UpdateButton ();

            ShowAll ();
        }

        private void UpdateButton ()
        {
            button.Sensitive = label.Sensitive = actions.Sensitive;
            label.TextWithMnemonic = actions.ActiveSortAction.Label;        
        }

        private void OnActionChanged (object o, EventArgs args)
        {
            UpdateButton ();
        }
    }
}
