// 
// AetherClient.cs
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

using System;
using System.Collections.Generic;

using Migo2.Async;

using Banshee.Paas.Data;

namespace Banshee.Paas.Aether
{
    public abstract class AetherClient : IDisposable
    {
        private CommandQueue event_queue;
        private readonly object sync = new object ();

        protected object SyncRoot {
            get { return sync; }
        }

        protected CommandQueue EventQueue {
            get { return event_queue; }
        }

        public event EventHandler<ItemEventArgs>    ItemsAdded;
        public event EventHandler<ItemEventArgs>    ItemsRemoved;

        public event EventHandler<ChannelEventArgs> ChannelsAdded;
        public event EventHandler<ChannelEventArgs> ChannelsRemoved;

        public event EventHandler<AetherClientStateChangedEventArgs> StateChanged;

        public AetherClient ()
        {
            event_queue = new CommandQueue ();
        }

        public virtual void Dispose ()
        {
            event_queue.Dispose ();
            event_queue = null;
        }

        protected virtual void OnChannelAdded (PaasChannel channel)
        {
            var handler = ChannelsAdded;

            if (handler != null) {
                event_queue.Register (
                    new EventWrapper<ChannelEventArgs> (handler, this, new ChannelEventArgs (channel))
                );
            }
        }

        protected virtual void OnChannelsAdded (IEnumerable<PaasChannel> channels)
        {
            var handler = ChannelsAdded;

            if (handler != null) {
                event_queue.Register (
                    new EventWrapper<ChannelEventArgs> (handler, this, new ChannelEventArgs (channels))
                );
            }
        }
        
        protected virtual void OnChannelRemoved (PaasChannel channel)
        {
            var handler = ChannelsRemoved;

            if (handler != null) {
                event_queue.Register (
                    new EventWrapper<ChannelEventArgs> (handler, this, new ChannelEventArgs (channel))
                );
            }
        }

        protected virtual void OnChannelsRemoved (IEnumerable<PaasChannel> channels)
        {
            var handler = ChannelsRemoved;

            if (handler != null) {
                event_queue.Register (
                    new EventWrapper<ChannelEventArgs> (handler, this, new ChannelEventArgs (channels))
                );
            }
        }

        protected virtual void OnItemsAdded (IEnumerable<PaasItem> items)
        {
            var handler = ItemsAdded;

            if (handler != null) {
                event_queue.Register (
                    new EventWrapper<ItemEventArgs> (handler, this, new ItemEventArgs (items))
                );
            }
        }

        protected virtual void OnItemRemoved (PaasItem item)
        {
            var handler = ItemsRemoved;

            if (handler != null) {
                event_queue.Register (
                    new EventWrapper<ItemEventArgs> (handler, this, new ItemEventArgs (item))
                );            
            }
        }

        protected virtual void OnItemsRemoved (IEnumerable<PaasItem> items)
        {
            var handler = ItemsRemoved;
            
            if (handler != null) {
                event_queue.Register (
                    new EventWrapper<ItemEventArgs> (handler, this, new ItemEventArgs (items))
                );
            }
        }

        protected virtual void OnStateChanged (AetherClientState oldState, AetherClientState newState)
        {
            var handler = StateChanged;
            
            if (handler != null) {
                event_queue.Register (new EventWrapper<AetherClientStateChangedEventArgs> (
                    handler, this, new AetherClientStateChangedEventArgs (oldState, newState)
                ));
            }
        }
    }
}
