/***************************************************************************
 *  HelixRemotePlayer.cs
 *
 *  Copyright (C) 2006 Novell, Inc
 *  Written by Aaron Bockover <aaron@abock.org>
 *             Timo Hoenig <thoenig@suse.de>, <thoenig@nouse.net>
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
using System.Diagnostics;

using DBus;

namespace Helix
{
    public static class DBusServiceLauncher
    {
        [Interface("org.freedesktop.DBus")]
        internal abstract class DBusProxy
        {
            [Method] public abstract uint StartServiceByName(string name, uint flag);
        }

        public static bool Launch(string serviceName)
        {
            Connection connection = Bus.GetSessionBus();
            Service service = Service.Get(connection, "org.freedesktop.DBus");
            DBusProxy proxy = (DBusProxy)service.GetObject(typeof(DBusProxy), "/org/freedesktop/DBus");

            System.GC.SuppressFinalize(proxy);

            uint ret = proxy.StartServiceByName(serviceName, /* flags, ignored by D-BUS */ 0);
            switch(ret) {
                 case 1: return true;
                 case 2: return false;
                 default: 
                     throw new ApplicationException(String.Format("Could not activate service: {0}", ret));
            }
        }
    }

    [Interface(RemotePlayer.Interface)]
    public abstract class RemotePlayer : IDisposable
    {
        public const string Interface = "org.gnome.HelixDbusPlayer";
        public const string ServiceName = "org.gnome.HelixDbusPlayer";
        public const string ObjectPath = "/org/gnome/HelixDbusPlayer/Player";
        
        public event MessageHandler Message;
        
        private static RemotePlayer instance;
        
        public static RemotePlayer Connect()
        {
            if(instance == null) {
                if(DBusServiceLauncher.Launch(ServiceName)) {
                    Console.WriteLine("Started {0}", ServiceName);
                } else {
                    Console.WriteLine("{0} was already started", ServiceName);
                }

                Service service = Service.Get(Bus.GetSessionBus(), ServiceName);     
                service.SignalCalled += OnSignalCalled;   
                instance = (RemotePlayer)service.GetObject(typeof(RemotePlayer), ObjectPath);
            }
            
            return instance;
        }

        public void Dispose()
        {
            Dispose(true);
        }
        
        public void Dispose(bool disposing)
        {
            if(disposing) {
                try {
                    Shutdown();
                } catch(Exception e) {
                    Console.WriteLine(e);
                }
                
                GC.SuppressFinalize(this);
            }
        }
        
        private static void OnSignalCalled(Signal signal)
        {
            if(signal.PathName != ObjectPath || signal.InterfaceName != Interface 
                || signal.Name != "Message" || instance == null) {
                return;
            }
            
            instance.Signal(signal);
        }
        
        private void Signal(Signal signal)
        {
            Message message = null;
            string current_key = null;
        
            foreach(DBus.DBusType.IDBusType argument in signal.Arguments) {
                if(message == null) {
                    message = new Message((MessageType)argument.Get());
                    continue;
                }
            
                object current = argument.Get();
            
                if(current_key == null) {
                    current_key = (string)current;
                    continue;
                }
                
                message.AppendArgument(current_key, current);
                current_key = null;
            }
            
            if(message != null) {
                OnMessage(message);
            }
        }
        
        private void OnMessage(Message message)
        {
            MessageHandler handler = Message;
            if(handler != null) {
                handler(this, new MessageArgs(message));
            }
        }

        [Method] public abstract bool OpenUri(string uri);
        [Method] public abstract void Play();
        [Method] public abstract void Pause();
        [Method] public abstract void Stop();
        [Method] public abstract bool StartSeeking();
        [Method] public abstract void StopSeeking();
        [Method] public abstract bool SetPosition(uint position);
        [Method] public abstract uint GetPosition();
        [Method] public abstract uint GetLength();
        [Method] public abstract uint GetVolume();
        [Method] public abstract void SetVolume(uint volume);
        [Method] public abstract string GetGroupTitle(uint groupIndex);
        [Method] public abstract void Shutdown();
        [Method] public abstract void Ping();
        [Method] public abstract bool GetIsLive();
    }
    
    public enum ContentState {
        NotLoaded = 0,
        Contacting,
        Loading,
        Stopped,
        Playing,
        Paused
    };
    
    public enum MessageType {
        None = 0,
        VisualState,
        IdealSize,
        Length,
        Title,
        Groups,
        GroupStarted,
        Contacting,
        Buffering,
        ContentConcluded,
        ContentState,
        Status,
        Volume,
        Mute,
        ClipBandwidth,
        Error,
        GotoUrl,
        RequestAuthentication,
        RequestUpgrade,
        HasComponent
    }
    
    public delegate void MessageHandler(object o, MessageArgs args);
    
    public class MessageArgs
    {
        public Message message;
        
        public MessageArgs(Message message)
        {
            this.message = message;
        }

        public Message Message {
            get { return message; }
        }
        
        public MessageType Type {
            get { return message.Type; }
        }
    }
    
    public class Message : IEnumerable
    {
        private MessageType type;
        private Hashtable arguments = new Hashtable();
        
        internal Message(MessageType type)
        {
            this.type = type;
        }
        
        internal void AppendArgument(string key, object value)
        {
            arguments[key] = value;
        }
        
        public IEnumerator GetEnumerator()
        {
            return arguments.Keys.GetEnumerator();
        }
        
        public override string ToString()
        {
            string str = String.Format("{0}\n", type.ToString());
            foreach(string key in this) {
                object value = this[key];
                str += String.Format("  {0} = {1} ({2})\n", key, value, value.GetType());
            }
            
            return str;
        }
        
        public MessageType Type {
            get { return type; }
        }
        
        public object this [string key] {
            get { return arguments[key]; }
        }
    }
}
