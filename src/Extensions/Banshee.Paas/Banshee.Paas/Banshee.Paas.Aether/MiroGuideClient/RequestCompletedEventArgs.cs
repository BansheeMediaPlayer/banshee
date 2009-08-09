// 
// RequestCompletedEventArgs.cs
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

namespace Banshee.Paas.Aether.MiroGuide
{
    public class RequestCompletedEventArgs : AsyncCompletedEventArgs
    {
        private readonly MiroGuideClientMethod method;
        private readonly bool timedout;
        
        public MiroGuideClientMethod Method {
            get { return method; } 
        }

        public bool Timedout { 
            get { return timedout; } 
        }
        
        public RequestCompletedEventArgs (Exception err,
                                          bool cancelled,
                                          MiroGuideClientMethod method,
                                          bool timedout,
                                          object userState) : base (err, cancelled, userState)
        {
            this.method = method;
            this.timedout = timedout;        
        }
    }
}
