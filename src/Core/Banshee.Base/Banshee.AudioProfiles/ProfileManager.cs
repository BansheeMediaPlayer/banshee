/***************************************************************************
 *  ProfileManager.cs
 *
 *  Copyright (C) 2006 Novell, Inc.
 *  Written by Aaron Bockover <aaron@abock.org>
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
using System.IO;
using System.Text;
using System.Xml;
using System.Collections;
using System.Collections.Generic;

namespace Banshee.AudioProfiles
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

    public class ProfileManager : IEnumerable<Profile>
    {
        private static System.Globalization.CultureInfo culture_info = new System.Globalization.CultureInfo("en-US");
        
        internal static System.Globalization.CultureInfo CultureInfo {
            get { return culture_info; }
        }
    
        private XmlDocument document;
        private List<Profile> profiles = new List<Profile>();
        private Dictionary<string, PipelineVariable> preset_variables = new Dictionary<string, PipelineVariable>();

        public event TestProfileHandler TestProfile;

        public ProfileManager(string path)
        {
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
                    if(!preset_variables.ContainsKey(variable.ID)) {
                        preset_variables.Add(variable.ID, variable);
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
                    profiles.Add(new Profile(this, profile_node));
                } catch(Exception e) {
                    Console.WriteLine(e);
                }
            }
        }

        public void Add(Profile profile)
        {
            profiles.Add(profile);
        }

        public void Remove(Profile profile)
        {
            profiles.Remove(profile);
        }

        public PipelineVariable GetPresetPipelineVariableById(string id)
        {
            if(id == null) {
                throw new ArgumentNullException("id");
            }

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
            foreach(Profile profile in this) {
                if(profile.Available == null) {
                    profile.Available = OnTestProfile(profile);
                }
                
                if(profile.Available == true) {
                    yield return profile;
                }
            }
        }
        
        public Profile GetConfiguredActiveProfile(string id)
        {
            return ProfileConfiguration.LoadActiveProfile(this, id);
        }
        
        public Profile GetConfiguredActiveProfile(string id, string [] mimetypes)
        {
            Profile profile = GetConfiguredActiveProfile(id);
            if(profile != null) {
                profile.LoadConfiguration(id);
                return profile;
            }
            
            foreach(string mimetype in mimetypes) {
                profile = GetProfileForMimeType(mimetype);
                if(profile != null) {
                    profile.LoadConfiguration(id);
                    return profile;
                }
            }
            
            return null;
        }
        
        public void TestAll()
        {
            foreach(Profile profile in this) {
                profile.Available = OnTestProfile(profile);
            }
        }
        
        public Profile GetProfileForMimeType(string mimetype)
        {
            foreach(Profile profile in GetAvailableProfiles()) {
                if(profile.HasMimeType(mimetype)) {
                    return profile;
                }
            }
            
            return null;
        }

        public IEnumerator<Profile> GetEnumerator()
        {
            return profiles.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return profiles.GetEnumerator();
        }
        
        public int ProfileCount {
            get { return profiles.Count; }
        }
        
        public int AvailableProfileCount {
            get {
                int count = 0;
                foreach(Profile profile in GetAvailableProfiles()) {
                    count++;
                }
                return count;
            }
        }
        
        public override string ToString()
        {
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

