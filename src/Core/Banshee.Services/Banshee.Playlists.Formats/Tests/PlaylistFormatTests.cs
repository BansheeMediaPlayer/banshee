//
// PlaylistFormatTests.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
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

#if ENABLE_TESTS

using System;
using System.IO;
using System.Collections.Generic;

using NUnit.Framework;

using Banshee.Playlists.Formats;

using Hyena;
using Hyena.Tests;

namespace Banshee.Playlists.Formats.Tests
{
    [TestFixture]
    public class PlaylistFormatsTest : TestBase
    {

#region Setup

        private static Uri BaseUri = new Uri ("/iamyourbase/");
        private List<Dictionary<string, object>> elements = new List<Dictionary<string, object>> ();
        private string playlists_dir;

        [TestFixtureSetUp]
        public void Init ()
        {
            Mono.Addins.AddinManager.Initialize (BinDir);

            playlists_dir = Path.Combine (TestsDir, "data/playlist-data");
            IPlaylistFormat playlist = LoadPlaylist (new M3uPlaylistFormat (), "extended.m3u");
            foreach (Dictionary<string, object> element in playlist.Elements) {
                elements.Add (element);
            }
        }

#endregion

#region Tests

        [Test]
        public void ReadAsfReferenceLocal ()
        {
            IPlaylistFormat pl = LoadPlaylist (new AsfReferencePlaylistFormat (), "reference_local.asx");
            Assert.AreEqual (elements[2]["uri"], pl.Elements[0]["uri"]);
        }

        [Test]
        public void ReadAsfReferenceRemote ()
        {
            IPlaylistFormat pl = LoadPlaylist (new AsfReferencePlaylistFormat (), "reference_remote.asx");
            Assert.AreEqual ("mmsh://remote/remote.mp3", (pl.Elements[0]["uri"] as Uri).AbsoluteUri);
        }

        [Test]
        public void ReadAsxSimple ()
        {
            LoadTest (new AsxPlaylistFormat (), "simple.asx", true);
        }

        [Test]
        public void ReadAsxExtended ()
        {
            LoadTest (new AsxPlaylistFormat (), "extended.asx", true);
        }

        [Test]
        public void ReadM3uSimple ()
        {
            LoadTest (new M3uPlaylistFormat (), "simple.m3u", false);
        }

        [Test]
        public void ReadM3uExtended ()
        {
            LoadTest (new M3uPlaylistFormat (), "extended.m3u", false);
        }

        [Test]
        public void ReadPlsSimple ()
        {
            LoadTest (new PlsPlaylistFormat (), "simple.pls", false);
        }

        [Test]
        public void ReadPlsExtended ()
        {
            LoadTest (new PlsPlaylistFormat (), "extended.pls", false);
        }

        [Test]
        public void ReadDetectMagic ()
        {
            PlaylistParser parser = new PlaylistParser ();
            parser.BaseUri = BaseUri;

            foreach (string path in Directory.GetFiles (playlists_dir)) {
                parser.Parse (new SafeUri (Path.Combine (Environment.CurrentDirectory, path)));
            }

            parser.Parse (new SafeUri ("http://download.banshee.fm/test/extended.pls"));
            AssertTest (parser.Elements, false);
        }

#endregion

#region Utilities

        private IPlaylistFormat LoadPlaylist (IPlaylistFormat playlist, string filename)
        {
            playlist.BaseUri = BaseUri;
            playlist.Load (File.OpenRead (Path.Combine (playlists_dir, filename)), true);
            return playlist;
        }

        private void LoadTest (IPlaylistFormat playlist, string filename, bool mmsh)
        {
            LoadPlaylist (playlist, filename);
            AssertTest (playlist.Elements, mmsh);
        }

        private void AssertTest (List<Dictionary<string, object>> plelements, bool mmsh)
        {
            int i = 0;
            foreach (Dictionary<string, object> element in plelements) {
                if (mmsh) {
                    Assert.AreEqual (((Uri)elements[i]["uri"]).AbsoluteUri.Replace ("http", "mmsh"), ((Uri)element["uri"]).AbsoluteUri);
                } else {
                    Assert.AreEqual ((Uri)elements[i]["uri"], (Uri)element["uri"]);
                }

                if (element.ContainsKey ("title")) {
                    Assert.AreEqual ((string)elements[i]["title"], (string)element["title"]);
                }

                if (element.ContainsKey ("duration")) {
                    Assert.AreEqual ((TimeSpan)elements[i]["duration"], (TimeSpan)element["duration"]);
                }

                i++;
            }
        }

#endregion

    }
}

#endif
