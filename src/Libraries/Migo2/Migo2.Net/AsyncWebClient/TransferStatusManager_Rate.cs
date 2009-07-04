// 
// TransferStatusManager_Rate.cs
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
using System.Timers;

namespace Migo2.Net
{
    internal sealed partial class TransferStatusManager
    {
        private DateTime lastTick;        
        
        private long transferRate = -1;
        private long transferRatePreviously = -1;
        
        private long bytesThisInterval;
        private int interval = (1500 * 1); // 1.5 seconds
     
        private Timer statusTimer;
        private bool enableTransferRateReporting;
        
        public EventHandler<DownloadStatusUpdatedEventArgs> StatusUpdated;    
        
        public bool EnableStatusReporting {
            get { return enableTransferRateReporting; }
            set { 
                lock (sync) {
                    if (value != enableTransferRateReporting) {
                        enableTransferRateReporting = !enableTransferRateReporting;
                        
                        if (enableTransferRateReporting) {
                            statusTimer = new Timer ();

                            statusTimer.Elapsed += OnstatusTimerElapsedHandler;
                            statusTimer.Interval = interval;
                            statusTimer.Enabled = true;   
                            lastTick = DateTime.Now;
                        } else {
                            if (statusTimer != null) {
                                statusTimer.Enabled = false;
                                statusTimer.Elapsed -= OnstatusTimerElapsedHandler;
                                statusTimer.Dispose ();
                                statusTimer = null;
                            }                          
                        }
                    }
                } 
            }
        }
        
        public int Interval {
            get { return interval; }
            set { 
                lock (sync) {
                    interval = value;
                    
                    if (statusTimer != null) {
                        statusTimer.Interval = interval;
                    }
                }
            }
        }
        
        public long TransferRate {
            get {
                lock (sync) {
                    return transferRate;
                }
            }
        }
        
        private void ResetTransferRates ()
        {
            lock (sync) {
                bytesThisInterval = 0;            
                transferRate = -1;
                transferRatePreviously = -1;     
            }
        }
        
        private long CalculateTransferRate ()
        {
            long bytesPerSecond;

            TimeSpan duration = (DateTime.Now - lastTick);
            double secondsElapsed = duration.TotalSeconds;

            if ((int)secondsElapsed == 0) {
                return 0;
            }

            bytesPerSecond = (long) ((bytesThisInterval / secondsElapsed));

            lastTick = DateTime.Now;
            bytesThisInterval = 0;

            return bytesPerSecond;
        }          

        private void UpdateTransferRate ()
        {
            transferRate = CalculateTransferRate ();

            if (transferRatePreviously == 0) {
                if (transferRate > 0) {
                    transferRatePreviously = transferRate;
                    return;                    
                }
            }
            
            if (transferRate != transferRatePreviously) {
                transferRate = ((transferRate + transferRatePreviously) / 2);
                transferRatePreviously = transferRate;                
            }
        }         
        
        private void OnstatusTimerElapsedHandler (object source, System.Timers.ElapsedEventArgs e)
        {
            DownloadStatus status;
            
            lock (sync) {
                UpdateTransferRate ();
                status = new DownloadStatus (progress, bytesReceived, totalBytes, TotalBytesReceived, transferRate);    
            }

            OnStatusUpdated (status);
        } 
        
        private void OnStatusUpdated (DownloadStatus status)
        {
            EventHandler<DownloadStatusUpdatedEventArgs> handler = StatusUpdated;
            
            if (handler != null) {
                handler (this, new DownloadStatusUpdatedEventArgs (status, null));
            }
        }        
    }
}
