//
// DefaultApplicationHelper.cs
//
// Authors:
//   Gabriel Burt <gburt@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
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
using Hyena;
using Mono.Addins;

using Banshee.Configuration;

namespace Banshee.Configuration
{
    public interface IDefaultHelper
    {
        bool IsDefault { get; }
        void MakeDefault ();
    }

    public static class DefaultApplicationHelper
    {
        public static SchemaEntry<bool> MakeDefaultSchema = new SchemaEntry<bool> ("core", "make_default", true,
            "Whether to ensure Banshee is the default media player every time it starts", null);

        public static SchemaEntry<bool> RememberChoiceSchema = new SchemaEntry<bool> ("core", "remember_make_default", false,
            "Whether to remember whether to ensure Banshee is the default media player every time it starts", null);

        public static SchemaEntry<bool> EverAskedSchema = new SchemaEntry<bool> ("core", "ever_asked_make_default", false,
            "Whether the user has ever responded to whether they'd like to make Banshee the default player", null);

        private static IDefaultHelper helper;
        private static IDefaultHelper Helper {
            get {
                if (helper == null) {
                    foreach (TypeExtensionNode node in AddinManager.GetExtensionNodes ("/Banshee/Platform/DefaultApplicationHelper")) {
                        try {
                            helper = (IDefaultHelper)node.CreateInstance (typeof (IDefaultHelper));
                            Log.DebugFormat ("Loaded Default Application Helper: {0}", helper.GetType ().FullName);
                            break;
                        } catch (Exception e) {
                            Log.Error ("Default Application Helper backend failed to load", e);
                        }
                    }
                }
                return helper;
            }
        }

        public static bool NeverAsk {
            get {
                return EverAskedSchema.Get () && RememberChoiceSchema.Get () && !MakeDefaultSchema.Get ();
            }
        }

        public static bool HaveHelper {
            get { return Helper != null; }
        }

        public static bool IsDefault {
            get { return Helper.IsDefault; }
        }

        public static void MakeDefault ()
        {
            Helper.MakeDefault ();
        }
    }
}
