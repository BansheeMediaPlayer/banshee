// 
// TaskAddedEventArgs.cs
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

using Migo2.Collections;

namespace Migo2.Async
{
    public class TaskAddedEventArgs<T> : ManipulatedEventArgs<T> where T : Task 
    {  
        private readonly Pair<int,T> taskPair;
        private readonly ICollection<Pair<int,T>> taskPairs;
        
        public Pair<int,T> TaskPair
        {
            get { return taskPair; }
        }
        
        // All indices are listed in ascending order from the start of the 
        // list so that in order addition will not affect indices.
        public ICollection<Pair<int,T>> TaskPairs
        {
            get { return taskPairs; }
        }
        
        public TaskAddedEventArgs (int pos, T task) : base (task, null)
        {
            if (task == null) {
                throw new ArgumentNullException ("task");
            }            
            
            this.taskPair = new Pair<int,T> (pos, task);                       
        }
        
        public TaskAddedEventArgs (ICollection<Pair<int,T>> taskPairs)
        {
            if (taskPairs == null) {
                throw new ArgumentNullException ("taskPairs");
            }
            
            List<T> tsks = new List<T> (taskPairs.Count);
            
            foreach (Pair<int,T> kvp in taskPairs) {                
                if (kvp.Second == null) {
                    throw new ArgumentNullException ("No task in tasks may be null");
                }                
                
                tsks.Add (kvp.Second);
            }

            this.Tasks = tsks;            
            this.taskPairs = taskPairs;
            this.taskPair = default (Pair<int,T>);
        }
    }
}
