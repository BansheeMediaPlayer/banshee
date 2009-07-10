// 
// SyndicationClient.cs
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
using System.Linq;
using System.Collections.Generic;

using Migo2.Async;

using Banshee.ServiceStack;

using Banshee.Paas.Data;

namespace Banshee.Paas.Aether
{
    public class SyndicationClient : AetherClient, IDisposable
    {
        private bool disposed;
        private ChannelUpdateManager channel_manager;
        
        private Dictionary<long, DeletedChannelInfo> deleted;
        private Dictionary<long, ChannelUpdateTask> updating;        
        
        private readonly object sync = new object ();

        public event EventHandler<ItemEventArgs>    ItemsAdded;
        public event EventHandler<ItemEventArgs>    ItemsRemoved;

        public event EventHandler<ChannelEventArgs> ChannelAdded;
        public event EventHandler<ChannelEventArgs> ChannelRemoved;

        public event EventHandler<ChannelEventArgs> ChannelUpdating;
        public event EventHandler<ChannelUpdateCompletedEventArgs> ChannelUpdateCompleted;

        public SyndicationClient ()
        {
            channel_manager = new ChannelUpdateManager (4);
            channel_manager.TaskCompleted += TaskCompletedHandler;
            
            channel_manager.Started += (sender, e) => {
                OnStateChanged (AetherClientState.Idle, AetherClientState.Busy);
            };
            
            channel_manager.Stopped += (sender, e) => {
                OnStateChanged (AetherClientState.Busy, AetherClientState.Idle); 
            };

            channel_manager.TaskStarted += (sender, e) => {
                OnChannelUpdating (e.Task.UserState as PaasChannel);
            };

            deleted = new Dictionary<long, DeletedChannelInfo> ();
            updating = new Dictionary<long, ChannelUpdateTask> ();
        }

        public void Dispose ()
        {
            lock (sync) {
                disposed = true;
            }
            
            channel_manager.Dispose ();
            channel_manager.TaskCompleted -= TaskCompletedHandler;
            
            channel_manager = null;
        }

        public bool IsUpdating (PaasChannel channel)
        {
            lock (sync) {
                return updating.ContainsKey (channel.DbId);
            }
        }


        public void DeleteChannel (PaasChannel channel)
        {
            DeleteChannel (channel, false);
        }

        public void DeleteChannel (PaasChannel channel, bool keepFiles)
        {
            if (channel == null) {
                throw new ArgumentNullException ("channel");
            }
        
            lock (sync) {
                if (!disposed) {
                    if (updating.ContainsKey (channel.DbId)) {
                        deleted.Add (
                            channel.DbId, 
                            new DeletedChannelInfo { KeepFiles = keepFiles, Channel = channel }
                        );
                        
                        updating[channel.DbId].CancelAsync ();
                    } else {
                        DeleteChannelImpl (channel, keepFiles);
                    }
                }
            }
        }

        public void DeleteChannels (IEnumerable<PaasChannel> channels, bool keepFiles)
        {
            if (channels == null) {
                throw new ArgumentNullException ("channels");
            }
        
            foreach (PaasChannel channel in channels) {
                DeleteChannel (channel, keepFiles);
            }
        }

        private void DeleteChannelImpl (PaasChannel channel, bool keepFiles)
        {
            List<PaasItem> items = new List<PaasItem> (channel.Items);

            if (items != null) {
                DeleteItems (items, keepFiles, true);
            }                

            PaasChannel.Provider.Delete (channel);
            OnChannelRemoved (channel);
        }

//        private void DeleteItem (PaasItem item)
//        {
//            DeleteItem (item, false);
//        }

//        private void DeleteItem (PaasItem item, bool keepFile)
//        {
//            if (item == null) {
//                throw new ArgumentNullException ("item");
//            }
//
//            lock (sync) {
//                if (!disposed) {
//                    PaasItem.Provider.Delete (item);
//                    OnItemRemoved (item);                
//                }
//            }
//        }

