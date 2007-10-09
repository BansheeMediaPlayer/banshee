/***************************************************************************
 *  MetadataService.cs
 *
 *  Copyright (C) 2006-2007 Novell, Inc.
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
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using Banshee.Kernel;

namespace Banshee.Metadata
{
    public class MetadataService : BaseMetadataProvider
    {
        private static MetadataService instance;
        public static MetadataService Instance {
            get {
                if(instance == null) {
                    instance = new MetadataService();
                }
                
                return instance;
            }
        }
        
        private class MetadataJobWrapper
        {
            public IMetadataLookupJob Job;
            public int ScheduleCount;
            public JobPriority Priority;
            
            public MetadataJobWrapper(IMetadataLookupJob job, JobPriority priority)
            {
                Job = job;
                Priority = priority;
                ScheduleCount = 1;
            }
        }
        
        private Dictionary<IBasicTrackInfo, MetadataJobWrapper> queries = new Dictionary<IBasicTrackInfo, MetadataJobWrapper>();
        private List<IMetadataProvider> providers = new List<IMetadataProvider>();
        private MetadataSettings settings;

        public MetadataService()
        {
            AddProvider(new Banshee.Metadata.Embedded.EmbeddedMetadataProvider());
            AddProvider(new Banshee.Metadata.MusicBrainz.MusicBrainzMetadataProvider());
            AddProvider(new Banshee.Metadata.Rhapsody.RhapsodyMetadataProvider());
            
            Scheduler.JobFinished += OnSchedulerJobFinished;
            Scheduler.JobUnscheduled += OnSchedulerJobUnscheduled;
            
            Settings = new MetadataSettings();
        }

        public override IMetadataLookupJob CreateJob(IBasicTrackInfo track, MetadataSettings settings)
        {
            return new MetadataServiceJob(this, track, settings);
        }
        
        public override void Lookup(IBasicTrackInfo track)
        {
            Lookup(track, JobPriority.Highest);
        }
        
        public void Lookup(IBasicTrackInfo track, JobPriority priority)
        {
            if(track == null || queries == null) {
                return;
            }
            
            lock(queries) {
                if(!queries.ContainsKey(track)) {
                    IMetadataLookupJob job = CreateJob(track, Settings);
                    if(job == null) {
                        return;
                    }
                    
                    queries.Add(track, new MetadataJobWrapper(job, priority));
                    Scheduler.Schedule(job, priority);
                } else if(Scheduler.CurrentJob == queries[track].Job) {
                    queries[track].ScheduleCount++;
                }
            }
        }

        public void AddProvider(IMetadataProvider provider)
        {
            AddProvider(-1, provider);
        }

        public void AddProvider(int position, IMetadataProvider provider)
        {
            lock(providers) {
                if(position < 0) {
                    providers.Add(provider);
                } else {
                    providers.Insert(position, provider);
                }
            }
        }

        public void RemoveProvider(IMetadataProvider provider)
        {
            lock(providers) {
                providers.Remove(provider);
            }
        }
        
        private bool RemoveJob(IMetadataLookupJob job)
        {
            if(job == null) {
                return false;
            }
            
            lock(queries) {
                if(queries.ContainsKey(job.Track)) {
                    queries.Remove(job.Track);
                    return true;
                }
                
                return false;
            }
        }
        
        private void OnSchedulerJobFinished(IJob job)
        {
            if(!(job is IMetadataLookupJob)) {
                return;
            }
            
            IMetadataLookupJob lookup_job = (IMetadataLookupJob)job;
            bool remove_job = true;
            
            lock(queries) {
                if(queries.ContainsKey(lookup_job.Track)) {
                    MetadataJobWrapper wrapper = queries[lookup_job.Track];
                    if(--wrapper.ScheduleCount > 0) {
                        Scheduler.Schedule(job, wrapper.Priority);
                        remove_job = false;
                    }
                }
            }
            
            if(!remove_job || (remove_job && RemoveJob(lookup_job))) {
                Settings.ProxyToMain(delegate { 
                    OnHaveResult(lookup_job.Track, lookup_job.ResultTags); 
                });
            }
        }
        
        private void OnSchedulerJobUnscheduled(IJob job)
        {
            RemoveJob(job as IMetadataLookupJob);
        }
        
        public ReadOnlyCollection<IMetadataProvider> Providers {
            get {
                lock(providers) {
                    return new ReadOnlyCollection<IMetadataProvider>(providers);
                }
            }
        }
        
        public override MetadataSettings Settings {
            get { return settings; }
            set { 
                settings = value;
                foreach(IMetadataProvider provider in providers) {
                    provider.Settings = value;
                }
            }
        }
    }
}
