/***************************************************************************
 *  BatchTranscoder.cs
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
using System.IO;
using System.Collections;
using Mono.Unix;

using Banshee.Widgets;

namespace Banshee.Base
{
    public delegate void FileCompleteHandler(object o, FileCompleteArgs args);

    public class FileCompleteArgs : EventArgs
    {
        public TrackInfo Track;
        public Uri InputUri;
        public Uri EncodedFileUri;
    }

    public class BatchTranscoder
    {
        public class QueueItem
        {
            private object source;
            private Uri destination;
            
            public QueueItem(object source, Uri destination)
            {
                this.source = source;
                this.destination = destination;
            }
            
            public object Source {
                get { return source; }
            }
            
            public Uri Destination {
                get { return destination; }
            }
        }
    
        private ArrayList error_list = new ArrayList();
    
        private Queue batch_queue = new Queue();
        private QueueItem current = null;
        
        private Transcoder transcoder;
        private PipelineProfile profile;
        private int finished_count;
        private int total_count;
        
        private ActiveUserEvent user_event;
        
        public event FileCompleteHandler FileFinished; 
        public event EventHandler BatchFinished;
        public event EventHandler Canceled;
        
        private const int progress_precision = 1000;
        
        public BatchTranscoder(PipelineProfile profile)
        {
            transcoder = new GstTranscoder();
            transcoder.Progress += OnTranscoderProgress;
            transcoder.Error += OnTranscoderError;
            transcoder.Finished += OnTranscoderFinished;
            
            this.profile = profile;
        }
        
        public void AddTrack(TrackInfo track, Uri outputUri)
        {
            batch_queue.Enqueue(new QueueItem(track, outputUri));
        }
        
        public void AddTrack(Uri inputUri, Uri outputUri)
        {
            batch_queue.Enqueue(new QueueItem(inputUri, outputUri));
        }
        
        public void Start()
        {
            if(user_event == null) {
                user_event = new ActiveUserEvent(Catalog.GetString("File Transcoder"));
                user_event.Header = Catalog.GetString("Transcoding Files");
                user_event.CancelRequested += OnCancelRequested;
                user_event.Icon = IconThemeUtils.LoadIcon("encode-action-24", 22);
                user_event.Message = Catalog.GetString("Initializing Transcoder...");
            }
            
            total_count = batch_queue.Count;
            finished_count = 0;
            error_list.Clear();
            TranscodeNext();
        }
        
        private void TranscodeNext()
        {
            current = batch_queue.Dequeue() as QueueItem;
            
            if(current == null) {
                return;
            }
            
            Uri output_uri = current.Destination;
            Uri input_uri = null;
            
            if(current.Source is TrackInfo) {
                TrackInfo track = current.Source as TrackInfo;
                user_event.Message = String.Format("{0} - {1}", track.DisplayArtist, track.DisplayTitle);
                input_uri = track.Uri;
            } else if(current.Source is Uri) {
                input_uri = current.Source as Uri;
                user_event.Message = Path.GetFileName(input_uri.LocalPath);
            } else {
                return;
            }
            
            if(user_event.IsCancelRequested) {
                return;
            }
            
            if(Path.GetExtension(input_uri.LocalPath) != "." + profile.Extension) {
                transcoder.BeginTranscode(input_uri, output_uri, profile);
            } else {
                OnTranscoderFinished(this, new EventArgs());
            }   
        }
        
        private void PostTranscode()
        {
            current = null;
            finished_count++;
            
            if(batch_queue.Count > 0) {
                TranscodeNext();
            } else {
                user_event.Dispose();
            
                EventHandler handler = BatchFinished;
                if(handler != null) {
                    handler(this, new EventArgs());
                }
            }
        }
        
        private void OnTranscoderFinished(object o, EventArgs args)
        {
            FileCompleteHandler handler = FileFinished;
            
            if(handler != null && current != null) {
                FileCompleteArgs cargs = new FileCompleteArgs();
                cargs.EncodedFileUri = current.Destination;
                
                if(current.Source is TrackInfo) {
                    cargs.Track = current.Source as TrackInfo;
                    cargs.InputUri = cargs.Track.Uri;
                } else if(current.Source is Uri) {
                    cargs.InputUri = (current.Source as Uri);
                }
                
                handler(this, cargs);
            }
            
            PostTranscode();
        }
        
        private void OnTranscoderError(object o, EventArgs args)
        {
            error_list.Add(current);
            PostTranscode();
        }
        
        private void OnTranscoderProgress(object o, TranscoderProgressArgs args)
        {
            user_event.Progress = ((double)(progress_precision * finished_count) +
                (args.Progress * (double)progress_precision)) / 
                (double)(progress_precision * total_count);
        }

        private void OnCancelRequested(object o, EventArgs args)
        {
            if(user_event == null) {
                return;
            }
            
            if(transcoder != null) {
                transcoder.Cancel();
            }
            
            error_list.Clear();
            
            batch_queue.Clear();
            current = null;
            
            user_event.Dispose();
            user_event = null;
            
            if(Canceled != null) {
                Canceled(this, new EventArgs());
            }
        }
        
        public IEnumerable ErrorList {
            get { return error_list; }
        }
        
        public int ErrorCount {
            get { return error_list.Count; }
        }
    }
}
