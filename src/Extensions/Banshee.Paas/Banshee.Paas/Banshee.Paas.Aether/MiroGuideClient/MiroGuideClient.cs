// 
// MiroGuideClient.cs
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

// This and AetherRequest are still a bit of an experiment.  This should be 
// implemented as a more formal state machine.  This will not be used by users for sometime.

using System;
using System.IO;
using System.Net;
using System.Web;

using System.Linq;
using System.Text;

using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

using Hyena;
using Hyena.Json;

using Migo2.Async;

using Banshee.ServiceStack;

using Banshee.Paas.Data;
using Banshee.Paas.Aether;

namespace Banshee.Paas.Aether.MiroGuide
{   
    public sealed class MiroGuideClient : AetherClient
    {
        private MiroGuideAccountInfo account;

        private string client_id;
        private MiroGuideClientInfo client_info;
        
        private string session_id;
        private Uri aether_service_uri;

        private AsyncStateManager asm;        
        
        private AetherRequest request;

        public EventHandler<RequestCompletedEventArgs> Completed;

        public EventHandler<ClientIDChangedEventArgs> ClientIDChanged;

        public EventHandler<DownloadRequestEventArgs> DownloadCancelled;
        public EventHandler<DownloadRequestEventArgs> DownloadRequested;

        public EventHandler<GetChannelsCompletedEventArgs> GetChannelsCompleted;

        public EventHandler<SubscriptionRequestedEventArgs> SubscriptionRequested;
        
        public MiroGuideAccountInfo Account {
            get { return Account; }
        }

        public string ClientID { 
            get {
                return client_id;
            }
            
            set { client_id = value; }
        }

        public ICredentials Credentials { get; set; }

        public string ServiceUri {
            get { return aether_service_uri.AbsoluteUri; }
            
            set { 
                Uri tmp = new Uri (value.Trim ().TrimEnd ('/'));
                
                if (tmp.Scheme != "http" && tmp.Scheme != "https") {
                    throw new UriFormatException ("Scheme must be either http or https.");
                }

                aether_service_uri = tmp;
            }
        }
        
        public string SessionID { 
            get {        
                return session_id;
            }
        }

        public string UserAgent { get; set; }

        private MiroGuideClientInfo ClientInfo {
            get {
                if (client_info == null) {
                    if (!String.IsNullOrEmpty (client_id)) {
                        client_info = MiroGuideClientInfo.Provider.FetchFirstMatching ("ClientID = ?", client_id);
                    } else {
                        throw new InvalidOperationException ("Cannot access ClientInfo with null or empty client_id");
                    }
                }

                return client_info;
            }
            
            set {
                if (value == null) {
                    client_info = null;
                } else {
                    client_id = value.ClientID;
                    client_info = value;                    
                }
            }
        }

        public MiroGuideClient (MiroGuideAccountInfo account)
        {
            if (account == null) {
                throw new ArgumentNullException ("account");    
            }
            
            asm = new AsyncStateManager ();
            
            this.account = account;
            this.account.Updated += (sender, e) => {
                lock (SyncRoot) {
                    if (!String.IsNullOrEmpty (account.ServiceUri)) {
                        ServiceUri = account.ServiceUri;                
                    }
                    
                    client_id  = account.ClientID;
                    session_id = null;
                }
            };
            
            if (!String.IsNullOrEmpty (account.ServiceUri)) {
                ServiceUri = account.ServiceUri;                
            }

            client_id = account.ClientID;
        }

        public void UpdateAsync ()
        {
            lock (SyncRoot) {
                if (asm.Busy) {
                    return;
                }

                BeginRequest (
                    CreateGetRequestState (
                        MiroGuideClientMethod.RequestDeltas, "/aether/api/deltas/", null, 
                        (ServiceFlags.RequireAuth | ServiceFlags.RequireClientID), null, null
                    ), true
                );
            }
        }

