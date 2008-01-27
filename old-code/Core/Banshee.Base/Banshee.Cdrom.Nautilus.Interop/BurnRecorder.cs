using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Banshee.Cdrom.Nautilus.Interop
{
    internal class BurnRecorder : GLib.Object 
    {
        private List<BurnRecorderTrack> tracks = new List<BurnRecorderTrack>();
    
        [DllImport("libnautilus-burn")]
        private static extern IntPtr nautilus_burn_recorder_get_type();

        public static new GLib.GType GType { 
            get {
                IntPtr raw_ret = nautilus_burn_recorder_get_type();
                GLib.GType ret = new GLib.GType(raw_ret);
                return ret;
            }
        }
        
        public BurnRecorder(IntPtr raw) : base(raw) 
        {
        }
    
        [DllImport("libnautilus-burn")]
        private static extern IntPtr nautilus_burn_recorder_new();

        public BurnRecorder() : base(IntPtr.Zero)
        {
            if(GetType() != typeof(BurnRecorder)) {
                CreateNativeObject(new string[0], new GLib.Value[0]);
                return;
            }
            Raw = nautilus_burn_recorder_new();
        }

        ~BurnRecorder()
        {
            Dispose();
        }

        [DllImport("libnautilus-burn")]
        private static extern int nautilus_burn_recorder_blank_disc(IntPtr raw, 
            IntPtr drive, int type, int flags, out IntPtr error);

        public int BlankDisc(BurnDrive drive, BurnRecorderBlankType type, 
            BurnRecorderBlankFlags flags) 
        {
            IntPtr error = IntPtr.Zero;
            
            int result = nautilus_burn_recorder_blank_disc(Handle, 
                drive == null ? IntPtr.Zero : drive.Handle, 
                (int)type, (int)flags, out error);
                
            if(error != IntPtr.Zero) {
                throw new GLib.GException(error);
            }
            
            return result;
        }

        [DllImport("libnautilus-burn")]
        private static extern int nautilus_burn_recorder_error_quark();

        public static int ErrorQuark() 
        {
            return nautilus_burn_recorder_error_quark();
        }

        [DllImport("libnautilus-burn")]
        private static extern bool nautilus_burn_recorder_cancel(IntPtr raw, bool skip_if_dangerous);

        public bool Cancel(bool skip_if_dangerous) 
        {
            return nautilus_burn_recorder_cancel(Handle, skip_if_dangerous);
        }

        [DllImport("libnautilus-burn")]
        private static extern BurnRecorderResult nautilus_burn_recorder_write_tracks(IntPtr raw, 
            IntPtr drive, IntPtr tracks, int speed, int flags, out IntPtr error);

        private BurnRecorderResult WriteTracks(BurnDrive drive, GLib.List tracks, int speed, 
            BurnRecorderWriteFlags flags) 
        {
            IntPtr error = IntPtr.Zero;
            
            BurnRecorderResult result = nautilus_burn_recorder_write_tracks(Handle, 
                drive == null ? IntPtr.Zero : drive.Handle, tracks.Handle, 
                speed, (int)flags, out error);
            
            if(error != IntPtr.Zero) {
                throw new GLib.GException(error);
            }
            
            return result;
        }
        
        public void AddTrack(BurnRecorderTrack track)
        {
            tracks.Add(track);
        }
        
        public void RemoveTrack(BurnRecorderTrack track)
        {
            tracks.Remove(track);
        }
        
        public bool ContainsTrack(BurnRecorderTrack track)
        {
            return tracks.Contains(track);
        }
        
        public void ClearTracks()
        {
            tracks.Clear();
        }

        public BurnRecorderResult WriteTracks(BurnDrive drive, 
            IEnumerable<BurnRecorderTrack> tracks,
            int speed, BurnRecorderWriteFlags flags)
        {
            GLib.List list = new GLib.List(IntPtr.Zero);

            foreach(BurnRecorderTrack track in tracks) {
                list.Append(track.Handle);
            }

            return WriteTracks(drive, list, speed, flags);
        }
        
        public BurnRecorderResult WriteTracks(BurnDrive drive, int speed, 
            BurnRecorderWriteFlags flags)
        {
            return WriteTracks(drive, this.tracks, speed, flags);
        }
        
        // Signals
        
        #pragma warning disable 0169
        
        [GLib.CDeclCallback]
        delegate void ProgressChangedSignalDelegate (IntPtr arg0, double arg1, int arg2, IntPtr gch);

        static void ProgressChangedSignalCallback (IntPtr arg0, double arg1, int arg2, IntPtr gch)
        {
            GLib.Signal sig = ((GCHandle) gch).Target as GLib.Signal;
            if (sig == null)
                throw new Exception("Unknown signal GC handle received " + gch);

            ProgressChangedArgs args = new ProgressChangedArgs ();
            args.Args = new object[2];
            args.Args[0] = arg1;
            args.Args[1] = arg2;
            ProgressChangedHandler handler = (ProgressChangedHandler) sig.Handler;
            handler (GLib.Object.GetObject (arg0), args);

        }

        [GLib.CDeclCallback]
        delegate void ProgressChangedVMDelegate (IntPtr recorder, double fraction, int secs);

        static ProgressChangedVMDelegate ProgressChangedVMCallback;

        static void progresschanged_cb (IntPtr recorder, double fraction, int secs)
        {
            BurnRecorder obj = GLib.Object.GetObject (recorder, false) as BurnRecorder;
            obj.OnProgressChanged (fraction, secs);
        }

        private static void OverrideProgressChanged (GLib.GType gtype)
        {
            if (ProgressChangedVMCallback == null)
                ProgressChangedVMCallback = new ProgressChangedVMDelegate (progresschanged_cb);
            OverrideVirtualMethod (gtype, "progress-changed", ProgressChangedVMCallback);
        }

        [GLib.DefaultSignalHandler(Type=typeof(BurnRecorder), ConnectionMethod="OverrideProgressChanged")]
        protected virtual void OnProgressChanged (double fraction, int secs)
        {
            GLib.Value ret = GLib.Value.Empty;
            GLib.ValueArray inst_and_params = new GLib.ValueArray (3);
            GLib.Value[] vals = new GLib.Value [3];
            vals [0] = new GLib.Value (this);
            inst_and_params.Append (vals [0]);
            vals [1] = new GLib.Value (fraction);
            inst_and_params.Append (vals [1]);
            vals [2] = new GLib.Value (secs);
            inst_and_params.Append (vals [2]);
            g_signal_chain_from_overridden (inst_and_params.ArrayPtr, ref ret);
            foreach (GLib.Value v in vals)
                v.Dispose ();
        }

        [GLib.Signal("progress-changed")]
        public event ProgressChangedHandler ProgressChanged {
            add {
                GLib.Signal sig = GLib.Signal.Lookup (this, "progress-changed", 
                    new ProgressChangedSignalDelegate(ProgressChangedSignalCallback));
                sig.AddDelegate (value);
            }
            remove {
                GLib.Signal sig = GLib.Signal.Lookup (this, "progress-changed", 
                    new ProgressChangedSignalDelegate(ProgressChangedSignalCallback));
                sig.RemoveDelegate (value);
            }
        }

        [GLib.CDeclCallback]
        delegate void ActionChangedSignalDelegate (IntPtr arg0, int arg1, int arg2, IntPtr gch);

        static void ActionChangedSignalCallback (IntPtr arg0, int arg1, int arg2, IntPtr gch)
        {
            GLib.Signal sig = ((GCHandle) gch).Target as GLib.Signal;
            if (sig == null)
                throw new Exception("Unknown signal GC handle received " + gch);

            ActionChangedArgs args = new ActionChangedArgs ();
            args.Args = new object[2];
            args.Args[0] = (BurnRecorderActions) arg1;
            args.Args[1] = (BurnRecorderMedia) arg2;
            ActionChangedHandler handler = (ActionChangedHandler) sig.Handler;
            handler (GLib.Object.GetObject (arg0), args);

        }

        [GLib.CDeclCallback]
        delegate void ActionChangedVMDelegate (IntPtr recorder, int action, int media);

        static ActionChangedVMDelegate ActionChangedVMCallback;

        static void actionchanged_cb (IntPtr recorder, int action, int media)
        {
            BurnRecorder obj = GLib.Object.GetObject (recorder, false) as BurnRecorder;
            obj.OnActionChanged ((BurnRecorderActions) action, (BurnRecorderMedia) media);
        }

        private static void OverrideActionChanged (GLib.GType gtype)
        {
            if (ActionChangedVMCallback == null)
                ActionChangedVMCallback = new ActionChangedVMDelegate (actionchanged_cb);
            OverrideVirtualMethod (gtype, "action-changed", ActionChangedVMCallback);
        }

        [GLib.DefaultSignalHandler(Type=typeof(BurnRecorder), ConnectionMethod="OverrideActionChanged")]
        protected virtual void OnActionChanged (BurnRecorderActions action, BurnRecorderMedia media)
        {
            GLib.Value ret = GLib.Value.Empty;
            GLib.ValueArray inst_and_params = new GLib.ValueArray (3);
            GLib.Value[] vals = new GLib.Value [3];
            vals [0] = new GLib.Value (this);
            inst_and_params.Append (vals [0]);
            vals [1] = new GLib.Value (action);
            inst_and_params.Append (vals [1]);
            vals [2] = new GLib.Value (media);
            inst_and_params.Append (vals [2]);
            g_signal_chain_from_overridden (inst_and_params.ArrayPtr, ref ret);
            foreach (GLib.Value v in vals)
                v.Dispose ();
        }

        [GLib.Signal("action-changed")]
        public event ActionChangedHandler ActionChanged {
            add {
                GLib.Signal sig = GLib.Signal.Lookup (this, "action-changed", 
                    new ActionChangedSignalDelegate(ActionChangedSignalCallback));
                sig.AddDelegate (value);
            }
            remove {
                GLib.Signal sig = GLib.Signal.Lookup (this, "action-changed", 
                    new ActionChangedSignalDelegate(ActionChangedSignalCallback));
                sig.RemoveDelegate (value);
            }
        }

        [GLib.CDeclCallback]
        delegate void InsertMediaRequestSignalDelegate (IntPtr arg0, bool arg1, 
            bool arg2, bool arg3, IntPtr gch);

        static void InsertMediaRequestSignalCallback (IntPtr arg0, bool arg1, 
            bool arg2, bool arg3, IntPtr gch)
        {
            GLib.Signal sig = ((GCHandle) gch).Target as GLib.Signal;
            if (sig == null)
                throw new Exception("Unknown signal GC handle received " + gch);

            InsertMediaRequestArgs args = new InsertMediaRequestArgs ();
            args.Args = new object[3];
            args.Args[0] = arg1;
            args.Args[1] = arg2;
            args.Args[2] = arg3;
            InsertMediaRequestHandler handler = (InsertMediaRequestHandler) sig.Handler;
            handler (GLib.Object.GetObject (arg0), args);

        }

        [GLib.CDeclCallback]
        delegate void InsertMediaRequestVMDelegate (IntPtr recorder, 
            bool is_reload, bool can_rewrite, bool busy);

        static InsertMediaRequestVMDelegate InsertMediaRequestVMCallback;

        static void insertmediarequest_cb (IntPtr recorder, bool is_reload, bool can_rewrite, bool busy)
        {
            BurnRecorder obj = GLib.Object.GetObject (recorder, false) as BurnRecorder;
            obj.OnInsertMediaRequest (is_reload, can_rewrite, busy);
        }

        private static void OverrideInsertMediaRequest (GLib.GType gtype)
        {
            if (InsertMediaRequestVMCallback == null)
                InsertMediaRequestVMCallback = new InsertMediaRequestVMDelegate (insertmediarequest_cb);
            OverrideVirtualMethod (gtype, "insert-media-request", InsertMediaRequestVMCallback);
        }

        [GLib.DefaultSignalHandler(Type=typeof(BurnRecorder), ConnectionMethod="OverrideInsertMediaRequest")]
        protected virtual void OnInsertMediaRequest (bool is_reload, bool can_rewrite, bool busy)
        {
            GLib.Value ret = GLib.Value.Empty;
            GLib.ValueArray inst_and_params = new GLib.ValueArray (4);
            GLib.Value[] vals = new GLib.Value [4];
            vals [0] = new GLib.Value (this);
            inst_and_params.Append (vals [0]);
            vals [1] = new GLib.Value (is_reload);
            inst_and_params.Append (vals [1]);
            vals [2] = new GLib.Value (can_rewrite);
            inst_and_params.Append (vals [2]);
            vals [3] = new GLib.Value (busy);
            inst_and_params.Append (vals [3]);
            g_signal_chain_from_overridden (inst_and_params.ArrayPtr, ref ret);
            foreach (GLib.Value v in vals)
                v.Dispose ();
        }

        [GLib.Signal("insert-media-request")]
        public event InsertMediaRequestHandler InsertMediaRequest {
            add {
                GLib.Signal sig = GLib.Signal.Lookup (this, "insert-media-request", 
                    new InsertMediaRequestSignalDelegate(InsertMediaRequestSignalCallback));
                sig.AddDelegate (value);
            }
            remove {
                GLib.Signal sig = GLib.Signal.Lookup (this, "insert-media-request", 
                    new InsertMediaRequestSignalDelegate(InsertMediaRequestSignalCallback));
                sig.RemoveDelegate (value);
            }
        }

        [GLib.CDeclCallback]
        delegate int WarnDataLossSignalDelegate (IntPtr arg0, IntPtr gch);

        static int WarnDataLossSignalCallback (IntPtr arg0, IntPtr gch)
        {
            GLib.Signal sig = ((GCHandle) gch).Target as GLib.Signal;
            if (sig == null)
                throw new Exception("Unknown signal GC handle received " + gch);

            WarnDataLossArgs args = new WarnDataLossArgs ();
            WarnDataLossHandler handler = (WarnDataLossHandler) sig.Handler;
            handler (GLib.Object.GetObject (arg0), args);

            if (args.RetVal == null)
                throw new Exception("args.RetVal unset in callback");
            return ((int)args.RetVal);
        }

        [GLib.CDeclCallback]
        delegate int WarnDataLossVMDelegate (IntPtr recorder);

        static WarnDataLossVMDelegate WarnDataLossVMCallback;

        static int warndataloss_cb (IntPtr recorder)
        {
            BurnRecorder obj = GLib.Object.GetObject (recorder, false) as BurnRecorder;
            return obj.OnWarnDataLoss ();
        }

        private static void OverrideWarnDataLoss (GLib.GType gtype)
        {
            if (WarnDataLossVMCallback == null)
                WarnDataLossVMCallback = new WarnDataLossVMDelegate (warndataloss_cb);
            OverrideVirtualMethod (gtype, "warn-data-loss", WarnDataLossVMCallback);
        }

        [GLib.DefaultSignalHandler(Type=typeof(BurnRecorder), ConnectionMethod="OverrideWarnDataLoss")]
        protected virtual int OnWarnDataLoss ()
        {
            GLib.Value ret = new GLib.Value (GLib.GType.Int);
            GLib.ValueArray inst_and_params = new GLib.ValueArray (1);
            GLib.Value[] vals = new GLib.Value [1];
            vals [0] = new GLib.Value (this);
            inst_and_params.Append (vals [0]);
            g_signal_chain_from_overridden (inst_and_params.ArrayPtr, ref ret);
            foreach (GLib.Value v in vals)
                v.Dispose ();
            return (int) ret;
        }

        [GLib.Signal("warn-data-loss")]
        public event WarnDataLossHandler WarnDataLoss {
            add {
                GLib.Signal sig = GLib.Signal.Lookup (this, "warn-data-loss", 
                    new WarnDataLossSignalDelegate(WarnDataLossSignalCallback));
                sig.AddDelegate (value);
            }
            remove {
                GLib.Signal sig = GLib.Signal.Lookup (this, "warn-data-loss", 
                    new WarnDataLossSignalDelegate(WarnDataLossSignalCallback));
                sig.RemoveDelegate (value);
            }
        }

        [GLib.CDeclCallback]
        delegate void AnimationChangedSignalDelegate (IntPtr arg0, bool arg1, IntPtr gch);

        static void AnimationChangedSignalCallback (IntPtr arg0, bool arg1, IntPtr gch)
        {
            GLib.Signal sig = ((GCHandle) gch).Target as GLib.Signal;
            if (sig == null)
                throw new Exception("Unknown signal GC handle received " + gch);

            AnimationChangedArgs args = new AnimationChangedArgs ();
            args.Args = new object[1];
            args.Args[0] = arg1;
            AnimationChangedHandler handler = (AnimationChangedHandler) sig.Handler;
            handler (GLib.Object.GetObject (arg0), args);

        }

        [GLib.CDeclCallback]
        delegate void AnimationChangedVMDelegate (IntPtr recorder, bool spinning);

        static AnimationChangedVMDelegate AnimationChangedVMCallback;

        static void animationchanged_cb (IntPtr recorder, bool spinning)
        {
            BurnRecorder obj = GLib.Object.GetObject (recorder, false) as BurnRecorder;
            obj.OnAnimationChanged (spinning);
        }

        private static void OverrideAnimationChanged (GLib.GType gtype)
        {
            if (AnimationChangedVMCallback == null)
                AnimationChangedVMCallback = new AnimationChangedVMDelegate (animationchanged_cb);
            OverrideVirtualMethod (gtype, "animation-changed", AnimationChangedVMCallback);
        }

        [GLib.DefaultSignalHandler(Type=typeof(BurnRecorder), ConnectionMethod="OverrideAnimationChanged")]
        protected virtual void OnAnimationChanged (bool spinning)
        {
            GLib.Value ret = GLib.Value.Empty;
            GLib.ValueArray inst_and_params = new GLib.ValueArray (2);
            GLib.Value[] vals = new GLib.Value [2];
            vals [0] = new GLib.Value (this);
            inst_and_params.Append (vals [0]);
            vals [1] = new GLib.Value (spinning);
            inst_and_params.Append (vals [1]);
            g_signal_chain_from_overridden (inst_and_params.ArrayPtr, ref ret);
            foreach (GLib.Value v in vals)
                v.Dispose ();
        }

        [GLib.Signal("animation-changed")]
        public event AnimationChangedHandler AnimationChanged {
            add {
                GLib.Signal sig = GLib.Signal.Lookup (this, "animation-changed", 
                    new AnimationChangedSignalDelegate(AnimationChangedSignalCallback));
                sig.AddDelegate (value);
            }
            remove {
                GLib.Signal sig = GLib.Signal.Lookup (this, "animation-changed", 
                    new AnimationChangedSignalDelegate(AnimationChangedSignalCallback));
                sig.RemoveDelegate (value);
            }
        }
        
        #pragma warning restore 0169
    }
}
