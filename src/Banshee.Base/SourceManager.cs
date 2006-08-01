/***************************************************************************
 *  SourceManager.cs
 *
 *  Copyright (C) 2005 Novell
 *  Written by Aaron Bockover (aaron@aaronbock.net)
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
using Banshee.Base;

namespace Banshee.Sources
{
    public delegate void SourceEventHandler(SourceEventArgs args);
    public delegate void SourceAddedHandler(SourceAddedArgs args);
    
    public class SourceEventArgs : EventArgs
    {
        public Source Source;
    }
    
    public class SourceAddedArgs : SourceEventArgs
    {
        public int Position;
    }
    
    public static class SourceManager 
    {
        private static ArrayList sources = new ArrayList();
        private static Source active_source;
        private static Source default_source;
        
        public static event SourceEventHandler SourceUpdated;
        public static event SourceEventHandler SourceViewChanged;
        public static event SourceAddedHandler SourceAdded;
        public static event SourceEventHandler SourceRemoved;
        public static event SourceEventHandler ActiveSourceChanged;
        public static event TrackEventHandler SourceTrackAdded;
        public static event TrackEventHandler SourceTrackRemoved;
        
        public static void AddSource(Source source, bool isDefault)
        {
            if(source == null) {
                return;
            }
            
            int position = FindSourceInsertPosition(source);
            sources.Insert(position, source);
            
            if(isDefault) {
                default_source = source;
            }

            source.Updated += OnSourceUpdated;
            source.ViewChanged += OnSourceViewChanged;
            source.TrackAdded += OnSourceTrackAdded;
            source.TrackRemoved += OnSourceTrackRemoved;

            ThreadAssist.ProxyToMain(delegate {
                SourceAddedHandler handler = SourceAdded;
                if(handler != null) {
                    SourceAddedArgs args = new SourceAddedArgs();
                    args.Position = position;
                    args.Source = source;
                    handler(args);
                }
            });
        }
        
        public static void AddSource(Source source)
        {
            AddSource(source, false);
        }
        
        public static void RemoveSource(Source source)
        {
            if(source == default_source) {
                default_source = null;
            }
            
            sources.Remove(source);

            source.Updated -= OnSourceUpdated;
            source.ViewChanged -= OnSourceViewChanged;
            source.TrackAdded -= OnSourceTrackAdded;
            source.TrackRemoved -= OnSourceTrackRemoved;

            ThreadAssist.ProxyToMain(delegate {
                if(source == active_source) {
                    SetActiveSource(default_source);
                }
                
                SourceEventHandler handler = SourceRemoved;
                if(handler != null) {
                    SourceEventArgs args = new SourceEventArgs();
                    args.Source = source;
                    handler(args);
                }
            });
        }
        
        public static bool ContainsSource(Source source)
        {
            return sources.Contains(source);
        }
        
        private static void OnSourceUpdated(object o, EventArgs args)
        {
            SourceEventHandler handler = SourceUpdated;
            if(handler != null) {
                SourceEventArgs evargs = new SourceEventArgs();
                evargs.Source = o as Source;
                handler(evargs);
            }
        }
        
        private static void OnSourceViewChanged(object o, EventArgs args)
        {
            SourceEventHandler handler = SourceViewChanged;
            if(handler != null) {
                SourceEventArgs evargs = new SourceEventArgs();
                evargs.Source = o as Source;
                handler(evargs);
            }
        }
        
        private static void OnSourceTrackAdded(object o, TrackEventArgs args)
        {
            TrackEventHandler handler = SourceTrackAdded;
            if(handler != null) {
                handler(o, args);
            }
        }
        
        private static void OnSourceTrackRemoved(object o, TrackEventArgs args)
        {
            TrackEventHandler handler = SourceTrackRemoved;
            if(handler != null) {
                handler(o, args);
            }
        }
        
        private static int FindSourceInsertPosition(Source source)
        {
            for(int i = sources.Count - 1; i >= 0; i--) {
                if((sources[i] as Source).Order == source.Order) {
                    return i;
                } 
            }
        
            for(int i = 0; i < sources.Count; i++) {
                if((sources[i] as Source).Order >= source.Order) {
                    return i;
                }
            }
            
            return sources.Count;    
        }
        
        public static Source DefaultSource {
            get {
                return default_source;
            }
            
            set {
                default_source = value;
            }
        }
        
        public static Source ActiveSource {
            get {
                return active_source;
            }
        }
        
        public static void SetActiveSource(Source source)
        {
            SetActiveSource(source, true);
        }
        
        public static void SetActiveSource(Source source, bool notify)
        {
            if (active_source != null)
                active_source.Deactivate ();
            
            active_source = source;
            source.Activate();
            
            if(!notify) {
                return;
            }
                
            ThreadAssist.ProxyToMain(delegate {
                SourceEventHandler handler = ActiveSourceChanged;
                if(handler != null) {
                    SourceEventArgs args = new SourceEventArgs();
                    args.Source = active_source;
                    handler(args);
                } 
            });
        }
        
        public static int ActiveSourceIndex {
            get {
                for(int i = 0; i < sources.Count; i++) {
                    if((sources[i] as Source) == active_source) {
                        return i;
                    }
                }
                
                return -1;
            }
        }
        
        public static ICollection Sources {
            get {
                return sources;
            }
        }
    }
}
