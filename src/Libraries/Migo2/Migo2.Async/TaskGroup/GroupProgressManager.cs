// 
// GroupProgressManager.cs
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
using System.ComponentModel;
using System.Collections.Generic;

namespace Migo2.Async
{
    public class GroupProgressManager<T> where T : Task
    {   
        private int progress;
        private int oldProgress;        

        private int totalTicks;
        private int currentTicks;
       
        private Dictionary<T, int> progDict;
        
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;
        
        public GroupProgressManager ()
        {
            progDict = new Dictionary<T, int> ();
        }
        
        public virtual void Add (T task)
        {
            Add (task, true);
        }
         
        public virtual void Add (IEnumerable<T> tasks)
        {
            foreach (T task in tasks) {
                Add (task, false);
            }
            
            OnProgressChanged ();
        }        
        
        protected virtual void Add (T task, bool update)
        {   
            if (progDict.ContainsKey (task)) {
                throw new ArgumentException ("Task was added previously");                 
            }
            
            totalTicks += 100;
            progDict.Add (task, 0);
       
            if (update) {
                OnProgressChanged ();
            }            
        }
        
        public virtual void Remove (T task)
        {
            Remove (task, true);
        }
        
        public virtual void Remove (IEnumerable<T> tasks)
        {
            foreach (T task in tasks) {
                try {
                    Remove (task, false);
                } catch { continue; }
            }
            
            OnProgressChanged ();
        }
        
        protected virtual void Remove (T task, bool update)
        {
            if (task.Progress == 100) {
                if (progDict.ContainsKey (task)) {                
                    progDict.Remove (task);
                }
            } else {            
                int prog = 0;
                
                if (progDict.ContainsKey (task)) {
                    prog = progDict[task];
                    progDict.Remove (task);

                    currentTicks -= prog;
                    totalTicks -= 100; 
                }

                if (update) {
                    OnProgressChanged ();
                }
            }
        }

        public virtual void Reset () 
        {
            progress = 0;
            oldProgress = 0;        

            totalTicks = 0;
            currentTicks = 0;
            
            progDict.Clear ();
        }
        
        public virtual void Update (T task, int newProg)
        {            
            if (newProg < 0) {
                throw new ArgumentOutOfRangeException (
                    "newProg must be greater than or equal to 0"
                );
            }
            
            int delta = 0;
            
            if (progDict.ContainsKey (task)) {      
                int prog = progDict[task];              
                
                if (prog != newProg) {
                    progDict[task] = newProg;
                    delta = newProg - prog;
                }
            }

            if (delta != 0) {
                currentTicks += delta;
                OnProgressChanged ();
            }            
        }
        
        protected virtual void OnProgressChanged ()
        {
            if (totalTicks == 0) {
                progress = 0;
            } else {
                progress = Convert.ToInt32 (
                    (currentTicks * 100) / totalTicks
                );
            }
            
            if (progress != oldProgress) {
                oldProgress = progress;
                
                EventHandler<ProgressChangedEventArgs> handler = ProgressChanged;                
                
                if (handler != null) {
                    handler (
                        this, new ProgressChangedEventArgs (progress, null) 
                    );
                }
            }
        }
    }
}
