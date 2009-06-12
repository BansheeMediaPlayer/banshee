//
// MemoryTrackListModell.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections;
using System.Collections.Generic;

namespace Banshee.Collection
{
    public class MemoryTrackListModel : TrackListModel, IEnumerable<TrackInfo>
    {
        private static Random random = new Random ();
        private List<TrackInfo> tracks = new List<TrackInfo> ();

        public MemoryTrackListModel () : base ()
        {
        }

        public override void Clear ()
        {
            lock (this) {
                tracks.Clear ();
            }
        }
        
        public override void Reload ()
        {
            lock (this) {
                OnReloaded ();
            }
        }

        public void Add (TrackInfo track)
        {
            lock (this) {
                tracks.Add (track);
            }
        }

        public void Remove (TrackInfo track)
        {
            lock (this) {
                tracks.Remove (track);
            }
        }

        public bool Contains (TrackInfo track)
        {
            lock (this) {
                return IndexOf (track) != -1;
            }
        }
    
        public override TrackInfo this[int index] {
            get { lock (this) { return (index >= 0 && index < tracks.Count) ? tracks[index] : null; } }
        }

        public override TrackInfo GetRandom (DateTime since, Banshee.PlaybackController.PlaybackShuffleMode mode, bool repeat)
        {
            if (Count == 0)
                return null;

            return this [random.Next (0, Count - 1)];
        }

        public override int Count { 
            get { lock (this) { return tracks.Count; } }
        }
        
        public override int IndexOf (TrackInfo track)
        {
            lock (this) { return tracks.IndexOf (track); }
        }

        public IEnumerator<TrackInfo> GetEnumerator ()
        {
            foreach (TrackInfo track in tracks) {
                yield return track;
            }
        }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return GetEnumerator ();
        }
    }
}
