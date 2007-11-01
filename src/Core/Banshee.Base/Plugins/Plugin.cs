/***************************************************************************
 *  Plugin.cs
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
using Banshee.Base;

namespace Banshee.Plugins
{
    public class InvalidPluginException : ApplicationException
    {
        public InvalidPluginException(string message) : base(message)
        {
        }
    }

    public abstract class Plugin : IPlugin
    {
        private bool initialized;
        private bool broken;
        private bool dispose_requested;
    
        protected abstract string ConfigurationName { get; }
        public abstract string DisplayName { get; }
        public abstract string Description { get; }
        public abstract string [] Authors { get; }
        
        public Plugin()
        {
            broken = false;
        }
        
        internal void Initialize()
        {
            if(broken) {
                return;
            }
            
            if(initialized) {
                Dispose();
            }
            
            try {
                dispose_requested = false;
                PluginInitialize();
                initialized = true;
                if(!Globals.UIManager.IsInitialized) {
                    Globals.UIManager.Initialized += OnUIManagerInitialized;
                } else {
                    InterfaceInitialize();
                }
            } catch(Exception e) {
                LogCore.Instance.PushWarning(String.Format("Could not initialize plugin `{0}'", Name),
                    e.Message, false);
                LogCore.Instance.PushDebug(String.Format("Exception caught while initializing plugin `{0}'", Name),
                    e.ToString (), false);
                broken = true;
                initialized = false;
            }
        }
        
        public void Dispose()
        {
            if(initialized && !broken) {
                dispose_requested = true;
                PluginDispose();
                initialized = false;
            }
        }
        
        protected abstract void PluginInitialize();
        
        protected virtual void PluginDispose()
        {
        }
        
        protected virtual void InterfaceInitialize()
        {
        }

        public virtual Gtk.Widget GetConfigurationWidget()
        {
            return null;
        }

        private void OnUIManagerInitialized(object o, EventArgs args)
        {
            Globals.UIManager.Initialized -= OnUIManagerInitialized;
                
            if(initialized) {
                InterfaceInitialize();
            }
        }
        
        protected bool DisposeRequested {
            get { return dispose_requested; }
        }
        
        internal bool HasConfigurationWidget {
            get { return ReflectionUtil.IsVirtualMethodImplemented(GetType(), "GetConfigurationWidget"); }
        }
        
        internal bool Broken {
            get { return broken; }
        }
        
        public string Name {
            get { return ConfigurationName; }
        }
        
        public string ConfigurationNamespace {
            get { return "plugins." + StringUtil.CamelCaseToUnderCase(ConfigurationName); }
        }
        
        public bool Initialized {
            get { return initialized; }
        }
    }
}
