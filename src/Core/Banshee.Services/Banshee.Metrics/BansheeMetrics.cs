//
// BansheeMetrics.cs
//
// Author:
//   Gabriel Burt <gabriel.burt@gmail.com>
//
// Copyright (c) 2010 Novell, Inc.
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
using System.Linq;
using System.Reflection;
using Mono.Unix;

using Hyena;
using Hyena.Metrics;
using Hyena.Data.Sqlite;

using Banshee.Configuration;
using Banshee.ServiceStack;
using Banshee.Networking;
using Banshee.Sources;
using Banshee.PlaybackController;

namespace Banshee.Metrics
{
    public class BansheeMetrics : IDisposable
    {
        private static BansheeMetrics banshee_metrics;
        public static BansheeMetrics Instance { get { return banshee_metrics; } }

        public static event System.Action Started;
        public static event System.Action Stopped;

        public static void Start ()
        {
            // Only enable collection 5% of the time
            var one_in_twenty = new Random ().NextDouble () < 0.05;
            if (one_in_twenty && banshee_metrics == null) {
                Log.Information ("Starting collection of anonymous usage data");
                try {
                    banshee_metrics = new BansheeMetrics ();
                } catch (Exception e) {
                    Log.Error ("Failed to start collection of anonymous usage data", e);
                    banshee_metrics = null;
                }
            }
        }

        public static void Stop ()
        {
            if (banshee_metrics != null) {
                Log.Information ("Stopping collection of anonymous usage data");
                banshee_metrics.Dispose ();
                banshee_metrics = null;
            }
        }

        private MetricsCollection metrics;
        private string id_key = "AnonymousUsageData.Userid";
        private string last_post_key = "AnonymousUsageData.LastPostStamp";
        private Metric shutdown, duration, active_source_changed, sqlite_executed;
        private Metric playback_source_changed, shuffle_changed, repeat_changed;

        private BansheeMetrics ()
        {
            banshee_metrics = this;
            string unique_userid = DatabaseConfigurationClient.Client.Get<string> (id_key, null);

            if (String.IsNullOrEmpty (unique_userid)) {
                unique_userid = System.Guid.NewGuid ().ToString ();
                DatabaseConfigurationClient.Client.Set<string> (id_key, unique_userid);
            }

            metrics = new MetricsCollection (unique_userid, new DbSampleStore (
                ServiceManager.DbConnection, "AnonymousUsageData"
            ));

            Configuration.Start ();

            if (Application.ActiveClient != null && Application.ActiveClient.IsStarted) {
                Initialize (null);
            } else {
                Application.ClientStarted += Initialize;
            }
        }

        private void Initialize (Client client)
        {
            Application.ClientStarted -= Initialize;

            var handler = Started;
            if (handler != null) {
                handler ();
            }

            Application.RunTimeout (5*1000, delegate {
                if (BansheeMetrics.Instance == null) {
                    return false;
                }

                ThreadAssist.SpawnFromMain (delegate {
                    metrics.AddDefaults ();
                    AddMetrics ();

                    if (ApplicationContext.CommandLine.Contains ("debug-metrics")) {
                        Log.InformationFormat ("Anonymous usage data collected:\n{0}", metrics.ToJsonString ());
                        System.IO.File.WriteAllText ("usage-data.json", metrics.ToJsonString ());
                    }

                    if (!ServiceManager.Get<Network> ().Connected) {
                        return;
                    }

                    // Don't post to server more than every 48 hours
                    var last_post_time = DatabaseConfigurationClient.Client.Get<DateTime> (last_post_key, DateTime.MinValue);
                    var last_post_rel = (DateTime.Now - last_post_time).TotalDays;
                    if (last_post_rel < 0 || last_post_rel > 4.0) {
                        var poster = new HttpPoster ("http://metrics.banshee.fm/submit/", metrics);
                        bool posted = poster.Post ();
                        Log.InformationFormat ("Posted usage data? {0}", posted);

                        // Clear the old metrics, even if we failed to post them; it might be a server-side
                        // problem w/ the data we want to send (eg too big, out of space) and we don't want
                        // to keep retrying to send the same data.
                        metrics.Store.Clear ();
                        DatabaseConfigurationClient.Client.Set<DateTime> (last_post_key, DateTime.Now);
                    }
                });

                return false;
            });
        }

