// 
// MiroGuideSourceManager.cs
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

using Banshee.ServiceStack;
using Banshee.Paas.Aether.MiroGuide;

namespace Banshee.Paas.MiroGuide
{
    public class MiroGuideSourceManager : IDisposable
    {
        private MiroGuideSource mg_source;
        
        public MiroGuideSourceManager ()
        {
        }

        public void Initialize (MiroGuideClient client)
        {
            mg_source = new MiroGuideSource (client);
            mg_source.AddChildSource (new SearchSource (client));
            mg_source.AddChildSource (new HDChannelsSource (client));            
            mg_source.AddChildSource (new FeaturedChannelsSource (client));
            mg_source.AddChildSource (new PopularChannelsSource (client));            
            mg_source.AddChildSource (new TopRatedChannelsSource (client));            
            
            ServiceManager.SourceManager.AddSource (mg_source);            
        }

        public void Dispose ()
        {
            if (mg_source != null) {
                ServiceManager.SourceManager.RemoveSource (mg_source);            
                mg_source.Dispose ();
                mg_source = null;
            }
        }
    }
}