        public void Unsubscribe (IEnumerable<PaasChannel> channels)
        {
            JsonObject json_data;
            JsonArray request_data = new JsonArray ();
            
            foreach (var channel in channels.Where (c => c.ClientID == (int)AetherClientID.MiroGuide)) {
                request_data.Add ((int)channel.ExternalID);
            }

            json_data = new JsonObject ();
            json_data["channels"] = request_data;

            if (request_data.Count > 0) {            
                lock (SyncRoot) {
                    BeginRequest (
                        CreatePostRequestState (
                            MiroGuideClientMethod.Unsubscribe, "/aether/api/unsubscribe/", 
                            "application/x-www-form-urlencoded", null,
                            String.Format ("channels={0}", SerializeJson (json_data)),
                            ServiceFlags.RequireAuth, null, null
                        ), true
                    );
                }                            
            }
        }

        public void CancelAsync ()
        {
            lock (SyncRoot) {
                if (asm.Busy && asm.SetCancelled ()) {
                    if (request != null) {
                        request.CancelAsync ();
                    }
                }
            }
        }

        public void GetChannelsAsync (MiroGuideFilterType filterType, 
                                      string filterValue, 
                                      MiroGuideSortType sortType, 
                                      bool reverse, 
                                      uint limit, uint offset)
        {
            GetChannelsAsync (filterType, filterValue, sortType, reverse, limit, offset, null);
        }
        
        public void GetChannelsAsync (MiroGuideFilterType filterType,
                                      string filterValue, 
                                      MiroGuideSortType sortType, 
                                      bool reverse, 
                                      uint limit, 
                                      uint offset, 
                                      object userState)
        {
            if (String.IsNullOrEmpty (filterValue)) {
                return;
            }

            GetChannelsAsync (new SearchContext (filterType, filterValue, sortType, reverse, limit, offset), userState);
        }

        public void GetChannelsAsync (SearchContext context)
        {
            GetChannelsAsync (context, null);
        }
        
        public void GetChannelsAsync (SearchContext context, object userState)
        {
            if (context == null) {
                throw new ArgumentNullException ("context");
            }
            
            NameValueCollection nvc = new NameValueCollection ();

            nvc.Add ("datatype", "json");

            nvc.Add ("filter", ToQueryPart (context.FilterType));
            nvc.Add ("filter_value", context.FilterValue);
            nvc.Add ("sort", String.Format ("{0}{1}", ((context.Reverse) ? "-" : String.Empty), ToQueryPart (context.SortType)));
            
            nvc.Add ("limit", context.Limit.ToString ());
            nvc.Add ("offset", (context.Offset+context.Count).ToString ());

            lock (SyncRoot) {
                if (asm.Busy) { // Remove!!!  Allow requests to be queued in the future.
                    return;
                }
                
                BeginRequest (
                    CreateGetRequestState (
                        MiroGuideClientMethod.GetChannels, "/api/get_channels", nvc,
                        ServiceFlags.None, context, userState
                    ), true
                );
            }         
        }

        public void RequestSubsubscription (Uri uri)
        {
            if (uri == null) {
                throw new ArgumentNullException ("uri");
            }

            OnSubscriptionRequested (uri);
        }

        public void RequestSubsubscription (IEnumerable<Uri> uris)
        {
            if (uris == null) {
                throw new ArgumentNullException ("uris");
            }

            OnSubscriptionRequested (uris);
        }

        private void GetSessionAsync (MiroGuideRequestState callingMethodState)
        {
            if (callingMethodState == null) {
                throw new ArgumentNullException ("callingMethodState");
            }
            
            lock (SyncRoot) {
                BeginRequest (
                    CreatePostRequestState (
                        MiroGuideClientMethod.GetSession, "/aether/api/auth/", "application/x-www-form-urlencoded",
                        null, String.Format ("username={0}&password_hash={1}", account.Username, account.PasswordHash),
                        ServiceFlags.None, null, null, callingMethodState
                    ), false
                );
            }
        }

        private void GetClientIDAsync (MiroGuideRequestState callingMethodState)
        {
            if (callingMethodState == null) {
                throw new ArgumentNullException ("callingMethodState");
            }

            lock (SyncRoot) {
                BeginRequest (
                    CreateGetRequestState (
                        MiroGuideClientMethod.RegisterClient, "/aether/api/register/", null, 
                        ServiceFlags.RequireAuth, null, null, callingMethodState
                    ), false
                );
            }
        }

