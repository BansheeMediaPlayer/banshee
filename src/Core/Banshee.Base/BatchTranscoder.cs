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
using System.Collections.Generic;
using Mono.Unix;

using Banshee.Widgets;
using Banshee.AudioProfiles;

namespace Banshee.Base
{
    public delegate void FileCompleteHandler(object o, FileCompleteArgs args);

    public class FileCompleteArgs : EventArgs
    {
        public TrackInfo Track;
        public SafeUri InputUri;
        public SafeUri EncodedFileUri;
    }

    public class BatchTranscoder
    {
        private static Dictionary<string, Type> alternate_wav_transcoders = new Dictionary<string, Type>();
    
        public static void RegisterAlternateWavTranscoder(string extension, Type type)
        {
            lock(alternate_wav_transcoders) {
                if(alternate_wav_transcoders.ContainsKey(extension)) {
                    alternate_wav_transcoders[extension] = type;
                } else {
                    alternate_wav_transcoders.Add(extension, type);
                }
            }
        }
    
        public class QueueItem
        {
            private object source;
            private SafeUri destination;
            
            public QueueItem(object source, SafeUri destination)
            {
                this.source = source;
                this.destination = destination;
            }
            
            public object Source {
                get { return source; }
            }
            
            public SafeUri Destination {
                get { return destination; }
            }
        }
    
        private List<QueueItem> error_list = new List<QueueItem>();
    
        private Queue batch_queue = new Queue();
        private QueueItem current = null;
        
        private Transcoder transcoder;
        private Transcoder default_transcoder;
        
        private Profile profile;
        private int finished_count;
        private int total_count;
        private bool finished = false;
        private string desired_profile_name;
        
        private ActiveUserEvent user_event;
        
        public event FileCompleteHandler FileFinished; 
        public event EventHandler BatchFinished;
        public event EventHandler Canceled;
        
        private const int progress_precision = 1000;
        
        public BatchTranscoder(Profile profile) : this(profile, null)
        {
        }
        
        public BatchTranscoder(Profile profile, string desiredProfileName)
        {
            transcoder = new GstTranscoder();
            transcoder.Progress += OnTranscoderProgress;
            transcoder.Error += OnTranscoderError;
            transcoder.Finished += OnTranscoderFinished;
            
            default_transcoder = transcoder;
            
            this.desired_profile_name = desiredProfileName;
            this.profile = profile;
        }
        
        public void AddTrack(TrackInfo track, SafeUri outputUri)
        {
            batch_queue.Enqueue(new QueueItem(track, outputUri));
        }
        
        public void AddTrack(SafeUri inputUri, SafeUri outputUri)
        {
            batch_queue.Enqueue(new QueueItem(inputUri, outputUri));
        }
        
        public void Start()
        {
            if(user_event == null) {
                user_event = new ActiveUserEvent(Catalog.GetString("Converting Files"));
                user_event.Header = Catalog.GetString("Converting Files");
                user_event.CancelMessage = Catalog.GetString(
                    "Files are currently being converted to another audio format. Would you like to stop this?");
                user_event.CancelRequested += OnCancelRequested;
                user_event.Icon = IconThemeUtils.LoadIcon("encode-action-24", 22);
                user_event.Message = Catalog.GetString("Initializing Transcoder...");
            }
            
            total_count = batch_queue.Count;
            finished_count = 0;
            error_list.Clear();
            
            try {
                TranscodeNext();
            } catch(Exception e) {
                Console.WriteLine(e);
                
                if (current != null)
                    error_list.Add(current);
                
                if(user_event != null) {
                    user_event.Dispose();
                }
                
                OnBatchFinished();
            }
        }
        
        private void TranscodeNext()
        {
            if(batch_queue.Count <= 0) {
                return;
            }

            current = batch_queue.Dequeue() as QueueItem;
            
            if(current == null) {
                return;
            }
            
            SafeUri output_uri = current.Destination;
            SafeUri input_uri = null;
            
            if(current.Source is TrackInfo) {
                TrackInfo track = current.Source as TrackInfo;
                user_event.Message = String.Format("{0} - {1}", track.DisplayArtist, track.DisplayTitle);
                input_uri = track.Uri;
            } else if(current.Source is SafeUri) {
                input_uri = current.Source as SafeUri;
                user_event.Message = Path.GetFileName(input_uri.LocalPath);
            } else {
                OnTranscoderError(this, new EventArgs());
                return;
            }
            
            if(user_event.IsCancelRequested) {
                return;
            }
            
            string input_extension = Path.GetExtension(input_uri.LocalPath);
            if(input_extension == String.Empty) {
                OnTranscoderError(this, new EventArgs());
                return;
            }

            input_extension = input_extension.Substring(1); // remove the leading .
            
            transcoder = default_transcoder;
            
            if(profile.OutputFileExtension == "wav" && alternate_wav_transcoders.ContainsKey(input_extension)) {
                Transcoder alt_transcoder = (Transcoder)Activator.CreateInstance(alternate_wav_transcoders[input_extension]);
                alt_transcoder.Progress += OnTranscoderProgress;
                alt_transcoder.Error += OnTranscoderError;
                alt_transcoder.Finished += OnTranscoderFinished;
                transcoder = alt_transcoder;
            }
            
            Banshee.IO.IOProxy.Directory.Create(Path.GetDirectoryName(output_uri.LocalPath));

            if(input_extension != profile.OutputFileExtension) {
                transcoder.BeginTranscode(input_uri, output_uri, profile);
            } else if(desired_profile_name != null && profile.Name != desired_profile_name) {
                OnTranscoderError(this, new EventArgs());
            } else {
                OnTranscoderFinished(this, new EventArgs());
            }
            
            if(transcoder != default_transcoder) {
                transcoder.Dispose();
                transcoder = default_transcoder;
            }
        }
        
        private void PostTranscode()
        {
            current = null;
            finished_count++;
            
            if(batch_queue.Count > 0) {
                TranscodeNext();
            } else {
                if(user_event != null) {
                    user_event.Dispose();
                }
                OnBatchFinished();
            }
        }
        
        private void OnBatchFinished()
        {
            if (finished)
                return;

            finished = true;
            EventHandler handler = BatchFinished;
            if(handler != null) {
                handler(this, new EventArgs());
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
                } else if(current.Source is SafeUri) {
                    cargs.InputUri = (current.Source as SafeUri);
                }
                
                handler(this, cargs);
            }
            
            PostTranscode();
        }
        
        private void OnTranscoderError(object o, EventArgs args)
        {
            if (current != null)
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
