//
// EqualizerManager.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//   Alexander Hixon <hixon.alexander@mediati.org>
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
using System.IO;
using System.Xml;
using System.Collections;
using System.Collections.Generic;

using Mono.Unix;

using Hyena;
using Hyena.Json;

using Banshee.Base;
using Banshee.MediaEngine;
using Banshee.ServiceStack;
using Banshee.Configuration;

namespace Banshee.Equalizer
{
    public class EqualizerManager : IEnumerable<EqualizerSetting>, IEnumerable
    {
        private string legacy_xml_path = System.IO.Path.Combine (
            Paths.ApplicationData, "equalizers.xml");

        private static EqualizerManager instance;
        public static EqualizerManager Instance {
            get {
                if (instance == null) {
                    instance = new EqualizerManager (System.IO.Path.Combine (
                        Paths.ApplicationData, "equalizers.json"));
                }

                return instance;
            }
        }

        private List<EqualizerSetting> equalizers = new List<EqualizerSetting> ();

        public string Path { get; private set; }
        public EqualizerSetting SelectedEqualizer { get; private set; }

        public delegate void EqualizerSettingEventHandler (object o, Hyena.EventArgs<EqualizerSetting> args);
        public event EqualizerSettingEventHandler EqualizerAdded;
        public event EqualizerSettingEventHandler EqualizerRemoved;
        public event EqualizerSettingEventHandler EqualizerChanged;

        private EqualizerManager (string path)
        {
            Path = path;

            try {
                Load ();
            } catch (Exception e) {
                Log.Error ("Failed to load equalizer", e);
            }
        }

        public void Add (EqualizerSetting eq)
        {
            eq.Changed += OnEqualizerSettingChanged;
            equalizers.Add (eq);
            QueueSave ();
            OnEqualizerAdded (eq);
        }

        public void Remove (EqualizerSetting eq)
        {
            Remove (eq, true);
        }

        private void Remove (EqualizerSetting eq, bool shouldQueueSave)
        {
            if (eq == null || eq.IsReadOnly) {
                return;
            }

            eq.Changed -= OnEqualizerSettingChanged;
            equalizers.Remove (eq);
            OnEqualizerRemoved (eq);

            if (shouldQueueSave) {
                QueueSave ();
            }
        }

        public void Clear ()
        {
            while (equalizers.Count > 0) {
                Remove (equalizers[0], false);
            }

            QueueSave ();
        }

        public EqualizerSetting Find (string name)
        {
            return String.IsNullOrEmpty (name) ? null : equalizers.Find (eq => eq.Name == name);
        }

        public void Select ()
        {
            Select (PresetSchema.Get ());
        }

        public void Select (string name)
        {
            Select (Find (name));
        }

        public void Select (EqualizerSetting eq)
        {
            if (SelectedEqualizer == eq) {
                return;
            }

            bool sync = SelectedEqualizer != eq;
            SelectedEqualizer = eq;

            if (eq != null) {
                PresetSchema.Set (eq.Name);
                Log.DebugFormat ("Selected equalizer: {0}", eq.Name);
            }

            if (IsActive && sync) {
                FlushToEngine (eq);
            }
        }

        private void FlushToEngine (EqualizerSetting eq)
        {
            if (eq == null) {
                var engine_eq = (IEqualizer)ServiceManager.PlayerEngine.ActiveEngine;
                engine_eq.AmplifierLevel = 0;
                for (uint i = 0; i < engine_eq.EqualizerFrequencies.Length; i++) {
                    engine_eq.SetEqualizerGain (i, 0);
                }

                Log.DebugFormat ("Disabled equalizer");
            } else {
                eq.FlushToEngine ();
                Log.DebugFormat ("Syncing equalizer to engine: {0}", eq.Name);
            }
        }

        public bool IsActive {
            get { return EnabledSchema.Get (); }
            set {
                EnabledSchema.Set (value);

                if (value) {
                    if (SelectedEqualizer != null) {
                        FlushToEngine (SelectedEqualizer);
                    }
                } else {
                    FlushToEngine (null);
                }
            }
        }

