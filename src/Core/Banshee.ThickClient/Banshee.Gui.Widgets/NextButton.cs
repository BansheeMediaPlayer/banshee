//
// NextButton.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using Gtk;

using Banshee.Gui;
using Hyena.Widgets;

namespace Banshee.Gui.Widgets
{
    public class NextButton : MenuButton
    {
        PlaybackShuffleActions shuffle_actions;
        Widget button;
        bool with_repeat_actions;

        public NextButton (InterfaceActionService actionService) : this (actionService, false)
        {
        }

        public NextButton (InterfaceActionService actionService, bool withRepeatActions)
        {
            with_repeat_actions = withRepeatActions;
            shuffle_actions = actionService.PlaybackActions.ShuffleActions;

            button = actionService.PlaybackActions["NextAction"].CreateToolItem ();
            var menu = shuffle_actions.CreateMenu (with_repeat_actions);
            Construct (button, menu, true);

            TooltipText = actionService.PlaybackActions["NextAction"].Tooltip;

            shuffle_actions.Changed += OnActionsChanged;
        }

        private void OnActionsChanged (object o, EventArgs args)
        {
            if (!shuffle_actions.Sensitive) {
                Menu.Deactivate ();
            }

            Menu = shuffle_actions.CreateMenu (with_repeat_actions);

            ToggleButton.Sensitive = shuffle_actions.Sensitive;
            if (Arrow != null) {
                Arrow.Sensitive = shuffle_actions.Sensitive;
            }
        }
    }
}
