//
// AmzMp3DownloaderService.cs
//  
// Author:
//   Aaron Bockover <abockover@novell.com>
// 
// Copyright 2010 Novell, Inc.
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
using System.IO;

using Mono.Unix;

using Hyena;

using Banshee.ServiceStack;
using Banshee.Configuration;

namespace Banshee.AmazonMp3
{
    public class AmazonMp3DownloaderService : IExtensionService, IDisposable
    {
        public void Initialize ()
        {
            ServiceManager.Get<DBusCommandService> ().ArgumentPushed += OnCommandLineArgument;
            GLib.Timeout.Add (2500, delegate {
                foreach (var path in ApplicationContext.CommandLine.Files) {
                    OnCommandLineArgument (path, null, true);
                }
                return false;
            });

            if (DatabaseConfigurationClient.Client.Get<int> ("amazonmp3", "smart_playlist_version", 0) == 0) {
                var sp = new Banshee.SmartPlaylist.SmartPlaylistDefinition (
                    Catalog.GetString ("Amazon MP3s"),
                    Catalog.GetString ("Songs purchased from the Amazon MP3 Store"),
                    "comment=\"amazon\"", true).ToSmartPlaylistSource (ServiceManager.SourceManager.MusicLibrary);
                sp.Save ();
                sp.RefreshAndReload ();
                DatabaseConfigurationClient.Client.Set<int> ("amazonmp3", "smart_playlist_version", 1);
            }
        }

        public void Dispose ()
        {
            ServiceManager.Get<DBusCommandService> ().ArgumentPushed -= OnCommandLineArgument;
        }

        private void OnCommandLineArgument (string argument, object value, bool isFile)
        {
            if (isFile && File.Exists (argument) && Path.GetExtension (argument) == ".amz") {
                DownloadAmz (argument);
            }
        }

        public void DownloadAmz (string path)
        {
            try {
                new AmazonDownloadManager (path);
                Log.Information ("Downloading Amazon MP3 purchase", path);
            } catch (Exception e) {
                Log.Error ("Invalid .amz file: " + path, e);
                Log.Error (Catalog.GetString ("Invalid Amazon MP3 downloader file"), path, true);
            }
        }

        string IService.ServiceName {
            get { return "AmazonMp3DownloaderService"; }
        }
    }
}