        public void Load ()
        {
            var timer = Log.DebugTimerStart ();

            if (equalizers.Count > 0) {
                Clear ();
            }

            try {
                if (Banshee.IO.File.Exists (new SafeUri (Path))) {
                    using (var reader = new StreamReader (Path)) {
                        var deserializer = new Deserializer (reader);
                        foreach (var node in (JsonArray)deserializer.Deserialize ()) {
                            var eq_data = (JsonObject)node;
                            var name = (string)eq_data["name"];
                            var preamp = Convert.ToDouble (eq_data["preamp"]);
                            var bands = (JsonArray)eq_data["bands"];

                            var eq = new EqualizerSetting (this, name);
                            eq.SetAmplifierLevel (preamp, false);
                            for (uint band = 0; band < bands.Count; band++) {
                                eq.SetGain (band, Convert.ToDouble (bands[(int)band]), false);
                            }
                            Add (eq);
                        }
                    }
                } else if (Banshee.IO.File.Exists (new SafeUri (legacy_xml_path))) {
                    try {
                        using (var reader = new XmlTextReader (legacy_xml_path)) {
                            if (reader.ReadToDescendant ("equalizers")) {
                                while (reader.ReadToFollowing ("equalizer")) {
                                    var eq = new EqualizerSetting (this, reader["name"]);
                                    while (reader.Read () && !(reader.NodeType == XmlNodeType.EndElement &&
                                        reader.Name == "equalizer")) {
                                        if (reader.NodeType != XmlNodeType.Element) {
                                            continue;
                                        } else if (reader.Name == "preamp") {
                                            eq.SetAmplifierLevel (reader.ReadElementContentAsDouble (), false);
                                        } else if (reader.Name == "band") {
                                            eq.SetGain (Convert.ToUInt32 (reader["num"]),
                                                reader.ReadElementContentAsDouble (), false);
                                        }
                                    }
                                    Add (eq);
                                }
                            }
                        }
                        Log.Information ("Converted legacy XML equalizer presets to new JSON format");
                    } catch (Exception xe) {
                        Log.Error ("Could not load equalizers.xml", xe);
                    }
                }
            } catch (Exception e) {
                Log.Error ("Could not load equalizers.json", e);
            }

            Log.DebugTimerPrint (timer, "Loaded equalizer presets: {0}");

            equalizers.AddRange (GetDefaultEqualizers ());
            Select ();
        }

        private IEnumerable<EqualizerSetting> GetDefaultEqualizers ()
        {
            yield return new EqualizerSetting (this, Catalog.GetString ("Classical"), 0, new [] {
                0, 0, 0, 0, 0, 0, -7.2, -7.2, -7.2, -9.6
            });
            yield return new EqualizerSetting (this, Catalog.GetString ("Club"), 0, new [] {
                0, 0, 8, 5.6, 5.6, 5.6, 3.2, 0, 0, 0
            });
            yield return new EqualizerSetting (this, Catalog.GetString ("Dance"), -1.1, new [] {
                9.6, 7.2, 2.4, -1.1, -1.1, -5.6, -7.2, -7.2, -1.1, -1.1
            });
            yield return new EqualizerSetting (this, Catalog.GetString ("Full Bass"), -1.1, new [] {
                -8, 9.6, 9.6, 5.6, 1.6, -4, -8, -10.4, -11.2, -11.2
            });
            yield return new EqualizerSetting (this, Catalog.GetString ("Full Bass and Treble"), -1.1, new [] {
                7.2, 5.6, -1.1, -7.2, -4.8, 1.6, 8, 11.2, 12, 12
            });
            yield return new EqualizerSetting (this, Catalog.GetString ("Full Treble"), -1.1, new [] {
                -9.6, -9.6, -9.6, -4, 2.4, 11.2, 11.5, 11.8, 11.8, 12
            });
            yield return new EqualizerSetting (this, Catalog.GetString ("Laptop Speakers and Headphones"), -1.1, new [] {
                4.8, 11.2, 5.6, -3.2, -2.4, 1.6, 4.8, 9.6, 11.9, 11.9
            });
            yield return new EqualizerSetting (this, Catalog.GetString ("Large Hall"), -1.1, new [] {
                10.4, 10.4, 5.6, 5.6, -1.1, -4.8, -4.8, -4.8, -1.1, -1.1
            });
            yield return new EqualizerSetting (this, Catalog.GetString ("Live"), -1.1, new [] {
                -4.8, -1.1, 4, 5.6, 5.6, 5.6, 4, 2.4, 2.4, 2.4
            });
            yield return new EqualizerSetting (this, Catalog.GetString ("Party"), -1.1, new [] {
                7.2, 7.2, -1.1, -1.1, -1.1, -1.1, -1.1, -1.1, 7.2, 7.2
            });
            yield return new EqualizerSetting (this, Catalog.GetString ("Pop"), -1.1, new [] {
                -1.6, 4.8, 7.2, 8, 5.6, -1.1, -2.4, -2.4, -1.6, -1.6
            });
            yield return new EqualizerSetting (this, Catalog.GetString ("Reggae"), -1.1, new [] {
                -1.1, -1.1, -1.1, -5.6, -1.1, 6.4, 6.4, -1.1, -1.1, -1.1
            });
            yield return new EqualizerSetting (this, Catalog.GetString ("Rock"), -1.1, new [] {
                8, 4.8, -5.6, -8, -3.2, 4, 8.8, 11.2, 11.2, 11.2
            });
            yield return new EqualizerSetting (this, Catalog.GetString ("Ska"), -1.1, new [] {
                -2.4, -4.8, -4, -1.1, 4, 5.6, 8.8, 9.6, 11.2, 9.6
            });
            yield return new EqualizerSetting (this, Catalog.GetString ("Smiley Face Curve"), -7, new [] {
                12, 8, 6, 3, 0.0, 0.0, 3, 6, 8, 12
            });
            yield return new EqualizerSetting (this, Catalog.GetString ("Soft"), -1.1, new [] {
                4.8, 1.6, -1.1, -2.4, -1.1, 4, 8, 9.6, 11.2, 12,
            });
            yield return new EqualizerSetting (this, Catalog.GetString ("Soft Rock"), -1.1, new [] {
                4, 4, 2.4, -1.1, -4, -5.6, -3.2, -1.1, 2.4, 8.8,
            });
            yield return new EqualizerSetting (this, Catalog.GetString ("Techno"), -1.1, new [] {
                8, 5.6, -1.1, -5.6, -4.8, -1.1, 8, 9.6, 9.6, 8.8
            });
        }

