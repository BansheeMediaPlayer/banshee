// 
// DownloadStatus.cs
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

namespace Migo2.Net
{
    public struct DownloadStatus
    {
        private readonly int progress;
        private readonly long bytesReceived;
        private readonly long totalBytes;
        private readonly long totalBytesReceived;
        private readonly long transferRate;
        
        public long BytesReceived
        {
            get { return bytesReceived; }
        }

        public int Progress
        {
            get { return progress; }
        }

        public long TotalBytes
        {
            get { return totalBytes; }
        }

        public long TotalBytesReceived
        {
            get { return totalBytesReceived; }
        }

        public long TransferRate {
            get { return transferRate; }
        }

        public DownloadStatus (int progress,
                               long bytesReceived,
                               long totalBytes,
                               long totalBytesReceived,
                               long transferRate)
        {
            this.progress = progress;
            this.bytesReceived = bytesReceived;
            this.totalBytes = totalBytes;
            this.totalBytesReceived = totalBytesReceived;
            this.transferRate = transferRate;            
        }
    }
}

