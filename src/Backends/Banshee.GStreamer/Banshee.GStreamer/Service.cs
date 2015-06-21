//
// Service.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright 2006-2010 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Hyena;
using Hyena.SExpEngine;
using Banshee.ServiceStack;
using Banshee.MediaProfiles;

namespace Banshee.GStreamer
{
    public class Service : IExtensionService
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void BansheeLogHandler (LogEntryType type, IntPtr component, IntPtr message);

        private BansheeLogHandler native_log_handler = null;

        public Service ()
        {
        }

        [DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern void gstreamer_initialize (bool debugging, BansheeLogHandler log_handler);

        void IExtensionService.Initialize ()
        {
            bool debugging = ApplicationContext.Debugging;
            if (debugging) {
                native_log_handler = new BansheeLogHandler (NativeLogHandler);
            }

            gstreamer_initialize (debugging, native_log_handler);

            var profile_manager = ServiceManager.MediaProfileManager;
            if (profile_manager != null) {
                profile_manager.Initialized += OnMediaProfileManagerInitialized;
            }
        }

        private void OnMediaProfileManagerInitialized (object o, EventArgs args)
        {
            MediaProfileManager profile_manager = ServiceManager.MediaProfileManager;
            if (profile_manager != null) {
                Pipeline.AddSExprFunction ("gst-element-is-available", SExprTestElement);
                Pipeline.AddSExprFunction ("gst-construct-pipeline", SExprConstructPipeline);
                Pipeline.AddSExprFunction ("gst-construct-caps", SExprConstructCaps);
                Pipeline.AddSExprFunction ("gst-construct-element", SExprConstructElement);

                profile_manager.TestProfile += OnTestMediaProfile;
                profile_manager.TestAll ();
            }
        }

        void IDisposable.Dispose ()
        {
        }

        private void NativeLogHandler (LogEntryType type, IntPtr componentPtr, IntPtr messagePtr)
        {
            string component = componentPtr == IntPtr.Zero ? null : GLib.Marshaller.Utf8PtrToString (componentPtr);
            string message = componentPtr == IntPtr.Zero ? null : GLib.Marshaller.Utf8PtrToString (messagePtr);

            if (message == null) {
                return;
            } else if (component != null) {
                message = String.Format ("(libbanshee:{0}) {1}", component, message);
            }

            Log.Commit (type, message, null, false);
        }

        private static void OnTestMediaProfile (object o, TestProfileArgs args)
        {
            bool no_test = ApplicationContext.EnvironmentIsSet ("BANSHEE_PROFILES_NO_TEST");
            bool available = false;

            foreach (Pipeline.Process process in args.Profile.Pipeline.GetPendingProcessesById ("gstreamer")) {
                string pipeline = args.Profile.Pipeline.CompileProcess (process);
                if (no_test || TestPipeline (pipeline)) {
                    args.Profile.Pipeline.AddProcess (process);
                    available = true;
                    break;
                } else if (!no_test) {
                    Hyena.Log.DebugFormat ("GStreamer pipeline does not run: {0}", pipeline);
                }
            }

            args.ProfileAvailable = available;
        }

        [DllImport (PlayerEngine.LibBansheeLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool gstreamer_test_pipeline (IntPtr pipeline);

        internal static bool TestPipeline (string pipeline)
        {
            if (String.IsNullOrEmpty (pipeline)) {
                return false;
            }

            IntPtr pipeline_ptr = GLib.Marshaller.StringToPtrGStrdup (pipeline);

            if (pipeline_ptr == IntPtr.Zero) {
                return false;
            }

            try {
                return gstreamer_test_pipeline (pipeline_ptr);
            } finally {
                GLib.Marshaller.Free (pipeline_ptr);
            }
        }

        private TreeNode SExprTestElement (EvaluatorBase evaluator, TreeNode [] args)
        {
            if (args.Length != 1) {
                throw new ArgumentException ("gst-test-element accepts one argument");
            }

            TreeNode arg = evaluator.Evaluate (args[0]);
            if (!(arg is StringLiteral)) {
                throw new ArgumentException ("gst-test-element requires a string argument");
            }

            StringLiteral element_node = (StringLiteral)arg;
            return new BooleanLiteral (TestPipeline (element_node.Value));
        }

        private TreeNode SExprConstructPipeline (EvaluatorBase evaluator, TreeNode [] args)
        {
            StringBuilder builder = new StringBuilder ();
            List<string> elements = new List<string> ();

            for (int i = 0; i < args.Length; i++) {
                TreeNode node = evaluator.Evaluate (args[i]);
                if (!(node is LiteralNodeBase)) {
                    throw new ArgumentException ("node must evaluate to a literal");
                }

                string value = node.ToString ().Trim ();

                if (value.Length == 0) {
                    continue;
                }

                elements.Add (value);
            }

            for (int i = 0; i < elements.Count; i++) {
                builder.Append (elements[i]);
                if (i < elements.Count - 1) {
                    builder.Append (" ! ");
                }
            }

            return new StringLiteral (builder.ToString ());
        }

        private TreeNode SExprConstructElement (EvaluatorBase evaluator, TreeNode [] args)
        {
            return SExprConstructPipelinePart (evaluator, args, true);
        }

        private TreeNode SExprConstructCaps (EvaluatorBase evaluator, TreeNode [] args)
        {
            return SExprConstructPipelinePart (evaluator, args, false);
        }

        private TreeNode SExprConstructPipelinePart (EvaluatorBase evaluator, TreeNode [] args, bool element)
        {
            StringBuilder builder = new StringBuilder ();

            TreeNode list = new TreeNode ();
            foreach (TreeNode arg in args) {
                list.AddChild (evaluator.Evaluate (arg));
            }

            list = list.Flatten ();

            for (int i = 0; i < list.ChildCount; i++) {
                TreeNode node = list.Children[i];

                string value = node.ToString ().Trim ();

                builder.Append (value);

                if (i == 0) {
                    if (list.ChildCount > 1) {
                        builder.Append (element ? ' ' : ',');
                    }

                    continue;
                } else if (i % 2 == 1) {
                    builder.Append ('=');
                } else if (i < list.ChildCount - 1) {
                    builder.Append (element ? ' ' : ',');
                }
            }

            return new StringLiteral (builder.ToString ());
        }

        string IService.ServiceName {
            get { return "GStreamerCoreService"; }
        }
    }
}