        public void Save ()
        {
            try {
                using (var writer = new StreamWriter (Path)) {
                    writer.Write ("[");
                    writer.WriteLine ();
                    for (int i = 0; i < equalizers.Count; i++) {
                        if (equalizers[i].IsReadOnly) {
                            continue;
                        }
                        writer.Write (equalizers[i]);
                        if (i < equalizers.Count - 1) {
                            writer.Write (",");
                        }
                        writer.WriteLine ();
                    }
                    writer.Write ("]");
                }
                Log.Debug ("EqualizerManager", "Saved equalizers to disk");
            } catch (Exception e) {
                Log.Error ("Unable to save equalizers", e);
            }
        }

        protected virtual void OnEqualizerAdded (EqualizerSetting eq)
        {
            EqualizerSettingEventHandler handler = EqualizerAdded;
            if (handler != null) {
                handler (this, new EventArgs<EqualizerSetting> (eq));
            }
        }

        protected virtual void OnEqualizerRemoved (EqualizerSetting eq)
        {
            EqualizerSettingEventHandler handler = EqualizerRemoved;
            if (handler != null) {
                handler (this, new EventArgs<EqualizerSetting> (eq));
            }
        }

        protected virtual void OnEqualizerChanged (EqualizerSetting eq)
        {
            EqualizerSettingEventHandler handler = EqualizerChanged;
            if (handler != null) {
                handler (this, new EventArgs<EqualizerSetting> (eq));
            }
        }

        private void OnEqualizerSettingChanged (object o, EventArgs args)
        {
            OnEqualizerChanged (o as EqualizerSetting);
            QueueSave ();
        }

        private uint queue_save_id = 0;
        private void QueueSave ()
        {
            if (queue_save_id > 0) {
                return;
            }

            queue_save_id = GLib.Timeout.Add (2500, delegate {
                Save ();
                queue_save_id = 0;
                return false;
            });
        }

        IEnumerator IEnumerable.GetEnumerator ()
        {
            return equalizers.GetEnumerator ();
        }

        public IEnumerator<EqualizerSetting> GetEnumerator ()
        {
            return equalizers.GetEnumerator ();
        }

        public static readonly SchemaEntry<bool> EnabledSchema = new SchemaEntry<bool> (
            "player_engine", "equalizer_enabled",
            false,
            "Equalizer status",
            "Whether or not the equalizer is set to be enabled."
        );

        public static readonly SchemaEntry<string> PresetSchema = new SchemaEntry<string> (
            "player_engine", "equalizer_preset",
            "Rock",
            "Equalizer preset",
            "Default preset to load into equalizer."
        );
    }
}
