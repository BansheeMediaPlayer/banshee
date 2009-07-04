// 
// TransferStatusManager.cs
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

namespace Migo2.Net
{
    internal sealed partial class TransferStatusManager
    {
        private int progress;
        private long progressStep;
        private long progressThisStep;

        private long bytesReceived;
        private long bytesReceivedPreviously;

        private long totalBytes = -1;

        private readonly object sync = new object ();

        public event EventHandler<DownloadProgressChangedEventArgs> ProgressChanged;

        public long BytesReceived {
            get {
                return bytesReceived;
            }
            set {
                SetBytesReceived (value);
            }
        }

        public long BytesReceivedPreviously {
            get {
                return bytesReceivedPreviously;
            }
            set {
                if (value < 0) {
                    throw new ArgumentOutOfRangeException ("BytesReceivedPreviously", "Must be > 0");
                }

                bytesReceivedPreviously = value;
                progressThisStep += value;                
                UpdateProgress ();
            }
        }

        public int Progress {
            get {
                return progress;
            }
        }

        public object SyncRoot {
            get {
                return sync;
            }
        }

        public long TotalBytesReceived {
            get {
                return bytesReceived + bytesReceivedPreviously;
            }
        }

        public long TotalBytes {
            get {
                return totalBytes;
            }
            set {
                SetTotalBytes (value);
            }
        }

        public TransferStatusManager ()
        {
            Reset ();
        }

        public void AddBytes (long bytes)
        {
            if (bytes < 0) {
                throw new ArgumentOutOfRangeException ("bytes cannot be less than 0");
            }

            lock (sync) {
                bytesReceived += bytes;
                progressThisStep += bytes;
                bytesThisInterval += bytes;
            }

            UpdateProgress ();
        }

        public void Reset ()
        {
            lock (sync) {
                progress = 0;

                progressStep = 0;
                progressThisStep = 0;

                totalBytes = -1;
                bytesReceived = 0;
                bytesReceivedPreviously = 0;
                
                ResetTransferRates ();
            }

            UpdateProgress ();
        }

        private void SetBytesReceived (long bytesReceived)
        {
            if (bytesReceived < 0) {
                throw new ArgumentOutOfRangeException ("bytesReceived cannot be less than 0");
            }

            lock (sync) {
                if (totalBytes > -1 && totalBytes < bytesReceived) {
                    throw new ArgumentOutOfRangeException ("bytesReceived cannot be greater than TotalBytes");
                }

                progressThisStep = 0;
                this.bytesReceived = bytesReceived;                                
            }

            UpdateProgress ();
        }

        private void SetTotalBytes (long totalBytes)
        {
            bool update = false;

            if (totalBytes < -1 || totalBytes == 0) {
                throw new ArgumentOutOfRangeException ("totalBytes cannot be less than -1 or equal to 0");
            }

            lock (sync) {
                if (totalBytes != -1 && totalBytes < bytesReceived) {
                    throw new ArgumentOutOfRangeException ("totalBytes cannot be less than bytesReceived");
                }

                if (this.totalBytes != totalBytes) {
                    this.totalBytes = totalBytes;
                    progressStep = (totalBytes / 100);
                    update = true;
                }
            }

            if (update) {
                UpdateProgress ();
            }
        }

        private void UpdateProgress ()
        {
            DownloadProgressChangedEventArgs args = null;

            lock (sync) {
                long totalBytesReceived = TotalBytesReceived;

                if (totalBytes > 0 &&
                    progressThisStep >= progressStep ||
                    (totalBytesReceived == totalBytes && progress != 100)) {
                    progress = Convert.ToInt32 ((totalBytesReceived * 100) / totalBytes);

                    if (progress >= 0) {
                        args = new DownloadProgressChangedEventArgs (progress, null, totalBytesReceived, totalBytes);
                    }

                    progressThisStep = 0;
                }
            }

            if (args != null) {
                OnProgressChanged (args);
            }
        }

        private void OnProgressChanged (DownloadProgressChangedEventArgs args)
        {
            EventHandler <DownloadProgressChangedEventArgs>
            handler = ProgressChanged;

            if (handler != null) {
                handler (this, args);
            }
        }
    }
}
