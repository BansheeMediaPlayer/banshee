/***************************************************************************
 *  Manager.cs
 *
 *  Copyright (C) 2005-2006 Novell, Inc.
 *  Written by Aaron Bockover (aaron@abock.org)
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
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using NDesk.DBus;

namespace NetworkManager
{
    public enum State : uint {
        Unknown = 0,
        Asleep,
        Connecting,
        Connected,
        Disconnected
    }
    
    [Interface("org.freedesktop.NetworkManager")]
    public interface INetworkManager
    {
        event StateChangeHandler StateChange;
        State state();
    }
    
    public delegate void StateChangeHandler(State state);
    
    public class Manager
    {
        private const string BusName = "org.freedesktop.NetworkManager";
        private const string ObjectPath = "/org/freedesktop/NetworkManager";

        private INetworkManager manager;
        
        public event StateChangeHandler StateChange;

        public Manager()
        {
            if(!Bus.System.NameHasOwner(BusName)) {
                throw new ApplicationException(String.Format("Name {0} has no owner", BusName));
            }

            manager = Bus.System.GetObject<INetworkManager>(BusName, new ObjectPath(ObjectPath));
            manager.StateChange += OnStateChange;
        }
        
        private void OnStateChange(State state)
        {
            StateChangeHandler handler = StateChange;
            if(handler != null) {
                handler(state);
            }
        }

        public State State {
            get { return manager.state(); }
        }
    }
}

