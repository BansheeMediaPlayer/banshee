//
// MediaProfilerManager.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2007-2008 Novell, Inc.
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
using System.IO;
using System.Text;
using System.Xml;
using System.Collections;
using System.Collections.Generic;

using Banshee.ServiceStack;

namespace Banshee.MediaProfiles
{
    public class TestProfileArgs : EventArgs
    {
        private bool profile_available = true;
        private Profile profile;

        public TestProfileArgs(Profile profile)
        {
            this.profile = profile;
        }

        public Profile Profile {
            get { return profile; }
        }

        public bool ProfileAvailable {
            get { return profile_available; }
            set { profile_available = value; }
        }
    }

    public delegate void TestProfileHandler(object o, TestProfileArgs args);

    public class MediaProfileManager : IEnumerable<Profile>, IService
    {
        internal static System.Globalization.CultureInfo CultureInfo {
            get { return System.Globalization.CultureInfo.InvariantCulture; }
        }

        private XmlDocument document;
        private List<Profile> profiles;
        private Dictionary<string, PipelineVariable> preset_variables;

        public event TestProfileHandler TestProfile;
        public event EventHandler Initialized;

        public MediaProfileManager()
        {
        }

        private void Initialize ()
        {
            if (profiles != null) {
                return;
            }


            uint timer = Hyena.Log.DebugTimerStart ();

            profiles = new List<Profile> ();
            preset_variables = new Dictionary<string, PipelineVariable> ();

            string path = Hyena.Paths.GetInstalledDataDirectory ("audio-profiles");
            if(File.Exists(path)) {
                LoadFromFile(path);
            } else if(Directory.Exists(path)) {
                string base_file = Path.Combine(path, "base.xml");
                if(File.Exists(base_file)) {
                    LoadFromFile(base_file);
                }

                foreach(string file in Directory.GetFiles(path, "*.xml")) {
                    if(Path.GetFileName(file) != "base.xml") {
                        LoadFromFile(file);
                    }
                }
            }

            Hyena.Log.DebugTimerPrint (timer, "Initialized MediaProfileManager: {0}");

            EventHandler handler = Initialized;
            if (handler != null) {
                handler (this, EventArgs.Empty);
            }
        }

        private void LoadFromFile(string path)
        {
            document = new XmlDocument();

            try {
                document.Load(path);
                Load();
            } catch(Exception e) {
                Console.WriteLine("Could not load profile: {0}\n{1}", path, e);
            }

            document = null;
        }

        private void Load()
        {
            LoadPresetVariables(document.DocumentElement.SelectSingleNode("/audio-profiles/preset-variables"));
            LoadProfiles(document.DocumentElement.SelectSingleNode("/audio-profiles/profiles"));
        }

        private void LoadPresetVariables(XmlNode node)
        {
            if(node == null) {
                return;
            }

            foreach(XmlNode variable_node in node.SelectNodes("variable")) {
                try {
                    PipelineVariable variable = new PipelineVariable(variable_node);
                    if(!preset_variables.ContainsKey(variable.Id)) {
                        preset_variables.Add(variable.Id, variable);
                    }
                } catch {
                }
            }
        }

        private void LoadProfiles(XmlNode node)
        {
            if(node == null) {
                return;
            }

            foreach(XmlNode profile_node in node.SelectNodes("profile")) {
                try {
                    Add (new Profile(this, profile_node));
                } catch(Exception e) {
                    Console.WriteLine(e);
                }
            }
        }

        private Dictionary<string, string> mimetype_extensions = new Dictionary<string, string> ();
        public void Add(Profile profile)
        {
            Initialize ();
            foreach (string mimetype in profile.MimeTypes) {
                mimetype_extensions[mimetype] = profile.OutputFileExtension;
            }
            profiles.Add(profile);
        }

        public void Remove(Profile profile)
        {
            Initialize ();
            profiles.Remove(profile);
        }

        public PipelineVariable GetPresetPipelineVariableById(string id)
        {
            if(id == null) {
                throw new ArgumentNullException("id");
            }

            Initialize ();
            return preset_variables[id];
        }

        protected virtual bool OnTestProfile(Profile profile)
        {
            TestProfileHandler handler = TestProfile;
            if(handler == null) {
                return true;
            }

            TestProfileArgs args = new TestProfileArgs(profile);
            handler(this, args);
            return args.ProfileAvailable;
        }

        public IEnumerable<Profile> GetAvailableProfiles()
        {
            Initialize ();

            foreach(Profile profile in this) {
                if(profile.Available == null) {
                    profile.Available = OnTestProfile(profile);
                }

                if(profile.Available == true) {
                    yield return profile;
                }
            }
        }

        public ProfileConfiguration GetActiveProfileConfiguration (string id)
        {
            Initialize ();

            return ProfileConfiguration.LoadActive (this, id);
        }

        public ProfileConfiguration GetActiveProfileConfiguration(string id, string [] mimetypes)
        {
            Initialize ();

            ProfileConfiguration config = GetActiveProfileConfiguration (id);
            if (config != null) {
                // Ensure the profile configuration is valid for the mimetype restriction
                foreach (string profile_mimetype in config.Profile.MimeTypes) {
                    foreach (string mimetype in mimetypes) {
                        if (mimetype == profile_mimetype) {
                            return config;
                        }
                    }
                }
            }

            foreach(string mimetype in mimetypes) {
                Profile profile = GetProfileForMimeType(mimetype);
                if(profile != null) {
                    profile.LoadConfiguration (id);
                    return profile.Configuration;
                }
            }

            return null;
        }

        public void TestAll()
        {
            Initialize ();

            foreach(Profile profile in this) {
                profile.Available = OnTestProfile(profile);
            }
        }

        public Profile GetProfileForMimeType(string mimetype)
        {
            Initialize ();

            foreach(Profile profile in GetAvailableProfiles()) {
                if(profile.HasMimeType(mimetype)) {
                    return profile;
                }
            }

            return null;
        }

        public Profile GetProfileForExtension (string extension)
        {
            if (extension == null || extension.Length < 2)
                return null;

            if (extension[0] == '.')
                extension = extension.Substring (1, extension.Length - 1);

            Initialize ();

            foreach (Profile profile in this) {
                if (profile.OutputFileExtension == extension) {
                    return profile;
                }
            }
            return null;
        }

        public string GetExtensionForMimeType (string mimetype)
        {
            Initialize ();

            if (mimetype != null && mimetype_extensions.ContainsKey (mimetype))
                return mimetype_extensions[mimetype];
            return null;
        }

        public IEnumerator<Profile> GetEnumerator()
        {
            Initialize ();
            return profiles.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            Initialize ();
            return profiles.GetEnumerator();
        }

        public int ProfileCount {
            get { Initialize (); return profiles.Count; }
        }

        public int AvailableProfileCount {
            get {
                Initialize ();

                int count = 0;
                #pragma warning disable 0168, 0219
                foreach(Profile profile in GetAvailableProfiles()) {
                    count++;
                }
                #pragma warning restore 0168, 0219
                return count;
            }
        }

        string Banshee.ServiceStack.IService.ServiceName {
            get { return "MediaProfileManager"; }
        }

        public override string ToString()
        {
            Initialize ();
            StringBuilder builder = new StringBuilder();

            builder.Append("Preset Pipeline Variables:\n\n");
            foreach(PipelineVariable variable in preset_variables.Values) {
                builder.Append(variable);
                builder.Append("\n");
            }

            builder.Append("Profiles:\n\n");
            foreach(Profile profile in profiles) {
                builder.Append(profile);
                builder.Append("\n\n");
            }

            return builder.ToString().Trim();
        }
    }
}