        private void AddMetrics ()
        {
            Add ("Client",       Application.ActiveClient);
            Add ("BuildHostCpu", Application.BuildHostCpu);
            Add ("BuildHostOS",  Application.BuildHostOperatingSystem);
            Add ("BuildTime",    Application.BuildTime);
            Add ("BuildVendor",  Application.BuildVendor);
            Add ("Version",      Application.Version);
            Add ("StartedAt",    ApplicationContext.StartedAt);

            // Query basic stats about what content the user has
            foreach (var src in ServiceManager.SourceManager.FindSources<PrimarySource> ()) {
                var type_name = src.TypeName;
                var reader = new HyenaDataReader (ServiceManager.DbConnection.Query (
                    @"SELECT COUNT(*),
                             COUNT(CASE ifnull(Rating, 0)          WHEN 0 THEN NULL ELSE 1 END),
                             COUNT(CASE ifnull(BPM, 0)             WHEN 0 THEN NULL ELSE 1 END),
                             COUNT(CASE ifnull(LastStreamError, 0) WHEN 0 THEN NULL ELSE 1 END),
                             COUNT(CASE ifnull(Composer, 0)        WHEN 0 THEN NULL ELSE 1 END),
                             COUNT(CASE ifnull(LicenseUri, 0)      WHEN 0 THEN NULL ELSE 1 END),
                             COUNT(CASE ifnull(Grouping, 0)        WHEN 0 THEN NULL ELSE 1 END),
                             COUNT(CASE PlayCount                  WHEN 0 THEN 1 ELSE NULL END),
                             AVG(Score),
                             AVG(BitRate),
                             SUM(PlayCount),
                             SUM(SkipCount),
                             CAST (SUM(PlayCount * (Duration/1000)) AS INTEGER),
                             SUM(FileSize)
                    FROM CoreTracks WHERE PrimarySourceID = ?", src.DbId
                ));

                // DateAdded, Grouping
                var results = new string [] {
                    "TrackCount", "RatedTrackCount", "BpmTrackCount", "ErrorTrackCount", "ComposerTrackCount",
                    "LicenseUriTrackCount", "GroupingTrackCount", "UnplayedTrackCount", "AvgScore",
                    "AvgBitRate", "TotalPlayCount", "TotalSkipCount", "TotalPlaySeconds", "TotalFileSize"
                };

                for (int i = 0; i < results.Length; i++) {
                    Add (String.Format ("{0}/{1}", type_name, results[i]), reader.Get<long> (i));
                }
                reader.Dispose ();
            }

            // Wire up event-triggered metrics
            active_source_changed = Add ("ActiveSourceChanged");
            ServiceManager.SourceManager.ActiveSourceChanged += OnActiveSourceChanged;

            shutdown = Add ("ShutdownAt",  () => DateTime.Now);
            duration = Add ("RunDuration", () => DateTime.Now - ApplicationContext.StartedAt);
            Application.ShutdownRequested += OnShutdownRequested;

            sqlite_executed = Add ("LongSqliteCommand");
            HyenaSqliteCommand.CommandExecuted += OnSqliteCommandExecuted;
            HyenaSqliteCommand.RaiseCommandExecuted = true;
            HyenaSqliteCommand.RaiseCommandExecutedThresholdMs = 400;

            playback_source_changed = Add ("PlaybackSourceChanged");
            ServiceManager.PlaybackController.SourceChanged += OnPlaybackSourceChanged;

            shuffle_changed = Add ("ShuffleModeChanged");
            ServiceManager.PlaybackController.ShuffleModeChanged += OnShuffleModeChanged;

            repeat_changed = Add ("RepeatModeChanged");
            ServiceManager.PlaybackController.RepeatModeChanged += OnRepeatModeChanged;
        }

        public Metric Add (string name)
        {
            return metrics.Add (String.Format ("Banshee/{0}", name));
        }

        public Metric Add (string name, object value)
        {
            return metrics.Add (String.Format ("Banshee/{0}", name), value);
        }

        public Metric Add (string name, Func<object> func)
        {
            return metrics.Add (String.Format ("Banshee/{0}", name), func);
        }

        public void Dispose ()
        {
            var handler = Stopped;
            if (handler != null) {
                handler ();
            }

            Configuration.Stop ();

            // Disconnect from events we're listening to
            ServiceManager.SourceManager.ActiveSourceChanged -= OnActiveSourceChanged;
            Application.ShutdownRequested -= OnShutdownRequested;
            HyenaSqliteCommand.CommandExecuted -= OnSqliteCommandExecuted;

            ServiceManager.PlaybackController.SourceChanged      -= OnPlaybackSourceChanged;
            ServiceManager.PlaybackController.ShuffleModeChanged -= OnShuffleModeChanged;
            ServiceManager.PlaybackController.RepeatModeChanged  -= OnRepeatModeChanged;

            // Delete any collected data
            metrics.Store.Clear ();
            metrics.Dispose ();
            metrics = null;

            // Forget the user's unique id
            DatabaseConfigurationClient.Client.Set<string> (id_key, "");
        }

        private string GetSourceString (Source src)
        {
            if (src == null)
                return null;

            var parent = src.Parent;
            if (parent == null) {
                return src.GetType ().ToString ();
            } else {
                return String.Format ("{0}/{1}", parent.GetType (), src.GetType ());
            }
        }

        #region Event Handlers

        private void OnActiveSourceChanged (SourceEventArgs args)
        {
            active_source_changed.PushSample (GetSourceString (ServiceManager.SourceManager.ActiveSource));
        }

        private bool OnShutdownRequested ()
        {
            shutdown.TakeSample ();
            duration.TakeSample ();
            return true;
        }

        private void OnSqliteCommandExecuted (object o, CommandExecutedArgs args)
        {
            sqlite_executed.PushSample (String.Format ("{0}ms -- {1}", args.Ms, args.Sql));
        }

        private void OnPlaybackSourceChanged (object o, EventArgs args)
        {
            playback_source_changed.PushSample (GetSourceString (ServiceManager.PlaybackController.Source as Source));
        }

        private void OnShuffleModeChanged (object o, EventArgs<string> args)
        {
            shuffle_changed.PushSample (args.Value);
        }

        private void OnRepeatModeChanged (object o, EventArgs<PlaybackRepeatMode> args)
        {
            repeat_changed.PushSample (args.Value);
        }

        #endregion

        public static SchemaEntry<bool> EnableCollection = new SchemaEntry<bool> (
            "core", "send_anonymous_usage_data", false, // disabled by default
            "Improve Banshee by sending anonymous usage data", null
        );
    }
}
