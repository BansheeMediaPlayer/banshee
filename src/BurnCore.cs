/***************************************************************************
 *  BurnCore.cs
 *
 *  Copyright (C) 2005 Novell
 *  Written by Aaron Bockover (aaron@aaronbock.net)
 ****************************************************************************/

/*  THIS FILE IS LICENSED UNDER THE MIT LICENSE AS OUTLINED IMMEDIATELY BELOW: 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a
 *  copy of this software and associated documentation files (the "Software"),  
 *  to deal in the Software without restriction, including without limitation  
 *  the rights to use, copy, modify, merge, publish, distribute, sublicense,  
 *  and/or sell copies of the Software, and to permit persons to whom the  
 *  Software is furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in 
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
 *  FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
 *  DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Collections;
using System.Threading;
using System.Runtime.InteropServices;
using System.IO;
using Mono.Unix;
using Gtk;
using Nautilus;

using Banshee.Base;
using Banshee.Widgets;

namespace Banshee
{
    public class BurnCore
    {
        public enum DiskType {
            Audio,
            Mp3,
            Data
        };
    
        private BatchTranscoder transcoder;
        private ArrayList errors = new ArrayList();
        private Queue encodeQueue = new Queue();
        private Queue burnQueue = new Queue();
        private DiskType diskType;
        private bool canceled;
        private double estimated_encoded_bytes;
        
        public BurnCore(DiskType diskType)
        {
            this.diskType = diskType;
        }
        
        public void AddTrack(TrackInfo track)
        {
            encodeQueue.Enqueue(track);
        }
        
        public void Burn()
        {
            PipelineProfile profile = null;
            
            canceled = false;
            
            foreach(TrackInfo track in encodeQueue) {
                // 44.1 kHz sample rate * 16 bit channel resolution * 2 channels (stereo)
                estimated_encoded_bytes += track.Duration.TotalSeconds * 176400.0;
            }

            long free_space = PathUtil.GetDirectoryAvailableSpace(Paths.TempDir);
            if(free_space >= 0 && estimated_encoded_bytes >= free_space) {
                LogCore.Instance.PushError(Catalog.GetString("Insufficient Disk Space"),
                    String.Format(Catalog.GetString("Creating this CD requires at least {0} MiB of free disk space."),
                        Math.Ceiling(estimated_encoded_bytes / 1000000)));
                return;
            }
            
            switch(diskType) {
                case DiskType.Audio:
                    foreach(PipelineProfile cp in PipelineProfile.Profiles) {
                       if(cp.Extension == "wav") {
                           profile = new PipelineProfile(cp);
                           break;
                       }
                    }
                    break;
                case DiskType.Mp3:
                    foreach(PipelineProfile cp in PipelineProfile.Profiles) {
                        if(cp.Extension == "mp3") {
                            profile = new PipelineProfile(cp);
                            profile.Bitrate = 192;
                            try {
                                profile.Bitrate = (int)Globals.Configuration.Get(GConfKeys.IpodBitrate);
                            } catch {
                            }
                            
                            break;
                        }
                    }
                    break;
                        
                case DiskType.Data:
                    CreateImage();
                    return;
            }
            
            if(profile == null) {
                 LogCore.Instance.PushError(
                     Catalog.GetString("Could Not Write CD"),
                     Catalog.GetString("No suitable encoder could be found to convert selected songs."));
                 return;
            }
        
            transcoder = new BatchTranscoder(profile);
            transcoder.FileFinished += OnFileEncodeComplete;
            transcoder.BatchFinished += OnFileEncodeTransactionFinished;
            transcoder.Canceled += OnFileEncodeTransactionCanceled;
            
            int transcode_count = 0;
            
            while(encodeQueue.Count > 0) {
                TrackInfo ti = encodeQueue.Dequeue() as TrackInfo;
                string outputFile = null;
                
                try {
                    new Gnome.Vfs.FileInfo(ti.Uri.AbsoluteUri);
                } catch {
                    errors.Add(String.Format(Catalog.GetString("File does not exist: {0}"), ti.Uri.AbsoluteUri));
                    continue;
                }
                
                if(Path.GetExtension(ti.Uri.LocalPath) == "." + profile.Extension) {
                    outputFile = ti.Uri.LocalPath;
                } else {
                    outputFile = Paths.TempDir + "/"  +
                        Path.GetFileNameWithoutExtension(ti.Uri.LocalPath) + "." + 
                        profile.Extension;
                }
                
                transcoder.AddTrack(ti, new Uri(outputFile));
                transcode_count++;
            }
            
            if(transcode_count > 0) {
                transcoder.Start();
            } else {
                LogCore.Instance.PushError(
                    Catalog.GetString("Problem creating CD"),
                    Catalog.GetString("None of the songs selected for this CD could be found."));
            }
        }
        
        private void OnFileEncodeComplete(object o, FileCompleteArgs args)
        {
            burnQueue.Enqueue(args.EncodedFileUri);
        }
        
        private void OnFileEncodeTransactionFinished(object o, EventArgs args)
        {
            if(!canceled) {
                if(diskType == DiskType.Mp3) {
                    CreateImage();
                } else {
                    DoBurn();
                }
            }
        }
        
        private void OnFileEncodeTransactionCanceled(object o, EventArgs args)
        {
            canceled = true;
        }
        
        private void CleanTemp()
        {
            foreach(string file in Directory.GetFiles(Paths.TempDir)) {
                try {
                    File.Delete(file);
                } catch {
                }
            }
        }
        
        private void DoBurn()
        {
            if(burnQueue.Count <= 0) {
                LogCore.Instance.PushError(
                    Catalog.GetString("Problem creating CD"),
                    Catalog.GetString("None of the songs selected for this CD could be " +
                        "converted to the format required for an audio CD."));
                CleanTemp();
                return;
            }
            
            if(transcoder != null && transcoder.ErrorCount > 0) {
                foreach(BatchTranscoder.QueueItem item in transcoder.ErrorList) {
                    if(item.Source is TrackInfo) {
                        TrackInfo track = item.Source as TrackInfo;
                        errors.Add(String.Format("{0} - {1}", track.DisplayArtist, track.DisplayTitle));
                    } else if(item.Source is Uri) {
                        errors.Add(Path.GetFileName((item.Source as Uri).LocalPath));
                    }
                }
            }
            
            if(!HandleErrors()) {
                return;
            }
            
            ThreadAssist.ProxyToMain(delegate {
                new Burner(diskType, burnQueue);
            });
        }
        
        private void CreateImage()
        {
            NautilusImageCreator imager = new NautilusImageCreator("Banshee Songs");
            imager.Error += OnCreateImageError;
            
            if(diskType == DiskType.Data) {
                foreach(TrackInfo ti in encodeQueue) {
                    try {
                        new Gnome.Vfs.FileInfo(ti.Uri.AbsoluteUri);
                        imager.AddUri(ti.Uri);
                    } catch {
                        errors.Add(String.Format(Catalog.GetString("File does not exist: {0}"), ti.Uri.AbsoluteUri));
                        continue;
                    }
                }
            } else {
                foreach(Uri uri in burnQueue) {
                    try {
                        new Gnome.Vfs.FileInfo(uri.AbsoluteUri);
                        imager.AddUri(uri);
                    } catch {
                        errors.Add(String.Format(Catalog.GetString("File does not exist: {0}"), uri.AbsoluteUri));
                        continue;
                    }
                }
            }
            
            if(!HandleErrors()) {
                return;
            }
            
            ThreadAssist.Spawn(delegate {
                imager.Start();
            });
        }
        
        private bool HandleErrors()
        {
            Banshee.Gui.ErrorListDialog dialog = new Banshee.Gui.ErrorListDialog();

            dialog.Header = Catalog.GetString("Problem creating CD");
            dialog.Message = Catalog.GetString("Some songs could either not be found or could not be converted to the format required for an audio CD.");
            dialog.AddStockButton("gtk-cancel", ResponseType.Cancel);
            dialog.AddButton(Catalog.GetString("Continue Anyway"), ResponseType.Ok);
            dialog.IconName = "gtk-dialog-warning";

            foreach(string error in errors) {
                dialog.AppendString(error);
            }
            
            ResponseType response = dialog.Run();
            dialog.Destroy();

            if(response != ResponseType.Ok) {
                CleanTemp();
                return false;
            }
            
            return true;
        }
        
        private void OnCreateImageError(object o, EventArgs args)
        {
            NautilusImageCreator imager = o as NautilusImageCreator;
            LogCore.Instance.PushError(Catalog.GetString("Error Creating CD Image"), imager.ErrorMessage);    
        }
    }
    
    public class Burner
    {
        private Queue burnQueue;
        private BurnCore.DiskType diskType;
        private BurnDrive drive;
        private BurnRecorder recorder;
        private BurnRecorderActions currentAction;
        private long TotalDuration;
        private ActiveUserEvent user_event;
        
        public Burner(BurnCore.DiskType diskType, Queue burnQueue)
        {
            this.diskType = diskType;
            this.burnQueue = burnQueue;
            
            user_event = new ActiveUserEvent(Catalog.GetString("Writing CD"));
            user_event.Header = Catalog.GetString("Writing Audio CD");
            user_event.Icon = Gdk.Pixbuf.LoadFromResource("cd-action-burn-24.png");
            user_event.CancelRequested += OnUserEventCancelRequested;
            
            ThreadAssist.Spawn(BurnThread);
        }
        
        private bool GetBoolPref(string key, bool def)
        {
            try {
                return (bool)Globals.Configuration.Get(key);
            } catch(Exception) {
                return def;
            }
        }

        private void OnUserEventCancelRequested(object o, EventArgs args) 
        {
            if(recorder != null) {
                recorder.Cancel(false);
            }
        }
        
        private bool HaveRequiredSpace() 
        {
            long available = (long)(((drive.MediaSize  / 1024 / 1024) - 1) * 48 / 7);
            long remaining = (long)(available - TotalDuration);

            if(remaining < 0) {
                int minutes = (int)(-remaining / 60);
                string msg =
                    Catalog.GetString("The inserted media is not large enough to hold your selected music.") + " " +
                    Catalog.GetPluralString(
                        "{0} more minute is needed on the media.",
                        "{0} more minutes are needed on the media.",
                        minutes);
            
                LogCore.Instance.PushWarning(Catalog.GetString("Not Enough Space on Disc"), msg);
                return false;
            }

            return true;
        }
        
        private void BurnThread()
        {
            ArrayList tracks = new ArrayList();
    
            try {
                if(recorder == null) {
                    recorder = new BurnRecorder();
                    recorder.ProgressChanged += OnProgressChanged;
                    recorder.ActionChanged += OnActionChanged;
                    recorder.InsertMediaRequest += OnInsertMediaRequest;
                    //recorder.WarnDataLoss += OnWarnDataLoss;
                }
                
                string selectedBurnerId;
                
                try { 
                    selectedBurnerId = (string)Globals.Configuration.Get(GConfKeys.CDBurnerId);
                } catch(Exception) { 
                    selectedBurnerId = null;
                }
                
                drive = BurnUtil.GetDriveByIdOrDefault(selectedBurnerId);
                    
                if(drive == null) {
                    throw new ApplicationException(Catalog.GetString("No CD writers were found on your system."));
                }
                
                selectedBurnerId = BurnUtil.GetDriveUniqueId(drive);
                
                string burnKeyParent = GConfKeys.CDBurnerRoot + selectedBurnerId + "/";
                
                foreach(Uri uri in burnQueue) {
                    tracks.Add(new BurnRecorderTrack(uri.LocalPath, 
                        diskType == BurnCore.DiskType.Audio ?
                            BurnRecorderTrackType.Audio :
                            BurnRecorderTrackType.Data));
                }
                
                BurnRecorderWriteFlags flags = BurnRecorderWriteFlags.Debug;
                
                if(GetBoolPref(burnKeyParent + "Eject", true))
                    flags |= BurnRecorderWriteFlags.Eject;
                
                if(GetBoolPref(burnKeyParent + "DAO", true))
                    flags |= BurnRecorderWriteFlags.DiscAtOnce;
                
                if(GetBoolPref(burnKeyParent + "Overburn", false)) 
                    flags |= BurnRecorderWriteFlags.Overburn;
                
                if(GetBoolPref(burnKeyParent + "Simulate", false))
                    flags |= BurnRecorderWriteFlags.DummyWrite;
                
                if(GetBoolPref(burnKeyParent + "Burnproof", true))
                    flags |= BurnRecorderWriteFlags.Burnproof;

                BurnRecorderResult result = (BurnRecorderResult)recorder.WriteTracks(drive,
                    tracks.ToArray(typeof(BurnRecorderTrack)) as BurnRecorderTrack [],
                    drive.MaxWriteSpeed, flags);
                
                if(result == BurnRecorderResult.Error) {
                    string header = null;
                    string message = null;
                    
                    if(header == null || header == String.Empty) {
                        header = Catalog.GetString("Error Writing CD");
                    }
                    
                    if(message == null || message ==String.Empty) {
                        message = Catalog.GetString("An unknown error occurred when attempting to write the CD");
                    }
                    
                    LogCore.Instance.PushError(header, message);
                } else if(result != BurnRecorderResult.Cancel) {
                    LogCore.Instance.PushInformation(
                        Catalog.GetString("CD Writing Complete"),
                        Catalog.GetString("The selected audio was successfully written to the CD.")
                    );
                }
            } catch(Exception e) {
                LogCore.Instance.PushError(Catalog.GetString("Error Writing CD"), e.Message);    
            } finally {
                foreach(string file in Directory.GetFiles(Paths.TempDir)) {
                    File.Delete(file);
                }
                
                user_event.Dispose();
            }
        }

        private void OnProgressChanged(object o, ProgressChangedArgs args) 
        {
            if(currentAction == BurnRecorderActions.Writing) {
                user_event.Progress = args.Fraction;
            }
        }
        
        private void OnActionChanged(object o, ActionChangedArgs args) 
        {
            currentAction = args.Action;

            switch(currentAction) {
                case BurnRecorderActions.PreparingWrite:
                    user_event.Message = Catalog.GetString("Preparing to write...");
                    user_event.CanCancel = true;
                    break;
                case BurnRecorderActions.Writing:
                    user_event.Message = Catalog.GetString("Writing disk...");
                    user_event.CanCancel = true;
                    break;
                case BurnRecorderActions.Fixating:
                    user_event.Message = Catalog.GetString("Fixating disk...");
                    user_event.Progress = 0.0;
                    user_event.CanCancel = false;
                    break;
            }
        }
       
        private bool media_present;
        
        private void OnInsertMediaRequest(object o, InsertMediaRequestArgs args)
        {
            media_present = false;
            
            if(user_event.IsCancelRequested) {
                recorder.Cancel(false);
                return;
            }
            
            user_event.Message = Catalog.GetString("Waiting for Media");
            
            ThreadAssist.ProxyToMain(delegate {
                HigMessageDialog dialog = new HigMessageDialog(null, DialogFlags.Modal, MessageType.Info,
                    ButtonsType.OkCancel, Catalog.GetString("Insert Blank CD"),
                    Catalog.GetString("Please insert a blank CD disk for the write process."));
                
                dialog.Title = Catalog.GetString("Insert Blank CD");
                IconThemeUtils.SetWindowIcon(dialog);
                dialog.DefaultResponse = ResponseType.Ok;
                dialog.Response += OnMediaRequestResponse;
                dialog.ShowAll();
            });
            
            while(!media_present && !user_event.IsCancelRequested) {
                Thread.Sleep(250);
            }
        }

        private void OnMediaRequestResponse(object o, ResponseArgs args)
        {
            if(args.ResponseId == ResponseType.Ok) {
                media_present = drive.MediaSize > 0;
                if(!media_present) {
                    return;
                }
            }
            
            (o as Dialog).Response -= OnMediaRequestResponse;
            (o as Dialog).Destroy();
            
            if(args.ResponseId != ResponseType.Ok) {
                user_event.Cancel();
            }
        }
    }
    
    public class NautilusImageCreator
    {
        private ArrayList uris = new ArrayList(); 
        private Gnome.Vfs.Uri dest = new Gnome.Vfs.Uri("burn:///");
        
        private ActiveUserEvent user_event;
        private int uri_index = 0;
        
        private string error_message;
        private string image_label;
        private bool cancel;
        
        public EventHandler Canceled;
        public EventHandler Finished;
        public EventHandler Error;
        
        public NautilusImageCreator(string imageLabel)
        {
            image_label = imageLabel;
        }
        
        public void AddUri(System.Uri uri)
        {
            uris.Add(uri);        
        }
        
        public void Start()
        {
            if(user_event == null) {        
                user_event = new ActiveUserEvent(Catalog.GetString("Creating Image"));
                user_event.Header = Catalog.GetString("Transferring songs");
                user_event.Icon = Gdk.Pixbuf.LoadFromResource("cd-action-burn-24.png");
                user_event.CancelRequested += OnUserEventCancelRequested;
            }
            
            if(uris.Count <= 0) {
                throw new ApplicationException("No files to transfer");
            }
        
            Gnome.Vfs.Result result = Gnome.Vfs.Result.Ok;
            uri_index = 0;
            
            foreach(Uri uri in uris) {
                Gnome.Vfs.Uri source = new Gnome.Vfs.Uri(uri.AbsoluteUri);
                Gnome.Vfs.Uri target = dest.Clone();
                target = target.AppendFileName(source.ExtractShortName());
                Gnome.Vfs.XferProgressCallback cb = new Gnome.Vfs.XferProgressCallback(OnProgress);
				
				user_event.Message = String.Format(Catalog.GetString("Transferring song {0}"), uri_index + 1);
				user_event.Progress = uri_index / (double)uris.Count;
				result = Gnome.Vfs.Xfer.XferUri(source, target, 
				    Gnome.Vfs.XferOptions.Default,
				    Gnome.Vfs.XferErrorMode.Abort,
				    Gnome.Vfs.XferOverwriteMode.Replace,
				    cb);
				    
                uri_index++;
                
                if(result != Gnome.Vfs.Result.Ok) {
                    if(!cancel) {
                        OnError(Catalog.GetString("Could not transfer files to create disk image."));
                    }
                    
                    return;
                }
                
                if(cancel) {
                    return;
                }
            }
            
            WriteImage();
        }
        
        private int OnProgress(Gnome.Vfs.XferProgressInfo info)
        {
            if(cancel) {
                return (int)Gnome.Vfs.XferErrorAction.Abort;
            }
            
            if(info.BytesTotal > 0) {
                double progress = info.BytesCopied / (double)info.BytesTotal;
                user_event.Progress = (uri_index / (double)uris.Count) + (progress / (double)uris.Count);
            }

            switch(info.Status) {
                case Gnome.Vfs.XferProgressStatus.Vfserror:
                    OnError(Catalog.GetString("Could not transfer file to disk layout."));
                    return (int)Gnome.Vfs.XferErrorAction.Abort;
                case Gnome.Vfs.XferProgressStatus.Overwrite:
                    OnError(Catalog.GetString("File is already in disk layout."));
                    return (int)Gnome.Vfs.XferOverwriteAction.Abort;
                default:
                    return 1;
            }
        }

        private void WriteImage()
        {
            System.Diagnostics.Process.Start("nautilus-cd-burner");
            OnFinished();
        }

        private void OnUserEventCancelRequested(object o, EventArgs args) 
        {
            OnCanceled();
        }
        
        private void OnCanceled()
        {
            cancel = true;
            user_event.Dispose();

            EventHandler handler = Canceled;
            if(handler != null) {
                handler(this, new EventArgs());
            }
        }
        
        private void OnError(string message)
        {
            error_message = message;
            user_event.Dispose();
            
            EventHandler handler = Error;
            if(handler != null) {
                handler(this, new EventArgs());
            }
        }
        
        private void OnFinished()
        {
            user_event.Dispose();
            
            EventHandler handler = Finished;
            if(handler != null) {
                handler(this, new EventArgs());
            }
        }
        
        public string ErrorMessage {
            get { return error_message; }
        }
    }
}

