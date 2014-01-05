//
// MediaProfileBackend.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//   Bertrand Lorentz <bertrand.lorentz@gmail.com>
//
// Copyright 2006-2010 Novell, Inc.
// Copyright 2014 Bertrand Lorentz
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
using System.Collections.Generic;
using System.Text;

using Hyena;
using Hyena.SExpEngine;
using Banshee.MediaProfiles;
using Banshee.ServiceStack;

namespace Banshee.GStreamerSharp
{
    public static class MediaProfileBackend
    {
        public static void OnMediaProfileManagerInitialized (object o, EventArgs args)
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

        internal static bool TestPipeline (string pipeline)
        {
            if (String.IsNullOrEmpty (pipeline)) {
                return false;
            }

            try {
                Gst.Parse.Launch (pipeline);
            } catch (GLib.GException) {
                return false;
            }
            return true;
        }

        private static TreeNode SExprTestElement (EvaluatorBase evaluator, TreeNode [] args)
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

        private static TreeNode SExprConstructPipeline (EvaluatorBase evaluator, TreeNode [] args)
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

        private static TreeNode SExprConstructElement (EvaluatorBase evaluator, TreeNode [] args)
        {
            return SExprConstructPipelinePart (evaluator, args, true);
        }

        private static TreeNode SExprConstructCaps (EvaluatorBase evaluator, TreeNode [] args)
        {
            return SExprConstructPipelinePart (evaluator, args, false);
        }

        private static TreeNode SExprConstructPipelinePart (EvaluatorBase evaluator, TreeNode [] args, bool element)
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
    }
}

