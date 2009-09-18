// 
// MiroGuideSearchEntry.cs
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

using Mono.Unix;

using Banshee.Widgets;

using Banshee.Paas.Aether.MiroGuide;

namespace Banshee.Paas.MiroGuide.Gui
{
    public class MiroGuideSearchEntry : SearchEntry
    {
        public MiroGuideSearchEntry ()
        {
            string default_str = Catalog.GetString ("Search Miro Guide");
            EmptyMessage = default_str;
            
// MG doesn't support search filtering by any type other than hd
/*            
            AddFilterOption ((int)MiroGuideSearchFilter.Default, default_str);

            AddFilterSeparator ();
            AddFilterOption ((int)MiroGuideSearchFilter.HD, Catalog.GetString ("HD Channels"));
            AddFilterOption ((int)MiroGuideSearchFilter.Video, Catalog.GetString ("Video Channels"));                        
            AddFilterOption ((int)MiroGuideSearchFilter.Audio, Catalog.GetString ("Audio Channels"));
*/            
        }
    }
}