        private void DeleteItems (IEnumerable<PaasItem> items, bool keepFiles, bool notify)
        {
            if (items == null) {
                throw new ArgumentNullException ("items");
            }

            lock (sync) {
                if (!disposed) {
                    PaasItem.Provider.Delete (items);
                    if (notify) {
                        OnItemsRemoved (items);                                        
                    }
                }
            }            
        }

        public void RemoveItem (PaasItem item)
        {
            RemoveItem (item, false);
        }

        public void RemoveItem (PaasItem item, bool keepFile)
        {
            if (item == null) {
                throw new ArgumentNullException ("item");
            }

            lock (sync) {
                if (!disposed) {
                    item.Active = false;
                    item.Save ();
                    
                    OnItemRemoved (item);
                }
            }
        }

        public void RemoveItems (IEnumerable<PaasItem> items, bool keepFiles)
        {
            if (items == null) {
                throw new ArgumentNullException ("items");
            }

            lock (sync) {
                if (!disposed) {
                    ServiceManager.DbConnection.BeginTransaction ();

                    try {
                        foreach (PaasItem item in items) {
                            item.Active = false;
                            item.Save ();
                        }
                        
                        ServiceManager.DbConnection.CommitTransaction ();
                        OnItemsRemoved (items);
                    } catch (Exception e) {
                        Hyena.Log.Exception (e);
                        ServiceManager.DbConnection.RollbackTransaction ();
                    }
                }
            }            
        }

        public void SubscribeToChannel (string url, DownloadPreference download_pref)
        {
            if (!IsValidUrl (url)) {
                throw new ArgumentException ("Invalid URL!", "url");            
            }
            
            lock (sync) {
                if (disposed) {
                    return;
                }

                PaasChannel channel = PaasChannel.Provider.FetchFirstMatching ("Url = ?", url);
                
                if (channel == null)  {
                    channel = new PaasChannel () {
                        Url = url,
                        DownloadPreference = (int) download_pref,
                        ClientID = (long) AetherClientID.Syndication
                    };
                    
                    channel.Save ();
                    QueueUpdate (channel);
                    OnChannelAdded (channel);
                }
            }                
        }

        public void UpdateAsync ()
        {
            lock (sync) {
                if (disposed) {
                    return;
                }

                QueueUpdate (
                    PaasChannel.Provider.FetchAllMatching ("ClientID = ?", (long) AetherClientID.Syndication)
                );
            }
        }

        public void QueueUpdate (PaasChannel channel)
        {
            lock (sync) {
                if (disposed) {
                    return;
                }
                
                if (!updating.ContainsKey (channel.DbId)) {
                    ChannelUpdateTask task = new ChannelUpdateTask (channel);                    
                    updating[channel.DbId] = task;
                    channel_manager.Add (task);
                }
            }        
        }

        public void QueueUpdate (IEnumerable<PaasChannel> channels)
        {
            lock (sync) {
                if (disposed) {
                    return;
                }

                List<ChannelUpdateTask> tasks = new List<ChannelUpdateTask> ();
                
                foreach (PaasChannel channel in channels.Where (
                    (channel) => 
                        channel.ClientID == (long) AetherClientID.Syndication &&
                        !updating.ContainsKey (channel.DbId)
                    )) {
                    
                    ChannelUpdateTask task = new ChannelUpdateTask (channel);
                    updating[channel.DbId] = task;                   
                    tasks.Add (task);
                }

                channel_manager.Add (tasks);
            }        
        }

        private bool IsValidUrl (string url)
        {
            try {
                Uri uri = new Uri (url);
                
                if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) {
                    return true;
                }
            } catch {}
            
            return false;
        }