        private AetherRequest CreateRequest ()
        {
            AetherRequest req = new AetherRequest () {
                Timeout = (30 * 1000),
                Credentials = Credentials,
                UserAgent = UserAgent
            };

            req.Completed += OnRequestCompletedHandler;

            return req;
        }
        
        private void BeginRequest (MiroGuideRequestState state, bool changeState)
        {            
            if (changeState) {
                asm.SetBusy ();
                OnStateChanged (AetherClientState.Idle, AetherClientState.Busy);
            }
            
            if (asm.Cancelled) {
                state = GetHead (state);
                state.Cancelled = true;
                Complete (state);
                return;
            }

            try {                
                if (state.ServiceFlags != ServiceFlags.None) {
                    if ((state.ServiceFlags & ServiceFlags.RequireAuth) != 0) {
                        if (String.IsNullOrEmpty (SessionID)) {                        
                            GetSessionAsync (state);
                            return;
                        } else {
                            state.AddParameter ("session", SessionID);
                        }
                    }

                    if ((state.ServiceFlags & ServiceFlags.RequireClientID) != 0) {
                        if (String.IsNullOrEmpty (ClientID)) {
                            GetClientIDAsync (state);
                            return;
                        } else {
                            state.AddParameter ("clientid", ClientID);
                            state.AddParameter ("since", ClientInfo.LastUpdated);
                        }
                    }
                }            
    
                request = CreateRequest ();
    
                switch (state.HttpMethod) {
                case HttpMethod.GET:
                    request.BeginGetRequest (state.GetFullUri (), state);
                    break;
                case HttpMethod.POST:
                    request.ContentType = state.ContentType;
                    request.BeginPostRequest (
                        state.GetFullUri (), Encoding.UTF8.GetBytes (state.RequestData), state
                    );
                    break;
                }
            } catch (Exception e) {
                state = GetHead (state);
                state.Error = e;            
                Hyena.Log.Exception (e);
                Complete (state);
            }
        }

        private void ApplyUpdate (AetherDelta delta)
        {
            List<PaasItem> new_items = new List<PaasItem> ();
            List<PaasItem> removed_items = new List<PaasItem> ();
            List<PaasChannel> new_channels = new List<PaasChannel> ();
            List<PaasChannel> removed_channels = new List<PaasChannel> ();

            List<long> new_downloads = new List<long> ();
            List<long> cancelled_downloads = new List<long> ();

            Dictionary<long, PaasChannel> channel_cache = new Dictionary<long, PaasChannel> ();
            
            ServiceManager.DbConnection.BeginTransaction ();
            
            try {
                foreach (PaasChannel pc in delta.NewChannels) {
                    try {
                        PaasChannel.Provider.Save (pc);
                        channel_cache.Add (pc.ExternalID, pc);
                        new_channels.Add (pc);
                    } catch (Exception e) {
                        Hyena.Log.Exception (e);
                        continue;
                    }
                }
                
                foreach (PaasItem pi in delta.NewItems) {
                    try {
                        pi.ChannelID = GetChannelIDFromExternalID (channel_cache, pi.ExternalChannelID);
                        pi.Save ();
                        new_items.Add (pi);
                    } catch (Exception e) {
                        Hyena.Log.Exception (e);
                        continue;
                    }                
                }

                string[] cids = delta.RemovedChannels.Select (id => id.ToString ()).ToArray<string> ();
                string[] iids = delta.RemovedItems.Select (id => id.ToString ()).ToArray<string> ();

                if (cids.Length > 0) {
                    var channels = PaasChannel.Provider.FetchAllMatching (
                        String.Format ("ClientID = {0} AND ExternalID IN ({1})", 
                        (long)AetherClientID.MiroGuide, String.Join (",", cids))
                    );
                    
                    removed_channels.AddRange (channels);

                    foreach (var channel in channels) {
                        removed_items.AddRange (channel.Items);
                    }
    
                    PaasChannel.Provider.Delete (removed_channels);
                }
                
                if (iids.Length > 0) {            
                    removed_items.AddRange (
                        PaasItem.Provider.FetchAllMatching (
                            String.Format ("ClientID = {0} AND ExternalID IN ({1})", 
                            (long)AetherClientID.MiroGuide, String.Join (",", iids))
                        )
                    );                  
                }

                if (removed_items.Count > 0) {
                    PaasItem.Provider.Delete (removed_items);                                        
                }

                new_downloads.AddRange (delta.NewDownloads);
                cancelled_downloads.AddRange (delta.CancelledDownloads);

                ClientInfo.LastUpdated = delta.Updated;
                ClientInfo.Save ();

                ServiceManager.DbConnection.CommitTransaction ();                
            } catch {
                new_channels.Clear ();
                new_items.Clear ();
                removed_items.Clear ();
                removed_channels.Clear ();
                new_downloads.Clear ();
                cancelled_downloads.Clear ();
                
                ServiceManager.DbConnection.RollbackTransaction  ();
                throw;
            } finally {
                channel_cache.Clear ();
            }

            if (new_channels.Count > 0) {
                OnChannelsAdded (new_channels);
            }

            if (new_items.Count > 0) {
                OnItemsAdded (new_items);
            }
            
            if (removed_items.Count > 0) {
                OnItemsRemoved (removed_items);
            }
            
            if (removed_channels.Count > 0) {
                OnChannelsRemoved (removed_channels);
            }

            if (new_downloads.Count > 0) {
                OnDownloadQueued (new_downloads);
            }

            if (cancelled_downloads.Count > 0) {
                OnDownloadCancelled (cancelled_downloads);
            }
        }

