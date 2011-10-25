// 
// AmzXspfPlaylistTest.cs
// 
// Author:
//   Bertrand Lorentz <bertrand.lorentz@gmail.com>
// 
// Copyright 2011 Bertrand Lorentz
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

#if ENABLE_TESTS

using System;
using System.IO;

using Gtk;
using Mono.Addins;
using NUnit.Framework;

using Xspf = Media.Playlists.Xspf;

using Hyena;
using Hyena.Tests;

namespace Banshee.AmazonMp3.Tests
{
    [TestFixture]
    public class AmzXspfPlaylistTest : TestBase
    {
        private string data_dir;

        [TestFixtureSetUp]
        public void Init ()
        {
            data_dir = Path.Combine (TestsDir, "data/amz");
        }

        [Test]
        public void LoadEncryptedAmz ()
        {
            TestPlaylist ("encrypted.amz", 12);
        }

        [Test]
        public void LoadDecryptedAmz ()
        {
            TestPlaylist ("decrypted.amz", 12);
        }

        [Test]
        public void LoadCleartextAmz ()
        {
            TestPlaylist ("clear.amz", 4);
        }

        public void TestPlaylist (string filename, int track_number)
        {
            var playlist = new AmzXspfPlaylist (Path.Combine (data_dir, filename));

            Assert.AreEqual (track_number, playlist.DownloadableTrackCount);
            foreach (var track in playlist.DownloadableTracks) {
                StringAssert.StartsWith ("http://", track.Locations[0].AbsoluteUri);
            }
        }
    }
}

#endif
