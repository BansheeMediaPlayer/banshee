
/***************************************************************************
 *  MMKeysPlugin.cs
 *
 *  Written by Danilo Reinhardt (danilo.reinhardt@gmx.net)
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
using System.IO;
using Gtk;
using Gdk;
using Mono.Unix;

using Banshee.Base;

namespace Banshee.Plugins.MMKeys
{
	public class MMKeysPlugin : Banshee.Plugins.Plugin
	{
	   protected override string ConfigurationName { get { return "MMKeys"; } }
        public override string DisplayName { get { return Catalog.GetString("Multimedia Keys"); } }
	
		public override string Description {
            get {
                return Catalog.GetString(
                    "Adds support for multimedia keys configured through Gnome."
                );
            }
        }

        public override string [] Authors {
            get {
                return new string [] {
                    "Danilo Reinhardt"
                };
            }
        }

        private SpecialKeys special_keys;
		
		protected override void PluginInitialize()
        {
            special_keys = new SpecialKeys();
            special_keys.Delay = new TimeSpan(500 * TimeSpan.TicksPerMillisecond);

            special_keys.RegisterHandler(OnSpecialKeysPressed, 
                SpecialKey.AudioPlay,
                SpecialKey.AudioPrev,
                SpecialKey.AudioNext
            );
        }
        
        private void OnSpecialKeysPressed(object o, SpecialKey key)
        {
           switch(key) {
                case SpecialKey.AudioPlay:
                    Globals.ActionManager["PlayPauseAction"].Activate();
                    break;
                case SpecialKey.AudioNext:
                    Globals.ActionManager["NextAction"].Activate();
                    break;
                case SpecialKey.AudioPrev:
                    Globals.ActionManager["PreviousAction"].Activate();
                    break;
            }
        }
        
        protected override void PluginDispose()
        {
            special_keys.UnregisterHandler(OnSpecialKeysPressed, 
                SpecialKey.AudioPlay, 
                SpecialKey.AudioPrev,
                SpecialKey.AudioNext);
            special_keys.Dispose();
        }
        
        public override Gtk.Widget GetConfigurationWidget()
        {
            return new MMKeysConfigPage();
        }
	}
}