        private void TaskCompletedHandler (object sender, TaskCompletedEventArgs<ChannelUpdateTask> e)
        {
            List<PaasItem> new_items = null;
            List<PaasItem> removed_items = null;

            lock (sync) {
                if (disposed) {
                    return;
                }

                // TODO - Check encoding
                ChannelUpdateTask task = e.Task;
                PaasChannel channel = task.Channel;

                if (deleted.ContainsKey (channel.DbId)) {
                    DeleteChannelImpl (channel, deleted[channel.DbId].KeepFiles);
                    
                    deleted.Remove (channel.DbId);
                    updating.Remove (channel.DbId);
                    OnChannelUpdateCompleted (channel, false);
                    return;
                }                

                try {
                    if (e.Error == null && e.State == TaskState.Succeeded && !String.IsNullOrEmpty (task.Result)) {
                        RssParser parser = new RssParser (task.Result);
    
                        ServiceManager.DbConnection.BeginTransaction ();
                        
                        try {
                            parser.UpdateChannel (channel);
                            channel.Save ();
    
                            IEnumerable<PaasItem> local_items = channel.Items;
                            IEnumerable<PaasItem> remote_items = parser.GetItems ();

                            var cmp = new PaasItemEqualityComparer ();

                            new_items = new List<PaasItem> (remote_items.Except (local_items, cmp));
                            removed_items = new List<PaasItem> (
                                local_items.Except (remote_items, cmp).Where ((i) => (!i.IsDownloaded || !i.Active))
                            );

                            if (new_items.Count > 0) {
                                foreach (PaasItem item in new_items) {
                                    item.ChannelID = channel.DbId;
                                    item.Save ();
                                }
                            }
                            
                            if (removed_items.Count > 0) {
                                DeleteItems (removed_items, true, false);
                            }
                        } catch (Exception ex) {
                            ServiceManager.DbConnection.RollbackTransaction ();
                            Hyena.Log.Exception (ex);                           
                            throw;
                        }
    
                        ServiceManager.DbConnection.CommitTransaction ();
        
                        if (new_items != null && new_items.Count > 0) {
                            OnItemsAdded (new_items);                    
                        }
            
                        if (removed_items != null && removed_items.Count > 0) {
                            OnItemsRemoved (removed_items);                    
                        }

                        OnChannelUpdateCompleted (channel, true);
                    }
                } catch (Exception ex) {
                    Hyena.Log.Exception (ex);
                    OnChannelUpdateCompleted (channel, false);
                } finally {
                    updating.Remove (channel.DbId);                       
                }
            }            
        }

        private void OnChannelUpdating (PaasChannel channel)
        {
            var handler = ChannelUpdating;

            if (handler != null) {
                handler (this, new ChannelEventArgs (channel));
            }            
        }

        private void OnChannelUpdateCompleted (PaasChannel channel, bool succeeded)
        {
            var handler = ChannelUpdateCompleted;

            if (handler != null) {
                handler (this, new ChannelUpdateCompletedEventArgs (channel, succeeded));
            }            
        }

        private void OnChannelAdded (PaasChannel channel)
        {
            var handler = ChannelAdded;

            if (handler != null) {
                handler (this, new ChannelEventArgs (channel));
            }            
        }

        private void OnChannelRemoved (PaasChannel channel)
        {
            var handler = ChannelRemoved;

            if (handler != null) {
                handler (this, new ChannelEventArgs (channel));
            }            
        }

        private void OnItemsAdded (IEnumerable<PaasItem> items)
        {
            var handler = ItemsAdded;

            if (handler != null) {
                handler (this, new ItemEventArgs (items));
            }            
        }
/*
        private void OnItemAdded (PaasItem item)
        {
            EventHandler<ItemEventArgs> handler = ItemsAdded;

            if (handler != null) {
                handler (this, new ItemEventArgs (item));
            }            
        }
*/
        private void OnItemRemoved (PaasItem item)
        {
            var handler = ItemsRemoved;

            if (handler != null) {
                handler (this, new ItemEventArgs (item));
            }            
        }

        private void OnItemsRemoved (IEnumerable<PaasItem> items)
        {
            var handler = ItemsRemoved;

            if (handler != null) {
                handler (this, new ItemEventArgs (items));
            }            
        }

        private class DeletedChannelInfo
        {
            public bool KeepFiles { get; set; }
            public PaasChannel Channel { get; set; }
        }
    }
}
