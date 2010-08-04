//
// EqualizerView.cs
//
// Authors:
//   Aaron Bockover <abockover@novell.com>
//   Ivan N. Zlatev <contact i-nZ.net>
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
using System.Collections;

using Gtk;
using Gdk;
using Mono.Unix;

using Hyena;
using Banshee.ServiceStack;
using Banshee.MediaEngine;

namespace Banshee.Equalizer.Gui
{
    public class EqualizerView : HBox
    {
        private HBox band_box;
        private uint [] frequencies;
        private EqualizerBandScale [] band_scales;
        private EqualizerBandScale amplifier_scale;
        private EqualizerSetting active_eq;
        private int [] range = new int[3];
        private bool loading;

        public event EqualizerChangedEventHandler EqualizerChanged;
        public event AmplifierChangedEventHandler AmplifierChanged;

        public EqualizerView () : base ()
        {
            BuildWidget ();
        }

        private void BuildWidget ()
        {
            Spacing = 10;

            int [] br = ((IEqualizer)ServiceManager.PlayerEngine.ActiveEngine).BandRange;
            int mid = (br[0] + br[1]) / 2;

            range[0] = br[0];
            range[1] = mid;
            range[2] = br[1];

            amplifier_scale = new EqualizerBandScale (0, range[1] * 10, range[0] * 10, range[2] * 10,
                                                      Catalog.GetString ("Preamp"));
            amplifier_scale.ValueChanged += OnAmplifierValueChanged;
            amplifier_scale.Show ();
            PackStart (amplifier_scale, false, false, 0);

            EqualizerLevelsBox eq_levels = new EqualizerLevelsBox (
                FormatDecibelString (range[2]),
                FormatDecibelString (range[1]),
                FormatDecibelString (range[0])
            );

            eq_levels.Show ();
            PackStart (eq_levels, false, false, 0);

            band_box = new HBox ();
            band_box.Homogeneous = true;
            band_box.Show ();
            PackStart (band_box, true, true, 0);

            BuildBands ();
        }

        private string FormatDecibelString (int db)
        {
            // Translators: {0} is a numerical value, and dB is the Decibel symbol
            if (db > 0) {
                return String.Format (Catalog.GetString ("+{0} dB"), db);
            } else {
                return String.Format (Catalog.GetString ("{0} dB"), db);
            }
        }

        private void BuildBands ()
        {
            foreach (Widget widget in band_box.Children) {
                band_box.Remove (widget);
            }

            if (frequencies == null || frequencies.Length <= 0) {
                frequencies = new uint[0];
                band_scales = new EqualizerBandScale[0];
                return;
            }

            band_scales = new EqualizerBandScale[10];

            for (uint i = 0; i < 10; i++) {
                // Translators: {0} is a numerical value, Hz and kHz are Hertz unit symbols
                string label = frequencies[i] < 1000 ?
                    String.Format (Catalog.GetString ("{0} Hz"), frequencies[i]) :
                    String.Format (Catalog.GetString ("{0} kHz"), (int)Math.Round (frequencies[i] / 1000.0));

                band_scales[i] = new EqualizerBandScale (i, range[1] * 10, range[0] * 10, range[2] * 10, label);
                band_scales[i].ValueChanged += OnEqualizerValueChanged;
                band_scales[i].Show ();

                band_box.PackStart (band_scales[i], true, true, 0);
            }
        }

        private void OnEqualizerValueChanged (object o, EventArgs args)
        {
            if (loading) {
                return;
            }

            EqualizerBandScale scale = o as EqualizerBandScale;

            if (active_eq != null) {
                active_eq.SetGain (scale.Band, (double)scale.Value / 10, true);
            }

            if (EqualizerChanged != null) {
                EqualizerChanged (this, new EqualizerChangedEventArgs (scale.Band, scale.Value));
            }
        }

        private void OnAmplifierValueChanged (object o, EventArgs args)
        {
            if (loading) {
                return;
            }

            EqualizerBandScale scale = o as EqualizerBandScale;
            if (active_eq != null) {
                active_eq.AmplifierLevel = (double) (scale.Value / 10.0);
            }

            if (AmplifierChanged != null) {
                AmplifierChanged (this, new EventArgs<int> (scale.Value));
            }
        }

        public uint [] Frequencies {
            get { return (uint [])frequencies.Clone (); }
            set {
                frequencies = (uint [])value.Clone ();
                BuildBands ();
            }
        }

        public int [] Preset {
            get {
                int [] result = new int[band_scales.Length];
                for (int i = 0; i < band_scales.Length; i++) {
                    result[i] = (int) band_scales[i].Value;
                }

                return result;
            }

            set {
                for (int i = 0; i < value.Length; i++) {
                    band_scales[i].Value = value[i];
                }
            }
        }

        public void SetBand (uint band, double value)
        {
            band_scales[band].Value = (int) (value * 10);
        }

        public double AmplifierLevel {
            get { return (double) amplifier_scale.Value / 10; }
            set { amplifier_scale.Value = (int) (value * 10); }
        }

        public EqualizerSetting EqualizerSetting {
            get { return active_eq; }
            set {
                if (active_eq == value) {
                    return;
                }

                loading = true;
                active_eq = value;

                if (active_eq == null) {
                    AmplifierLevel = 0;
                    loading = false;
                    return;
                }

                amplifier_scale.Sensitive = !value.IsReadOnly;
                AmplifierLevel = active_eq.AmplifierLevel;

                for (uint i = 0; i < active_eq.BandCount; i++) {
                    band_scales[i].Sensitive = !value.IsReadOnly;
                    SetBand (i, active_eq[i]);
                }

                loading = false;
            }
        }
    }

    public delegate void EqualizerChangedEventHandler (object o, EqualizerChangedEventArgs args);
    public delegate void AmplifierChangedEventHandler (object o, EventArgs<int> args);

    public sealed class EqualizerChangedEventArgs : EventArgs
    {
        private uint band;
        private int value;

        public EqualizerChangedEventArgs (uint band, int value)
        {
            this.band = band;
            this.value = value;
        }

        public uint Band {
            get { return this.band; }
        }

        public int Value {
            get { return this.value; }
        }
    }
}
