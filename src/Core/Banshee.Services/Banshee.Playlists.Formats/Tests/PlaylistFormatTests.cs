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
        private List<PlaylistElement> elements = new List<PlaylistElement> ();
        private string playlists_dir;

        [TestFixtureSetUp]
        public void Init ()
        {
            Mono.Addins.AddinManager.Initialize (BinDir);

            playlists_dir = Path.Combine (TestsDir, "data/playlist-data");
            IPlaylistFormat playlist = LoadPlaylist (new M3uPlaylistFormat (), "extended.m3u");
            foreach (PlaylistElement element in playlist.Elements) {
                elements.Add (element);
            }
        }

#endregion

#region Tests

        [Test]
        public void ReadAsfReferenceLocal ()
        {
            IPlaylistFormat pl = LoadPlaylist (new AsfReferencePlaylistFormat (), "reference_local.asx");
            Assert.AreEqual (elements[2].Uri, pl.Elements[0].Uri);
        }

        [Test]
        public void ReadAsfReferenceRemote ()
        {
            IPlaylistFormat pl = LoadPlaylist (new AsfReferencePlaylistFormat (), "reference_remote.asx");
            Assert.AreEqual ("mmsh://remote/remote.mp3", (pl.Elements[0].Uri).AbsoluteUri);
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
        public void ReadAsxEntryRef ()
        {
            var parsed_playlist = PlaylistParser.Parse (new SafeUri ("http://download.banshee.fm/test/remote.asx"), BaseUri);
            IPlaylistFormat plref = LoadPlaylist (new AsxPlaylistFormat (), "entryref.asx");
            Assert.AreEqual (2, plref.Elements.Count);
            AssertEqual (parsed_playlist.Elements, plref.Elements);
        }

        private void AssertEqual (List<PlaylistElement> l1, List<PlaylistElement> l2)
        {
            Assert.AreEqual (l1.Count, l2.Count);
            for (int i = 0; i < l1.Count; i++) {
                AssertEqual (l1[i], l2[i]);
            }
        }

        private void AssertEqual (PlaylistElement e1, PlaylistElement e2)
        {
            Assert.AreEqual (e1.Title, e2.Title);
            Assert.AreEqual (e1.Duration, e2.Duration);
            Assert.AreEqual (e1.Uri.ToString (), e2.Uri.ToString ());
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

        [Test] // https://bugzilla.gnome.org/show_bug.cgi?id=661507
        public void ReadM3uWithDosPathAsRootPath ()
        {
            playlists_dir = Path.Combine (TestsDir, "data/playlist-data");
            IPlaylistFormat playlist = LoadPlaylist (new M3uPlaylistFormat (),
                                                     "dos_path_nokia.m3u",
                                                     new Uri ("E:\\"));
            Assert.AreEqual (playlist.Elements [0].Uri.AbsoluteUri.ToString (),
                             "file:///iamyourbase/Music/Atari%20Doll/Atari%20Doll/01.%20Queen%20for%20a%20Day.mp3");
            Assert.AreEqual (playlist.Elements [1].Uri.AbsoluteUri.ToString (),
                             "file:///iamyourbase/Music/Barenaked%20Ladies/All%20Their%20Greatest%20Hits%201991/04.%20One%20Week.mp3");
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
            foreach (string path in Directory.GetFiles (playlists_dir)) {
                PlaylistParser.Parse (new SafeUri (Path.Combine (Environment.CurrentDirectory, path)), BaseUri);
            }

            var parsed_playlist = PlaylistParser.Parse (new SafeUri ("http://download.banshee.fm/test/extended.pls"), BaseUri);
            AssertTest (parsed_playlist.Elements, false);
        }

#endregion

#region Utilities

        private IPlaylistFormat LoadPlaylist (IPlaylistFormat playlist, string filename, Uri rootPath)
        {
            playlist.BaseUri = BaseUri;
            if (rootPath != null) {
                playlist.RootPath = rootPath;
            }
            playlist.Load (File.OpenRead (Path.Combine (playlists_dir, filename)), true);
            return playlist;
        }

        private IPlaylistFormat LoadPlaylist (IPlaylistFormat playlist, string filename)
        {
            return LoadPlaylist (playlist, filename, null);
        }

        private void LoadTest (IPlaylistFormat playlist, string filename, bool mmsh)
        {
            LoadPlaylist (playlist, filename);
            AssertTest (playlist.Elements, mmsh);
        }

        private void AssertTest (List<PlaylistElement> plelements, bool mmsh)
        {
            int i = 0;
            foreach (PlaylistElement element in plelements) {
                if (mmsh) {
                    Assert.AreEqual (elements[i].Uri.AbsoluteUri.Replace ("http", "mmsh"), element.Uri.AbsoluteUri);
                } else {
                    Assert.AreEqual (elements[i].Uri, element.Uri);
                }

                if (element.Title != null) {
                    Assert.AreEqual (elements[i].Title, element.Title);
                }

                if (element.Duration != default(TimeSpan)) {
                    Assert.AreEqual (elements[i].Duration, element.Duration);
                }

                i++;
            }
        }

#endregion

    }
}

#endif