        private long GetChannelIDFromExternalID (Dictionary<long, PaasChannel> channel_cache, long external_id)
        {
            PaasChannel c;

            if (channel_cache.ContainsKey (external_id)) {
                c = channel_cache[external_id];
            } else {
                c = PaasChannel.Provider.FetchFirstMatching ("ExternalID = ?", external_id);
                channel_cache.Add (external_id, c);
            }

            return c.DbId;
        }

        private void Complete (MiroGuideRequestState state)
        {
            try {
                switch (state.Method) {
                case MiroGuideClientMethod.GetSession:
                    HandleGetSessionResponse (state.ResponseData);
                    break;
                case MiroGuideClientMethod.RegisterClient:
                    HandleRegisterClientResponse (state.ResponseData);
                    break;
                }
            } catch (Exception e) {
                state = GetHead (state);
                state.Error = e;
                Hyena.Log.Exception (e);                                    
            }
                    
            if (state.CallingState != null) {
                BeginRequest (state.CallingState, false);
                return; 
            } else {              
                switch (state.Method) {
                case MiroGuideClientMethod.RequestDeltas:
                    HandleGetDeltasResponse (state);
                    break;
                case MiroGuideClientMethod.GetChannels:
                    HandleGetChannelsResponse (state);
                    break;
                }                
                
                asm.Reset ();
                OnStateChanged (AetherClientState.Busy, AetherClientState.Idle);
                OnCompleted (state);
            }
        }

        private MiroGuideRequestState CreateGetRequestState (MiroGuideClientMethod acm,
                                                             string path,
                                                             NameValueCollection parameters, 
                                                             ServiceFlags flags,
                                                             object internalState,
                                                             object userState)
        {
            return CreateGetRequestState (
                acm, path, parameters, flags, internalState, userState, null
            );
        }

        private MiroGuideRequestState CreateGetRequestState (MiroGuideClientMethod acm,
                                                             string path,
                                                             NameValueCollection parameters, 
                                                             ServiceFlags flags,
                                                             object internalState,
                                                             object userState,
                                                             MiroGuideRequestState callingState)
        {
            return CreateRequestState (
                acm, path, HttpMethod.GET, null, parameters, null, 
                flags, internalState, userState, callingState
            );
        }

