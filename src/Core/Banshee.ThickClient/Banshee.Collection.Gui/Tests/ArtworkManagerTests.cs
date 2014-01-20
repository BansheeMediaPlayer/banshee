//
// ArtworkManagerTests.cs
//
// Author:
//   Andrés G. Aragoneses <knocte@gmail.com>
//
// Copyright 2013 Andrés G. Aragoneses
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

using Banshee.Base;
using Hyena.Tests;

using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;
using System.IO;
using Gdk;

namespace Banshee.Collection.Gui.Tests
{
    class CustomArtworkManager : ArtworkManager
    {
        internal static int SizeTest = 36;
        protected override void Init ()
        {
            AddCachedSize (SizeTest);
        }
    }

    [TestFixture]
    public class ArtworkManagerTests : TestBase
    {
        static string ExtractPngFromResource ()
        {
            var first_image = Assembly.GetExecutingAssembly ().GetManifestResourceNames ().Where (n => n.EndsWith (".png")).First ();
            var temp_png = Path.Combine (Path.GetTempPath (), first_image);
            Stream s = Assembly.GetExecutingAssembly ().GetManifestResourceStream (first_image);
            using (FileStream file = new FileStream (temp_png, FileMode.Create)) {
                byte[] b = new byte[s.Length + 1];
                s.Read (b, 0, Convert.ToInt32 (s.Length));
                file.Write (b, 0, Convert.ToInt32 (b.Length - 1));
                file.Flush ();
            }
            return temp_png;
        }

        static ArtworkManagerTests ()
        {
            GLib.GType.Init ();
            Mono.Addins.AddinManager.Initialize (BinDir);
            Banshee.IO.Provider.SetProvider (new Banshee.IO.SystemIO.Provider ());
        }

        [Test]
        public void TestSizePath ()
        {
            var png_file_path = ExtractPngFromResource ();
            string jpg_file_path = null;

            try {
                var artist_album_id = CoverArtSpec.CreateArtistAlbumId ("Metallica", "Master Of Puppets");
                jpg_file_path = CoverArtSpec.GetPathForSize (artist_album_id, CustomArtworkManager.SizeTest); // i.e.: /home/knocte/.cache/media-art/36/album-d33f25dbd7dfb4817a7e99f6bc2de49e.jpg"
                var pixbuf = new Pixbuf (png_file_path);

                var dir = System.IO.Path.GetDirectoryName (jpg_file_path);
                if (!System.IO.Directory.Exists (dir)) {
                    System.IO.Directory.CreateDirectory (dir);
                }

                pixbuf.Save (jpg_file_path, "jpeg");

                var artwork_manager = new CustomArtworkManager ();
                Assert.IsNull (artwork_manager.LookupScaleSurface (artist_album_id, 1, false),
                               "Should have got null at the first request, with an invalid size");
                Assert.IsNotNull (artwork_manager.LookupScaleSurface (artist_album_id, CustomArtworkManager.SizeTest, false),
                                  "Null at the second request, was null cached incorrectly?");

            } finally {
                File.Delete (png_file_path);
                if (File.Exists (jpg_file_path)) {
                    File.Delete (jpg_file_path);
                }
            }
        }
    }
}

#endif