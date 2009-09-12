// 
// SearchContext.cs
//  
// Author:
//   Mike Urbanski <michael.c.urbanski@gmail.com>
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

namespace Banshee.Paas.Aether.MiroGuide
{
    public class SearchContext
    {
        private uint count;
        private uint limit;
        
        private int page;
        private uint offset;        
        private bool channels_available;
        
        private MiroGuideSortType sort_type;

        private readonly bool reverse;
        
        private readonly string filter_value;
        private readonly MiroGuideFilterType filter_type;        
                        
        public uint Count {
            get { return count; }
        }

        public uint Limit {
            get { return limit; }
            set { limit = value; }
        }

        public int Page {
            get { return page; }
        }

        public uint Offset {
            get { return offset; }
        }

        public bool Reverse {
            get { return reverse; }
        }

        public bool ChannelsAvailable {
            get { return channels_available; }
        }

        public MiroGuideFilterType FilterType {
            get { return filter_type; }
        }

        public string FilterValue {
            get { return filter_value; }
        }

        public MiroGuideSortType SortType {
            get { return sort_type; }
            set { sort_type = value; }            
        }

        public SearchContext (MiroGuideFilterType filterType, 
                              string filterValue, 
                              MiroGuideSortType sortType, 
                              bool reverse, uint limit, uint offset)
        {
            if (String.IsNullOrEmpty (filterValue)) {
                return;
            }

            page = -1;
            count = 0;
            
            channels_available = true;
            
            this.limit = limit;
            this.offset = offset;
            this.reverse = reverse;
            
            sort_type = sortType;            
            
            filter_type = filterType;
            filter_value = filterValue;
        }

        public void IncrementResultCount (uint results)
        {
            ++page;            
            count += results;
            
            if (results < limit) {
                channels_available = false;
            }
        }

        public void ResetCount ()
        {
            page = -1;
            count = 0;
            offset = 0;

            channels_available = true;
        }
    }
}