        private MiroGuideRequestState CreatePostRequestState (MiroGuideClientMethod acm,
                                                              string path,
                                                              string contentType,
                                                              NameValueCollection parameters, 
                                                              string requestData,
                                                              ServiceFlags flags,
                                                              object internalState,
                                                              object userState)
        {
            return CreatePostRequestState (
                acm, path, contentType, parameters, requestData, 
                flags, internalState, userState, null
            );
        }

        private MiroGuideRequestState CreatePostRequestState (MiroGuideClientMethod acm,
                                                              string path,
                                                              string contentType,
                                                              NameValueCollection parameters, 
                                                              string requestData,
                                                              ServiceFlags flags,
                                                              object internalState,
                                                              object userState,
                                                              MiroGuideRequestState callingState)
        {
            return CreateRequestState (
                acm, path, HttpMethod.POST, contentType, parameters, requestData, 
                flags, internalState, userState, callingState
            );
        }

        private MiroGuideRequestState CreateRequestState (MiroGuideClientMethod acm,
                                                          string path,
                                                          HttpMethod method,                                                          
                                                          string contentType,
                                                          NameValueCollection parameters, 
                                                          string requestData,
                                                          ServiceFlags flags,
                                                          object internalState,                                                          
                                                          object userState,
                                                          MiroGuideRequestState callingState)
        {
            MiroGuideRequestState state = new MiroGuideRequestState () {
                Method = acm,
                RequestData = requestData,
                HttpMethod = method,
                ContentType = contentType,                
                ServiceFlags = flags,
                UserState = userState,
                InternalState = internalState,
                CallingState = callingState,
                BaseUri = ServiceUri+path                
            };

            if (parameters != null) { 
                state.AddParameters (parameters);
            }

            return state;
        }

        private MiroGuideRequestState GetHead (MiroGuideRequestState state)
        {
            while (state.CallingState != null) {
                state = state.CallingState;
            }

            return state;
        }

        private object DeserializeJson (string response)
        {
            Deserializer d = new Deserializer ();
            d.SetInput (response);
            return d.Deserialize ();            
        }
        
        private string SerializeJson (object json)
        {
            return new Serializer (json).Serialize ();
        }
        
        private void HandleGetSessionResponse (string response)
        {
            JsonObject resp = DeserializeJson (response) as JsonObject;

            if (resp["status"] as string == "success") {
                session_id = resp["session"] as string;
            } else {
                throw new Exception ("Response did not contain session id");
            }
        }

        private void HandleRegisterClientResponse (string response)
        {
            JsonObject resp = DeserializeJson (response) as JsonObject;

            if (resp["status"] as string == "success") {
                client_id = resp["clientid"] as string;
                
                MiroGuideClientInfo mi = new MiroGuideClientInfo () {
                    ClientID = client_id
                };
    
                mi.Save ();

                ClientInfo = mi;
                OnClientIDChanged (mi.ClientID);
            } else {
                throw new Exception ("Response did not contain client id");
            }
        }

        private void HandleGetChannelsResponse (MiroGuideRequestState state)
        {
            Console.WriteLine (state.ResponseData);
            List<MiroGuideChannelInfo> channels = null;
            
            try {
                if (state.Succeeded) {
                    channels = new List<MiroGuideChannelInfo> ();           
                    
                    foreach (JsonObject o in DeserializeJson (state.ResponseData) as JsonArray) {
                        try {
                            channels.Add (new MiroGuideChannelInfo (o));
                        } catch { continue; }
                    }
        
                    SearchContext context = state.InternalState as SearchContext;
                    context.IncrementResultCount ((uint)channels.Count);    
                }
            } catch (Exception e) {
                state.Error = e;
            } finally {
                OnGetChannelsCompleted (state, channels);            
            }            
        }

        // Remove
        private void HandleGetDeltasResponse (MiroGuideRequestState state)
        {
            try {
                if (state.Succeeded) {
                    ApplyUpdate (new AetherDelta (state.ResponseData));
                }
            } catch (Exception e) {
                state.Error = e;
            } finally {
                //OnGetChannelsCompleted (state, channels);            
            }            
        }

