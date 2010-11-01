//
// CoverArtSpecTests.cs
//
// Author:
//   John Millikin <jmillikin@gmail.com>
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
using NUnit.Framework;

using Banshee.Base;

namespace Banshee.Base.Tests
{
    [TestFixture]
    public class DigestPartTests
    {
        private void AssertDigested (string original, string expected)
        {
            Assert.AreEqual (expected, CoverArtSpec.Digest (original));
        }

        [Test]
        public void TestEmpty ()
        {
            AssertDigested (null, null);
            AssertDigested ("", null);
            AssertDigested (" ", "7215ee9c7d9dc229d2921a40e899ec5f");

            // Test that bgo#620010 is fixed
            Assert.AreEqual (null, CoverArtSpec.CreateArtistAlbumId (null, null));
            Assert.AreEqual (null, CoverArtSpec.CreateArtistAlbumId (null, "All Albums (6)"));
        }

        [Test]
        public void TestUnicode ()
        {
            AssertDigested ("\u00e9", "5526861fbb1e71a1bda6ac364310a807");
            AssertDigested ("e\u0301", "5526861fbb1e71a1bda6ac364310a807");
        }

        [Test]
        public void TestEscaped ()
        {
            AssertDigested ("(a)", "69dfdf4e6a7c8489262f9d8b9958c9b3");
        }

        [Test]
        public void TestExamples ()
        {
            AssertDigested ("Metallica", "8b0ee5a501cef4a5699fd3b2d4549e8f");
            AssertDigested ("And Justice For All", "17e81b0a8cc3038f346e809fccda207d");
            AssertDigested ("Radio ga ga", "baad45f3f55461478cf25e3221f4f40d");
            AssertDigested ("World Soccer", "cd678d70c5c329759a7a7b476f7c71b1");
        }
    }
}

#endif