        private void OnRequestCompletedHandler (object sender, AetherRequestCompletedEventArgs e)
        {
            lock (SyncRoot) {
                request.Completed -= OnRequestCompletedHandler;
                MiroGuideRequestState state = e.UserState as MiroGuideRequestState;
                
                state.Completed = true;
                state.ResponseData = (e.Data != null) ? Encoding.UTF8.GetString (e.Data) : String.Empty;
                
                if (e.Cancelled || asm.Cancelled) {
                    state = GetHead (state);
                    state.Cancelled = true;
                } else if (e.Timedout) {
                    state = GetHead (state);                    
                    state.Timedout = true;
                } else if (e.Error != null) {
                    state = GetHead (state);
                    state.Error = e.Error;
                    Hyena.Log.Exception (e.Error);                                    
                }
                
                Complete (state);
            }
        }

        private void OnCompleted (MiroGuideRequestState state)
        {
            var handler = Completed;

            RequestCompletedEventArgs e = new RequestCompletedEventArgs (
                state.Error, state.Cancelled, state.Method, state.Timedout, state.UserState
            );

            if (handler != null) {
                EventQueue.Register (new EventWrapper<RequestCompletedEventArgs> (handler, this, e));
            }
        }

        private void OnClientIDChanged (string id)
        {
            var handler = ClientIDChanged;

            if (handler != null) {
                EventQueue.Register (
                    new EventWrapper<ClientIDChangedEventArgs> (handler, this, new ClientIDChangedEventArgs (id))
                );
            }
        }

        private void OnDownloadQueued (IEnumerable<long> ids)
        {
            var handler = DownloadRequested;

            if (handler != null) {
                EventQueue.Register (
                    new EventWrapper<DownloadRequestEventArgs> (handler, this, new DownloadRequestEventArgs (ids))
                );
            }
        }

        private void OnDownloadCancelled (IEnumerable<long> ids)
        {
            var handler = DownloadCancelled;

            if (handler != null) {
                EventQueue.Register (
                    new EventWrapper<DownloadRequestEventArgs> (handler, this, new DownloadRequestEventArgs (ids))
                );
            }
        }

        private void OnGetChannelsCompleted (MiroGuideRequestState state, IEnumerable<MiroGuideChannelInfo> channels)
        {
            var handler = GetChannelsCompleted;
            
            GetChannelsCompletedEventArgs e = new GetChannelsCompletedEventArgs (
                state.InternalState as SearchContext, channels, 
                state.Error, state.Cancelled, state.Timedout, state.UserState
            );

            if (handler != null) {
                EventQueue.Register (new EventWrapper<GetChannelsCompletedEventArgs> (handler, this, e));
            }
        }

        private void OnSubscriptionRequested (Uri uri)
        {
            OnSubscriptionRequested (new SubscriptionRequestedEventArgs (uri));              
        }

        private void OnSubscriptionRequested (IEnumerable<Uri> uris)
        {
            OnSubscriptionRequested (new SubscriptionRequestedEventArgs (uris));     
        }

        private void OnSubscriptionRequested (SubscriptionRequestedEventArgs e)
        {
            var handler = SubscriptionRequested;

            if (handler != null) {
                EventQueue.Register (
                    new EventWrapper<SubscriptionRequestedEventArgs> (handler, this, e)
                );
            }            
        }

        private string ToQueryPart (MiroGuideFilterType type)
        {
            switch (type)
            {
            case MiroGuideFilterType.Category:  return "category";     
            case MiroGuideFilterType.Language:  return "language";      
            case MiroGuideFilterType.Name:      return "name";
            case MiroGuideFilterType.Search:    return "search";                
            case MiroGuideFilterType.Tag:       return "tag";
            default:
                goto case MiroGuideFilterType.Search;
            }
        }

        private string ToQueryPart (MiroGuideSortType type)
        {
            switch (type)
            {
            case MiroGuideSortType.Age:     return "age";     
            case MiroGuideSortType.ID:      return "id";      
            case MiroGuideSortType.Name:    return "name";    
            case MiroGuideSortType.Popular: return "popular"; 
            case MiroGuideSortType.Rating:  return "rating";
            default:
                goto case MiroGuideSortType.Name;
            }
        }
    }
}
